using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Interview;

public class ClarifyingQuestion
{
    public string Text { get; set; } = string.Empty;
    public QuestionKind Kind { get; set; } = QuestionKind.Single;
    public List<string> Options { get; set; } = new();   // 2-5 concrete suggestions
    public bool AllowOther { get; set; } = true;          // always allow free text
}
