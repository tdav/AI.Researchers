# AI Researchers Platform — Phase 4b: Agents + Orchestrator + Worker

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Собрать research-движок: 5 агентов на `IChatClient` (structured JSON), детерминированный оркестратор (Plan → цикл[Search→Fetch→Analyze→Critic] → Write), фоновый worker с очередью, guard'ы цикла, запись прогресса/источников/находок/отчёта в БД. После утверждения outline (Phase 3) задача автоматически исполняется до `Completed` с готовым отчётом.

**Architecture:** Агенты — сервисы Core поверх `IChatClient`, каждый возвращает строго типизированный JSON через `GetResponseAsync<T>` (подтверждено gate'ом). Оркестратор-код владеет циклом и guard'ами, вызывает поисковик/фетчер (Phase 4a) как код, агентов — для генерации/анализа, и пишет всё в `AppDbContext`. Worker = `Channel<Guid>` + `BackgroundService`, исполняет одну задачу за раз. На старте приложения «зависшие» `Running` помечаются `Failed`. LLM-вызовы обёрнуты в retry на transient (per gate-results).

**Tech Stack:** .NET 10, Microsoft.Extensions.AI 10.6.0 (`IChatClient`), EF Core 10, `System.Threading.Channels`, `BackgroundService`.

> **Тесты:** формальные тесты не пишем (правило проекта). Проверка — `dotnet build` + реальный прогон одной задачи до `Completed` (Task 6, через probe-эндпоинт/енки). Спека §3 (workflow), §4 (Source/Finding/ProgressEvent/Report), §6 (guard'ы, lifecycle, retry). Зависит от Phase 1 (домен/EF), 2 (`IChatClient`, таймаут 300s), 3 (Queued-задачи с outline), 4a (`ISearchProvider`/`IContentFetcher`).

> **Перенесено из gate-results (обязательно):** LLM-вызовы агентов должны переживать transient HTTP/timeout — обёртка с retry+backoff (Task 2). Таймаут HttpClient уже 300s (Phase 2).

---

## File Structure (создаётся в этой фазе)

```
src/AiResearchers.Core/Agents/
  AgentDtos.cs                 # FocusAreasResult, SearchQueriesResult, FindingsResult, CritiqueResult
  LlmRetry.cs                  # retry-обёртка для IChatClient вызовов
  IPlannerAgent.cs / PlannerAgent.cs
  ISearcherAgent.cs / SearcherAgent.cs
  IAnalystAgent.cs / AnalystAgent.cs
  ICriticAgent.cs / CriticAgent.cs
  IWriterAgent.cs / WriterAgent.cs
  AgentsServiceCollectionExtensions.cs   # AddAgents()
src/AiResearchers.Core/Orchestration/
  IResearchOrchestrator.cs / ResearchOrchestrator.cs
  IResearchQueue.cs
src/AiResearchers.Infrastructure/Orchestration/
  ChannelResearchQueue.cs       # IResearchQueue на Channel
  ResearchBackgroundService.cs  # worker
  InterruptedTaskRecovery.cs    # стартовая зачистка Running→Failed
  OrchestrationServiceCollectionExtensions.cs  # AddOrchestration()
src/AiResearchers.Web/
  Program.cs                    # + AddAgents/AddResearchSources/AddOrchestration + hosted services
  Pages/Research/Outline.cshtml.cs  # enqueue после approve
```

> Оркестратор живёт в Core, но ему нужен доступ к БД. Чтобы не тащить EF в Core, оркестратор зависит от `AppDbContext`-абстракции через интерфейс? — НЕТ, для простоты MVP оркестратор размещаем так: он принимает `IServiceProvider`/scoped-зависимости. Решение: **оркестратор кладём в Infrastructure** (где уже есть EF), а его контракт `IResearchOrchestrator` — в Core. Соответственно `ResearchOrchestrator.cs` переезжает в `src/AiResearchers.Infrastructure/Orchestration/`. Агенты остаются в Core (зависят только от `IChatClient`).

---

### Task 1: Core — DTO агентов и контракты

**Files:**
- Create: `src/AiResearchers.Core/Agents/AgentDtos.cs`
- Create: `src/AiResearchers.Core/Agents/IPlannerAgent.cs`
- Create: `src/AiResearchers.Core/Agents/ISearcherAgent.cs`
- Create: `src/AiResearchers.Core/Agents/IAnalystAgent.cs`
- Create: `src/AiResearchers.Core/Agents/ICriticAgent.cs`
- Create: `src/AiResearchers.Core/Agents/IWriterAgent.cs`

- [ ] **Step 1: DTO**

Create `src/AiResearchers.Core/Agents/AgentDtos.cs`:
```csharp
namespace AiResearchers.Core.Agents;

public class FocusAreasResult
{
    public List<string> FocusAreas { get; set; } = new();
}

public class SearchQueriesResult
{
    public List<string> Queries { get; set; } = new();
}

public class FindingItem
{
    public string Text { get; set; } = string.Empty;
    public string? SectionTitle { get; set; }
}

public class FindingsResult
{
    public List<FindingItem> Findings { get; set; } = new();
}

public class CritiqueResult
{
    public bool Enough { get; set; }
    public List<string> NewFocusAreas { get; set; } = new();
}
```

- [ ] **Step 2: Контракты агентов**

Create `src/AiResearchers.Core/Agents/IPlannerAgent.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface IPlannerAgent
{
    Task<IReadOnlyList<string>> PlanFocusAreasAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}
```

Create `src/AiResearchers.Core/Agents/ISearcherAgent.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface ISearcherAgent
{
    Task<IReadOnlyList<string>> GenerateQueriesAsync(
        ResearchTask task, string focusArea, CancellationToken cancellationToken = default);
}
```

Create `src/AiResearchers.Core/Agents/IAnalystAgent.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface IAnalystAgent
{
    Task<IReadOnlyList<FindingItem>> ExtractFindingsAsync(
        ResearchTask task, string focusArea, string sourceText,
        CancellationToken cancellationToken = default);
}
```

Create `src/AiResearchers.Core/Agents/ICriticAgent.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface ICriticAgent
{
    Task<CritiqueResult> CritiqueAsync(
        ResearchTask task, IReadOnlyList<string> findingsSummary,
        CancellationToken cancellationToken = default);
}
```

Create `src/AiResearchers.Core/Agents/IWriterAgent.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface IWriterAgent
{
    Task<string> WriteReportAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Core`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Core/Agents/AgentDtos.cs src/AiResearchers.Core/Agents/I*Agent.cs
git commit -m "feat: add research agent contracts and dtos"
```

---

### Task 2: Core — LLM retry-обёртка и реализации агентов

**Files:**
- Create: `src/AiResearchers.Core/Agents/LlmRetry.cs`
- Create: `src/AiResearchers.Core/Agents/PlannerAgent.cs`
- Create: `src/AiResearchers.Core/Agents/SearcherAgent.cs`
- Create: `src/AiResearchers.Core/Agents/AnalystAgent.cs`
- Create: `src/AiResearchers.Core/Agents/CriticAgent.cs`
- Create: `src/AiResearchers.Core/Agents/WriterAgent.cs`
- Create: `src/AiResearchers.Core/Agents/AgentsServiceCollectionExtensions.cs`

- [ ] **Step 1: Retry-обёртка (transient HTTP/timeout)**

Create `src/AiResearchers.Core/Agents/LlmRetry.cs`:
```csharp
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

// Retry с backoff для structured-output вызовов IChatClient.
// Покрывает transient HTTP-ошибки и таймауты, выявленные в LLM gate.
public static class LlmRetry
{
    public static async Task<T?> GetJsonAsync<T>(
        IChatClient client, string prompt, CancellationToken cancellationToken, int maxAttempts = 3)
    {
        Exception? last = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ChatResponse<T> response =
                    await client.GetResponseAsync<T>(prompt, cancellationToken: cancellationToken);
                return response.Result;
            }
            catch (Exception ex) when (
                (ex is HttpRequestException || ex is TaskCanceledException)
                && !cancellationToken.IsCancellationRequested
                && attempt < maxAttempts)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }
        throw new InvalidOperationException(
            $"LLM call failed after {maxAttempts} attempts.", last);
    }
}
```

- [ ] **Step 2: `PlannerAgent`**

Create `src/AiResearchers.Core/Agents/PlannerAgent.cs`:
```csharp
using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class PlannerAgent : IPlannerAgent
{
    private readonly IChatClient chatClient;

    public PlannerAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<string>> PlanFocusAreasAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        StringBuilder outline = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            outline.Append("- ").Append(s.Title).Append(": ").Append(s.Description).Append('\n');
        }

        string prompt =
            $"You plan web research. Topic: \"{task.Topic}\". Report outline:\n{outline}" +
            $"List 4-6 distinct focus areas (search angles) that together cover the outline. " +
            $"Language: {task.Language}. Return JSON: {{ \"focusAreas\": [\"...\"] }}.";

        FocusAreasResult? result =
            await LlmRetry.GetJsonAsync<FocusAreasResult>(this.chatClient, prompt, cancellationToken);
        return result?.FocusAreas ?? new List<string>();
    }
}
```

- [ ] **Step 3: `SearcherAgent`**

Create `src/AiResearchers.Core/Agents/SearcherAgent.cs`:
```csharp
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class SearcherAgent : ISearcherAgent
{
    private readonly IChatClient chatClient;

    public SearcherAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<string>> GenerateQueriesAsync(
        ResearchTask task, string focusArea, CancellationToken cancellationToken = default)
    {
        string prompt =
            $"Topic: \"{task.Topic}\". Focus area: \"{focusArea}\". " +
            $"Generate 2-3 effective web search queries (plain keywords, no operators) to research this focus area. " +
            $"Return JSON: {{ \"queries\": [\"...\"] }}.";

        SearchQueriesResult? result =
            await LlmRetry.GetJsonAsync<SearchQueriesResult>(this.chatClient, prompt, cancellationToken);
        return result?.Queries ?? new List<string> { focusArea };
    }
}
```

- [ ] **Step 4: `AnalystAgent`**

Create `src/AiResearchers.Core/Agents/AnalystAgent.cs`:
```csharp
using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class AnalystAgent : IAnalystAgent
{
    private const int MaxSourceChars = 6000;
    private readonly IChatClient chatClient;

    public AnalystAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<FindingItem>> ExtractFindingsAsync(
        ResearchTask task, string focusArea, string sourceText,
        CancellationToken cancellationToken = default)
    {
        string trimmed = sourceText.Length > MaxSourceChars
            ? sourceText[..MaxSourceChars]
            : sourceText;

        StringBuilder sections = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            sections.Append("- ").Append(s.Title).Append('\n');
        }

        string prompt =
            $"Topic: \"{task.Topic}\". Focus area: \"{focusArea}\". Report sections:\n{sections}" +
            $"From the SOURCE TEXT below, extract 0-5 concrete factual findings relevant to the topic. " +
            $"For each, optionally name the most relevant section from the list above. " +
            $"Ignore navigation/boilerplate. Language: {task.Language}. " +
            $"Return JSON: {{ \"findings\": [{{ \"text\": \"...\", \"sectionTitle\": \"...\" }}] }}.\n\n" +
            $"SOURCE TEXT:\n{trimmed}";

        FindingsResult? result =
            await LlmRetry.GetJsonAsync<FindingsResult>(this.chatClient, prompt, cancellationToken);
        return result?.Findings ?? new List<FindingItem>();
    }
}
```

- [ ] **Step 5: `CriticAgent`**

Create `src/AiResearchers.Core/Agents/CriticAgent.cs`:
```csharp
using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class CriticAgent : ICriticAgent
{
    private readonly IChatClient chatClient;

    public CriticAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<CritiqueResult> CritiqueAsync(
        ResearchTask task, IReadOnlyList<string> findingsSummary,
        CancellationToken cancellationToken = default)
    {
        StringBuilder outline = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            outline.Append("- ").Append(s.Title).Append('\n');
        }

        string summary = findingsSummary.Count == 0
            ? "(no findings yet)"
            : string.Join("\n", findingsSummary.Take(40).Select(f => "- " + f));

        string prompt =
            $"Topic: \"{task.Topic}\". Report outline:\n{outline}" +
            $"Findings collected so far:\n{summary}\n" +
            $"Decide if coverage is sufficient for all sections. " +
            $"If not, propose up to 3 new focus areas to fill the biggest gaps. " +
            $"Return JSON: {{ \"enough\": true|false, \"newFocusAreas\": [\"...\"] }}.";

        CritiqueResult? result =
            await LlmRetry.GetJsonAsync<CritiqueResult>(this.chatClient, prompt, cancellationToken);
        return result ?? new CritiqueResult { Enough = true };
    }
}
```

- [ ] **Step 6: `WriterAgent`**

Create `src/AiResearchers.Core/Agents/WriterAgent.cs`:
```csharp
using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class WriterAgent : IWriterAgent
{
    private readonly IChatClient chatClient;

    public WriterAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    // Writer возвращает Markdown как plain text (не JSON).
    public async Task<string> WriteReportAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        StringBuilder context = new();
        context.Append("Outline and findings:\n");
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            context.Append("\n## ").Append(s.Title).Append(" — ").Append(s.Description).Append('\n');
            IEnumerable<Finding> sectionFindings = task.Findings
                .Where(f => f.OutlineSectionId == s.Id);
            foreach (Finding f in sectionFindings)
            {
                context.Append("- ").Append(f.Text).Append('\n');
            }
        }

        // Находки без секции — общий пул.
        List<Finding> unassigned = task.Findings.Where(f => f.OutlineSectionId is null).ToList();
        if (unassigned.Count > 0)
        {
            context.Append("\nAdditional findings:\n");
            foreach (Finding f in unassigned)
            {
                context.Append("- ").Append(f.Text).Append('\n');
            }
        }

        string prompt =
            $"Write a research report in Markdown. Topic: \"{task.Topic}\". Language: {task.Language}. " +
            $"Use the outline section titles as `##` headings, in order. " +
            $"Base the content ONLY on the findings provided; if a section has no findings, note that data was insufficient. " +
            $"Do not invent sources. Keep it well-structured.\n\n{context}";

        ChatResponse response =
            await this.chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }
}
```

- [ ] **Step 7: DI-расширение агентов**

Create `src/AiResearchers.Core/Agents/AgentsServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Core.Agents;

public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddScoped<ISearcherAgent, SearcherAgent>();
        services.AddScoped<IAnalystAgent, AnalystAgent>();
        services.AddScoped<ICriticAgent, CriticAgent>();
        services.AddScoped<IWriterAgent, WriterAgent>();
        return services;
    }
}
```

- [ ] **Step 8: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Core`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Core/Agents/
git commit -m "feat: implement research agents with llm retry"
```

---

### Task 3: Оркестратор (Infrastructure) + контракты очереди (Core)

**Files:**
- Create: `src/AiResearchers.Core/Orchestration/IResearchOrchestrator.cs`
- Create: `src/AiResearchers.Core/Orchestration/IResearchQueue.cs`
- Create: `src/AiResearchers.Infrastructure/Orchestration/ResearchOrchestrator.cs`

- [ ] **Step 1: Контракты в Core**

Create `src/AiResearchers.Core/Orchestration/IResearchOrchestrator.cs`:
```csharp
namespace AiResearchers.Core.Orchestration;

public interface IResearchOrchestrator
{
    Task RunAsync(Guid researchTaskId, CancellationToken cancellationToken);
}
```

Create `src/AiResearchers.Core/Orchestration/IResearchQueue.cs`:
```csharp
namespace AiResearchers.Core.Orchestration;

public interface IResearchQueue
{
    void Enqueue(Guid researchTaskId);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Оркестратор (детерминированный цикл + персистентность)**

Create `src/AiResearchers.Infrastructure/Orchestration/ResearchOrchestrator.cs`:
```csharp
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
    private const int MaxSourcesPerRound = 6;
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

            var pending = new Queue<string>(focusAreas);
            int round = 0;
            while (pending.Count > 0 && round < task.MaxRounds && !cancellationToken.IsCancellationRequested)
            {
                round++;
                task.CurrentRound = round;
                await this.SaveAsync(task, ProgressPhase.Searching, $"Раунд {round}", cancellationToken);

                int processed = 0;
                while (pending.Count > 0 && processed < MaxSourcesPerRound)
                {
                    string focus = pending.Dequeue();
                    await this.ProcessFocusAsync(task, focus, round, cancellationToken);
                    processed++;
                }

                // Critic
                List<string> summary = task.Findings.Select(f => f.Text).ToList();
                CritiqueResult critique = await this.critic.CritiqueAsync(task, summary, cancellationToken);
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

            // Writer
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

    private async Task ProcessFocusAsync(ResearchTask task, string focus, int round, CancellationToken ct)
    {
        IReadOnlyList<string> queries;
        try
        {
            queries = await this.searcher.GenerateQueriesAsync(task, focus, ct);
        }
        catch (Exception ex)
        {
            await this.SaveAsync(task, ProgressPhase.Searching, $"Searcher fail: {ex.Message}", ct, EventLevel.Warn);
            return;
        }

        var seenUrls = new HashSet<string>(task.Sources.Select(s => s.Url));
        foreach (string query in queries)
        {
            IReadOnlyList<SearchResultItem> results;
            try
            {
                results = await this.search.SearchAsync(query, 5, ct);
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
                task.Sources.Add(source);

                FetchedContent content = await this.fetcher.FetchAsync(r.Url, ct);
                source.FetchedAt = DateTimeOffset.UtcNow;
                if (!content.Success)
                {
                    source.Status = SourceStatus.Failed;
                    continue;
                }
                source.Status = SourceStatus.Fetched;
                source.ExtractedText = content.Text;

                IReadOnlyList<FindingItem> findings =
                    await this.analyst.ExtractFindingsAsync(task, focus, content.Text, ct);
                foreach (FindingItem fi in findings)
                {
                    Guid? sectionId = this.MatchSection(task, fi.SectionTitle);
                    this.db.Findings.Add(new Finding
                    {
                        ResearchTaskId = task.Id,
                        SourceId = source.Id,
                        OutlineSectionId = sectionId,
                        Text = fi.Text,
                        Round = round
                    });
                    task.Findings.Add(new Finding { Text = fi.Text }); // для in-memory summary критика
                }
                await this.SaveAsync(task, ProgressPhase.Analyzing,
                    $"{r.Title}: +{findings.Count} фактов", ct);
            }
        }
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
```

> **Замечание для исполнителя:** строка `task.Findings.Add(new Finding { Text = fi.Text })` добавляет находку в in-memory коллекцию ТОЛЬКО для сводки критика и НЕ должна попасть в БД как дубль. Поскольку `task` отслеживается EF, добавление в его навигацию приведёт к INSERT. Исправь: держи сводку критика в отдельном локальном `List<string>` в методе `RunAsync` (накопитель), передавай его в `ProcessFocusAsync` по ссылке, а в БД пиши находку один раз через `this.db.Findings.Add(...)`. Не добавляй в `task.Findings`. (Этот комментарий — обязательная правка: не плоди дубли.)

- [ ] **Step 3: Собрать**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: после применения правки из замечания — `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/AiResearchers.Core/Orchestration/ src/AiResearchers.Infrastructure/Orchestration/ResearchOrchestrator.cs
git commit -m "feat: add deterministic research orchestrator"
```

---

### Task 4: Очередь, worker, стартовая зачистка + DI

**Files:**
- Create: `src/AiResearchers.Infrastructure/Orchestration/ChannelResearchQueue.cs`
- Create: `src/AiResearchers.Infrastructure/Orchestration/ResearchBackgroundService.cs`
- Create: `src/AiResearchers.Infrastructure/Orchestration/InterruptedTaskRecovery.cs`
- Create: `src/AiResearchers.Infrastructure/Orchestration/OrchestrationServiceCollectionExtensions.cs`

- [ ] **Step 1: Очередь на Channel**

Create `src/AiResearchers.Infrastructure/Orchestration/ChannelResearchQueue.cs`:
```csharp
using System.Threading.Channels;
using AiResearchers.Core.Orchestration;

namespace AiResearchers.Infrastructure.Orchestration;

public class ChannelResearchQueue : IResearchQueue
{
    private readonly Channel<Guid> channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid researchTaskId)
    {
        this.channel.Writer.TryWrite(researchTaskId);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return this.channel.Reader.ReadAllAsync(cancellationToken);
    }
}
```

- [ ] **Step 2: Worker (одна задача за раз, scoped оркестратор)**

Create `src/AiResearchers.Infrastructure/Orchestration/ResearchBackgroundService.cs`:
```csharp
using AiResearchers.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiResearchers.Infrastructure.Orchestration;

public class ResearchBackgroundService : BackgroundService
{
    private readonly IResearchQueue queue;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ResearchBackgroundService> logger;

    public ResearchBackgroundService(
        IResearchQueue queue, IServiceScopeFactory scopeFactory, ILogger<ResearchBackgroundService> logger)
    {
        this.queue = queue;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid id in this.queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = this.scopeFactory.CreateScope();
                IResearchOrchestrator orchestrator =
                    scope.ServiceProvider.GetRequiredService<IResearchOrchestrator>();
                await orchestrator.RunAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Worker failed processing {Id}", id);
            }
        }
    }
}
```

- [ ] **Step 3: Стартовая зачистка зависших задач**

Create `src/AiResearchers.Infrastructure/Orchestration/InterruptedTaskRecovery.cs`:
```csharp
using AiResearchers.Core.Enums;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiResearchers.Infrastructure.Orchestration;

// При старте приложения: задачи, оставшиеся Running после прошлого падения/рестарта, → Failed.
public class InterruptedTaskRecovery : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;

    public InterruptedTaskRecovery(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = this.scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ResearchTasks
            .Where(t => t.Status == ResearchStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, ResearchStatus.Failed)
                .SetProperty(t => t.FailureReason, "interrupted by restart"), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

- [ ] **Step 4: DI-расширение оркестрации**

Create `src/AiResearchers.Infrastructure/Orchestration/OrchestrationServiceCollectionExtensions.cs`:
```csharp
using AiResearchers.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiResearchers.Infrastructure.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddSingleton<IResearchQueue, ChannelResearchQueue>();
        services.AddScoped<IResearchOrchestrator, ResearchOrchestrator>();
        services.AddHostedService<ResearchBackgroundService>();
        services.AddHostedService<InterruptedTaskRecovery>();
        return services;
    }
}
```

- [ ] **Step 5: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Infrastructure/Orchestration/
git commit -m "feat: add research queue, background worker and interrupted-task recovery"
```

---

### Task 5: Связать всё в Web + enqueue после approve

**Files:**
- Modify: `src/AiResearchers.Web/Program.cs`
- Modify: `src/AiResearchers.Web/Pages/Research/Outline.cshtml.cs`
- Modify: `src/AiResearchers.Web/appsettings.json` (секция `Search`)

- [ ] **Step 1: Регистрация сервисов в `Program.cs`**

Modify `src/AiResearchers.Web/Program.cs` — после существующих `AddLlm`/`AddInterview`:
```csharp
builder.Services.AddInterview();
builder.Services.AddResearchSources(builder.Configuration);
builder.Services.AddAgents();
builder.Services.AddOrchestration();
```
Добавить usings:
```csharp
using AiResearchers.Core.Agents;
using AiResearchers.Infrastructure.Research;
using AiResearchers.Infrastructure.Orchestration;
```

- [ ] **Step 2: Секция `Search` в `appsettings.json`**

Modify `src/AiResearchers.Web/appsettings.json` — добавить рядом с `Llm`:
```json
  "Search": {
    "Provider": "Searxng",
    "SearxngEndpoint": "http://localhost:8888"
  }
```

- [ ] **Step 3: Enqueue после утверждения outline**

Modify `src/AiResearchers.Web/Pages/Research/Outline.cshtml.cs`:
- Добавить в конструктор зависимость `IResearchQueue` (поле `this.queue`):
```csharp
using AiResearchers.Core.Orchestration;
// ...
private readonly IResearchQueue queue;

public OutlineModel(AppDbContext db, IInterviewService interview, IResearchQueue queue)
{
    this.db = db;
    this.interview = interview;
    this.queue = queue;
}
```
- В `OnPostApproveAsync`, СРАЗУ ПОСЛЕ `await this.db.SaveChangesAsync();` и установки `task.Status = ResearchStatus.Queued;`, добавить постановку в очередь перед `return`:
```csharp
        task.Status = ResearchStatus.Queued;
        await this.db.SaveChangesAsync();

        this.queue.Enqueue(task.Id);

        return this.RedirectToPage("/Dashboard/Index");
```

- [ ] **Step 4: Собрать**

Run: `dotnet build`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AiResearchers.Web/Program.cs src/AiResearchers.Web/appsettings.json src/AiResearchers.Web/Pages/Research/Outline.cshtml.cs
git commit -m "feat: wire research engine into web and enqueue on outline approval"
```

---

### Task 6: Прогон одной задачи до Completed

> Требуется: Postgres + SearXNG (Phase 4a) + Ollama. Это медленный прогон (много LLM-вызовов и сетевых запросов) — дай ему время.

- [ ] **Step 1: Поднять зависимости и приложение**

```bash
docker compose up -d
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/AiResearchers.Web --no-launch-profile --urls http://127.0.0.1:5390
```

- [ ] **Step 2: Создать и утвердить задачу через wizard**

В браузере пройти wizard (как в Phase 3) и утвердить outline. После approve задача попадёт в очередь и worker начнёт её исполнять (статус `Queued`→`Running`).

- [ ] **Step 3: Наблюдать прогресс в БД**

Периодически (раз в ~20–30 c):
```bash
docker exec airesearchers-postgres psql -U airesearchers -d airesearchers -c "SELECT \"Status\",\"CurrentRound\" FROM \"ResearchTasks\" ORDER BY \"CreatedAt\" DESC LIMIT 1;"
docker exec airesearchers-postgres psql -U airesearchers -d airesearchers -c "SELECT \"Phase\",\"Message\" FROM \"ProgressEvents\" ORDER BY \"Timestamp\" DESC LIMIT 5;"
docker exec airesearchers-postgres psql -U airesearchers -d airesearchers -c "SELECT count(*) AS sources FROM \"Sources\"; SELECT count(*) AS findings FROM \"Findings\";"
```
Expected: статус проходит `Running` с растущим `CurrentRound`; появляются `ProgressEvents`, `Sources`, `Findings`; в конце статус `Completed` и одна строка в `Reports`.

- [ ] **Step 4: Проверить отчёт**

```bash
docker exec airesearchers-postgres psql -U airesearchers -d airesearchers -c "SELECT left(\"MarkdownContent\",400) FROM \"Reports\" ORDER BY \"GeneratedAt\" DESC LIMIT 1;"
```
Expected: непустой Markdown с заголовками секций.

- [ ] **Step 5: Остановить приложение.** Если прогон выявил баг — исправь в соответствующей задаче и переcommit'ь. Эта проверка без отдельного коммита.

---

## Definition of Done (Phase 4b)

- `dotnet build` всего solution — 0 ошибок.
- Утверждённая задача автоматически исполняется worker'ом: `Queued`→`Running`→`Completed`.
- В БД пишутся `FocusAreas`, `Sources` (Found/Fetched/Failed), `Findings`, `ProgressEvents`; создаётся `Report` с Markdown.
- Guard'ы соблюдаются: не более `MaxRounds` раундов, не более `MaxSourcesPerRound` за раунд; Critic может завершить раньше.
- Рестарт приложения переводит зависшие `Running` в `Failed`.
- LLM-вызовы переживают transient через `LlmRetry`.
- Всё закоммичено (Tasks 1–5).

## Что НЕ входит (Phase 5)

- UI экрана прогресса (htmx polling), просмотр отчёта (рендер Markdown + источники), экспорт MD/PDF, кнопка отмены/повтора. Сейчас прогресс и отчёт видны только в БД.
- Тесты — отдельным планом после подтверждения.

## Self-Review (выполнено)

- **Spec coverage:** §3 workflow (Plan→цикл[Search→Fetch→Analyze→Critic]→Write) — Task 3 ✓; §4 запись Source/Finding/ProgressEvent/Report — Task 3 ✓; §6 guard'ы (MaxRounds/MaxSourcesPerRound), lifecycle (Cancelled/Failed/interrupted recovery), retry — Tasks 2,3,4 ✓; очередь/worker (один активный) — Task 4 ✓; enqueue после approve — Task 5 ✓.
- **Placeholder scan:** TBD нет. Обязательная правка про дубль `Finding` в Task 3 Step 2 — явная инструкция исполнителю (исправить in-memory сводку критика на отдельный список), не пропуск.
- **Type consistency:** контракты агентов и DTO (`PlanFocusAreasAsync`/`GenerateQueriesAsync`/`ExtractFindingsAsync`/`CritiqueAsync`/`WriteReportAsync`, `FocusAreasResult.FocusAreas`, `SearchQueriesResult.Queries`, `FindingsResult.Findings`, `CritiqueResult.Enough/NewFocusAreas`), `IResearchOrchestrator.RunAsync`, `IResearchQueue.Enqueue/DequeueAllAsync`, сущности `Source/Finding/FocusArea/ProgressEvent/Report`, `ResearchStatus`/`ProgressPhase`/`SourceStatus`/`EventLevel` — согласованы между Core (Tasks 1–3) и Infrastructure (Tasks 3–4) и Web (Task 5). `AddAgents`/`AddResearchSources`/`AddOrchestration` совпадают с регистрацией в Task 5.
- **Открытый риск (не блокер):** оркестратор использует один scoped `AppDbContext` на весь длительный прогон. Для MVP приемлемо (один worker, последовательно). Если всплывут проблемы с разросшимся change-tracker на длинных прогонах — в Phase 5/hardening разнести на под-scope'ы. Отражено здесь намеренно.
```
