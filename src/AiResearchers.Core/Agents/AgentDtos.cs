namespace AiResearchers.Core.Agents;

public class FocusAreasResult
{
    public List<string> FocusAreas { get; set; } = new();
}

public class SearchQueriesResult
{
    public List<string> Queries { get; set; } = new();
}

public class FindingItem
{
    public string Text { get; set; } = string.Empty;
    public string? SectionTitle { get; set; }
}

public class FindingsResult
{
    public List<FindingItem> Findings { get; set; } = new();
}

public class CritiqueResult
{
    public bool Enough { get; set; }
    public List<string> NewFocusAreas { get; set; } = new();
}
