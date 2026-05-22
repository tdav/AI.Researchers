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
