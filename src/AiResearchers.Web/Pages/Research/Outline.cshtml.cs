using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Interview;
using AiResearchers.Core.Orchestration;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class OutlineModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IInterviewService interview;
    private readonly IResearchQueue queue;

    public OutlineModel(AppDbContext db, IInterviewService interview, IResearchQueue queue)
    {
        this.db = db;
        this.interview = interview;
        this.queue = queue;
    }

    public ResearchTask Task { get; private set; } = null!;
    public List<OutlineDraftSection> Sections { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .Include(t => t.OutlineSections)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;

        if (task.OutlineSections.Count > 0)
        {
            this.Sections = task.OutlineSections
                .OrderBy(s => s.Order)
                .Select(s => new OutlineDraftSection { Title = s.Title, Description = s.Description })
                .ToList();
        }
        else
        {
            this.Sections = (await this.interview.GenerateOutlineDraftAsync(task, this.HttpContext.RequestAborted)).ToList();
        }
        return this.Page();
    }

    // Утвердить: сохранить секции и перевести задачу в Queued
    public async Task<IActionResult> OnPostApproveAsync(Guid id, List<string> titles, List<string> descriptions)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.OutlineSections)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }

        this.db.OutlineSections.RemoveRange(task.OutlineSections);
        for (int i = 0; i < titles.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(titles[i]))
            {
                continue;
            }
            this.db.OutlineSections.Add(new OutlineSection
            {
                ResearchTaskId = task.Id,
                Title = titles[i].Trim(),
                Description = i < descriptions.Count ? descriptions[i].Trim() : string.Empty,
                Order = i
            });
        }

        task.Status = ResearchStatus.Queued;
        await this.db.SaveChangesAsync();

        this.queue.Enqueue(task.Id);

        return this.RedirectToPage("/Dashboard/Index");
    }
}
