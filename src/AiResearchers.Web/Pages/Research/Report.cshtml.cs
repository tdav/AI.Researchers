using AiResearchers.Core.Entities;
using AiResearchers.Core.Reporting;
using AiResearchers.Infrastructure.Persistence;
using Markdig;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class ReportModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IReportExporter exporter;

    public ReportModel(AppDbContext db, IReportExporter exporter)
    {
        this.db = db;
        this.exporter = exporter;
    }

    public ResearchTask Task { get; private set; } = null!;
    public Report Report { get; private set; } = null!;
    public HtmlString Html { get; private set; } = HtmlString.Empty;
    public IReadOnlyList<Source> Sources { get; private set; } = new List<Source>();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        this.Html = new HtmlString(Markdown.ToHtml(this.Report.MarkdownContent, pipeline));
        return this.Page();
    }

    public async Task<IActionResult> OnGetExportMdAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        byte[] bytes = this.exporter.ToMarkdown(this.Task, this.Report);
        return this.File(bytes, "text/markdown", $"report-{id}.md");
    }

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        byte[] bytes = this.exporter.ToPdf(this.Task, this.Report);
        return this.File(bytes, "application/pdf", $"report-{id}.pdf");
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        Report? report = await this.db.Reports.FirstOrDefaultAsync(r => r.ResearchTaskId == id);
        if (task is null || report is null)
        {
            return false;
        }
        this.Task = task;
        this.Report = report;
        this.Sources = await this.db.Sources
            .Where(s => s.ResearchTaskId == id && s.Status == Core.Enums.SourceStatus.Fetched)
            .ToListAsync();
        return true;
    }
}
