using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Orchestration;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class ProgressModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IResearchCancellation cancellation;

    public ProgressModel(AppDbContext db, IResearchCancellation cancellation)
    {
        this.db = db;
        this.cancellation = cancellation;
    }

    public ResearchTask Task { get; private set; } = null!;
    public int SourceCount { get; private set; }
    public int FindingCount { get; private set; }
    public IReadOnlyList<ProgressEvent> Events { get; private set; } = new List<ProgressEvent>();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        return this.Page();
    }

    // htmx polling-партиал
    public async Task<IActionResult> OnGetPanelAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        return this.Partial("_ProgressPanel", this);
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        this.cancellation.Cancel(id);
        // если задача ещё в очереди и не стартовала — пометим Cancelled напрямую
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is { Status: ResearchStatus.Queued })
        {
            task.Status = ResearchStatus.Cancelled;
            await this.db.SaveChangesAsync();
        }
        return this.RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return false;
        }
        this.Task = task;
        this.SourceCount = await this.db.Sources.CountAsync(s => s.ResearchTaskId == id);
        this.FindingCount = await this.db.Findings.CountAsync(f => f.ResearchTaskId == id);
        this.Events = await this.db.ProgressEvents
            .Where(e => e.ResearchTaskId == id)
            .OrderByDescending(e => e.Timestamp)
            .Take(15)
            .ToListAsync();
        return true;
    }

    public bool IsActive => this.Task.Status is ResearchStatus.Queued or ResearchStatus.Running;
}
