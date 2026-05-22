using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiResearchers.Web.Pages.Research;

public class NewModel : PageModel
{
    private readonly AppDbContext db;

    public NewModel(AppDbContext db)
    {
        this.db = db;
    }

    [BindProperty]
    public string Topic { get; set; } = string.Empty;

    [BindProperty]
    public ResearchDepth Depth { get; set; } = ResearchDepth.Standard;

    [BindProperty]
    public string Language { get; set; } = "ru";

    [BindProperty]
    public string? Audience { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(this.Topic))
        {
            this.ModelState.AddModelError(nameof(this.Topic), "Тема обязательна.");
            return this.Page();
        }

        ResearchTask task = new()
        {
            Topic = this.Topic.Trim(),
            Depth = this.Depth,
            Language = this.Language,
            Audience = string.IsNullOrWhiteSpace(this.Audience) ? null : this.Audience.Trim(),
            Status = ResearchStatus.Interviewing,
            MaxRounds = this.Depth switch
            {
                ResearchDepth.Quick => 2,
                ResearchDepth.Standard => 3,
                ResearchDepth.Deep => 5,
                _ => 3
            }
        };

        this.db.ResearchTasks.Add(task);
        await this.db.SaveChangesAsync();

        return this.RedirectToPage("Interview", new { id = task.Id });
    }
}
