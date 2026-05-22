# AI Researchers

Автономная платформа веб-исследований на .NET 10. Пользователь задаёт тему, платформа **подробно интервьюирует** его (уточняющие вопросы с вариантами ответов), согласует структуру отчёта, затем мультиагентный конвейер сам ищет в вебе, читает страницы, извлекает факты, оценивает полноту и собирает итоговый отчёт со списком источников. Работает на локальной LLM через Ollama — без облачных API.

Вдохновлено [TheBlewish/Automated-AI-Web-Researcher-Ollama](https://github.com/TheBlewish/Automated-AI-Web-Researcher-Ollama).

## Возможности

- **Умный итеративный интервьюер** — генерирует уточняющие вопросы с готовыми вариантами (single/multi-select) + свободный ввод; до 3 раундов; гибридная остановка («модель советует — пользователь решает»).
- **Мультиагентный конвейер** — Planner → Searcher → Analyst → Critic → Writer, детерминированная оркестрация.
- **Поиск + чтение** — SearXNG (JSON API) или скрейпинг DuckDuckGo; извлечение основного текста через Readability (SmartReader).
- **Фоновая обработка** — очередь на `Channel` + `BackgroundService`-воркер, отмена задач, восстановление прерванных задач при старте.
- **Web UI** — мастер создания задачи, экран прогресса (htmx-поллинг), просмотр отчёта (рендер Markdown) с боковой панелью источников.
- **Экспорт** — Markdown и PDF (QuestPDF).
- Тёмная тема, htmx + Tailwind.

## Технологии

| Слой | Стек |
|------|------|
| Платформа | .NET 10, ASP.NET Core Razor Pages |
| LLM | Ollama + `Microsoft.Extensions.AI` `IChatClient` (OllamaSharp), модель `bjoernb/gemma4-e2b-fast:latest` |
| БД | PostgreSQL 17, EF Core 10 + Npgsql |
| Поиск/чтение | SearXNG, AngleSharp, SmartReader |
| Отчёты | Markdig, QuestPDF |
| UI | htmx 2, Tailwind (Play CDN), dark mode |

## Архитектура

Слоистое решение:

- **`AiResearchers.Core`** — домен (entities, enums), контракты агентов, интервьюер. Зависит только от `Microsoft.Extensions.AI`.
- **`AiResearchers.Infrastructure`** — EF (`AppDbContext`, миграции), провайдеры поиска/чтения, реализации агентов, `ResearchOrchestrator`, очередь + воркер.
- **`AiResearchers.Web`** — Razor Pages, DI-композиция.
- **`AiResearchers.Tests`** — скелет (тесты ещё не подключены).
- **`tools/`** — `LlmSmokeTest` (проверка LLM), `SourcesProbe` (проверка поиска/чтения).

Конвейер исследования: тема → focus areas (Planner) → по каждой области поисковые запросы (Searcher) → поиск + загрузка страниц → извлечение фактов (Analyst) → оценка полноты (Critic, при нехватке добавляет новые focus areas и идёт следующий раунд) → сборка отчёта (Writer).

## Требования

- [.NET SDK 10](https://dotnet.microsoft.com/) (10.0.300+)
- [Docker](https://www.docker.com/) (PostgreSQL + SearXNG)
- [Ollama](https://ollama.com/) с загруженной моделью:
  ```bash
  ollama pull bjoernb/gemma4-e2b-fast:latest
  ```
- `dotnet-ef` для миграций:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

## Запуск

1. **Поднять инфраструктуру** (PostgreSQL :5433, SearXNG :8888):
   ```bash
   docker compose up -d
   ```
   > SearXNG по умолчанию отдаёт только HTML. Включи JSON API в `searxng/settings.yml`:
   > ```yaml
   > search:
   >   formats:
   >     - html
   >     - json
   > ```
   > затем `docker compose restart searxng`. Без JSON API используется fallback на DuckDuckGo.

2. **Применить миграции БД:**
   ```bash
   dotnet ef database update --project src/AiResearchers.Infrastructure
   ```

3. **Запустить веб-приложение:**
   ```bash
   dotnet run --project src/AiResearchers.Web
   ```
   Открыть http://localhost:5249

## Конфигурация

`src/AiResearchers.Web/appsettings.json`:

```json
{
  "Llm": {
    "Endpoint": "http://localhost:11434",
    "Model": "bjoernb/gemma4-e2b-fast:latest",
    "Temperature": 0.2
  },
  "Search": {
    "Provider": "Searxng",
    "SearxngEndpoint": "http://localhost:8888"
  }
}
```

Строка подключения к БД — в `appsettings.Development.json` (`ConnectionStrings:Postgres`).

> Локальная 2B-модель медленная. Таймаут LLM поднят до 300 с, добавлен retry. Глубина `Quick` ограничивает число focus areas и анализируемых страниц за раунд, чтобы прогон укладывался в минуты.

## Структура проекта

```
src/
  AiResearchers.Core/            домен, контракты, интервьюер
  AiResearchers.Infrastructure/  EF, провайдеры, оркестратор, воркер
  AiResearchers.Web/             Razor Pages UI
tests/
  AiResearchers.Tests/           скелет
tools/
  AiResearchers.LlmSmokeTest/    проверка LLM
  AiResearchers.SourcesProbe/    проверка поиска/чтения
docs/superpowers/                спеки и планы реализации
```
