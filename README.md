# sobranie-void — Assembly of the Void

A headless, self-sustaining multi-agent simulation of the Macedonian parliament (Собрание на Република Северна Македонија), running on a single Oracle Cloud Ampere A1 free-tier instance.

The orchestrator is a finite state machine in .NET 10 that acts as the Speaker of the House. A small cast of seven MPs is rendered through a local Macedonian-language LLM (`domestic-yak-8B-instruct`, Q4_K_M, via Ollama); the remaining 113 MPs are a statistical chorus that emits hand-authored reactions on a Poisson distribution. Real headlines scraped from Macedonian news portals drive the agenda, so the system is coupled to reality rather than looping on synthetic entropy.

The result, ideally: a readable live Hansard of a parliament that does not exist, commenting on political events that do.

## Status

- [x] Spike #1: model + inference validated on Oracle A1 (4 OCPU Ampere Altra, 24 GB RAM). See [`spike/REPORT.md`](spike/REPORT.md).
- [ ] MVP build in progress.

## Hard constraints

- **Compute**: 4 ARM64 OCPUs (Ampere Altra, Neoverse-N1), no GPU. All inference is CPU-only.
- **Memory**: 24 GB. Ollama holds ~5.2 GB for yak-8B Q4_K_M.
- **Inference throughput**: ~3.9 tokens/sec on 200-token responses. Speech cadence ≈ 45–60 s.
- **Prompt-prefix caching is load-bearing.** Cold prefill is ~5 t/s; warm prefill (KV cache hit) is ~830 t/s. The persona prefix must be byte-identical across turns for the same MP.

## Layout

```
sobranie-void/
├── spike/                 Spike #1: model validation script + report
├── src/
│   ├── Sobranie.Domain/           Entities, value objects, core records (no IO)
│   ├── Sobranie.Infrastructure/   EF Core/SQLite, Ollama client, scraper
│   └── Sobranie.Orchestrator/     ASP.NET Core host, FSM, SignalR hub
├── web/                   Angular 20 SPA (served by Orchestrator)
├── deploy/                systemd units, deploy script for Oracle A1
└── docs/                  Design notes, decisions, amendments
```

## Build locally

```powershell
dotnet build src/Sobranie.sln
dotnet run --project src/Sobranie.Orchestrator
```

## Deploy to the A1

See [`deploy/README.md`](deploy/README.md).

## License

MIT. See [`LICENSE`](LICENSE).
