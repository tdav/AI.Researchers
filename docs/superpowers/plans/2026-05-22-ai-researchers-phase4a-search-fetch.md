# AI Researchers Platform — Phase 4a: Search + Fetch Infrastructure

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Реализовать pluggable веб-поиск (`ISearchProvider`: SearXNG + DuckDuckGo) и извлечение основного текста страниц (`IContentFetcher`: HttpClient + SmartReader/AngleSharp). Это «руки» research-движка; оркестратор и агенты — в Phase 4b.

**Architecture:** Контракты в Core (зависят только от своих DTO). Реализации в Infrastructure через `IHttpClientFactory`. Выбор поисковика — конфиг `Search:Provider`. Фетчер скачивает HTML (свой UA, таймаут, простой retry на transient) и извлекает читаемый текст через SmartReader (Readability-порт), с AngleSharp как fallback на title.

**Tech Stack:** .NET 10, AngleSharp 1.4.0, SmartReader 0.11.0, `System.Net.Http.Json`, SearXNG (Docker), DuckDuckGo HTML endpoint.

> **Тесты:** формальные тесты не пишем (правило проекта). Проверка — `dotnet build` + консольный probe `tools/AiResearchers.SourcesProbe` (Task 5), который реально ищет и качает. Спека §2.4, §3 (Searcher/ContentFetcher). Зависит от Phase 1 (Core/Infrastructure/DI).

> **Важный нюанс SearXNG:** JSON-формат API по умолчанию **выключен**. Его нужно включить в `settings.yml` (`search.formats: [html, json]`) и задать `server.secret_key`. Без этого `?format=json` вернёт 403. Это покрыто Task 2.

---

## File Structure (создаётся в этой фазе)

```
src/AiResearchers.Core/Research/
  SearchResultItem.cs            # DTO результата поиска
  FetchedContent.cs              # DTO извлечённого контента
  ISearchProvider.cs
  IContentFetcher.cs
src/AiResearchers.Infrastructure/Research/
  SearchOptions.cs               # конфиг (Provider, SearxngEndpoint)
  SearxngSearchProvider.cs
  DuckDuckGoSearchProvider.cs
  HtmlContentFetcher.cs
  ResearchSourcesServiceCollectionExtensions.cs   # AddResearchSources(config)
searxng/settings.yml             # конфиг контейнера SearXNG (включает json)
docker-compose.yml               # + сервис searxng
tools/AiResearchers.SourcesProbe/
  AiResearchers.SourcesProbe.csproj
  Program.cs                     # search + fetch проба
```

---

### Task 1: Core — контракты и DTO

**Files:**
- Create: `src/AiResearchers.Core/Research/SearchResultItem.cs`
- Create: `src/AiResearchers.Core/Research/FetchedContent.cs`
- Create: `src/AiResearchers.Core/Research/ISearchProvider.cs`
- Create: `src/AiResearchers.Core/Research/IContentFetcher.cs`

- [ ] **Step 1: DTO результата поиска**

Create `src/AiResearchers.Core/Research/SearchResultItem.cs`:
```csharp
namespace AiResearchers.Core.Research;

public class SearchResultItem
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}
```

- [ ] **Step 2: DTO извлечённого контента**

Create `src/AiResearchers.Core/Research/FetchedContent.cs`:
```csharp
namespace AiResearchers.Core.Research;

public class FetchedContent
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
```

- [ ] **Step 3: Контракт поиска**

Create `src/AiResearchers.Core/Research/ISearchProvider.cs`:
```csharp
namespace AiResearchers.Core.Research;

public interface ISearchProvider
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Контракт фетчера**

Create `src/AiResearchers.Core/Research/IContentFetcher.cs`:
```csharp
namespace AiResearchers.Core.Research;

public interface IContentFetcher
{
    Task<FetchedContent> FetchAsync(string url, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Собрать и закоммитить**

Run: `dotnet build src/AiResearchers.Core`
Expected: `Build succeeded.`
```bash
git add src/AiResearchers.Core/Research/
git commit -m "feat: add search and content-fetch contracts in core"
```

---

### Task 2: SearXNG в docker-compose (с включённым JSON)

**Files:**
- Create: `searxng/settings.yml`
- Modify: `docker-compose.yml`

- [ ] **Step 1: Создать `searxng/settings.yml`**

Create `searxng/settings.yml`:
```yaml
use_default_settings: true
server:
  secret_key: "airesearchers_dev_secret_change_me"
  limiter: false
  image_proxy: false
search:
  formats:
    - html
    - json
```

- [ ] **Step 2: Добавить сервис в `docker-compose.yml`**

Modify `docker-compose.yml` — добавить сервис `searxng` в `services` (рядом с `postgres`):
```yaml
  searxng:
    image: searxng/searxng:latest
    container_name: airesearchers-searxng
    ports:
      - "8888:8080"
    volumes:
      - ./searxng:/etc/searxng:rw
    environment:
      SEARXNG_BASE_URL: http://localhost:8888/
```

- [ ] **Step 3: Поднять и проверить JSON API**

Run:
```bash
docker compose up -d searxng
```
Подождать ~5–10 c инициализации, затем:
```bash
curl -s "http://localhost:8888/search?q=test&format=json" | head -c 200
```
Expected: JSON, начинающийся с `{"query": "test"` (а не 403/HTML). Если 403 — проверь, что `settings.yml` смонтирован и `formats` содержит `json`, перезапусти: `docker compose restart searxng`.

- [ ] **Step 4: Commit**

```bash
git add searxng/settings.yml docker-compose.yml
git commit -m "chore: add searxng service with json api enabled"
```

---

### Task 3: Infrastructure — поисковые провайдеры + DI

**Files:**
- Modify: `src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj`
- Create: `src/AiResearchers.Infrastructure/Research/SearchOptions.cs`
- Create: `src/AiResearchers.Infrastructure/Research/SearxngSearchProvider.cs`
- Create: `src/AiResearchers.Infrastructure/Research/DuckDuckGoSearchProvider.cs`
- Create: `src/AiResearchers.Infrastructure/Research/ResearchSourcesServiceCollectionExtensions.cs`

- [ ] **Step 1: Пакеты в Infrastructure**

Run:
```bash
dotnet add src/AiResearchers.Infrastructure package AngleSharp --version 1.4.0
dotnet add src/AiResearchers.Infrastructure package Microsoft.Extensions.Http --version 10.0.4
```
Expected: пакеты добавлены. (`System.Net.Http.Json` входит в shared framework — отдельно не нужен.)

- [ ] **Step 2: `SearchOptions`**

Create `src/AiResearchers.Infrastructure/Research/SearchOptions.cs`:
```csharp
namespace AiResearchers.Infrastructure.Research;

public class SearchOptions
{
    public const string SectionName = "Search";

    // "Searxng" | "DuckDuckGo"
    public string Provider { get; set; } = "Searxng";
    public string SearxngEndpoint { get; set; } = "http://localhost:8888";
}
```

- [ ] **Step 3: `SearxngSearchProvider`**

Create `src/AiResearchers.Infrastructure/Research/SearxngSearchProvider.cs`:
```csharp
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiResearchers.Core.Research;
using Microsoft.Extensions.Options;

namespace AiResearchers.Infrastructure.Research;

public class SearxngSearchProvider : ISearchProvider
{
    private readonly HttpClient httpClient;
    private readonly SearchOptions options;

    public SearxngSearchProvider(HttpClient httpClient, IOptions<SearchOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        string url = $"{this.options.SearxngEndpoint.TrimEnd('/')}/search" +
                     $"?q={Uri.EscapeDataString(query)}&format=json";

        SearxngResponse? response =
            await this.httpClient.GetFromJsonAsync<SearxngResponse>(url, cancellationToken);

        if (response?.Results is null)
        {
            return new List<SearchResultItem>();
        }

        return response.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .Take(maxResults)
            .Select(r => new SearchResultItem
            {
                Url = r.Url!,
                Title = r.Title ?? string.Empty,
                Snippet = r.Content ?? string.Empty
            })
            .ToList();
    }

    private sealed class SearxngResponse
    {
        [JsonPropertyName("results")]
        public List<SearxngResult>? Results { get; set; }
    }

    private sealed class SearxngResult
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
```

- [ ] **Step 4: `DuckDuckGoSearchProvider`** (HTML endpoint, парсинг AngleSharp)

Create `src/AiResearchers.Infrastructure/Research/DuckDuckGoSearchProvider.cs`:
```csharp
using System.Net;
using AngleSharp.Html.Parser;
using AiResearchers.Core.Research;

namespace AiResearchers.Infrastructure.Research;

public class DuckDuckGoSearchProvider : ISearchProvider
{
    private readonly HttpClient httpClient;

    public DuckDuckGoSearchProvider(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        // html.duckduckgo.com отдаёт статическую страницу результатов, пригодную для парсинга.
        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        string html = await this.httpClient.GetStringAsync(url, cancellationToken);

        HtmlParser parser = new();
        AngleSharp.Html.Dom.IHtmlDocument doc = await parser.ParseDocumentAsync(html, cancellationToken);

        List<SearchResultItem> items = new();
        foreach (AngleSharp.Dom.IElement result in doc.QuerySelectorAll("div.result"))
        {
            AngleSharp.Dom.IElement? link = result.QuerySelector("a.result__a");
            if (link is null)
            {
                continue;
            }

            string href = link.GetAttribute("href") ?? string.Empty;
            string realUrl = ExtractRealUrl(href);
            if (string.IsNullOrWhiteSpace(realUrl))
            {
                continue;
            }

            string snippet = result.QuerySelector("a.result__snippet")?.TextContent?.Trim() ?? string.Empty;
            items.Add(new SearchResultItem
            {
                Url = realUrl,
                Title = link.TextContent.Trim(),
                Snippet = snippet
            });

            if (items.Count >= maxResults)
            {
                break;
            }
        }

        return items;
    }

    // DDG оборачивает ссылки в редирект /l/?uddg=<encoded-url>. Достаём реальный URL.
    private static string ExtractRealUrl(string href)
    {
        if (href.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
        {
            int idx = href.IndexOf("uddg=", StringComparison.OrdinalIgnoreCase) + "uddg=".Length;
            int amp = href.IndexOf('&', idx);
            string encoded = amp > idx ? href[idx..amp] : href[idx..];
            return WebUtility.UrlDecode(encoded);
        }
        return href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : string.Empty;
    }
}
```

- [ ] **Step 5: DI-расширение с выбором провайдера**

Create `src/AiResearchers.Infrastructure/Research/ResearchSourcesServiceCollectionExtensions.cs`:
```csharp
using System.Net.Http.Headers;
using AiResearchers.Core.Research;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Infrastructure.Research;

public static class ResearchSourcesServiceCollectionExtensions
{
    private const string UserAgent =
        "Mozilla/5.0 (compatible; AiResearchers/1.0; +local)";

    public static IServiceCollection AddResearchSources(
        this IServiceCollection services, IConfiguration configuration)
    {
        SearchOptions options = configuration.GetSection(SearchOptions.SectionName).Get<SearchOptions>()
            ?? new SearchOptions();
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));

        // Named HttpClient для поиска и фетча: общий UA и таймаут.
        services.AddHttpClient("research", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });

        if (string.Equals(options.Provider, "DuckDuckGo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISearchProvider>(sp =>
                new DuckDuckGoSearchProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("research")));
        }
        else
        {
            services.AddSingleton<ISearchProvider>(sp =>
                new SearxngSearchProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("research"),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchOptions>>()));
        }

        services.AddSingleton<IContentFetcher>(sp =>
            new HtmlContentFetcher(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("research")));

        return services;
    }
}
```

- [ ] **Step 6: Собрать**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: ошибка про отсутствующий `HtmlContentFetcher` (создаётся в Task 4) — это ОК, соберётся после Task 4. Если хочешь промежуточную сборку — закомментируй регистрацию `IContentFetcher` и верни в Task 4. Иначе переходи к Task 4 и собирай там.

- [ ] **Step 7: Commit** (после Task 4 — вместе с фетчером). Пропусти commit здесь.

---

### Task 4: Infrastructure — `HtmlContentFetcher`

**Files:**
- Create: `src/AiResearchers.Infrastructure/Research/HtmlContentFetcher.cs`

- [ ] **Step 1: Пакет SmartReader**

Run:
```bash
dotnet add src/AiResearchers.Infrastructure package SmartReader --version 0.11.0
```

- [ ] **Step 2: Реализация фетчера**

Create `src/AiResearchers.Infrastructure/Research/HtmlContentFetcher.cs`:
```csharp
using AiResearchers.Core.Research;
using SmartReader;

namespace AiResearchers.Infrastructure.Research;

public class HtmlContentFetcher : IContentFetcher
{
    private readonly HttpClient httpClient;

    public HtmlContentFetcher(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<FetchedContent> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        FetchedContent content = new() { Url = url };

        string? html = await this.DownloadAsync(url, cancellationToken);
        if (html is null)
        {
            content.Success = false;
            content.Error = "download failed";
            return content;
        }

        try
        {
            Reader reader = new(url, html);
            Article article = reader.GetArticle();
            content.Title = article.Title ?? string.Empty;
            content.Text = (article.TextContent ?? string.Empty).Trim();
            content.Success = !string.IsNullOrWhiteSpace(content.Text);
            if (!content.Success)
            {
                content.Error = "no readable content extracted";
            }
        }
        catch (Exception ex)
        {
            content.Success = false;
            content.Error = $"extract failed: {ex.Message}";
        }

        return content;
    }

    // Скачать HTML с одним повтором на transient-сбой.
    private async Task<string?> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await this.httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception) when (attempt == 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        return null;
    }
}
```

- [ ] **Step 3: Собрать (теперь регистрация из Task 3 валидна)**

Run: `dotnet build src/AiResearchers.Infrastructure`
Expected: `Build succeeded.` 0 errors.

- [ ] **Step 4: Commit (провайдеры + фетчер + DI)**

```bash
git add src/AiResearchers.Infrastructure/Research/ src/AiResearchers.Infrastructure/AiResearchers.Infrastructure.csproj
git commit -m "feat: add searxng/duckduckgo search providers and html content fetcher"
```

---

### Task 5: Probe-консоль и проверка

**Files:**
- Create: `tools/AiResearchers.SourcesProbe/AiResearchers.SourcesProbe.csproj`
- Create: `tools/AiResearchers.SourcesProbe/Program.cs`

- [ ] **Step 1: Создать проект**

Run:
```bash
dotnet new console -n AiResearchers.SourcesProbe -o tools/AiResearchers.SourcesProbe
dotnet sln add tools/AiResearchers.SourcesProbe
dotnet add tools/AiResearchers.SourcesProbe reference src/AiResearchers.Infrastructure src/AiResearchers.Core
dotnet add tools/AiResearchers.SourcesProbe package Microsoft.Extensions.DependencyInjection --version 10.0.8
dotnet add tools/AiResearchers.SourcesProbe package Microsoft.Extensions.Configuration --version 10.0.4
```

- [ ] **Step 2: `Program.cs`**

Replace `tools/AiResearchers.SourcesProbe/Program.cs`:
```csharp
using AiResearchers.Core.Research;
using AiResearchers.Infrastructure.Research;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

string query = args.Length > 0 ? string.Join(' ', args) : "local LLM private RAG 2026";

IConfiguration config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Search:Provider"] = Environment.GetEnvironmentVariable("SEARCH_PROVIDER") ?? "Searxng",
        ["Search:SearxngEndpoint"] = "http://localhost:8888"
    })
    .Build();

ServiceProvider services = new ServiceCollection()
    .AddResearchSources(config)
    .BuildServiceProvider();

ISearchProvider search = services.GetRequiredService<ISearchProvider>();
IContentFetcher fetcher = services.GetRequiredService<IContentFetcher>();

Console.WriteLine($"Search: \"{query}\" via {config["Search:Provider"]}");
IReadOnlyList<SearchResultItem> results = await search.SearchAsync(query, 5);
Console.WriteLine($"  {results.Count} results");
foreach (SearchResultItem r in results.Take(5))
{
    Console.WriteLine($"   - {r.Title} :: {r.Url}");
}

if (results.Count == 0)
{
    Console.WriteLine("NO RESULTS — check search provider/container.");
    return 1;
}

string firstUrl = results[0].Url;
Console.WriteLine($"\nFetch: {firstUrl}");
FetchedContent content = await fetcher.FetchAsync(firstUrl);
Console.WriteLine($"  success={content.Success} title=\"{content.Title}\" textLen={content.Text.Length} error={content.Error}");

return content.Success ? 0 : 2;
```

- [ ] **Step 3: Собрать**

Run: `dotnet build tools/AiResearchers.SourcesProbe`
Expected: `Build succeeded.`

- [ ] **Step 4: Прогнать probe (SearXNG-контейнер из Task 2 должен работать)**

Run:
```bash
dotnet run --project tools/AiResearchers.SourcesProbe -- local LLM private RAG
```
Expected: печать ≥1 результата с URL, затем строка fetch с `success=True` и `textLen` заметно > 0. Exit code 0.

Если SearXNG недоступен — прогнать DuckDuckGo:
```bash
SEARCH_PROVIDER=DuckDuckGo dotnet run --project tools/AiResearchers.SourcesProbe -- local LLM private RAG
```

- [ ] **Step 5: Commit**

```bash
git add tools/AiResearchers.SourcesProbe/ AiResearchers.slnx
git commit -m "feat: add sources probe console and verify search+fetch"
```

---

## Definition of Done (Phase 4a)

- `dotnet build` всего solution — 0 ошибок.
- SearXNG поднят, JSON API отвечает (не 403).
- `SourcesProbe` реально возвращает результаты поиска и извлекает текст ≥1 страницы (`success=True`, `textLen>0`).
- Конфиг `Search:Provider` переключает SearXNG/DuckDuckGo.
- Всё закоммичено.

## Что НЕ входит (Phase 4b)

- Агенты (Planner/Searcher-генерация-запросов/Analyst/Critic/Writer), deterministic-оркестратор, `Channel`+`BackgroundService` worker, guard'ы цикла, запись `ProgressEvent`/`Source`/`Finding` в БД, retry на LLM transient + увеличенный таймаут (per §gate-results).
- Phase 5 — UI прогресса/отчёта + экспорт.
- Тесты — отдельным планом после подтверждения.

## Self-Review (выполнено)

- **Spec coverage:** §2.4 (`ISearchProvider` SearXNG+DuckDuckGo pluggable; `IContentFetcher` AngleSharp+SmartReader) — Tasks 1,3,4 ✓; §3 шаги Searcher/ContentFetcher (механика) — Tasks 3,4 ✓. Оркестрация/агенты явно отнесены к 4b.
- **Placeholder scan:** реального TBD нет. Task 3 Step 6/7 намеренно откладывает финальную сборку/commit до Task 4 (циклическая зависимость регистрации на `HtmlContentFetcher`) — это явная инструкция, не пропуск.
- **Type consistency:** `ISearchProvider.SearchAsync(query,maxResults,ct)`, `IContentFetcher.FetchAsync(url,ct)`, `SearchResultItem{Url,Title,Snippet}`, `FetchedContent{Url,Title,Text,Success,Error}`, `SearchOptions{Provider,SearxngEndpoint}`, `AddResearchSources` — согласованы между Core (Task 1), Infrastructure (Tasks 3,4) и probe (Task 5).
```
