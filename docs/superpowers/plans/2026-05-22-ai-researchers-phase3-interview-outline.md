# AI Researchers Platform — Phase 3: Interview + Outline

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Реализовать гибридный pre-research опрос (форма + AI доп-вопросы) и AI-черновик структуры результата (outline) с ручной правкой и утверждением. По завершении задача переходит в статус `Queued` (готова для движка Phase 4).

**Architecture:** `InterviewService` (Core) — чистая LLM-логика поверх `IChatClient`, использует structured-JSON выход (проверен gate'ом Phase 2). DB-операции — в Razor PageModels (Web), сервис в БД не лазит. Wizard — Razor Pages + htmx: 3 шага (форма → доп-вопросы → outline). Структурированные ответы модели маппятся в DTO через `GetResponseAsync<T>`.

**Tech Stack:** .NET 10, Microsoft.Extensions.AI 10.6.0 (`IChatClient`, `GetResponseAsync<T>`), EF Core 10, Razor Pages, htmx 2.0.4, Tailwind.

> **Тесты:** формальные тесты не пишем (правило проекта). Проверка — `dotnet build` + ручной прогон wizard (Task 6). Спека: §3 (шаги 1–2), §5 (Wizard). Зависит от Phase 1 (домен/EF/Web) и Phase 2 (`IChatClient`).

> **Предусловие:** Postgres-контейнер запущен; Ollama запущен с моделью. `IChatClient` уже зарегистрирован (Phase 2).

---

## File Structure (создаётся в этой фазе)

```
src/AiResearchers.Core/
  Interview/OutlineDraftSection.cs        # DTO секции черновика
  Interview/IInterviewService.cs          # контракт
  Interview/InterviewService.cs           # LLM-логика (вопросы + outline)
  Interview/InterviewServiceCollectionExtensions.cs  # AddInterview()
src/AiResearchers.Web/Pages/Research/
  New.cshtml + New.cshtml.cs              # шаг 1: форма, создание задачи
  Interview.cshtml + Interview.cshtml.cs  # шаг 2: доп-вопросы (htmx) + ответы
  Outline.cshtml + Outline.cshtml.cs      # шаг 3: черновик outline, правка, утверждение
src/AiResearchers.Web/Pages/Shared/
  _OutlineEditor.cshtml                    # partial: редактор списка секций
```

---

### Task 1: Core — DTO, контракт и `InterviewService`

**Files:**
- Modify: `src/AiResearchers.Core/AiResearchers.Core.csproj`
- Create: `src/AiResearchers.Core/Interview/OutlineDraftSection.cs`
- Create: `src/AiResearchers.Core/Interview/IInterviewService.cs`
- Create: `src/AiResearchers.Core/Interview/InterviewService.cs`
- Create: `src/AiResearchers.Core/Interview/InterviewServiceCollectionExtensions.cs`

- [ ] **Step 1: Добавить пакеты в Core (нужен `IChatClient` + Options)**

Run:
```bash
dotnet add src/AiResearchers.Core package Microsoft.Extensions.AI.Abstractions --version 10.6.0
dotnet add src/AiResearchers.Core package Microsoft.Extensions.Options --version 10.0.4
dotnet add src/AiResearchers.Core package Microsoft.Extensions.DependencyInjection.Abstractions --version 10.0.8
```
Expected: пакеты добавлены.

> Core зависит только от абстракций (`Microsoft.Extensions.AI.Abstractions`), не от OllamaSharp. Реализацию `IChatClient` поставляет Infrastructure (Phase 2). Слои не нарушаются.

- [ ] **Step 2: DTO секции черновика**

Create `src/AiResearchers.Core/Interview/OutlineDraftSection.cs`:
```csharp
namespace AiResearchers.Core.Interview;

public class OutlineDraftSection
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Контракт сервиса**

Create `src/AiResearchers.Core/Interview/IInterviewService.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Interview;

public interface IInterviewService
{
    Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(
        ResearchTask task, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutlineDraftSection>> GenerateOutlineDraftAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Реализация сервиса**

Create `src/AiResearchers.Core/Interview/InterviewService.cs`:
```csharp
using System.Text;
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Interview;

public class InterviewService : IInterviewService
{
    private readonly IChatClient chatClient;

    public InterviewService(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        string prompt =
            $"You are scoping a research task. Topic: \"{task.Topic}\". " +
            $"Depth: {task.Depth}. Audience: {task.Audience ?? "general"}. " +
            $"{this.RenderAnswers(task)}" +
            $"Generate 3-5 concise clarifying questions that would most improve the research direction. " +
            $"Answer language: {task.Language}. " +
            $"Return JSON: {{ \"questions\": [\"...\"] }}.";

        ChatResponse<FollowUpQuestions> response =
            await this.chatClient.GetResponseAsync<FollowUpQuestions>(prompt, cancellationToken: cancellationToken);

        return response.Result?.Questions ?? new List<string>();
    }

    public async Task<IReadOnlyList<OutlineDraftSection>> GenerateOutlineDraftAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        string prompt =
            $"Design the structure of a research report. Topic: \"{task.Topic}\". " +
            $"Depth: {task.Depth}. Audience: {task.Audience ?? "general"}. " +
            $"{this.RenderAnswers(task)}" +
            $"Propose 4-7 report sections, each with a short title and a one-sentence description of what it covers. " +
            $"Section titles and descriptions language: {task.Language}. " +
            $"Return JSON: {{ \"sections\": [{{ \"title\": \"...\", \"description\": \"...\" }}] }}.";

        ChatResponse<OutlineDraft> response =
            await this.chatClient.GetResponseAsync<OutlineDraft>(prompt, cancellationToken: cancellationToken);

        return response.Result?.Sections ?? new List<OutlineDraftSection>();
    }

    private string RenderAnswers(ResearchTask task)
    {
        if (task.InterviewAnswers.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new("Known answers so far:\n");
        foreach (InterviewAnswer a in task.InterviewAnswers)
        {
            sb.Append("- Q: ").Append(a.Question).Append(" A: ").Append(a.Answer).Append('\n');
        }
        return sb.ToString();
    }

    private sealed class FollowUpQuestions
    {
        public List<string> Questions { get; set; } = new();
    }

    private sealed class OutlineDraft
    {
        public List<OutlineDraftSection> Sections { get; set; } = new();
    }
}
```

> **Замечание:** `task.InterviewAnswers` должна быть загружена вызывающим (PageModel грузит задачу с `.Include(t => t.InterviewAnswers)`). Сервис её только читает.

- [ ] **Step 5: DI-расширение**

Create `src/AiResearchers.Core/Interview/InterviewServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Core.Interview;

public static class InterviewServiceCollectionExtensions
{
    public static IServiceCollection AddInterview(this IServiceCollection services)
    {
        services.AddScoped<IInterviewService, InterviewService>();
        return services;
    }
}
```

- [ ] **Step 6: Зарегистрировать в Web `Program.cs`**

Modify `src/AiResearchers.Web/Program.cs` — после `AddLlm(...)`:
```csharp
builder.Services.AddLlm(builder.Configuration);
builder.Services.AddInterview();
```
Добавить using:
```csharp
using AiResearchers.Core.Interview;
```

- [ ] **Step 7: Собрать**

Run: `dotnet build`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/AiResearchers.Core/Interview/ src/AiResearchers.Core/AiResearchers.Core.csproj src/AiResearchers.Web/Program.cs
git commit -m "feat: add interview service for ai follow-up questions and outline draft"
```

---

### Task 2: Web — Шаг 1: форма нового исследования

**Files:**
- Create: `src/AiResearchers.Web/Pages/Research/New.cshtml`
- Create: `src/AiResearchers.Web/Pages/Research/New.cshtml.cs`
- Modify: `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml` (ссылка кнопки)

- [ ] **Step 1: PageModel формы**

Create `src/AiResearchers.Web/Pages/Research/New.cshtml.cs`:
```csharp
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiResearchers.Web.Pages.Research;

public class NewModel : PageModel
{
    private readonly AppDbContext db;

    public NewModel(AppDbContext db)
    {
        this.db = db;
    }

    [BindProperty]
    public string Topic { get; set; } = string.Empty;

    [BindProperty]
    public ResearchDepth Depth { get; set; } = ResearchDepth.Standard;

    [BindProperty]
    public string Language { get; set; } = "ru";

    [BindProperty]
    public string? Audience { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(this.Topic))
        {
            this.ModelState.AddModelError(nameof(this.Topic), "Тема обязательна.");
            return this.Page();
        }

        ResearchTask task = new()
        {
            Topic = this.Topic.Trim(),
            Depth = this.Depth,
            Language = this.Language,
            Audience = string.IsNullOrWhiteSpace(this.Audience) ? null : this.Audience.Trim(),
            Status = ResearchStatus.Interviewing,
            MaxRounds = this.Depth switch
            {
                ResearchDepth.Quick => 2,
                ResearchDepth.Standard => 3,
                ResearchDepth.Deep => 5,
                _ => 3
            }
        };

        this.db.ResearchTasks.Add(task);
        await this.db.SaveChangesAsync();

        return this.RedirectToPage("Interview", new { id = task.Id });
    }
}
```

- [ ] **Step 2: Razor форма**

Create `src/AiResearchers.Web/Pages/Research/New.cshtml`:
```cshtml
@page "/Research/New"
@model AiResearchers.Web.Pages.Research.NewModel
@{
    ViewData["Title"] = "Новое исследование";
}

<h1 class="text-2xl font-semibold mb-6">Новое исследование</h1>

<form method="post" class="space-y-4 max-w-2xl">
    <div>
        <label class="block text-sm text-gray-400 mb-1">Тема</label>
        <input asp-for="Topic" class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2"
               placeholder="Например: рынок локальных LLM в 2026" />
        <span asp-validation-for="Topic" class="text-red-400 text-sm"></span>
    </div>
    <div class="flex gap-4">
        <div class="flex-1">
            <label class="block text-sm text-gray-400 mb-1">Глубина</label>
            <select asp-for="Depth" asp-items="Html.GetEnumSelectList<AiResearchers.Core.Enums.ResearchDepth>()"
                    class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2"></select>
        </div>
        <div class="flex-1">
            <label class="block text-sm text-gray-400 mb-1">Язык отчёта</label>
            <input asp-for="Language" class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2" />
        </div>
    </div>
    <div>
        <label class="block text-sm text-gray-400 mb-1">Аудитория (опционально)</label>
        <input asp-for="Audience" class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2"
               placeholder="Например: технические руководители" />
    </div>
    <button type="submit" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2">
        Далее: уточняющие вопросы →
    </button>
</form>
```

- [ ] **Step 3: Ссылка с дашборда**

Modify `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml` — заменить заглушку кнопки:
```cshtml
    <a href="/Research/New" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2 text-sm">
        + Новое исследование
    </a>
```

- [ ] **Step 4: Собрать**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/AiResearchers.Web/Pages/Research/New.cshtml src/AiResearchers.Web/Pages/Research/New.cshtml.cs src/AiResearchers.Web/Pages/Dashboard/Index.cshtml
git commit -m "feat: add new research form (wizard step 1)"
```

---

### Task 3: Web — Шаг 2: доп-вопросы (htmx) и ответы

**Files:**
- Create: `src/AiResearchers.Web/Pages/Research/Interview.cshtml`
- Create: `src/AiResearchers.Web/Pages/Research/Interview.cshtml.cs`

- [ ] **Step 1: PageModel интервью**

Create `src/AiResearchers.Web/Pages/Research/Interview.cshtml.cs`:
```csharp
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Interview;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class InterviewModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IInterviewService interview;

    public InterviewModel(AppDbContext db, IInterviewService interview)
    {
        this.db = db;
        this.interview = interview;
    }

    public ResearchTask Task { get; private set; } = null!;
    public IReadOnlyList<string> Questions { get; private set; } = new List<string>();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;
        return this.Page();
    }

    // htmx: сгенерировать доп-вопросы и вернуть partial
    public async Task<IActionResult> OnPostGenerateAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;
        this.Questions = await this.interview.GenerateFollowUpQuestionsAsync(task, this.HttpContext.RequestAborted);
        return this.Partial("_QuestionsForm", this);
    }

    // Сохранить ответы пользователя и перейти к outline
    public async Task<IActionResult> OnPostSaveAsync(Guid id, List<string> questions, List<string> answers)
    {
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }

        for (int i = 0; i < questions.Count; i++)
        {
            string answer = i < answers.Count ? answers[i] : string.Empty;
            this.db.InterviewAnswers.Add(new InterviewAnswer
            {
                ResearchTaskId = task.Id,
                Question = questions[i],
                Answer = answer,
                Source = AnswerSource.Ai,
                Order = i
            });
        }
        await this.db.SaveChangesAsync();

        return this.RedirectToPage("Outline", new { id = task.Id });
    }
}
```

- [ ] **Step 2: Создать partial с формой вопросов**

Create `src/AiResearchers.Web/Pages/Shared/_QuestionsForm.cshtml`:
```cshtml
@model AiResearchers.Web.Pages.Research.InterviewModel

@if (Model.Questions.Count == 0)
{
    <p class="text-gray-500">Модель не вернула вопросов — можно пропустить шаг.</p>
    <a href="/Research/@Model.Task.Id/Outline" class="text-violet-300">Перейти к структуре →</a>
}
else
{
    <form method="post" asp-page-handler="Save" asp-route-id="@Model.Task.Id" class="space-y-4">
        @for (int i = 0; i < Model.Questions.Count; i++)
        {
            <div>
                <label class="block text-sm text-gray-300 mb-1">@Model.Questions[i]</label>
                <input type="hidden" name="questions" value="@Model.Questions[i]" />
                <input name="answers" class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2" />
            </div>
        }
        <button type="submit" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2">
            Далее: структура отчёта →
        </button>
    </form>
}
```

- [ ] **Step 3: Создать страницу интервью**

Create `src/AiResearchers.Web/Pages/Research/Interview.cshtml`:
```cshtml
@page "/Research/{id:guid}/Interview"
@model AiResearchers.Web.Pages.Research.InterviewModel
@{
    ViewData["Title"] = "Уточняющие вопросы";
}

<h1 class="text-2xl font-semibold mb-2">Уточняющие вопросы</h1>
<p class="text-gray-400 mb-6">Тема: @Model.Task.Topic</p>

<div id="questions-area">
    <button hx-post="/Research/@Model.Task.Id/Interview?handler=Generate"
            hx-target="#questions-area" hx-swap="innerHTML"
            hx-indicator="#gen-spin"
            class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2">
        Сгенерировать вопросы
    </button>
    <span id="gen-spin" class="htmx-indicator text-gray-400 ml-2">генерирую…</span>
</div>
```

> **Замечание:** htmx-POST с handler требует antiforgery-токен. Добавь в `_Layout.cshtml` в `<head>` мета и настрой htmx слать токен, ЛИБО отключи antiforgery для этих handler'ов. Простейший путь для MVP: в `Program.cs` добавить `builder.Services.AddRazorPages().AddRazorPagesOptions(o => o.Conventions.ConfigureFilter(...))` — НЕ нужно; вместо этого добавь в `_Layout` `<body>` атрибут `hx-headers='{"RequestVerificationToken": "@Html.AntiForgeryToken()"}'` не сработает (это поле). Корректный способ описан в Step 4.

- [ ] **Step 4: Настроить antiforgery для htmx**

Modify `src/AiResearchers.Web/Pages/Shared/_Layout.cshtml` — добавить в самый конец `<body>` (перед `</body>`), чтобы htmx слал токен с каждым POST:
```cshtml
    @Html.AntiForgeryToken()
    <script>
        document.body.addEventListener('htmx:configRequest', function (evt) {
            var token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) { evt.detail.headers['RequestVerificationToken'] = token.value; }
        });
    </script>
```
И в `Program.cs` зарегистрировать antiforgery header name (после `AddRazorPages`):
```csharp
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
```

- [ ] **Step 5: Собрать**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/AiResearchers.Web/Pages/Research/Interview.cshtml src/AiResearchers.Web/Pages/Research/Interview.cshtml.cs src/AiResearchers.Web/Pages/Shared/_QuestionsForm.cshtml src/AiResearchers.Web/Pages/Shared/_Layout.cshtml src/AiResearchers.Web/Program.cs
git commit -m "feat: add interview step with htmx-generated questions (wizard step 2)"
```

---

### Task 4: Web — Шаг 3: outline (черновик, правка, утверждение)

**Files:**
- Create: `src/AiResearchers.Web/Pages/Research/Outline.cshtml`
- Create: `src/AiResearchers.Web/Pages/Research/Outline.cshtml.cs`

- [ ] **Step 1: PageModel outline**

Create `src/AiResearchers.Web/Pages/Research/Outline.cshtml.cs`:
```csharp
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Interview;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class OutlineModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IInterviewService interview;

    public OutlineModel(AppDbContext db, IInterviewService interview)
    {
        this.db = db;
        this.interview = interview;
    }

    public ResearchTask Task { get; private set; } = null!;
    public List<OutlineDraftSection> Sections { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.InterviewAnswers)
            .Include(t => t.OutlineSections)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }
        this.Task = task;

        if (task.OutlineSections.Count > 0)
        {
            this.Sections = task.OutlineSections
                .OrderBy(s => s.Order)
                .Select(s => new OutlineDraftSection { Title = s.Title, Description = s.Description })
                .ToList();
        }
        else
        {
            this.Sections = (await this.interview.GenerateOutlineDraftAsync(task, this.HttpContext.RequestAborted)).ToList();
        }
        return this.Page();
    }

    // Утвердить: сохранить секции и перевести задачу в Queued
    public async Task<IActionResult> OnPostApproveAsync(Guid id, List<string> titles, List<string> descriptions)
    {
        ResearchTask? task = await this.db.ResearchTasks
            .Include(t => t.OutlineSections)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return this.NotFound();
        }

        this.db.OutlineSections.RemoveRange(task.OutlineSections);
        for (int i = 0; i < titles.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(titles[i]))
            {
                continue;
            }
            this.db.OutlineSections.Add(new OutlineSection
            {
                ResearchTaskId = task.Id,
                Title = titles[i].Trim(),
                Description = i < descriptions.Count ? descriptions[i].Trim() : string.Empty,
                Order = i
            });
        }

        task.Status = ResearchStatus.Queued;
        await this.db.SaveChangesAsync();

        return this.RedirectToPage("/Dashboard/Index");
    }
}
```

- [ ] **Step 2: Razor страница с редактором секций**

Create `src/AiResearchers.Web/Pages/Research/Outline.cshtml`:
```cshtml
@page "/Research/{id:guid}/Outline"
@model AiResearchers.Web.Pages.Research.OutlineModel
@{
    ViewData["Title"] = "Структура отчёта";
}

<h1 class="text-2xl font-semibold mb-2">Структура отчёта</h1>
<p class="text-gray-400 mb-6">Тема: @Model.Task.Topic. Отредактируй секции и утверди.</p>

<form method="post" asp-page-handler="Approve" asp-route-id="@Model.Task.Id" class="space-y-3 max-w-3xl">
    <div id="sections">
        @for (int i = 0; i < Model.Sections.Count; i++)
        {
            <div class="border border-gray-800 rounded-md p-3 space-y-2">
                <input name="titles" value="@Model.Sections[i].Title"
                       class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2 font-medium" />
                <textarea name="descriptions" rows="2"
                          class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2 text-sm text-gray-300">@Model.Sections[i].Description</textarea>
            </div>
        }
    </div>
    <button type="button" onclick="addSection()" class="text-violet-300 text-sm">+ добавить секцию</button>
    <div>
        <button type="submit" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2">
            Утвердить и поставить в очередь
        </button>
    </div>
</form>

<template id="section-tpl">
    <div class="border border-gray-800 rounded-md p-3 space-y-2">
        <input name="titles" class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2 font-medium" placeholder="Заголовок секции" />
        <textarea name="descriptions" rows="2" class="w-full bg-gray-900 border border-gray-700 rounded-md px-3 py-2 text-sm text-gray-300" placeholder="Описание"></textarea>
    </div>
</template>
<script>
    function addSection() {
        var tpl = document.getElementById('section-tpl');
        document.getElementById('sections').appendChild(tpl.content.cloneNode(true));
    }
</script>
```

- [ ] **Step 3: Собрать**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/AiResearchers.Web/Pages/Research/Outline.cshtml src/AiResearchers.Web/Pages/Research/Outline.cshtml.cs
git commit -m "feat: add outline draft editor and approval (wizard step 3)"
```

---

### Task 5: Ручная проверка полного wizard

- [ ] **Step 1: Поднять зависимости и запустить**

Run (Postgres-контейнер и Ollama должны работать):
```bash
docker compose up -d
dotnet run --project src/AiResearchers.Web
```
Expected: приложение слушает порт из launchSettings (или укажи `--urls`).

- [ ] **Step 2: Пройти сценарий в браузере**

1. `/Dashboard` → «+ Новое исследование».
2. Заполнить тему (например «локальные LLM 2026»), Depth=Standard, язык `ru`, сохранить → редирект на Interview.
3. Нажать «Сгенерировать вопросы» → через несколько секунд htmx подставит 3-5 вопросов. Заполнить ответы → «Далее».
4. На Outline появится AI-черновик из 4-7 секций. Отредактировать заголовок, добавить секцию, удалить текст одной → «Утвердить и поставить в очередь».
5. Редирект на Dashboard: задача видна со статусом `Queued`.

Expected: каждый шаг работает; в БД появились `InterviewAnswers` и `OutlineSections`; `ResearchTasks.Status = Queued`.

- [ ] **Step 3: Проверить БД**

Run:
```bash
docker exec airesearchers-postgres psql -U airesearchers -d airesearchers -c "SELECT \"Status\" FROM \"ResearchTasks\" ORDER BY \"CreatedAt\" DESC LIMIT 1; SELECT count(*) FROM \"OutlineSections\"; SELECT count(*) FROM \"InterviewAnswers\";"
```
Expected: статус `Queued`; счётчики секций и ответов > 0.

- [ ] **Step 4: Остановить приложение** (Ctrl+C).

> Эта задача — ручная проверка, без коммита (кода не меняет). Если на шаге всплыла ошибка — это баг реализации предыдущих задач, исправь там и переcommit'ь.

---

## Definition of Done (Phase 3)

- `dotnet build` всего solution — 0 ошибок.
- Wizard из 3 шагов работает end-to-end: форма → AI-вопросы (htmx) → AI-outline (правка) → утверждение.
- После утверждения: `InterviewAnswers` и `OutlineSections` сохранены, `ResearchTask.Status = Queued`.
- Всё закоммичено (Tasks 1–4).

## Что НЕ входит (следующие фазы)

- Phase 4 — Research engine: чтение `Queued`-задач из очереди, агенты, оркестратор, search/fetch, прогресс. (Сейчас `Queued`-задача просто стоит — потребителя нет.)
- Phase 5 — UI прогресса/отчёта + экспорт.
- Тесты — отдельным планом после подтверждения.

## Self-Review (выполнено)

- **Spec coverage:** §3 шаг 1 (форма) — Task 2 ✓; §3 шаг 1 AI доп-вопросы (гибрид) — Task 3 ✓; §3 шаг 2 AI-черновик outline + правка + утверждение — Task 4 ✓; §5 Wizard (3 шага) — Tasks 2–4 ✓. Статусы `Interviewing`→`Queued` соответствуют §4.
- **Placeholder scan:** в коде пропусков нет. «Замечание» про antiforgery в Task 3 решено конкретно в Step 4 (header name + htmx configRequest + токен). Task 5 — намеренно ручная проверка без коммита.
- **Type consistency:** `IInterviewService.GenerateFollowUpQuestionsAsync/GenerateOutlineDraftAsync`, `OutlineDraftSection {Title,Description}`, `AddInterview()`, `AnswerSource.Ai`, `ResearchStatus.Interviewing/Queued`, `OutlineSection {Title,Description,Order,ResearchTaskId}` — согласованы между Core (Task 1) и Web (Tasks 2–4). Имена полей форм (`questions`/`answers`, `titles`/`descriptions`) совпадают между Razor и PageModel-параметрами.
```
