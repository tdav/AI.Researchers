using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OllamaSharp;

string endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT") ?? "http://localhost:11434";
string model = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "bjoernb/gemma4-e2b-fast:latest";

Console.WriteLine($"LLM gate smoke-test\n  endpoint: {endpoint}\n  model:    {model}\n");

OllamaApiClient ollama = new(new Uri(endpoint), model);
IChatClient client = new ChatClientBuilder(ollama)
    .UseFunctionInvocation()
    .Build();

int passed = 0;
int total = 3;

// --- Check 1: базовый chat ---
try
{
    ChatResponse r = await client.GetResponseAsync("Reply with exactly the word: OK");
    string text = r.Text.Trim();
    bool ok = text.Contains("OK", StringComparison.OrdinalIgnoreCase);
    Report(1, "basic chat", ok, ok ? text : $"unexpected: '{text}'");
    if (ok) passed++;
}
catch (Exception ex) { Report(1, "basic chat", false, ex.Message); }

// --- Check 2: tool-calling ---
bool toolCalled = false;
string? toolArg = null;
try
{
    [Description("Get the current temperature for a city")]
    string GetTemperature([Description("City name")] string city)
    {
        toolCalled = true;
        toolArg = city;
        return "12";
    }

    ChatOptions options = new() { Tools = [AIFunctionFactory.Create(GetTemperature)] };
    ChatResponse r = await client.GetResponseAsync(
        "What is the current temperature in Paris? Use the available tool.", options);
    bool ok = toolCalled && !string.IsNullOrWhiteSpace(toolArg)
              && toolArg!.Contains("Paris", StringComparison.OrdinalIgnoreCase);
    Report(2, "tool-calling", ok,
        ok ? $"tool invoked with city='{toolArg}'"
           : $"toolCalled={toolCalled}, arg='{toolArg}', finalText='{r.Text.Trim()}'");
    if (ok) passed++;
}
catch (Exception ex) { Report(2, "tool-calling", false, ex.Message); }

// --- Check 3: structured JSON output ---
try
{
    ChatResponse<OutlineDraft> r = await client.GetResponseAsync<OutlineDraft>(
        "Propose a 3-section report outline for the topic 'electric vehicles'. " +
        "Return JSON with a 'sections' array of objects each having 'title' and 'description'.");
    bool ok = r.Result is { Sections.Count: > 0 }
              && r.Result.Sections.TrueForAll(s => !string.IsNullOrWhiteSpace(s.Title));
    Report(3, "structured JSON", ok,
        ok ? $"{r.Result!.Sections.Count} sections parsed"
           : "could not parse valid OutlineDraft");
    if (ok) passed++;
}
catch (Exception ex) { Report(3, "structured JSON", false, ex.Message); }

Console.WriteLine($"\n=== RESULT: {passed}/{total} passed ===");
if (passed == total)
    Console.WriteLine("VERDICT: multi-agent topology viable with this model.");
else if (passed >= 1)
    Console.WriteLine("VERDICT: PARTIAL. Tool-calling/JSON unreliable → consider fallback (single-agent or stronger model). See plan §Gate decision.");
else
    Console.WriteLine("VERDICT: FAIL. Model/endpoint not usable. Check Ollama is running and model is pulled.");

Environment.Exit(passed == total ? 0 : 1);

static void Report(int n, string name, bool ok, string detail)
    => Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] check {n}: {name} — {detail}");

internal sealed class OutlineDraft
{
    public List<OutlineDraftSection> Sections { get; set; } = new();
}

internal sealed class OutlineDraftSection
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
