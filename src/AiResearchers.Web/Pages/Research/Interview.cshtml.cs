using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Interview;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class InterviewModel : PageModel
{
    private const int MaxRounds = 3;

    private readonly AppDbContext db;
    private readonly IInterviewService interview;

    public InterviewModel(AppDbContext db, IInterviewService interview)
    {
        this.db = db;
        this.interview = interview;
    }

    public ResearchTask Task { get; private set; } = null!;

    public int Round { get; private set; } = 1;
    public int MaxInterviewRounds => MaxRounds;
    public IReadOnlyList<ClarifyingQuestion> Questions { get; private set; } = new List<ClarifyingQuestion>();
    public bool EnoughRecommended { get; private set; }
    public string? Rationale { get; private set; }

    public IReadOnlyList<KeyValuePair<int, List<InterviewAnswer>>> PriorRounds { get; private set; } =
        new List<KeyValuePair<int, List<InterviewAnswer>>>();

    [BindProperty]
    public List<QuestionPost> Posted { get; set; } = new();

    // Form field is named "Round" (see _ClarifyingRound.cshtml hidden input).
    [BindProperty(Name = "Round")]
    public int PostedRound { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ResearchTask? task = await this.LoadTask(id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;
        this.LoadPriorRounds(task);
        return this.Page();
    }

    // htmx: generate the first clarifying round and return the partial.
    public async Task<IActionResult> OnPostGenerateAsync(Guid id)
    {
        ResearchTask? task = await this.LoadTask(id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;
        this.LoadPriorRounds(task);
        this.Round = this.NextRoundNumber(task);
        await this.GenerateRound(task);
        return this.Partial("_ClarifyingRound", this);
    }

    // htmx: persist answers for the posted round, then either generate the next round
    // (action=more) or redirect to the outline (action=done).
    public async Task<IActionResult> OnPostSaveAsync(Guid id, string action)
    {
        ResearchTask? task = await this.LoadTask(id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;

        int savedRound = this.PostedRound > 0 ? this.PostedRound : this.NextRoundNumber(task);
        this.PersistAnswers(task, savedRound);
        await this.db.SaveChangesAsync();

        task = await this.LoadTask(id);
        this.Task = task!;
        this.LoadPriorRounds(task!);

        if (string.Equals(action, "done", StringComparison.OrdinalIgnoreCase))
        {
            this.Response.Headers["HX-Redirect"] = $"/Research/{id}/Outline";
            return new EmptyResult();
        }

        this.Round = this.NextRoundNumber(task!);
        await this.GenerateRound(task!);
        return this.Partial("_ClarifyingRound", this);
    }

    private async Task<ResearchTask?> LoadTask(Guid id) =>
        await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .FirstOrDefaultAsync(t => t.Id == id);

    private void LoadPriorRounds(ResearchTask task)
    {
        this.PriorRounds = task.InterviewAnswers
            .GroupBy(a => a.Round)
            .OrderBy(g => g.Key)
            .Select(g => new KeyValuePair<int, List<InterviewAnswer>>(
                g.Key, g.OrderBy(a => a.Order).ToList()))
            .ToList();
    }

    private int NextRoundNumber(ResearchTask task) =>
        task.InterviewAnswers.Count == 0 ? 1 : task.InterviewAnswers.Max(a => a.Round) + 1;

    private async Task GenerateRound(ResearchTask task)
    {
        InterviewRound round =
            await this.interview.GenerateClarifyingRoundAsync(task, this.Round, MaxRounds, this.HttpContext.RequestAborted);
        this.Questions = round.Questions;
        this.EnoughRecommended = round.EnoughRecommended;
        this.Rationale = round.Rationale;
    }

    private void PersistAnswers(ResearchTask task, int round)
    {
        for (int i = 0; i < this.Posted.Count; i++)
        {
            QuestionPost p = this.Posted[i];
            if (string.IsNullOrWhiteSpace(p.Text))
            {
                continue;
            }

            List<string> parts = new();
            if (p.Kind == QuestionKind.Multi)
            {
                parts.AddRange(p.SelectedOptions.Where(o => !string.IsNullOrWhiteSpace(o)));
            }
            else if (!string.IsNullOrWhiteSpace(p.Selected))
            {
                parts.Add(p.Selected);
            }
            if (!string.IsNullOrWhiteSpace(p.Other))
            {
                parts.Add(p.Other.Trim());
            }

            this.db.InterviewAnswers.Add(new InterviewAnswer
            {
                ResearchTaskId = task.Id,
                Question = p.Text,
                Answer = string.Join("; ", parts),
                Source = AnswerSource.Ai,
                Order = i,
                Round = round
            });
        }
    }
}

public class QuestionPost
{
    public string Text { get; set; } = string.Empty;
    public QuestionKind Kind { get; set; }
    public string? Selected { get; set; }
    public List<string> SelectedOptions { get; set; } = new();
    public string? Other { get; set; }
}
