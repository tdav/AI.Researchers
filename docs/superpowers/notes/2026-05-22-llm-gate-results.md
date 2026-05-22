# LLM Gate — результаты прогона

Дата: 2026-05-22

| Модель | basic chat | tool-calling | structured JSON | Итог |
|---|---|---|---|---|
| bjoernb/gemma4-e2b-fast:latest | PASS | PASS | PASS | 3/3 |
| qwen3.5:2b (fallback, если гоняли) | — | — | — | не гоняли |

## Решение (gate decision)

- **3/3 у целевой модели** → топология **multi-agent + deterministic workflow** (как в спеке) подтверждена. Phase 4 идёт по плану.
- **tool-calling FAIL, но chat+JSON OK** → multi-agent с function-tools ненадёжен. Выбрать одно:
  - (a) перейти на **single-agent + structured-JSON** оркестрацию (агенты обмениваются JSON, без function-tools), ИЛИ
  - (b) сменить рабочую модель на fallback с рабочим tool-calling (правка `Llm:Model` в конфиге — код не меняется).
- **chat FAIL** → проблема окружения (Ollama не запущен / модель не скачана), не модели. Починить инфраструктуру и перепрогнать.

## Выбранный путь

**Модель:** `bjoernb/gemma4-e2b-fast:latest` (5.1B Q4_K_M) — прошла все 3 проверки с результатом 3/3.

**Топология:** multi-agent + deterministic workflow, как описано в спеке (§2, §4). Function-calling и structured JSON работают корректно. Fallback (`qwen3.5:2b`) не потребовался.

Phase 4 (Research engine с агентами MAF и оркестратором) реализуется по исходному плану без изменений топологии.
