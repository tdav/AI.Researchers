# AI Researchers Platform — Design Spec

**Дата:** 2026-05-22
**Статус:** утверждён к планированию реализации

## 1. Обзор

Веб-платформа автономного исследования. Пользователь задаёт тему, платформа сначала **максимально подробно** выясняет направление и желаемую структуру результата, затем мульти-агентная система итеративно ищет в интернете, читает страницы, извлекает факты и собирает структурированный отчёт. Прогресс и результат доступны через web UI.

Концептуальный референс: `Automated-AI-Web-Researcher-Ollama` (тема → focus areas → цикл поиск/чтение/заметки → проверка пробелов → отчёт с источниками), перенесённый на .NET-стек и мульти-агентную оркестрацию.

### Стек

- .NET 10, ASP.NET Core
- Microsoft Agent Framework (MAF) — агенты и оркестрация
- Ollama локально, модель `bjoernb/gemma4-e2b-fast:latest` (через `IChatClient`, swappable)
- htmx + Tailwind CSS, dark mode
- PostgreSQL + EF Core 10
- Прогресс в UI — htmx polling

### Ключевые решения (зафиксированы в брейншторме)

| Тема | Решение |
|---|---|
| Источники research | Веб: поиск + скрейпинг |
| Pre-research интервью | Гибрид: форма + AI доп-вопросы |
| Структура результата | AI генерит черновик outline → пользователь правит/утверждает |
| Топология агентов | Мульти-агент + оркестратор |
| Модель оркестрации | Deterministic workflow (граф/пайплайн) |
| Прогресс в UI | htmx polling |
| Хранилище | PostgreSQL + EF Core |
| Поисковый провайдер | Pluggable `ISearchProvider` (SearXNG + DuckDuckGo) |
| Итоговый формат | Markdown отчёт + экспорт MD и PDF |

## 2. Архитектура

Слоистое решение (.NET solution):

### 2.1 AiResearchers.Web

- ASP.NET Core. Endpoints (Minimal API + Razor partials) возвращают HTML-фрагменты для htmx.
- htmx + Tailwind UI, dark mode.
- **Hosted Worker** — `BackgroundService` + `Channel<Guid>`: очередь исследований, одно активное за раз.

### 2.2 AiResearchers.Core (Application / Domain)

- Доменные сущности (см. §4).
- `ResearchService` — создание задачи, постановка в очередь, выдача статуса/прогресса.
- `InterviewService` — гибридное интервью (форма + AI доп-вопросы) и AI-черновик outline.
- Контракты: `IChatClient` (из Microsoft.Extensions.AI), `ISearchProvider`, `IContentFetcher`, `IReportExporter`, репозитории.

### 2.3 AiResearchers.Agents

- Агенты MAF: **Planner, Searcher, Analyst, Critic, Writer**.
- **Orchestrator** — deterministic workflow: владеет циклом, лимитом раундов, генерацией прогресс-событий.

### 2.4 AiResearchers.Infrastructure

- `OllamaChatClient` (`IChatClient`), swappable.
- Search providers: `SearxngSearchProvider`, `DuckDuckGoSearchProvider` за `ISearchProvider`; выбор в конфиге.
- `IContentFetcher`: AngleSharp + SmartReader (Readability-извлечение основного текста). Только HTML, без JS. Playwright — позже, за тем же интерфейсом.
- EF Core → PostgreSQL.
- `IReportExporter`: Markdown + PDF.

### 2.5 AiResearchers.Tests

- Unit + integration (см. §6).

**Принцип:** реализации завязаны через интерфейсы; смена поисковика, парсера или LLM не трогает агентов.

## 3. Research Workflow

Deterministic-оркестратор управляет потоком. Каждый шаг пишет `ProgressEvent`.

1. **Интервью** — форма (тема, глубина, аудитория, язык) + AI задаёт доп-вопросы по пробелам.
2. **Outline** — Planner предлагает черновик секций; пользователь правит/удаляет/добавляет и утверждает → `OutlineSection[]`.
3. *enqueue → worker берёт задачу.*
4. **Planner** — тема + outline → focus areas.
5. **Цикл раундов** (пока Critic не скажет «достаточно» ИЛИ не достигнут лимит):
   - **Searcher**: focus area → поисковые запросы → `ISearchProvider` → URL.
   - **ContentFetcher**: скачать + извлечь основной текст; дедуп URL.
   - **Analyst**: текст → факты/заметки с привязкой к источнику → `Finding`.
   - **Critic**: покрытие vs outline, пробелы → новые focus areas (продолжить) / стоп.
6. **Writer** — `Finding` + цитаты → отчёт по утверждённому outline (Markdown).
7. **Готово** — отчёт в БД, статус `Completed`. UI: просмотр + источники + экспорт.

**Guard'ы цикла (конфиг, по `Depth`):** max раундов, max источников/раунд, лимит времени. Защита от зацикливания слабой модели.

## 4. Модель данных (PostgreSQL / EF Core)

| Сущность | Ключевые поля | Назначение |
|---|---|---|
| **ResearchTask** | Id, Topic, Status, Language, Depth (Quick/Standard/Deep → maxRounds), Audience?, CurrentRound, MaxRounds, FailureReason?, CreatedAt, StartedAt?, CompletedAt? | Корень исследования |
| **InterviewAnswer** | Id, TaskId, Question, Answer, Source (Form/Ai), Order | Ответы интервью |
| **OutlineSection** | Id, TaskId, Title, Description, Order | Структура отчёта (правит пользователь) |
| **FocusArea** | Id, TaskId, Round, Title, Status | Направления копки |
| **Source** | Id, TaskId, FocusAreaId?, Url, Title, Snippet, Status (Found/Fetched/Failed), ExtractedText?, FetchedAt? | Найденные/скачанные страницы |
| **Finding** | Id, TaskId, OutlineSectionId?, SourceId, Text, Round, CreatedAt | Факт/заметка с привязкой к источнику |
| **ProgressEvent** | Id, TaskId, Timestamp, Phase, Level (Info/Warn/Error), Message | Живой лог → polling |
| **Report** | Id, TaskId, MarkdownContent, GeneratedAt | Финальный отчёт |

**Status (enum):** `Draft → Interviewing → OutlinePending → Queued → Running → Completed | Failed | Cancelled`.

**Связи:** Task 1—N остальное; `Finding → Source` (цитаты); `Finding → OutlineSection` (раскладка по секциям).

**MVP-lean:** цитаты — inline-ссылки в Markdown + таблица источников из `Source`. Без отдельной `ReportSection` — секции живут в `OutlineSection`, отчёт = цельный Markdown.

## 5. UI (htmx + Tailwind, dark mode)

Экраны:

1. **Dashboard** — список исследований со статусами (Completed / Running·раунд / Queued / Failed / Cancelled), кнопка «Новое исследование». Клик по строке → детально.
2. **Wizard** — Шаг 1 форма; Шаг 2 AI доп-вопросы (htmx подгружает); Шаг 3 редактируемый outline (AI-черновик); кнопка «Запустить исследование».
3. **Прогресс** — счётчики (источники / факты / секции), текущий focus и раунд, живой лог (polling), «Отменить».
4. **Отчёт** — рендер Markdown с inline-ссылками + панель источников + экспорт MD / PDF.

Прогресс/лог обновляются через htmx polling (`hx-trigger="every Ns"`) к статус-endpoint, отдающему HTML-фрагмент.

## 6. Обработка ошибок и lifecycle

### Внешние сбои (не валят задачу)

- **Поиск:** retry с backoff → fallback на другой провайдер. Все провалились в раунде → warn-событие, Critic работает с тем, что есть.
- **Скачивание/парсинг:** per-page timeout; 404 / timeout / anti-bot / пустой текст → `Source.Status=Failed`, пропуск, продолжаем.

### LLM / Ollama (критично для слабой модели)

- Нет связи → retry N раз с backoff → стабильно недоступен → задача `Failed (ollama unreachable)`.
- Невалидный JSON / нераспознанный вывод агента → строгий re-prompt + `format=json` + валидация схемы, ограниченное число retry → затем пропуск шага или `Failed`.

### Guard'ы цикла

- max раундов / max источников за раунд / лимит времени → принудительный стоп → Writer собирает **частичный отчёт** из накопленного (статус `Completed`, не `Failed`).

### Lifecycle задачи

- **Отмена пользователем** → `CancellationToken` → graceful stop → `Cancelled`.
- **Рестарт приложения посреди работы** → на старте: задачи `Running` → `Failed (interrupted by restart)`, ручной повтор. Авто-resume нет в MVP.
- **Пустой результат** (0 фактов) → отчёт с пометкой «недостаточно данных», статус `Completed`.
- **Очередь:** одна активная задача, остальные `Queued`, последовательно.

### Логи

- Серверные structured logs (исключения, диагностика) + `ProgressEvent` (пользовательский лог).

## 7. Стратегия тестов

> Тесты пишутся **только после явного подтверждения** пользователя на этапе реализации (правило проекта).

### Unit (моки внешних границ)

- `ResearchService` — enqueue, переходы статусов.
- `InterviewService` — форма → доп-вопросы → outline (mock `IChatClient`).
- **Orchestrator** — контроль цикла, лимиты раундов, Critic стоп/продолжить (детерминирован → хорошо тестируется).
- **Парсинг агентов** — фикс. ответ LLM → корректный разбор + валидация JSON + retry.
- Search-провайдеры — парс фикстур SearXNG JSON / DDG HTML.
- ContentFetcher — AngleSharp + SmartReader на HTML-фикстурах.
- `IReportExporter` — генерация MD + PDF.

### Integration

- EF Core против реального PostgreSQL (Testcontainers) — персистентность.
- E2E workflow с **fake** LLM + fake search/fetch → отчёт. Без сети и реального LLM.

### Политика моков

- Мокать только внешние границы (`IChatClient`, `ISearchProvider`, `IContentFetcher`).
- EF в integration — реальный Postgres (Testcontainers), не мок.

**Framework:** xUnit (предложение, подтвердить при реализации).

## 8. MVP-граница

**Не входит:**

- Аутентификация / много пользователей (один пользователь).
- Параллельные исследования (одно активное, остальные в очереди).
- Редактирование отчёта после генерации (только просмотр + экспорт; повтор = новое исследование).
- Шаринг / мультитенантность.
- Авто-возобновление прерванных задач.
- Рендер JS-страниц при скрейпинге (только HTML; Playwright — позже).

**Язык:** UI — русский; язык отчёта — по выбору в интервью (по умолчанию русский).

## 9. Pre-implementation gate (обязательно до старта кода)

**Smoke-test Ollama на tool-calling / structured output.** Модель `gemma4-e2b-fast` ~2B эффективных параметров — мульти-агент с tool-calling и JSON-выводом может разваливаться (невалидный JSON, путаница ролей).

Шаги:

1. Поднять Ollama + модель.
2. Подключить через `IChatClient` (OllamaSharp или OpenAI-совместимый endpoint Ollama).
3. Проверить: стабильный вызов одной function tool с валидными аргументами; стабильный `format=json` вывод по схеме.

**Если тест провален:** перейти на fallback — single agent + tools (одна роль, ReAct-цикл) ИЛИ подключить модель посильнее. `IChatClient` остаётся swappable в любом случае.

## 10. Открытые вопросы для этапа планирования

- Точные дефолты guard'ов по `Depth` (раунды/источники/таймаут).
- Библиотека PDF-рендера (например QuestPDF) — выбрать на этапе плана.
- Размещение Worker (hosted service внутри Web vs отдельный процесс) — для MVP hosted service в Web.
