# Design & Build Decisions

Chronological log of decisions that diverge from the v1.0 design doc or
from obvious defaults. Each entry: what changed, why, alternatives rejected.

## D-001: Model switched from VezilkaLLM to domestic-yak-8B-instruct

**Date**: Spike #1
**Context**: Design doc specified `VezilkaLLM` as the base model.
**Decision**: Use `LVSTCK/domestic-yak-8B-instruct` (Llama-3.1-8B derivative,
Macedonian-tuned, instruction-following).
**Why**: VezilkaLLM is a *base* (non-instruct) model. Feeding it our persona
prompts would produce completions, not role-play dialogue. Instruct tuning
is load-bearing for the Hansard format.
**Rejected**: Fine-tuning VezilkaLLM ourselves — out of budget for 4 OCPU
with no GPU.

## D-002: GGUF quant source — mradermacher, not bartowski

**Date**: Spike #1
**Decision**: Pull `mradermacher/domestic-yak-8B-instruct-GGUF:Q4_K_M`.
**Why**: bartowski (the usual go-to quantizer) has no quant for this model.
mradermacher's Q4_K_M is ~4.9 GB and fits the memory envelope with headroom.
**Note**: HuggingFace returns HTTP 401 (not 404) for non-existent repos when
unauthenticated. Cost us ~15 minutes chasing a phantom auth failure.

## D-003: Spec amendments from spike results

**Date**: Spike #1 REPORT.md
**Context**: A1 benchmark showed 3.86 tok/s generation, 5185 MB Ollama RSS,
3/5 persona probes acceptable. Cold prefill 5 tok/s vs warm 829 tok/s.
**Decisions**:
- **(A)** Prompt-prefix caching is load-bearing. All personas share an
  identical system-prompt prefix; per-turn content appended only.
- **(B)** Cadence revised from "every 30s" (design doc) to **45–60s per
  speech**. At 3.86 tok/s, 150-token speeches take ~39s of pure generation;
  30s cadence would compound latency.
- **(C)** `legalism` trait needs few-shot exemplars. Added
  `MPProfile.SignatureMoves` (ordered list of 3–5 exemplar utterances per MP)
  as in-context examples, not trait scalars alone.

## D-004: .NET 10 LTS, not .NET 9

**Date**: Sprint 0
**Decision**: Target `net10.0`.
**Why**: .NET 10 is LTS (released Nov 2025, supported to Nov 2028). .NET 9
is STS (ends May 2026 — 1 month out). Developer machine has SDK 10.0.103
installed; no .NET 9 present. Avoids a forced upgrade mid-project.
**Side effect**: Solution uses the new `.slnx` XML format instead of `.sln`.

## D-005: EF Core 10.0.0 transitive CVE acknowledged, not fixed

**Date**: Sprint 0
**Context**: `Microsoft.EntityFrameworkCore.Sqlite 10.0.0` pulls
`System.Security.Cryptography.Xml 9.0.0` which has advisory NU1903.
**Decision**: Add `WarningsNotAsErrors>NU1903;NU1902</WarningsNotAsErrors>`
in `src/Directory.Build.props` to allow the build to pass while keeping
`TreatWarningsAsErrors` on for everything else.
**Remediation**: Remove the suppression when EF Core 10.0.1 ships with an
updated transitive (expected within Q1 of EF's patch cadence).
**Rejected**: Pinning `System.Security.Cryptography.Xml` to a newer version
directly — fragile, breaks when EF upgrades cleanly.

## D-006: Angular 20, not 19

**Date**: Sprint 0
**Context**: `ng new` from the globally installed CLI produced an Angular
20 workspace.
**Decision**: Accept Angular 20. All features we use (signals, standalone
components, `@microsoft/signalr` 10.x client, `@if` control flow) work
identically or better than on 19.
**Non-decision**: Pinning to 19 would require a global CLI downgrade or
`npx @angular/cli@19` — both pointless churn.

## D-007: SignalR transcript rendering in a `<pre>` block (MVP)

**Date**: Sprint 0
**Decision**: For the smoke-test UI, accumulate chunks into a single
`transcript` signal rendered in `<pre>`.
**Why**: MVP focus is "does the pipe work end-to-end", not visual polish.
Seating chart / speaker highlighting deferred to a later sprint per the
design doc's "brutally simple" directive.

## D-008: Develop on Windows, deploy via `git pull` + `dotnet publish` on A1

**Date**: Sprint 0
**Decision**: No cross-compilation artifacts, no Docker, no CI/CD.
`git clone` on the A1, `dotnet publish -r linux-arm64 --no-self-contained`
on the A1 itself.
**Why**: Simplest possible supply chain. .NET 10 ASP.NET Core runtime is
available in the Microsoft apt feed for ARM64 Ubuntu 24.04. Self-contained
publishes would double disk usage for no benefit.
**Rejected**: GitHub Actions ARM64 runners — overkill for a hobby project,
and we'd still need to pull the artifact onto the A1.

## D-009: SignalR payload serialization forced to camelCase

**Date**: Sprint 0
**Context**: The Angular client unpacks `payload.chunk` and `payload.done`
(camelCase). SignalR's .NET JSON protocol defaults follow
`System.Text.Json` defaults (PascalCase for real DTOs; preserves casing for
anonymous objects).
**Decision**: Configure
`AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)`
in `Program.cs` preemptively.
**Why**: The smoke endpoint currently sends anonymous objects with lowercase
property names, so it happens to work — but the first real DTO we introduce
would break wire compat silently. Fix it now, not later.

## D-010: Auto-migrate on startup (no manual gating)

**Date**: Sprint 0 (post-commit)
**Decision**: Call `app.Services.MigrateSobranieDatabaseAsync()` before
`app.Run()` so every boot applies pending migrations.
**Why**: Single-node SQLite, single-process app, hobby project. There is no
scenario where a blue/green deploy, schema drift detection, or manual
migration gating adds value. Boot, migrate, serve.
**Rejected**: `db.Database.EnsureCreated()` — bypasses migrations, makes
future schema changes impossible without wiping the file.

## D-011: Suppress CA1861/CA1062/CA1812 on EF-generated migrations

**Date**: Sprint 0 (post-commit)
**Context**: `dotnet ef migrations add` generates code that trips
`TreatWarningsAsErrors` (CA1861 on column-type array literals, etc.).
**Decision**: Add `.editorconfig` in `Persistence/Migrations/` overriding
those rules to `severity = none` for that folder only.
**Why**: We don't hand-edit generated migration code. Suppressing globally
would hide real bugs; scoping to the Migrations folder is the targeted fix.
**Rejected**: Using partial classes to work around the analyzers — adds
maintenance burden for zero real-world benefit.

## D-012: UtteredAt, not UttereredAt

**Date**: Sprint 0 (post-commit)
**Context**: Typo in `Speech.UttereredAt` caught while generating the
initial migration. Fixed before the migration encoded it, which would have
required a rename migration to correct.
**Lesson**: Always run `dotnet build` **and** eyeball entity names before
`ef migrations add`. Migrations are a one-way ratchet for column names.

## D-013: TTS — Piper is out, eSpeak-NG is in (for MVP)

**Date**: Sprint 1 planning
**Context**: Librarian research (task `bg_4ffa660e`) confirmed:
- `rhasspy/piper-voices` has no `mk` voice across 35 supported languages.
- No community-trained Piper MK voice on HuggingFace.
- Coqui XTTS-v2 does not list Macedonian in its 17-language set.
- eSpeak-NG has confirmed `mk` support, ships in Ubuntu 24.04 ARM64 repos.
- OmniVoice (k2-fsa) supports 600+ languages zero-shot including MK, but
  CPU latency is ~20s per 7s clip — prohibitive for real-time on 4 cores.

**Decision**: MVP uses **eSpeak-NG** invoked as a subprocess from the
orchestrator (`espeak-ng -v mk "text" -w output.wav`). Zero extra deps,
`sudo apt install espeak-ng` on the A1.

**Rejected**:
- **Piper**: no MK voice exists. Training one requires a clean MK speech
  corpus (Common Voice MK etc.) and GPU-hours we don't have.
- **OmniVoice**: quality upgrade path after MVP, but 20s/clip makes it
  unusable for live parliament pace (45–60s speeches would pile up).
- **XTTS-v2**: no MK support.

**Follow-up**: post-MVP, evaluate OmniVoice with a job-queue model where
audio is pre-generated in the background during LLM inference. Not MVP
scope.

## D-014: Seed data uses fictional placeholder personas

**Date**: Sprint 1
**Decision**: `src/Sobranie.Orchestrator/seed-data.json` ships 4 fictional
parties (Alpha/Beta/Gamma/Delta) and 7 fictional MPs with Cyrillic
placeholder names (`Пратеник А1`, etc.). Real-name personas are a
deliberate non-decision for now.
**Why**:
- Eliminates defamation risk during development.
- Seed file is content, not code — can be swapped wholesale by editing
  JSON, no rebuild required.
- Persona prompts are in Macedonian and drive the MK LLM correctly; the
  *fiction* is the identity layer, not the linguistic layer.
**Follow-up**: Before any public launch, user decides: real names (and
accepts the legal posture) OR stays fictional (and we make that an explicit
product framing).

## D-015: LoggerMessage source generator for all structured logs

**Date**: Sprint 1
**Context**: `TreatWarningsAsErrors=true` makes CA1848 block any
`logger.LogXxx(...)` call. The rule recommends source-generated
`[LoggerMessage]` delegates for performance (no boxing, no `params object[]`
allocation on the hot path).
**Decision**: All Infrastructure/Orchestrator logging must use
`[LoggerMessage]` partial methods on a `partial class`. Not optional.
**Exception**: Serilog's `UseSerilogRequestLogging()` middleware is allowed
to internally use whatever it likes — we don't author that code.

## D-016: FSM speaker selection via utility-weighted softmax

**Date**: Sprint 1
**Context**: MVP step 3 needs a way to pick which of the 7 MainCast MPs
speaks next, every 45-60 seconds, without degenerating into either
round-robin (boring) or uniform random (ignores context).

**Decision**: Two-stage pipeline.

1. `UtilityCalculator` assigns each MP a scalar utility:
   `utility = base + (trait_match * weight) - recency_penalty`
   - `trait_match` is a bag-of-MK-keywords classifier over the current
     `InDebate` proposal headline + rewritten body, scoring populism,
     legalism, and aggression traits against the MP's stats.
   - `recency_penalty` is an exponential decay
     `magnitude * 0.5 ^ (turnsAgo / halfLife)` against the last N speeches
     (default N=8, halfLife=3).
2. `SpeakerSelector` applies softmax with configurable temperature
   (default 0.6) and does inverse-CDF sampling via a caller-supplied
   `Random`. Temperature -> 0 is greedy; -> infinity is uniform.

**Why not alternatives**:
- **Pure random**: ignores persona specialization and recency fatigue.
- **Argmax**: deterministic -> audience predicts next speaker instantly.
- **Embedding-based topic match**: requires an embedding model in-proc
  alongside the 8B chat model, blowing the RAM budget. Keyword classifier
  is 200 lines and "good enough" until we have real proposals.

**Recency half-life of 3** was picked so that a speaker who just talked
still has ~50% fatigue three turns later - enough to rotate the cast
without making it mechanical.

**Tuning knobs** live in `SobranieOptions.Fsm` and are bindable via
`appsettings.json` - no code change to retune.

**SQLite caveat**: `DateTimeOffset` columns cannot appear in
`ORDER BY`. The FSM orders recent speeches and proposals by auto-
increment `Id` (monotonic for insert-only workloads, equivalent in
practice to chronological order).

**Verified**: 8 unit tests in `Sobranie.Infrastructure.Tests` cover
softmax normalization, monotonicity, zero-score uniform fallback,
determinism under seeded RNG, and recency penalty. Boot smoke confirms
the orchestrator idles until `POST /api/session/start`, ticks cleanly,
and recovers from transient Ollama outages.
