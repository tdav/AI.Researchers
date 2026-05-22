# AI Researchers Platform — Phase 2: LLM Gate + Swappable IChatClient

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Подключить локальную модель Ollama через swappable `Microsoft.Extensions.AI.IChatClient` и прогнать обязательный pre-implementation gate — smoke-test на chat / tool-calling / structured-JSON. По результату — решение о топологии агентов (multi-agent vs single-agent fallback).

**Architecture:** `IChatClient` (из Microsoft.Extensions.AI) — единая swappable точка для LLM. Реализация в Infrastructure через OllamaSharp (`OllamaApiClient` реализует `IChatClient`), обёрнутая в function-invocation pipeline. Отдельный консольный проект `AiResearchers.LlmSmokeTest` гоняет 3 проверки против реальной модели и печатает вердикт. Конфиг (endpoint, model) — в appsettings; смена модели = правка конфига, без изменения кода.

**Tech Stack:** .NET 10, Microsoft.Extensions.AI 10.6.0, Microsoft.Extensions.AI.Abstractions 10.6.0, OllamaSharp 5.4.25, Ollama 0.24 (endpoint `http://localhost:11434`), модель `bjoernb/gemma4-e2b-fast:latest`.

> **Тесты:** по правилу проекта формальные unit/integration тесты в этой фазе НЕ пишутся. Smoke-test — это диагностический **консольный прогон**, не xUnit-тест. Проверка задач — через `dotnet build` и запуск smoke-test. Спека: `docs/superpowers/specs/2026-05-22-ai-researchers-design.md` (§9 Pre-implementation gate).

> **Предусловие:** Ollama запущен (`ollama serve` или служба), модель `bjoernb/gemma4-e2b-fast:latest` уже скачана (`ollama list` показывает её). Fallback-кандидаты, тоже присутствующие локально: `qwen3.5:2b`, `gpt-oss:120b-cloud`.

---

## File Structure (создаётся в этой фазе)

```
src/AiResearchers.Infrastructure/
  Llm/LlmOptions.cs                      # опции конфига (Endpoint, Model, Temperature)
  Llm/LlmServiceCollectionExtensions.cs  # AddLlm(configuration) → регистрирует IChatClient
src/AiResearchers.Web/
  appsettings.json                       # + секция "Llm" (дефолты)
  appsettings.Development.json           # + override модели при необходимости
tools/AiResearchers.LlmSmokeTest/
  AiResearchers.LlmSmokeTest.csproj
  Program.cs                             # 3 проверки + вердикт
docs/superpowers/notes/
  2026-05-22-llm-gate-results.md         # фиксируется ПОСЛЕ прогона (Task 3)
```

---

### Task 1: LLM-конфиг и swappable `IChatClient` в Infrastructure

**Files:**
- Modify: `src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj`
- Create: `src/AiResearchers.Infrastructure/Llm/LlmOptions.cs`
- Create: `src/AiResearchers.Infrastructure/Llm/LlmServiceCollectionExtensions.cs`
- Modify: `src/AiResearchers.Web/appsettings.json`

- [ ] **Step 1: Добавить NuGet-пакеты в Infrastructure**

Run:
```bash
dotnet add src/AiResearchers.Infrastructure package Microsoft.Extensions.AI --version 10.6.0
dotnet add src/AiResearchers.Infrastructure package OllamaSharp --version 5.4.25
dotnet add src/AiResearchers.Infrastructure package Microsoft.Extensions.Options.ConfigurationExtensions --version 10.0.4
```
Expected: `PackageReference for package ... added`.

- [ ] **Step 2: Создать `LlmOptions`**

Create `src/AiResearchers.Infrastructure/Llm/LlmOptions.cs`:
```csharp
namespace AiResearchers.Infrastructure.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "bjoernb/gemma4-e2b-fast:latest";
    public float Temperature { get; set; } = 0.2f;
}
```

- [ ] **Step 3: Создать `AddLlm` DI-расширение**

Create `src/AiResearchers.Infrastructure/Llm/LlmServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace AiResearchers.Infrastructure.Llm;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        LlmOptions options = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()
            ?? new LlmOptions();
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));

        // OllamaApiClient реализует Microsoft.Extensions.AI.IChatClient.
        // Function-invocation pipeline нужен, чтобы tool-calls исполнялись автоматически.
        services.AddSingleton<IChatClient>(_ =>
        {
            OllamaApiClient ollama = new(new Uri(options.Endpoint), options.Model);
            return new ChatClientBuilder(ollama)
                .UseFunctionInvocation()
                .Build();
        });

        return services;
    }
}
```

> **Замечание для исполнителя (swappability):** `IChatClient` — единственная точка зависимости для всего кода-потребителя. Смена модели — через конфиг `Llm:Model`. Смена провайдера (например на OpenAI-совместимый endpoint) — заменой реализации только в этом файле; потребители не меняются.

- [ ] **Step 4: Добавить секцию `Llm` в `appsettings.json`**

Modify `src/AiResearchers.Web/appsettings.json` — добавить секцию `Llm` на верхний уровень:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Llm": {
    "Endpoint": "http://localhost:11434",
    "Model": "bjoernb/gemma4-e2b-fast:latest",
    "Temperature": 0.2
  }
}
```

- [ ] **Step 5: Зарегистрировать в Web (проверка, что DI собирается)**

Modify `src/AiResearchers.Web/Program.cs` — добавить вызов после `AddInfrastructure`:
```csharp
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLlm(builder.Configuration);
```
Добавить using вверху файла:
```csharp
using AiResearchers.Infrastructure.Llm;
```

- [ ] **Step 6: Собрать**

Run: `dotnet build`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/AiResearchers.Infrastructure/Llm/ src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj src/AiResearchers.Web/appsettings.json src/AiResearchers.Web/Program.cs
git commit -m "feat: add swappable ollama IChatClient with config and DI"
```

---

### Task 2: Консольный smoke-test (3 проверки)

**Files:**
- Create: `tools/AiResearchers.LlmSmokeTest/AiResearchers.LlmSmokeTest.csproj`
- Create: `tools/AiResearchers.LlmSmokeTest/Program.cs`

- [ ] **Step 1: Создать консольный проект и добавить в solution**

Run:
```bash
dotnet new console -n AiResearchers.LlmSmokeTest -o tools/AiResearchers.LlmSmokeTest
dotnet sln add tools/AiResearchers.LlmSmokeTest
dotnet add tools/AiResearchers.LlmSmokeTest reference src/AiResearchers.Infrastructure
dotnet add tools/AiResearchers.LlmSmokeTest package Microsoft.Extensions.AI --version 10.6.0
```
Expected: создан проект, reference и пакет добавлены.

- [ ] **Step 2: Написать `Program.cs` с тремя проверками**

Replace `tools/AiResearchers.LlmSmokeTest/Program.cs`:
```csharp
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
```

> **Замечание для исполнителя (API):** код написан под Microsoft.Extensions.AI 10.6.0: `GetResponseAsync(string)`, `GetResponseAsync(string, ChatOptions)`, generic `GetResponseAsync<T>(string)` (structured output), `AIFunctionFactory.Create(delegate)`, `ChatClientBuilder(...).UseFunctionInvocation()`, `ChatResponse.Text`, `ChatResponse<T>.Result`. Если конкретная сигнатура в установленной версии отличается — поправь вызов, сохранив смысл проверки (тот же 3 сценария). Локальные функции с атрибутами `[Description]` поддерживаются `AIFunctionFactory`.

- [ ] **Step 3: Собрать**

Run: `dotnet build tools/AiResearchers.LlmSmokeTest`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add tools/AiResearchers.LlmSmokeTest/ AiResearchers.slnx
git commit -m "feat: add llm gate smoke-test console (chat, tool-calling, json)"
```

---

### Task 3: Прогон gate и фиксация решения

**Files:**
- Create: `docs/superpowers/notes/2026-05-22-llm-gate-results.md`

- [ ] **Step 1: Убедиться, что Ollama доступен**

Run:
```bash
curl -s http://localhost:11434/api/tags | head -c 200
```
Expected: JSON со списком моделей (включая `bjoernb/gemma4-e2b-fast:latest`). Если пусто/ошибка — запустить Ollama (`ollama serve`) и повторить.

- [ ] **Step 2: Прогнать smoke-test против целевой модели**

Run:
```bash
dotnet run --project tools/AiResearchers.LlmSmokeTest
```
Expected: печать `[PASS]/[FAIL]` по 3 проверкам и строка `=== RESULT: N/3 passed ===` + VERDICT. Exit code 0 при 3/3, иначе 1.

- [ ] **Step 3 (опционально): Прогнать fallback-модель для сравнения**

Если целевая модель показала < 3/3 на tool-calling/JSON — прогнать кандидата с лучшим tool-support:
```bash
LLM_MODEL=qwen3.5:2b dotnet run --project tools/AiResearchers.LlmSmokeTest
```
Записать его результат тоже.

- [ ] **Step 4: Зафиксировать результаты и решение**

Create `docs/superpowers/notes/2026-05-22-llm-gate-results.md` — заполнить РЕАЛЬНЫМИ числами прогона:
```markdown
# LLM Gate — результаты прогона

Дата: 2026-05-22

| Модель | basic chat | tool-calling | structured JSON | Итог |
|---|---|---|---|---|
| bjoernb/gemma4-e2b-fast:latest | ? | ? | ? | ?/3 |
| qwen3.5:2b (fallback, если гоняли) | ? | ? | ? | ?/3 |

## Решение (gate decision)

- **3/3 у целевой модели** → топология **multi-agent + deterministic workflow** (как в спеке) подтверждена. Phase 4 идёт по плану.
- **tool-calling FAIL, но chat+JSON OK** → multi-agent с function-tools ненадёжен. Выбрать одно:
  - (a) перейти на **single-agent + structured-JSON** оркестрацию (агенты обмениваются JSON, без function-tools), ИЛИ
  - (b) сменить рабочую модель на fallback с рабочим tool-calling (правка `Llm:Model` в конфиге — код не меняется).
- **chat FAIL** → проблема окружения (Ollama не запущен / модель не скачана), не модели. Починить инфраструктуру и перепрогнать.

## Выбранный путь

<заполнить по факту: какая модель и какая топология идёт в Phase 4>
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/notes/2026-05-22-llm-gate-results.md
git commit -m "docs: record llm gate smoke-test results and topology decision"
```

---

## Definition of Done (Phase 2)

- `dotnet build` всего solution — 0 ошибок.
- `IChatClient` регистрируется в DI через `AddLlm`, модель берётся из конфига (swappable).
- Smoke-test собирается и запускается; получены реальные результаты 3 проверок против `bjoernb/gemma4-e2b-fast:latest`.
- Решение по топологии (multi-agent vs fallback) зафиксировано в `docs/superpowers/notes/2026-05-22-llm-gate-results.md`.
- Всё закоммичено.

## Что НЕ входит (следующие фазы)

- Phase 3 — Interview + Outline (`InterviewService`, wizard, AI доп-вопросы, AI-черновик outline).
- Phase 4 — Research engine (агенты MAF / либо выбранная по gate топология, оркестратор, search/fetch провайдеры, очередь).
- Phase 5 — UI прогресса/отчёта + экспорт.
- Формальные тесты — отдельным планом после подтверждения.

## Self-Review (выполнено)

- **Spec coverage:** §9 спеки (Pre-implementation gate: chat / tool-calling / JSON, swappable `IChatClient`, fallback при провале) — Tasks 1–3 ✓. §2.4 (`IChatClient` в Infrastructure, swappable) — Task 1 ✓.
- **Placeholder scan:** единственные `?`/`<заполнить>` — в файле РЕЗУЛЬТАТОВ (Task 3), который по определению заполняется фактическими числами на этапе прогона; это не пропуск в коде. Два «Замечания для исполнителя» — намеренные пояснения (swappability, возможная правка сигнатуры API), не пропуски.
- **Type consistency:** `LlmOptions.Model/Endpoint/Temperature`, `AddLlm`, `IChatClient`, `OutlineDraft/OutlineDraftSection` используются согласованно между Task 1 и Task 2. `LlmOptions.SectionName="Llm"` совпадает с секцией в `appsettings.json`.
```
