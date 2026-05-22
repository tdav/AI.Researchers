using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Reporting;

public interface IReportExporter
{
    // Markdown как есть (bytes UTF-8).
    byte[] ToMarkdown(ResearchTask task, Report report);

    // PDF-рендер отчёта.
    byte[] ToPdf(ResearchTask task, Report report);
}
