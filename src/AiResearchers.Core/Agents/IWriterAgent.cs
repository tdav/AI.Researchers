using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface IWriterAgent
{
    Task<string> WriteReportAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}
