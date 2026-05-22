# AI Researchers Platform — Phase 5: Progress UI + Report View + Export

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Завершить продукт со стороны UI: экран живого прогресса (htmx polling), просмотр готового отчёта (рендер Markdown + источники), экспорт MD/PDF, отмена выполняющейся задачи. Dashboard ведёт на нужный экран в зависимости от статуса.

**Architecture:** Прогресс — Razor-страница, опрашивающая partial с панелью статуса каждые 3 c (htmx `hx-trigger="every 3s"`). Отчёт — Markdig рендерит Markdown в HTML + панель источников. Экспорт — `IReportExporter` (Core контракт): Markdown напрямую, PDF через QuestPDF (Infrastructure). Отмена — singleton-реестр `CancellationTokenSource` по id; worker линкует per-task токен; cancel-handler триггерит, оркестратор ловит `OperationCanceledException` → `Cancelled`.

**Tech Stack:** .NET 10, Razor Pages, htmx 2.0.4 polling, Markdig 1.2.0, QuestPDF 2026.5.0 (Community license).

> **Тесты:** формальные тесты не пишем (правило проекта). Проверка — `dotnet build` + браузерный прогон (Task 6). Спека §5 (экраны прогресса/отчёта, экспорт), §6 (отмена). Зависит от Phases 1–4b (готовый движок, пишущий ProgressEvents/Sources/Findings/Report; статусы и worker).

---

## File Structure (создаётся/меняется)

```
src/AiResearchers.Core/Reporting/
  IReportExporter.cs
src/AiResearchers.Core/Orchestration/
  IResearchCancellation.cs           # реестр отмены
src/AiResearchers.Infrastructure/Reporting/
  ReportExporter.cs                  # MD + PDF (QuestPDF)
  ReportingServiceCollectionExtensions.cs   # AddReporting()
src/AiResearchers.Infrastructure/Orchestration/
  ResearchCancellation.cs            # реализация реестра
  ResearchBackgroundService.cs       # ИЗМЕНИТЬ: per-task linked CTS из реестра
  OrchestrationServiceCollectionExtensions.cs  # ИЗМЕНИТЬ: + IResearchCancellation
src/AiResearchers.Web/Pages/Research/
  Progress.cshtml + .cs              # экран прогресса (polling)
  Report.cshtml + .cs                # просмотр отчёта + экспорт
src/AiResearchers.Web/Pages/Shared/
  _ProgressPanel.cshtml              # partial для polling
src/AiResearchers.Web/Pages/Dashboard/Index.cshtml   # ИЗМЕНИТЬ: ссылки по статусу
src/AiResearchers.Web/Program.cs     # ИЗМЕНИТЬ: AddReporting() + QuestPDF license
```

---

### Task 1: Core — контракты экспорта и отмены

**Files:**
- Create: `src/AiResearchers.Core/Reporting/IReportExporter.cs`
- Create: `src/AiResearchers.Core/Orchestration/IResearchCancellation.cs`

- [ ] **Step 1: `IReportExporter`**

Create `src/AiResearchers.Core/Reporting/IReportExporter.cs`:
```csharp
using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Reporting;

public interface IReportExporter
{
    // Markdown как есть (bytes UTF-8).
    byte[] ToMarkdown(ResearchTask task, Report report);

    // PDF-рендер отчёта.
    byte[] ToPdf(ResearchTask task, Report report);
}
```

- [ ] **Step 2: `IResearchCancellation`**

Create `src/AiResearchers.Core/Orchestration/IResearchCancellation.cs`:
```csharp
namespace AiResearchers.Core.Orchestration;

public interface IResearchCancellation
{
    // Регистрирует токен выполнения задачи; возвращает связанный CTS для линковки во worker.
    CancellationTokenSource Register(Guid researchTaskId, CancellationToken linkedTo);
    void Complete(Guid researchTaskId);
    bool Cancel(Guid researchTaskId);
}
```

- [ ] **Step 3: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Core`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Core/Reporting/ src/AiResearchers.Core/Orchestration/IResearchCancellation.cs
git commit -m "feat: add report exporter and cancellation contracts"
```

---

### Task 2: Infrastructure — exporter (Markdig title + QuestPDF) и DI

**Files:**
- Modify: `src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj`
- Create: `src/AiResearchers.Infrastructure/Reporting/ReportExporter.cs`
- Create: `src/AiResearchers.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs`

- [ ] **Step 1: Пакеты**

Run:
```bash
dotnet add src/AiResearchers.Infrastructure package QuestPDF --version 2026.5.0
```
(Markdig нужен только в Web для рендера HTML — добавим там, в Task 4.)

- [ ] **Step 2: `ReportExporter`**

Create `src/AiResearchers.Infrastructure/Reporting/ReportExporter.cs`:
```csharp
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
```

- [ ] **Step 3: DI-расширение**

Create `src/AiResearchers.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs`:
```csharp
using AiResearchers.Core.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Infrastructure.Reporting;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<IReportExporter, ReportExporter>();
        return services;
    }
}
```

- [ ] **Step 4: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Infrastructure/Reporting/ src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj
git commit -m "feat: add markdown/pdf report exporter with questpdf"
```

---

### Task 3: Отмена — реестр + интеграция во worker

**Files:**
- Create: `src/AiResearchers.Infrastructure/Orchestration/ResearchCancellation.cs`
- Modify: `src/AiResearchers.Infrastructure/Orchestration/ResearchBackgroundService.cs`
- Modify: `src/AiResearchers.Infrastructure/Orchestration/OrchestrationServiceCollectionExtensions.cs`

- [ ] **Step 1: Реализация реестра**

Create `src/AiResearchers.Infrastructure/Orchestration/ResearchCancellation.cs`:
```csharp
using System.Collections.Concurrent;
using AiResearchers.Core.Orchestration;

namespace AiResearchers.Infrastructure.Orchestration;

public class ResearchCancellation : IResearchCancellation
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> running = new();

    public CancellationTokenSource Register(Guid researchTaskId, CancellationToken linkedTo)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        this.running[researchTaskId] = cts;
        return cts;
    }

    public void Complete(Guid researchTaskId)
    {
        if (this.running.TryRemove(researchTaskId, out CancellationTokenSource? cts))
        {
            cts.Dispose();
        }
    }

    public bool Cancel(Guid researchTaskId)
    {
        if (this.running.TryGetValue(researchTaskId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Worker использует per-task linked CTS**

Modify `src/AiResearchers.Infrastructure/Orchestration/ResearchBackgroundService.cs`:
- Добавить зависимость `IResearchCancellation cancellation` в конструктор (поле `this.cancellation`).
- В цикле обработки заменить вызов оркестратора на использование linked-токена:
```csharp
            try
            {
                using IServiceScope scope = this.scopeFactory.CreateScope();
                IResearchOrchestrator orchestrator =
                    scope.ServiceProvider.GetRequiredService<IResearchOrchestrator>();
                CancellationTokenSource cts = this.cancellation.Register(id, stoppingToken);
                try
                {
                    await orchestrator.RunAsync(id, cts.Token);
                }
                finally
                {
                    this.cancellation.Complete(id);
                }
            }
```
(Конструктор: добавить `IResearchCancellation cancellation` параметром и присвоить `this.cancellation = cancellation;`. Добавить using `AiResearchers.Core.Orchestration;` если ещё нет.)

- [ ] **Step 3: Зарегистрировать реестр**

Modify `src/AiResearchers.Infrastructure/Orchestration/OrchestrationServiceCollectionExtensions.cs` — добавить в `AddOrchestration`:
```csharp
        services.AddSingleton<IResearchCancellation, ResearchCancellation>();
```
(перед регистрацией hosted services).

- [ ] **Step 4: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Infrastructure/Orchestration/
git commit -m "feat: add per-task cancellation registry wired into worker"
```

---

### Task 4: Web — экран прогресса (htmx polling) + отмена

**Files:**
- Modify: `src/AiResearchers.Web/AiResearchers.Web.csproj` (Markdig — нужен в Task 5; можно добавить здесь)
- Create: `src/AiResearchers.Web/Pages/Research/Progress.cshtml`
- Create: `src/AiResearchers.Web/Pages/Research/Progress.cshtml.cs`
- Create: `src/AiResearchers.Web/Pages/Shared/_ProgressPanel.cshtml`

- [ ] **Step 1: Добавить Markdig в Web**

Run:
```bash
dotnet add src/AiResearchers.Web package Markdig --version 1.2.0
```

- [ ] **Step 2: PageModel прогресса**

Create `src/AiResearchers.Web/Pages/Research/Progress.cshtml.cs`:
```csharp
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using AiResearchers.Core.Orchestration;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class ProgressModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IResearchCancellation cancellation;

    public ProgressModel(AppDbContext db, IResearchCancellation cancellation)
    {
        this.db = db;
        this.cancellation = cancellation;
    }

    public ResearchTask Task { get; private set; } = null!;
    public int SourceCount { get; private set; }
    public int FindingCount { get; private set; }
    public IReadOnlyList<ProgressEvent> Events { get; private set; } = new List<ProgressEvent>();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        return this.Page();
    }

    // htmx polling-партиал
    public async Task<IActionResult> OnGetPanelAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        return this.Partial("_ProgressPanel", this);
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        this.cancellation.Cancel(id);
        // если задача ещё в очереди и не стартовала — пометим Cancelled напрямую
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is { Status: ResearchStatus.Queued })
        {
            task.Status = ResearchStatus.Cancelled;
            await this.db.SaveChangesAsync();
        }
        return this.RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return false;
        }
        this.Task = task;
        this.SourceCount = await this.db.Sources.CountAsync(s => s.ResearchTaskId == id);
        this.FindingCount = await this.db.Findings.CountAsync(f => f.ResearchTaskId == id);
        this.Events = await this.db.ProgressEvents
            .Where(e => e.ResearchTaskId == id)
            .OrderByDescending(e => e.Timestamp)
            .Take(15)
            .ToListAsync();
        return true;
    }

    public bool IsActive => this.Task.Status is ResearchStatus.Queued or ResearchStatus.Running;
}
```

- [ ] **Step 3: Partial панели**

Create `src/AiResearchers.Web/Pages/Shared/_ProgressPanel.cshtml`:
```cshtml
@model AiResearchers.Web.Pages.Research.ProgressModel
@using AiResearchers.Core.Enums

<div id="progress-panel"
     @if (Model.IsActive) { <text> hx-get="/Research/@Model.Task.Id/Progress?handler=Panel" hx-trigger="every 3s" hx-swap="outerHTML" </text> }>
    <div class="flex items-center gap-3 mb-4">
        <span class="text-xs px-2 py-1 rounded-full bg-gray-800">@Model.Task.Status</span>
        <span class="text-gray-400 text-sm">раунд @Model.Task.CurrentRound / @Model.Task.MaxRounds</span>
        <span class="text-gray-400 text-sm">источников: @Model.SourceCount · фактов: @Model.FindingCount</span>
    </div>

    @if (Model.Task.Status == ResearchStatus.Completed)
    {
        <a href="/Research/@Model.Task.Id/Report" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2 text-sm">Открыть отчёт →</a>
    }
    @if (Model.Task.Status == ResearchStatus.Failed)
    {
        <p class="text-red-400 text-sm mb-2">Ошибка: @Model.Task.FailureReason</p>
    }

    <div class="mt-4 font-mono text-xs bg-black/30 rounded-md p-3 space-y-1">
        @foreach (var e in Model.Events)
        {
            <div class="@(e.Level == EventLevel.Error ? "text-red-400" : e.Level == EventLevel.Warn ? "text-amber-400" : "text-gray-300")">
                [@e.Phase] @e.Message
            </div>
        }
    </div>
</div>
```

- [ ] **Step 4: Страница прогресса**

Create `src/AiResearchers.Web/Pages/Research/Progress.cshtml`:
```cshtml
@page "/Research/{id:guid}/Progress"
@model AiResearchers.Web.Pages.Research.ProgressModel
@using AiResearchers.Core.Enums
@{
    ViewData["Title"] = "Прогресс";
}

<div class="flex items-center justify-between mb-4">
    <h1 class="text-2xl font-semibold">@Model.Task.Topic</h1>
    @if (Model.IsActive)
    {
        <form method="post" asp-page-handler="Cancel" asp-route-id="@Model.Task.Id">
            <button type="submit" class="border border-gray-700 hover:border-gray-500 text-gray-300 rounded-md px-3 py-1.5 text-sm">Отменить</button>
        </form>
    }
</div>

<partial name="_ProgressPanel" model="Model" />
```

- [ ] **Step 5: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Web/Pages/Research/Progress.cshtml src/AiResearchers.Web/Pages/Research/Progress.cshtml.cs src/AiResearchers.Web/Pages/Shared/_ProgressPanel.cshtml src/AiResearchers.Web/AiResearchers.Web.csproj
git commit -m "feat: add live progress screen with htmx polling and cancel"
```

---

### Task 5: Web — просмотр отчёта + экспорт MD/PDF

**Files:**
- Create: `src/AiResearchers.Web/Pages/Research/Report.cshtml`
- Create: `src/AiResearchers.Web/Pages/Research/Report.cshtml.cs`

- [ ] **Step 1: PageModel отчёта (рендер + экспорт-handlers)**

Create `src/AiResearchers.Web/Pages/Research/Report.cshtml.cs`:
```csharp
using AiResearchers.Core.Entities;
using AiResearchers.Core.Reporting;
using AiResearchers.Infrastructure.Persistence;
using Markdig;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Research;

public class ReportModel : PageModel
{
    private readonly AppDbContext db;
    private readonly IReportExporter exporter;

    public ReportModel(AppDbContext db, IReportExporter exporter)
    {
        this.db = db;
        this.exporter = exporter;
    }

    public ResearchTask Task { get; private set; } = null!;
    public Report Report { get; private set; } = null!;
    public HtmlString Html { get; private set; } = HtmlString.Empty;
    public IReadOnlyList<Source> Sources { get; private set; } = new List<Source>();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        this.Html = new HtmlString(Markdown.ToHtml(this.Report.MarkdownContent, pipeline));
        return this.Page();
    }

    public async Task<IActionResult> OnGetExportMdAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        byte[] bytes = this.exporter.ToMarkdown(this.Task, this.Report);
        return this.File(bytes, "text/markdown", $"report-{id}.md");
    }

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        if (!await this.LoadAsync(id))
        {
            return this.NotFound();
        }
        byte[] bytes = this.exporter.ToPdf(this.Task, this.Report);
        return this.File(bytes, "application/pdf", $"report-{id}.pdf");
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        ResearchTask? task = await this.db.ResearchTasks.FirstOrDefaultAsync(t => t.Id == id);
        Report? report = await this.db.Reports.FirstOrDefaultAsync(r => r.ResearchTaskId == id);
        if (task is null || report is null)
        {
            return false;
        }
        this.Task = task;
        this.Report = report;
        this.Sources = await this.db.Sources
            .Where(s => s.ResearchTaskId == id && s.Status == Core.Enums.SourceStatus.Fetched)
            .ToListAsync();
        return true;
    }
}
```

- [ ] **Step 2: Страница отчёта**

Create `src/AiResearchers.Web/Pages/Research/Report.cshtml`:
```cshtml
@page "/Research/{id:guid}/Report"
@model AiResearchers.Web.Pages.Research.ReportModel
@{
    ViewData["Title"] = "Отчёт";
}

<div class="flex items-center justify-between mb-6">
    <h1 class="text-2xl font-semibold">@Model.Task.Topic</h1>
    <div class="flex gap-2">
        <a href="/Research/@Model.Task.Id/Report?handler=ExportMd" class="border border-gray-700 rounded-md px-3 py-1.5 text-sm">⬇ MD</a>
        <a href="/Research/@Model.Task.Id/Report?handler=ExportPdf" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-3 py-1.5 text-sm">⬇ PDF</a>
    </div>
</div>

<div class="flex gap-8">
    <article class="prose prose-invert max-w-none flex-1 leading-relaxed">
        @Model.Html
    </article>
    <aside class="w-64 shrink-0">
        <div class="text-xs uppercase text-gray-500 mb-2">Источники (@Model.Sources.Count)</div>
        <ul class="space-y-2 text-sm">
            @foreach (var s in Model.Sources)
            {
                <li><a href="@s.Url" target="_blank" rel="noopener" class="text-violet-300 hover:underline break-words">@(string.IsNullOrWhiteSpace(s.Title) ? s.Url : s.Title)</a></li>
            }
        </ul>
    </aside>
</div>
```

> **Замечание:** класс `prose` — из Tailwind Typography-плагина, которого нет в Play CDN по умолчанию. Это лишь стилизация; контент отрендерится и без него. Оставляем `prose prose-invert` как прогрессивное улучшение (в hardening можно подключить плагин или задать свои стили).

- [ ] **Step 3: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Web/Pages/Research/Report.cshtml src/AiResearchers.Web/Pages/Research/Report.cshtml.cs
git commit -m "feat: add report view with markdown render, sources and md/pdf export"
```

---

### Task 6: Связать в Program.cs + ссылки на дашборде

**Files:**
- Modify: `src/AiResearchers.Web/Program.cs`
- Modify: `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml`

- [ ] **Step 1: Регистрация reporting + QuestPDF license**

Modify `src/AiResearchers.Web/Program.cs`:
- Добавить usings:
```csharp
using AiResearchers.Infrastructure.Reporting;
using QuestPDF.Infrastructure;
```
- В самом начале `Program.cs` (до `CreateBuilder`) задать лицензию QuestPDF:
```csharp
QuestPDF.Settings.License = LicenseType.Community;
```
- После `AddOrchestration()`:
```csharp
builder.Services.AddReporting();
```

- [ ] **Step 2: Ссылки строк дашборда по статусу**

Modify `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml` — обернуть строку задачи в ссылку: `Completed` → `/Research/{id}/Report`, иначе → `/Research/{id}/Progress`. Заменить блок `@foreach`:
```cshtml
        @foreach (var task in Model.Tasks)
        {
            var href = task.Status == AiResearchers.Core.Enums.ResearchStatus.Completed
                ? $"/Research/{task.Id}/Report"
                : $"/Research/{task.Id}/Progress";
            <a href="@href" class="flex items-center justify-between border border-gray-800 hover:border-gray-600 rounded-md px-4 py-3">
                <span>@task.Topic</span>
                <span class="text-xs px-2 py-1 rounded-full bg-gray-800 text-gray-300">@task.Status</span>
            </a>
        }
```

- [ ] **Step 3: Собрать**

Run: `dotnet build`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AiResearchers.Web/Program.cs src/AiResearchers.Web/Pages/Dashboard/Index.cshtml
git commit -m "feat: register reporting, set questpdf license and link dashboard rows by status"
```

---

### Task 7: Браузерная проверка полного цикла

> Postgres + SearXNG + Ollama запущены.

- [ ] **Step 1: Запустить** `dotnet run --project src/AiResearchers.Web` (Development).

- [ ] **Step 2:** Открыть Dashboard → выбрать `Completed`-задачу (из прогона Phase 4b) → откроется **Report**: отрендеренный Markdown с заголовками секций + панель источников. Нажать **⬇ MD** (скачивается .md) и **⬇ PDF** (скачивается .pdf, открывается, содержит заголовок темы и секции).

- [ ] **Step 3:** Создать новую задачу через wizard, утвердить outline → редирект на Dashboard; кликнуть задачу (`Queued`/`Running`) → **Progress**: панель статуса обновляется каждые 3 c (htmx polling), растут счётчики источников/фактов, идёт лог. Дождаться `Completed` → появляется кнопка «Открыть отчёт».

- [ ] **Step 4:** На активной задаче нажать **Отменить** → статус становится `Cancelled`, polling останавливается.

- [ ] **Step 5:** Остановить приложение. Проверка без коммита; баги — править в соответствующих задачах.

---

## Definition of Done (Phase 5)

- `dotnet build` всего solution — 0 ошибок.
- Dashboard ведёт на Progress (активные/Failed) или Report (Completed).
- Progress: htmx polling каждые 3 c, статус/раунд/счётчики/лог, кнопка отмены работает (→ `Cancelled`).
- Report: Markdown отрендерен в HTML, список источников, экспорт MD и PDF скачиваются и валидны.
- Всё закоммичено (Tasks 1–6).

## Что НЕ входит (опциональный hardening)

- Tailwind Typography (`prose`) и переход Tailwind CDN → standalone CLI-сборка (offline).
- Очистка NU1903 (уязвимый transitive `System.Security.Cryptography.Xml`) — пин на исправленную версию.
- Полный MD→PDF с таблицами/ссылками (сейчас PDF — упрощённый текстовый рендер).
- Авто-возобновление прерванных задач, повтор (`re-run`), пагинация дашборда.
- Тесты — отдельным планом после подтверждения.

## Self-Review (выполнено)

- **Spec coverage:** §5 экран прогресса (polling, счётчики, лог, отмена) — Task 4 ✓; §5 просмотр отчёта (рендер MD + источники) — Task 5 ✓; §5 экспорт MD/PDF — Tasks 2,5 ✓; §6 отмена (CancellationToken → Cancelled) — Tasks 1,3,4 ✓; навигация по статусу — Task 6 ✓.
- **Placeholder scan:** TBD нет. Замечание про `prose` (Tailwind Typography) — прогрессивное улучшение, контент работает и без плагина; явно отнесено в hardening.
- **Type consistency:** `IReportExporter.ToMarkdown/ToPdf(task,report)`, `IResearchCancellation.Register/Complete/Cancel`, `AddReporting`, `ProgressModel`/`ReportModel` handlers (`Panel`/`Cancel`/`ExportMd`/`ExportPdf`), `ResearchStatus`/`EventLevel`/`SourceStatus`, сущности `ResearchTask/Report/Source/ProgressEvent` — согласованы между Core (Task 1), Infrastructure (Tasks 2,3) и Web (Tasks 4–6). Worker-правка использует `IResearchCancellation`, зарегистрированный в Task 3.
- **Зависимость от 4b:** Task 3 модифицирует `ResearchBackgroundService`, созданный в 4b. Конструктор/поля worker'а должны существовать — выполнять Phase 5 строго после 4b.
```
