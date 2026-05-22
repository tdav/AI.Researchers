# Smart Iterative Interviewer — Design

Date: 2026-05-22
Status: Approved (design)

## Goal

Improve the research-scoping interviewer so it:

1. **Thinks well about its questions** — reasons about coverage gaps, prioritizes
   the highest-impact clarifications, never duplicates what the user already answered.
2. **Offers answer options** — every clarifying question ships with concrete suggested
   answers the user can pick, plus a free-text fallback.

The interview is **iterative**: after the user answers a round, the interviewer can
generate a follow-up round that builds on those answers. Stopping is **hybrid** — the
model recommends "enough" or "ask more", but the user makes the final call.

## Current State (baseline)

- `IInterviewService.GenerateFollowUpQuestionsAsync(task)` → `IReadOnlyList<string>`
  (plain question strings), single LLM call via `GetResponseAsync<FollowUpQuestions>`.
- `Interview.cshtml` + `Shared/_QuestionsForm.cshtml`: each question rendered as a
  free-text `<input>`. No options, no rounds.
- `InterviewModel.OnPostSaveAsync` persists each Q/A as an `InterviewAnswer`
  (`Source=Ai`, `Order=i`), then redirects to `Outline`.
- `InterviewAnswer` entity has no round concept.
- Model is a slow local 2B (`bjoernb/gemma4-e2b-fast`). Every extra LLM call costs
  tens of seconds — keep calls to **one per round**.

## Approach

**Chosen (A): Structured DTO + single-call hybrid + iterative rounds via page reload.**

- One LLM call per round returns both the questions (with per-question kind + options)
  and the `enoughRecommended` signal. No separate classifier or "is-enough" call.
- Iteration is achieved by feeding prior answers back into the prompt (the existing
  `RenderAnswers` already does this), so each round sees the full answer history.

Rejected:

- **B — per-question classifier call** to decide single/multi: doubles LLM calls, too
  slow for a 2B model.
- **C — static client-side option templates**: no per-topic reasoning, loses the
  "well-thought questions" requirement.

## Design

### 1. Core DTOs + enum

New `QuestionKind` enum in `Core/Enums` (alongside existing research enums):

```csharp
public enum QuestionKind { Single, Multi }
```

New DTOs in `Core/Interview`:

```csharp
public class ClarifyingQuestion
{
    public string Text { get; set; } = string.Empty;
    public QuestionKind Kind { get; set; } = QuestionKind.Single;
    public List<string> Options { get; set; } = new();   // 2-5 concrete suggestions
    public bool AllowOther { get; set; } = true;          // always allow free text
}

public class InterviewRound
{
    public List<ClarifyingQuestion> Questions { get; set; } = new();
    public bool EnoughRecommended { get; set; }
    public string? Rationale { get; set; }                // short "why" hint
}
```

### 2. IInterviewService

Replace:

```csharp
Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(ResearchTask task, CancellationToken ct = default);
```

with:

```csharp
Task<InterviewRound> GenerateClarifyingRoundAsync(ResearchTask task, CancellationToken ct = default);
```

`GenerateOutlineDraftAsync` is unchanged.

**Prompt requirements** (single call, hybrid signal folded in):

- Reason about coverage gaps for `task.Topic` given `Depth`, `Audience`, and the
  answers already collected (`RenderAnswers`).
- Prioritize the highest-impact clarifications. Do **not** repeat anything already
  answered.
- For each question, emit a `kind` (`single`/`multi`) appropriate to its meaning and
  2-5 concrete, mutually-distinct `options` in `task.Language`.
- Set `enoughRecommended = true` when prior answers already cover the scope well, or
  when the caller indicates the max round was reached.
- Output via `GetResponseAsync<InterviewRound>` using a private mirror class shaped for
  the JSON contract (mirrors the existing `FollowUpQuestions` private-class pattern).

The service stays stateless about rounds; the page model decides when to stop and
passes the relevant context.

### 3. Persistence

- Add `int Round` to the `InterviewAnswer` entity (default `1`). Generate and apply an
  EF Core migration (PostgreSQL schema change).
- The saved `Answer` is the resolved text: selected option label(s) joined by `"; "`,
  with the optional "Other" free-text appended. `Source` stays `Ai`.
- `Question` continues to store the question text.
- Suggested `Options` are **not** persisted — they are a transient UI affordance (YAGNI).

### 4. UI flow

`Interview.cshtml` + new `Shared/_ClarifyingRound.cshtml` partial (replacing the
free-text-only `_QuestionsForm.cshtml`).

- **Render a question:**
  - `Single` → radio group over `Options`, plus an "Другое" radio that reveals a text input.
  - `Multi` → checkboxes over `Options`, plus an "Другое" checkbox that reveals a text input.
  - If a question has empty `Options`, gracefully fall back to a single free-text input.
- **Submit a round (Save):** server resolves each question's selection(s) into answer
  text, persists `InterviewAnswer` rows with the current `Round`, then re-renders the
  page: prior rounds shown above as read-only summaries, current state below.
- **Hybrid stop controls** after answers are saved:
  - "Уточнить ещё" — generates the next round (visible only while `round < MaxInterviewRounds`).
  - "К структуре отчёта →" — proceeds to `Outline`.
  - `EnoughRecommended` decides which button is primary and surfaces `Rationale` as a
    small hint. `enough == true` → "К структуре" primary; otherwise → "Уточнить ещё" primary.
- `MaxInterviewRounds = 3`. At the max, only "К структуре" is shown, with a note that
  the limit was reached.

### 5. htmx mechanics

- Generate round: `hx-post` returns the `_ClarifyingRound` partial.
- Save round: standard form post → server persists → re-renders the page (prior rounds +
  fresh controls / next-round trigger).
- Antiforgery `RequestVerificationToken` header is already wired for htmx POSTs.

### 6. Error handling

- LLM failure or empty questions → message ("не удалось сгенерировать, можно перейти к
  структуре") plus a proceed link, matching the existing fallback pattern.
- A question with empty `Options` → free-text input only (graceful).
- Wrap the service call in try/catch in the page model (existing pattern).

### 7. Testing

Per project rule, tests are **not** added/modified without explicit confirmation.
Tests for the new DTOs, prompt mapping, round persistence, and answer-resolution logic
are out of scope for this spec unless separately approved.

## Out of Scope (YAGNI)

- Persisting suggested options or per-question kind in the DB.
- More than one LLM call per round (no separate classifier / enough-check call).
- Editing answers from a previous, already-submitted round.
- Auth, pagination, or any unrelated wizard changes.

## Migration / Compatibility Notes

- `GenerateFollowUpQuestionsAsync` is removed; the only caller is `InterviewModel`,
  which is updated in the same change.
- The `_QuestionsForm.cshtml` partial is replaced by `_ClarifyingRound.cshtml`.
- The `InterviewAnswer.Round` column requires an EF migration; existing rows default to
  round `1`.
