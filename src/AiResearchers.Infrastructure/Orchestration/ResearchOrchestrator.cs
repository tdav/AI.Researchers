using AiResearchers.Core.Agents;
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Orchestration;
using AiResearchers.Core.Research;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiResearchers.Infrastructure.Orchestration;

public class ResearchOrchestrator : IResearchOrchestrator
{
    // Bound the expensive Analyst LLM calls per round: with a slow local model, analyzing
    // every fetched page makes a round take 20-40 min. Cap focus areas, analyzed pages, and
    // results per query so a round stays in the minutes range.
    private const int MaxFocusPerRound = 4;
    private const int MaxAnalyzedPerRound = 5;
    private const int ResultsPerQuery = 3;
    private readonly AppDbContext db;
    private readonly IPlannerAgent planner;
    private readonly ISearcherAgent searcher;
    private readonly IAnalystAgent analyst;
    private readonly ICriticAgent critic;
    private readonly IWriterAgent writer;
    private readonly ISearchProvider search;
    private readonly IContentFetcher fetcher;
    private readonly ILogger<ResearchOrchestrator> logger;

    public ResearchOrchestrator(
        AppDbContext db, IPlannerAgent planner, ISearcherAgent searcher, IAnalystAgent analyst,
        ICriticAgent critic, IWriterAgent writer, ISearchProvider search, IContentFetcher fetcher,
        ILogger<ResearchOrchestrator> logger)
    {
        this.db = db;
        this.planner = planner;
        this.searcher = searcher;
        this.analyst = analyst;
        this.critic = critic;
        this.writer = writer;
        this.search = search;
        this.fetcher = fetcher;
        this.logger = logger;
    }

    public async Task RunAsync(Guid researchTaskId, CancellationToken cancellationToken)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.OutlineSections)
            .FirstOrDefaultAsync(t => t.Id == researchTaskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        try
        {
            task.Status = ResearchStatus.Running;
            task.StartedAt = DateTimeOffset.UtcNow;
            await this.SaveAsync(task, ProgressPhase.Planning, "Старт исследования", cancellationToken);

            IReadOnlyList<string> focusAreas = await this.planner.PlanFocusAreasAsync(task, cancellationToken);
            await this.AddFocusAreas(task, focusAreas, round: 1, cancellationToken);
            await this.Save(cancellationToken);

            // Separate local list for the critic's running summary — never touches task.Findings.
            // This prevents EF from inserting duplicate Finding rows.
            List<string> findingsSummary = new();

            var pending = new Queue<string>(focusAreas);
            int round = 0;
            while (pending.Count > 0 && round < task.MaxRounds && !cancellationToken.IsCancellationRequested)
            {
                round++;
                task.CurrentRound = round;
                await this.SaveAsync(task, ProgressPhase.Searching, $"Раунд {round}", cancellationToken);

                int processed = 0;
                int analyzedThisRound = 0;
                while (pending.Count > 0 && processed < MaxFocusPerRound && analyzedThisRound < MaxAnalyzedPerRound)
                {
                    string focus = pending.Dequeue();
                    int budget = MaxAnalyzedPerRound - analyzedThisRound;
                    analyzedThisRound += await this.ProcessFocusAsync(task, focus, round, findingsSummary, budget, cancellationToken);
                    processed++;
                }

                // Critic uses the local summary list — no EF navigation touched.
                CritiqueResult critique = await this.critic.CritiqueAsync(task, findingsSummary, cancellationToken);
                await this.SaveAsync(task, ProgressPhase.Critiquing,
                    critique.Enough ? "Critic: достаточно" : $"Critic: ещё {critique.NewFocusAreas.Count}", cancellationToken);

                if (critique.Enough)
                {
                    break;
                }
                foreach (string fa in critique.NewFocusAreas)
                {
                    pending.Enqueue(fa);
                }
                await this.AddFocusAreas(task, critique.NewFocusAreas, round + 1, cancellationToken);
            }

            // Writer: reload full task with findings from DB so navigation is populated.
            await this.SaveAsync(task, ProgressPhase.Writing, "Сборка отчёта", cancellationToken);
            ResearchTask full = await this.LoadFull(task.Id, cancellationToken);
            string markdown = await this.writer.WriteReportAsync(full, cancellationToken);

            this.db.Reports.Add(new Report { ResearchTaskId = task.Id, MarkdownContent = markdown });
            task.Status = ResearchStatus.Completed;
            task.CompletedAt = DateTimeOffset.UtcNow;
            await this.SaveAsync(task, ProgressPhase.Completed, "Готово", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            task.Status = ResearchStatus.Cancelled;
            await this.SaveAsync(task, ProgressPhase.Completed, "Отменено", CancellationToken.None);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Research {Id} failed", researchTaskId);
            task.Status = ResearchStatus.Failed;
            task.FailureReason = ex.Message;
            await this.SaveAsync(task, ProgressPhase.Completed, $"Ошибка: {ex.Message}", CancellationToken.None);
        }
    }

    // findingsSummary: accumulated finding texts for the critic — kept entirely in memory,
    // never added to task.Findings navigation (which would trigger EF INSERT duplicates).
    // Returns the number of pages successfully analyzed (LLM calls), bounded by 'budget'.
    private async Task<int> ProcessFocusAsync(
        ResearchTask task, string focus, int round, List<string> findingsSummary, int budget, CancellationToken ct)
    {
        int analyzed = 0;
        IReadOnlyList<string> queries;
        try
        {
            queries = await this.searcher.GenerateQueriesAsync(task, focus, ct);
        }
        catch (Exception ex)
        {
            await this.SaveAsync(task, ProgressPhase.Searching, $"Searcher fail: {ex.Message}", ct, EventLevel.Warn);
            return analyzed;
        }

        var seenUrls = new HashSet<string>(
            await this.db.Sources
                .Where(s => s.ResearchTaskId == task.Id)
                .Select(s => s.Url)
                .ToListAsync(ct));

        foreach (string query in queries)
        {
            IReadOnlyList<SearchResultItem> results;
            try
            {
                results = await this.search.SearchAsync(query, ResultsPerQuery, ct);
            }
            catch (Exception ex)
            {
                await this.SaveAsync(task, ProgressPhase.Searching, $"Поиск fail '{query}': {ex.Message}", ct, EventLevel.Warn);
                continue;
            }

            foreach (SearchResultItem r in results)
            {
                if (!seenUrls.Add(r.Url))
                {
                    continue;
                }

                Source source = new()
                {
                    ResearchTaskId = task.Id,
                    Url = r.Url,
                    Title = r.Title,
                    Snippet = r.Snippet,
                    Status = SourceStatus.Found
                };
                this.db.Sources.Add(source);
                await this.Save(ct);

                FetchedContent content = await this.fetcher.FetchAsync(r.Url, ct);
                source.FetchedAt = DateTimeOffset.UtcNow;
                if (!content.Success)
                {
                    source.Status = SourceStatus.Failed;
                    await this.Save(ct);
                    continue;
                }
                source.Status = SourceStatus.Fetched;
                source.ExtractedText = content.Text;

                IReadOnlyList<FindingItem> findings =
                    await this.analyst.ExtractFindingsAsync(task, focus, content.Text, ct);
                foreach (FindingItem fi in findings)
                {
                    Guid? sectionId = this.MatchSection(task, fi.SectionTitle);
                    // Write to DB exactly once. Never add to task.Findings navigation.
                    this.db.Findings.Add(new Finding
                    {
                        ResearchTaskId = task.Id,
                        SourceId = source.Id,
                        OutlineSectionId = sectionId,
                        Text = fi.Text,
                        Round = round
                    });
                    // Accumulate text for critic summary (local list only, not EF-tracked).
                    findingsSummary.Add(fi.Text);
                }
                await this.SaveAsync(task, ProgressPhase.Analyzing,
                    $"{r.Title}: +{findings.Count} фактов", ct);

                analyzed++;
                if (analyzed >= budget)
                {
                    return analyzed;
                }
            }
        }

        return analyzed;
    }

    private Guid? MatchSection(ResearchTask task, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }
        OutlineSection? match = task.OutlineSections
            .FirstOrDefault(s => string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    private async Task AddFocusAreas(ResearchTask task, IEnumerable<string> areas, int round, CancellationToken ct)
    {
        foreach (string a in areas)
        {
            this.db.FocusAreas.Add(new FocusArea { ResearchTaskId = task.Id, Round = round, Title = a });
        }
        await Task.CompletedTask;
    }

    private async Task<ResearchTask> LoadFull(Guid id, CancellationToken ct)
    {
        return await this.db.ResearchTasks
            .Include(t => t.OutlineSections)
            .Include(t => t.Findings)
            .FirstAsync(t => t.Id == id, ct);
    }

    private async Task SaveAsync(ResearchTask task, ProgressPhase phase, string message,
        CancellationToken ct, EventLevel level = EventLevel.Info)
    {
        this.db.ProgressEvents.Add(new ProgressEvent
        {
            ResearchTaskId = task.Id, Phase = phase, Level = level, Message = message
        });
        await this.Save(ct);
    }

    private async Task Save(CancellationToken ct)
    {
        await this.db.SaveChangesAsync(ct);
    }
}
