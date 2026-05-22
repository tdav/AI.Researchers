# AI Researchers Platform — Phase 1: Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Создать рабочий каркас платформы: .NET 10 solution из 4 проектов, доменная модель, EF Core + PostgreSQL со схемой, и ASP.NET Core Web с dashboard (htmx + Tailwind, dark mode), который запускается и читает задачи из БД.

**Architecture:** Слоистое решение (Core / Infrastructure / Web / Tests-скелет). Core — доменные сущности и контракты. Infrastructure — `AppDbContext` (Npgsql) и DI. Web — ASP.NET Core с Razor-layout, htmx-фрагментами и Tailwind dark mode. PostgreSQL поднимается через Docker Compose.

**Tech Stack:** .NET 10 (10.0.300), ASP.NET Core, EF Core 10.0.1, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1, PostgreSQL 17 (Docker), htmx 2.0.4, Tailwind CSS (Play CDN), Docker Compose.

> **Тесты:** по правилу проекта в этой фазе тесты НЕ пишутся. Проверка каждой задачи — через `dotnet build` и ручной запуск. Тестовый проект создаётся как пустой скелет; тестовая стратегия (§7 спеки) оформляется отдельным планом после подтверждения. Спека: `docs/superpowers/specs/2026-05-22-ai-researchers-design.md`.

---

## File Structure (создаётся в этой фазе)

```
AI.Researchers.Claude/
  AiResearchers.sln
  .editorconfig                              # стиль: this.-квалификация полей, без _-префикса
  .gitignore
  Directory.Build.props                      # общие свойства: net10.0, nullable, implicit usings
  docker-compose.yml                         # postgres:17
  src/
    AiResearchers.Core/
      AiResearchers.Core.csproj
      Enums/ResearchEnums.cs                  # все enum'ы домена
      Entities/ResearchTask.cs
      Entities/InterviewAnswer.cs
      Entities/OutlineSection.cs
      Entities/FocusArea.cs
      Entities/Source.cs
      Entities/Finding.cs
      Entities/ProgressEvent.cs
      Entities/Report.cs
    AiResearchers.Infrastructure/
      AiResearchers.Infrastructure.csproj
      Persistence/AppDbContext.cs
      Persistence/AppDbContextFactory.cs        # design-time factory для миграций
      Persistence/Configurations/ResearchTaskConfiguration.cs
      Persistence/Configurations/FindingConfiguration.cs
      DependencyInjection.cs                  # AddInfrastructure(...)
    AiResearchers.Web/
      AiResearchers.Web.csproj
      Program.cs
      appsettings.json
      appsettings.Development.json
      Components/                             # Razor Pages/Views
        _ViewImports.cshtml
        Shared/_Layout.cshtml
        Dashboard/Index.cshtml
        Dashboard/Index.cshtml.cs
      wwwroot/css/app.css
  tests/
    AiResearchers.Tests/
      AiResearchers.Tests.csproj              # пустой скелет, без тестов
```

> Проект `AiResearchers.Agents` и реализации провайдеров поиска/скрейпинга создаются в Фазе 4. Фаза 1 их не трогает.

---

### Task 0: Инициализация git

**Files:**
- Create: `.gitignore`

- [ ] **Step 1: Инициализировать репозиторий**

Run:
```bash
git init
git branch -M main
```
Expected: `Initialized empty Git repository in C:/Works_AI/AI.Researchers.Claude/.git/`

- [ ] **Step 2: Создать `.gitignore`**

Create `.gitignore`:
```gitignore
bin/
obj/
*.user
.vs/
.idea/
.superpowers/
appsettings.*.local.json
*.db
```

- [ ] **Step 3: Первый коммит**

```bash
git add .gitignore docs/
git commit -m "chore: init repo with design spec and phase 1 plan"
```
Expected: commit создан, `git status` чистый кроме неотслеженных будущих файлов.

---

### Task 1: PostgreSQL через Docker Compose

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Создать `docker-compose.yml`**

Create `docker-compose.yml`:
```yaml
services:
  postgres:
    image: postgres:17
    container_name: airesearchers-postgres
    environment:
      POSTGRES_USER: airesearchers
      POSTGRES_PASSWORD: airesearchers_dev
      POSTGRES_DB: airesearchers
    ports:
      - "5433:5432"
    volumes:
      - airesearchers_pgdata:/var/lib/postgresql/data

volumes:
  airesearchers_pgdata:
```

> Порт 5433 на хосте — чтобы не конфликтовать с локальным Postgres на 5432.

- [ ] **Step 2: Поднять контейнер**

Run:
```bash
docker compose up -d
```
Expected: `Container airesearchers-postgres  Started`

- [ ] **Step 3: Проверить готовность**

Run:
```bash
docker exec airesearchers-postgres pg_isready -U airesearchers
```
Expected: `/var/run/postgresql:5432 - accepting connections`

- [ ] **Step 4: Commit**

```bash
git add docker-compose.yml
git commit -m "chore: add postgres docker-compose"
```

---

### Task 2: Solution и проекты

**Files:**
- Create: `AiResearchers.sln`, `Directory.Build.props`, `.editorconfig`
- Create: `src/AiResearchers.Core/AiResearchers.Core.csproj`
- Create: `src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj`
- Create: `src/AiResearchers.Web/AiResearchers.Web.csproj`
- Create: `tests/AiResearchers.Tests/AiResearchers.Tests.csproj`

- [ ] **Step 1: Создать `Directory.Build.props`**

Create `Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Создать `.editorconfig` со стилем проекта**

Create `.editorconfig`:
```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
# Поля без _-префикса, обращение через this.
dotnet_style_qualification_for_field = true:warning
dotnet_style_qualification_for_property = true:suggestion
dotnet_style_qualification_for_method = true:suggestion
# Приватные поля: camelCase, без префикса
dotnet_naming_rule.private_fields_camel_case.severity = warning
dotnet_naming_rule.private_fields_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_camel_case.style = camel_case_style
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.camel_case_style.capitalization = camel_case
```

- [ ] **Step 3: Создать проекты и solution**

Run:
```bash
dotnet new sln -n AiResearchers
dotnet new classlib -n AiResearchers.Core -o src/AiResearchers.Core
dotnet new classlib -n AiResearchers.Infrastructure -o src/AiResearchers.Infrastructure
dotnet new web -n AiResearchers.Web -o src/AiResearchers.Web
dotnet new xunit -n AiResearchers.Tests -o tests/AiResearchers.Tests
```
Expected: четыре `The template ... was created successfully.`

> Удалить автогенерённые `Class1.cs` в Core и Infrastructure, и `UnitTest1.cs` в Tests:
```bash
rm src/AiResearchers.Core/Class1.cs src/AiResearchers.Infrastructure/Class1.cs tests/AiResearchers.Tests/UnitTest1.cs
```

- [ ] **Step 4: Связать проекты references и добавить в solution**

Run:
```bash
dotnet sln add src/AiResearchers.Core src/AiResearchers.Infrastructure src/AiResearchers.Web tests/AiResearchers.Tests
dotnet add src/AiResearchers.Infrastructure reference src/AiResearchers.Core
dotnet add src/AiResearchers.Web reference src/AiResearchers.Core src/AiResearchers.Infrastructure
dotnet add tests/AiResearchers.Tests reference src/AiResearchers.Core src/AiResearchers.Infrastructure
```
Expected: `Reference ... added to the project.`

- [ ] **Step 5: Собрать**

Run:
```bash
dotnet build
```
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 6: Commit**

```bash
git add AiResearchers.sln Directory.Build.props .editorconfig src/ tests/
git commit -m "chore: scaffold solution with core, infrastructure, web, tests projects"
```

---

### Task 3: Доменные enum'ы

**Files:**
- Create: `src/AiResearchers.Core/Enums/ResearchEnums.cs`

- [ ] **Step 1: Создать enum'ы**

Create `src/AiResearchers.Core/Enums/ResearchEnums.cs`:
```csharp
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
```

- [ ] **Step 2: Собрать**

Run: `dotnet build src/AiResearchers.Core`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/AiResearchers.Core/Enums/ResearchEnums.cs
git commit -m "feat: add domain enums"
```

---

### Task 4: Доменные сущности

**Files:**
- Create: `src/AiResearchers.Core/Entities/ResearchTask.cs`
- Create: `src/AiResearchers.Core/Entities/InterviewAnswer.cs`
- Create: `src/AiResearchers.Core/Entities/OutlineSection.cs`
- Create: `src/AiResearchers.Core/Entities/FocusArea.cs`
- Create: `src/AiResearchers.Core/Entities/Source.cs`
- Create: `src/AiResearchers.Core/Entities/Finding.cs`
- Create: `src/AiResearchers.Core/Entities/ProgressEvent.cs`
- Create: `src/AiResearchers.Core/Entities/Report.cs`

- [ ] **Step 1: `ResearchTask`**

Create `src/AiResearchers.Core/Entities/ResearchTask.cs`:
```csharp
using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class ResearchTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = string.Empty;
    public ResearchStatus Status { get; set; } = ResearchStatus.Draft;
    public ResearchDepth Depth { get; set; } = ResearchDepth.Standard;
    public string Language { get; set; } = "ru";
    public string? Audience { get; set; }
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; } = 3;
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<InterviewAnswer> InterviewAnswers { get; set; } = new();
    public List<OutlineSection> OutlineSections { get; set; } = new();
    public List<FocusArea> FocusAreas { get; set; } = new();
    public List<Source> Sources { get; set; } = new();
    public List<Finding> Findings { get; set; } = new();
    public List<ProgressEvent> ProgressEvents { get; set; } = new();
    public Report? Report { get; set; }
}
```

- [ ] **Step 2: `InterviewAnswer`**

Create `src/AiResearchers.Core/Entities/InterviewAnswer.cs`:
```csharp
using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class InterviewAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public AnswerSource Source { get; set; }
    public int Order { get; set; }

    public ResearchTask? ResearchTask { get; set; }
}
```

- [ ] **Step 3: `OutlineSection`**

Create `src/AiResearchers.Core/Entities/OutlineSection.cs`:
```csharp
namespace AiResearchers.Core.Entities;

public class OutlineSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }

    public ResearchTask? ResearchTask { get; set; }
    public List<Finding> Findings { get; set; } = new();
}
```

- [ ] **Step 4: `FocusArea`**

Create `src/AiResearchers.Core/Entities/FocusArea.cs`:
```csharp
using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class FocusArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public int Round { get; set; }
    public string Title { get; set; } = string.Empty;
    public FocusAreaStatus Status { get; set; } = FocusAreaStatus.Pending;

    public ResearchTask? ResearchTask { get; set; }
    public List<Source> Sources { get; set; } = new();
}
```

- [ ] **Step 5: `Source`**

Create `src/AiResearchers.Core/Entities/Source.cs`:
```csharp
using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public Guid? FocusAreaId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public SourceStatus Status { get; set; } = SourceStatus.Found;
    public string? ExtractedText { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }

    public ResearchTask? ResearchTask { get; set; }
    public FocusArea? FocusArea { get; set; }
    public List<Finding> Findings { get; set; } = new();
}
```

- [ ] **Step 6: `Finding`**

Create `src/AiResearchers.Core/Entities/Finding.cs`:
```csharp
namespace AiResearchers.Core.Entities;

public class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public Guid? OutlineSectionId { get; set; }
    public Guid SourceId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Round { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ResearchTask? ResearchTask { get; set; }
    public OutlineSection? OutlineSection { get; set; }
    public Source? Source { get; set; }
}
```

- [ ] **Step 7: `ProgressEvent`**

Create `src/AiResearchers.Core/Entities/ProgressEvent.cs`:
```csharp
using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class ProgressEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ProgressPhase Phase { get; set; }
    public EventLevel Level { get; set; } = EventLevel.Info;
    public string Message { get; set; } = string.Empty;

    public ResearchTask? ResearchTask { get; set; }
}
```

- [ ] **Step 8: `Report`**

Create `src/AiResearchers.Core/Entities/Report.cs`:
```csharp
namespace AiResearchers.Core.Entities;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public string MarkdownContent { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public ResearchTask? ResearchTask { get; set; }
}
```

- [ ] **Step 9: Собрать**

Run: `dotnet build src/AiResearchers.Core`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 10: Commit**

```bash
git add src/AiResearchers.Core/Entities/
git commit -m "feat: add domain entities"
```

---

### Task 5: EF Core DbContext и Npgsql

**Files:**
- Modify: `src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj`
- Create: `src/AiResearchers.Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/AiResearchers.Infrastructure/Persistence/Configurations/ResearchTaskConfiguration.cs`
- Create: `src/AiResearchers.Infrastructure/Persistence/Configurations/FindingConfiguration.cs`
- Create: `src/AiResearchers.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Добавить NuGet-пакеты в Infrastructure**

Run:
```bash
dotnet add src/AiResearchers.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.1
dotnet add src/AiResearchers.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 10.0.1
dotnet add src/AiResearchers.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions --version 10.0.0
dotnet add src/AiResearchers.Infrastructure package Microsoft.Extensions.Configuration.Abstractions --version 10.0.0
```
Expected: `PackageReference for package ... added`.

- [ ] **Step 2: Создать `AppDbContext`**

Create `src/AiResearchers.Infrastructure/Persistence/AppDbContext.cs`:
```csharp
using AiResearchers.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ResearchTask> ResearchTasks => this.Set<ResearchTask>();
    public DbSet<InterviewAnswer> InterviewAnswers => this.Set<InterviewAnswer>();
    public DbSet<OutlineSection> OutlineSections => this.Set<OutlineSection>();
    public DbSet<FocusArea> FocusAreas => this.Set<FocusArea>();
    public DbSet<Source> Sources => this.Set<Source>();
    public DbSet<Finding> Findings => this.Set<Finding>();
    public DbSet<ProgressEvent> ProgressEvents => this.Set<ProgressEvent>();
    public DbSet<Report> Reports => this.Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 3: Создать конфигурацию `ResearchTask` (связи + каскады)**

Create `src/AiResearchers.Infrastructure/Persistence/Configurations/ResearchTaskConfiguration.cs`:
```csharp
using AiResearchers.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiResearchers.Infrastructure.Persistence.Configurations;

public class ResearchTaskConfiguration : IEntityTypeConfiguration<ResearchTask>
{
    public void Configure(EntityTypeBuilder<ResearchTask> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Topic).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Language).HasMaxLength(16).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Depth).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(t => t.Status);

        builder.HasMany(t => t.InterviewAnswers).WithOne(a => a.ResearchTask)
            .HasForeignKey(a => a.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.OutlineSections).WithOne(s => s.ResearchTask)
            .HasForeignKey(s => s.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.FocusAreas).WithOne(f => f.ResearchTask)
            .HasForeignKey(f => f.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Sources).WithOne(s => s.ResearchTask)
            .HasForeignKey(s => s.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Findings).WithOne(f => f.ResearchTask)
            .HasForeignKey(f => f.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.ProgressEvents).WithOne(e => e.ResearchTask)
            .HasForeignKey(e => e.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.Report).WithOne(r => r.ResearchTask)
            .HasForeignKey<Report>(r => r.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

> **Замечание для исполнителя:** в этой конфигурации описаны только связи, исходящие из `ResearchTask` (одна-ко-многим к дочерним сущностям + один-к-одному к `Report`). Связи `Finding → Source` и `Finding → OutlineSection` настраиваются отдельно в Step 3b, потому что они идут НЕ от `ResearchTask`.

- [ ] **Step 3b: Создать конфигурацию `Finding` (связи к Source и OutlineSection)**

Create `src/AiResearchers.Infrastructure/Persistence/Configurations/FindingConfiguration.cs`:
```csharp
using AiResearchers.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiResearchers.Infrastructure.Persistence.Configurations;

public class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Text).IsRequired();

        // Finding → Source: Restrict, чтобы избежать множественных каскадных путей
        // к ResearchTask (Finding уже каскадно удаляется через ResearchTask).
        builder.HasOne(f => f.Source).WithMany(s => s.Findings)
            .HasForeignKey(f => f.SourceId).OnDelete(DeleteBehavior.Restrict);

        // Finding → OutlineSection: опциональная (nullable FK), при удалении секции FK обнуляется.
        builder.HasOne(f => f.OutlineSection).WithMany(o => o.Findings)
            .HasForeignKey(f => f.OutlineSectionId).OnDelete(DeleteBehavior.SetNull);
    }
}
```

> **Замечание:** `Finding → ResearchTask` (каскад) уже описан в `ResearchTaskConfiguration` через `HasMany(t => t.Findings)`. Здесь его повторять не нужно — EF свяжет обе конфигурации по одной и той же навигации/FK.

- [ ] **Step 4: Создать `DependencyInjection.cs`**

Create `src/AiResearchers.Infrastructure/DependencyInjection.cs`:
```csharp
using AiResearchers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
```

- [ ] **Step 5: Собрать**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/AiResearchers.Infrastructure/
git commit -m "feat: add ef core dbcontext, entity config and DI for postgres"
```

---

### Task 6: EF миграция и применение схемы

**Files:**
- Create: `src/AiResearchers.Infrastructure/Migrations/` (генерируется)
- Modify: `src/AiResearchers.Web/appsettings.Development.json` (connection string — создаётся в Task 7; здесь используем переменную для design-time)

> Для генерации миграции нужен connection string на design-time. Web ещё не настроен (Task 7), поэтому в этой задаче используем design-time factory.

- [ ] **Step 1: Установить инструмент `dotnet-ef`**

Run:
```bash
dotnet tool install --global dotnet-ef --version 10.0.1
```
Expected: `Tool 'dotnet-ef' (version '10.0.1') was successfully installed.`
(Если уже установлен другой версии — `dotnet tool update --global dotnet-ef --version 10.0.1`.)

- [ ] **Step 2: Создать design-time factory**

Create `src/AiResearchers.Infrastructure/Persistence/AppDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiResearchers.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            "Host=localhost;Port=5433;Database=airesearchers;Username=airesearchers;Password=airesearchers_dev";

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
```

- [ ] **Step 3: Создать миграцию**

Run:
```bash
dotnet ef migrations add InitialCreate --project src/AiResearchers.Infrastructure --startup-project src/AiResearchers.Infrastructure
```
Expected: `Done. To undo this action, use 'ef migrations remove'.` Папка `Migrations/` появилась с файлом `*_InitialCreate.cs`.

- [ ] **Step 4: Применить миграцию к БД (контейнер из Task 1 должен быть запущен)**

Run:
```bash
dotnet ef database update --project src/AiResearchers.Infrastructure --startup-project src/AiResearchers.Infrastructure
```
Expected: `Applying migration '...InitialCreate'. Done.`

- [ ] **Step 5: Проверить таблицы в БД**

Run:
```bash
docker exec airesearchers-postgres psql -U airesearchers -d airesearchers -c "\dt"
```
Expected: список таблиц включает `ResearchTasks`, `InterviewAnswers`, `OutlineSections`, `FocusAreas`, `Sources`, `Findings`, `ProgressEvents`, `Reports`, `__EFMigrationsHistory`.

- [ ] **Step 6: Commit**

```bash
git add src/AiResearchers.Infrastructure/Migrations/ src/AiResearchers.Infrastructure/Persistence/AppDbContextFactory.cs
git commit -m "feat: add initial ef migration and design-time factory"
```

---

### Task 7: Web — конфигурация, DI, layout (htmx + Tailwind dark mode)

**Files:**
- Modify: `src/AiResearchers.Web/AiResearchers.Web.csproj`
- Replace: `src/AiResearchers.Web/Program.cs`
- Create: `src/AiResearchers.Web/appsettings.json`
- Create: `src/AiResearchers.Web/appsettings.Development.json`
- Create: `src/AiResearchers.Web/Pages/_ViewImports.cshtml`
- Create: `src/AiResearchers.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/AiResearchers.Web/Pages/_ViewStart.cshtml`

> Используем Razor Pages (проще для htmx-страниц, чем MVC controllers).

- [ ] **Step 1: Заменить `Program.cs`**

Replace `src/AiResearchers.Web/Program.cs`:
```csharp
using AiResearchers.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/Dashboard"));

app.Run();
```

- [ ] **Step 2: Создать `appsettings.json`**

Create `src/AiResearchers.Web/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 3: Создать `appsettings.Development.json` с connection string**

Create `src/AiResearchers.Web/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=airesearchers;Username=airesearchers;Password=airesearchers_dev"
  }
}
```

- [ ] **Step 4: Создать `_ViewImports.cshtml`**

Create `src/AiResearchers.Web/Pages/_ViewImports.cshtml`:
```cshtml
@namespace AiResearchers.Web.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 5: Создать `_ViewStart.cshtml`**

Create `src/AiResearchers.Web/Pages/_ViewStart.cshtml`:
```cshtml
@{
    Layout = "_Layout";
}
```

- [ ] **Step 6: Создать `_Layout.cshtml` (htmx + Tailwind dark mode по умолчанию)**

Create `src/AiResearchers.Web/Pages/Shared/_Layout.cshtml`:
```cshtml
<!DOCTYPE html>
<html lang="ru" class="dark">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - AI Researchers</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = { darkMode: 'class' };
    </script>
    <script src="https://unpkg.com/htmx.org@2.0.4" integrity="sha384-HGfztofotfshcF7+8n44JQL2oJmowVChPTg48S+jvZoztPfvwD79OC/LTtG6dMp+" crossorigin="anonymous"></script>
</head>
<body class="bg-gray-950 text-gray-100 min-h-screen">
    <header class="border-b border-gray-800 px-6 py-4 flex items-center justify-between">
        <a href="/Dashboard" class="text-lg font-semibold text-violet-300">AI Researchers</a>
        <button id="theme-toggle" class="text-sm text-gray-400 hover:text-gray-100"
                onclick="document.documentElement.classList.toggle('dark')">тема</button>
    </header>
    <main class="max-w-5xl mx-auto px-6 py-8">
        @RenderBody()
    </main>
</body>
</html>
```

> **Замечание:** integrity-хэш htmx 2.0.4 указан как пример — исполнитель должен взять актуальный хэш со страницы релиза htmx или убрать атрибуты `integrity`/`crossorigin`, если используется vendored-копия. Tailwind Play CDN — для разработки; перевод на standalone CLI-сборку запланирован в фазе hardening.

- [ ] **Step 7: Собрать**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add src/AiResearchers.Web/Program.cs src/AiResearchers.Web/appsettings*.json src/AiResearchers.Web/Pages/
git commit -m "feat: configure web host, DI, razor layout with htmx and tailwind dark mode"
```

---

### Task 8: Dashboard-страница (список задач из БД)

**Files:**
- Create: `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml`
- Create: `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml.cs`

- [ ] **Step 1: Создать PageModel**

Create `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml.cs`:
```csharp
using AiResearchers.Core.Entities;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Web.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly AppDbContext db;

    public IndexModel(AppDbContext db)
    {
        this.db = db;
    }

    public IReadOnlyList<ResearchTask> Tasks { get; private set; } = new List<ResearchTask>();

    public async Task OnGetAsync()
    {
        this.Tasks = await this.db.ResearchTasks
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}
```

- [ ] **Step 2: Создать страницу**

Create `src/AiResearchers.Web/Pages/Dashboard/Index.cshtml`:
```cshtml
@page
@model AiResearchers.Web.Pages.Dashboard.IndexModel
@{
    ViewData["Title"] = "Дашборд";
}

<div class="flex items-center justify-between mb-6">
    <h1 class="text-2xl font-semibold">Исследования</h1>
    <a href="#" class="bg-violet-600 hover:bg-violet-500 text-white rounded-md px-4 py-2 text-sm">
        + Новое исследование
    </a>
</div>

@if (Model.Tasks.Count == 0)
{
    <div class="border border-dashed border-gray-700 rounded-lg p-10 text-center text-gray-500">
        Пока нет исследований. Создай первое.
    </div>
}
else
{
    <div class="space-y-2">
        @foreach (var task in Model.Tasks)
        {
            <div class="flex items-center justify-between border border-gray-800 rounded-md px-4 py-3">
                <span>@task.Topic</span>
                <span class="text-xs px-2 py-1 rounded-full bg-gray-800 text-gray-300">@task.Status</span>
            </div>
        }
    </div>
}
```

- [ ] **Step 3: Собрать**

Run: `dotnet build src/AiResearchers.Web`
Expected: `Build succeeded.`

- [ ] **Step 4: Запустить приложение и проверить вручную**

Run (контейнер Postgres должен быть запущен):
```bash
dotnet run --project src/AiResearchers.Web
```
Expected: лог `Now listening on: http://localhost:5xxx`. Открыть URL в браузере → редирект на `/Dashboard` → тёмная страница с заголовком «Исследования», кнопкой «+ Новое исследование» и пустым состоянием «Пока нет исследований». Кнопка «тема» переключает светлую/тёмную. Остановить: Ctrl+C.

- [ ] **Step 5: Commit**

```bash
git add src/AiResearchers.Web/Pages/Dashboard/
git commit -m "feat: add dashboard page listing research tasks"
```

---

## Definition of Done (Phase 1)

- `dotnet build` всего solution проходит без ошибок.
- `docker compose up -d` поднимает Postgres; миграция `InitialCreate` применена; 8 таблиц + история миграций существуют.
- `dotnet run --project src/AiResearchers.Web` запускает приложение; `/` редиректит на `/Dashboard`; страница рендерится в dark mode; пустое состояние видно; переключатель темы работает.
- Все задачи закоммичены.

## Что НЕ входит в Phase 1 (следующие планы)

- **Phase 2 — LLM gate:** smoke-test Ollama (tool-calling/JSON), `IChatClient`-обёртка (swappable), конфиг модели. Контракты `IChatClient`.
- **Phase 3 — Interview + Outline:** `InterviewService`, форма-wizard, AI доп-вопросы, AI-черновик outline, редактор секций.
- **Phase 4 — Research engine:** `ISearchProvider` (SearXNG/DuckDuckGo), `IContentFetcher` (AngleSharp+SmartReader), агенты MAF (Planner/Searcher/Analyst/Critic/Writer), deterministic-оркестратор, `Channel`+`BackgroundService` очередь, guard'ы цикла, прогресс-события.
- **Phase 5 — UI:** экран прогресса (polling), просмотр отчёта, экспорт MD/PDF (`IReportExporter`).
- **Тесты:** оформляются отдельным планом после подтверждения (правило проекта).

## Self-Review (выполнено)

- **Spec coverage (Phase 1 scope):** доменная модель (§4 спеки) — Tasks 3–4 ✓; персистентность Postgres/EF (§2.4) — Tasks 5–6 ✓; Web-каркас htmx+Tailwind dark mode (§2.1, §5 экран Dashboard) — Tasks 7–8 ✓. Остальные секции спеки явно отнесены к Phases 2–5.
- **Placeholder scan:** конкретных TODO/«добавить обработку» нет; два «Замечания для исполнителя» (cascade-path у `Finding`, integrity-хэш htmx) — это намеренные пояснения, не пропуски.
- **Type consistency:** имена сущностей/полей совпадают между Core (Tasks 3–4), конфигурацией EF (Task 5) и Dashboard PageModel (Task 8). `AppDbContext`, `AddInfrastructure`, `ResearchTask.Status/Topic/CreatedAt` используются согласованно.
