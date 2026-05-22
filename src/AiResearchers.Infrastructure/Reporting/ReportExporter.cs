using System.Text;
using AiResearchers.Core.Entities;
using AiResearchers.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AiResearchers.Infrastructure.Reporting;

public class ReportExporter : IReportExporter
{
    public byte[] ToMarkdown(ResearchTask task, Report report)
    {
        return Encoding.UTF8.GetBytes(report.MarkdownContent);
    }

    public byte[] ToPdf(ResearchTask task, Report report)
    {
        // MVP: рендерим заголовок + markdown как текст (по строкам), без полного MD-парсинга.
        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Text(task.Topic).FontSize(18).SemiBold();

                page.Content().PaddingVertical(10).Column(col =>
                {
                    foreach (string line in report.MarkdownContent.Replace("\r\n", "\n").Split('\n'))
                    {
                        if (line.StartsWith("## "))
                        {
                            col.Item().PaddingTop(8).Text(line[3..]).FontSize(14).SemiBold();
                        }
                        else if (line.StartsWith("# "))
                        {
                            col.Item().PaddingTop(10).Text(line[2..]).FontSize(16).Bold();
                        }
                        else if (!string.IsNullOrWhiteSpace(line))
                        {
                            col.Item().Text(line.TrimStart('-', ' '));
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("AI Researchers · ");
                    x.Span(report.GeneratedAt.ToString("yyyy-MM-dd"));
                });
            });
        });

        return document.GeneratePdf();
    }
}
