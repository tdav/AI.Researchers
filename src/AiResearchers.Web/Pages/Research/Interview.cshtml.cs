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
    private readonly AppDbContext db;
    private readonly IInterviewService interview;

    public InterviewModel(AppDbContext db, IInterviewService interview)
    {
        this.db = db;
        this.interview = interview;
    }

    public ResearchTask Task { get; private set; } = null!;
    public IReadOnlyList<string> Questions { get; private set; } = new List<string>();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;
        return this.Page();
    }

    // htmx: сгенерировать доп-вопросы и вернуть partial
    public async Task<IActionResult> OnPostGenerateAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;
        this.Questions = await this.interview.GenerateFollowUpQuestionsAsync(task, this.HttpContext.RequestAborted);
        return this.Partial("_QuestionsForm", this);
    }

    // Сохранить ответы пользователя и перейти к outline
    public async Task<IActionResult> OnPostSaveAsync(Guid id, List<string> questions, List<string> answers)
    {
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }

        for (int i = 0; i < questions.Count; i++)
        {
            string answer = i < answers.Count ? answers[i] : string.Empty;
            this.db.InterviewAnswers.Add(new InterviewAnswer
            {
                ResearchTaskId = task.Id,
                Question = questions[i],
                Answer = answer,
                Source = AnswerSource.Ai,
                Order = i
            });
        }
        await this.db.SaveChangesAsync();

        return this.RedirectToPage("Outline", new { id = task.Id });
    }
}
