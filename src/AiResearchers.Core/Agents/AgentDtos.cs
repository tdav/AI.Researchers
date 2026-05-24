using System.Text;

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

    // Per-section coverage assessment so the orchestrator can see which parts
    // of the outline are still thin, not just an overall enough/not-enough flag.
    public List<SectionCoverage> Coverage { get; set; } = new();

    // Findings that contradict each other across sources, surfaced for review.
    public List<string> Conflicts { get; set; } = new();
}

public class SectionCoverage
{
    public string Section { get; set; } = string.Empty;
    public bool Covered { get; set; }
    public int Confidence { get; set; }
}

// A finding paired with the outline section the analyst tagged it with.
// Kept in memory only (never EF-tracked) and fed to the critic for per-section
// coverage analysis.
public class FindingNote
{
    public string? Section { get; set; }
    public string Text { get; set; } = string.Empty;
}

// Normalizes finding text to a comparison key for near-duplicate detection:
// lowercased, alphanumerics only, single-spaced. Two findings that differ only
// in punctuation/casing/whitespace collapse to the same key.
public static class FindingDedup
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder sb = new(text.Length);
        bool prevSpace = false;
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                prevSpace = false;
            }
            else if (!prevSpace)
            {
                sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString().Trim();
    }
}
