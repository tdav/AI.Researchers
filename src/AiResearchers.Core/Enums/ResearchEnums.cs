namespace AiResearchers.Core.Enums;

public enum ResearchStatus
{
    Draft,
    Interviewing,
    OutlinePending,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum ResearchDepth
{
    Quick,
    Standard,
    Deep
}

public enum AnswerSource
{
    Form,
    Ai
}

public enum SourceStatus
{
    Found,
    Fetched,
    Failed
}

public enum FocusAreaStatus
{
    Pending,
    InProgress,
    Done
}

public enum ProgressPhase
{
    Interview,
    Planning,
    Searching,
    Fetching,
    Analyzing,
    Critiquing,
    Writing,
    Completed
}

public enum EventLevel
{
    Info,
    Warn,
    Error
}

public enum QuestionKind
{
    Single,
    Multi
}
