# LLM Gate — результаты прогона

Дата: 2026-05-22

Прогон диагностического smoke-test (`tools/AiResearchers.LlmSmokeTest`): basic chat / tool-calling / structured JSON. Сделано несколько прогонов для оценки стабильности (не один), т.к. повтор выявил нестабильность.

## Результаты по прогонам

| Модель | Прогон | basic chat | tool-calling | structured JSON | Итог |
|---|---|---|---|---|---|
| bjoernb/gemma4-e2b-fast:latest | #1 | PASS | PASS | PASS | 3/3 |
| bjoernb/gemma4-e2b-fast:latest | #2 | PASS | **FAIL** (error sending request) | PASS | 2/3 |
| bjoernb/gemma4-e2b-fast:latest | #3 | PASS | PASS | PASS | 3/3 |
| bjoernb/gemma4-e2b-fast:latest | #4 | PASS | PASS | PASS | 3/3 |
| bjoernb/gemma4-e2b-fast:latest | #5 | PASS | PASS | PASS | 3/3 |
| qwen3.5:2b (fallback) | #1 | PASS | PASS | **FAIL** (HttpClient.Timeout 100s) | 2/3 |

Итог по target-модели: **4 из 5 прогонов = 3/3**. Единственный сбой tool-calling — transient HTTP-ошибка (`An error occurred while sending the request`), вероятно cold-start / блип Ollama на первом вызове после сборки; не воспроизвёлся в 4 последующих прогонах.

## Анализ

- Сбои носят характер **таймаута / transient HTTP**, а НЕ неспособности модели:
  - gemma: разовый «error sending request» на tool-call.
  - qwen3.5:2b: JSON упал по дефолтному `HttpClient.Timeout = 100 s` (модель медленнее, не уложилась), вывод не был невалидным.
- structured JSON у target-модели — стабильно PASS (главный путь для детерминированных агентов, обменивающихся JSON).
- tool-calling у target-модели работает (4/5), но требует устойчивости к transient-ошибкам.

## Решение (gate decision)

**Топология: multi-agent + deterministic workflow — ПОДТВЕРЖДЕНА** на `bjoernb/gemma4-e2b-fast:latest`. Phase 4 идёт по спеке.

**Обязательные требования к Phase 4 (вытекают из прогона):**
1. Поднять `HttpClient.Timeout` LLM-клиента до **≥ 300 s** (дефолт 100 s мал для локальных моделей, особенно cold start). Настроить при сборке `OllamaApiClient`/HttpClient в `LlmServiceCollectionExtensions`.
2. **Retry + backoff на transient HTTP-ошибки** LLM (уже предписано спекой §6). Покрыть `HttpRequestException` и `TaskCanceledException` (timeout).
3. `IChatClient` остаётся swappable — смена модели через конфиг `Llm:Model` без правки кода (например на `gemma4:e4b`, если нужно больше качества).

## Выбранный путь

Phase 4 — **multi-agent + deterministic workflow** на `bjoernb/gemma4-e2b-fast:latest`, с увеличенным LLM-таймаутом и retry/backoff на transient-ошибки. Fallback-модели (`gemma4:e4b`, `qwen3.5:2b`) доступны через конфиг при необходимости.
