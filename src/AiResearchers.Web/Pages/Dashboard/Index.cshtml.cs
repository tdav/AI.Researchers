using AiResearchers.Core.Entities;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly AppDbContext db;

    public IndexModel(AppDbContext db)
    {
        this.db = db;
    }

    public IReadOnlyList<ResearchTask> Tasks { get; private set; } = new List<ResearchTask>();

    public async Task OnGetAsync()
    {
        this.Tasks = await this.db.ResearchTasks
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}
