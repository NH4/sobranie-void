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
