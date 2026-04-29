# Context

## Project Type

Macedonian parliamentary simulation. Headless ASP.NET Core orchestrator + SignalR real-time streaming to an Angular SPA. LLM (domestic-yak-8B-instruct Q4_K_M via Ollama) drives the MainCast; weighted-random ChorusLines provide ambient reactions.

---

## Domain Terms

Use these exact terms — do not substitute synonyms.

| Term | Definition |
|---|---|
| **Собрание** | Parliament of North Macedonia. 120 пратеници (MPs). |
| **Пратеник** | Member of parliament. |
| **Пратенички клуб** | Party parliamentary group. |
| **Коалиција** | Coalition. Some parties govern as a coalition; others are opposition. |
| **Претседател на Собрание** | Speaker of parliament. Currently Afrim Gashi (Алтернатива/ВЛЕН). |
| **Заменик-претседател** | Deputy speaker. Currently Antonijo Miloshoski (ВМРО-ДПМНЕ). |
| **Предлог** | A topical proposal / debate motion. Sourced verbatim from time.mk RSS. |
| **Дневен ред** | Agenda — the active Предлог under debate. |
| **Гласање** | Vote. Not used in simulation. |
| **Деловник** | Rules of Procedure of the Assembly. |
| **Член** | Article of the Деловник or Устав. |

### Parties

| Party | Short | Color |
|---|---|---|
| ВМРО-ДПМНЕ | ВМРО | `#C8102E` |
| Социјалдемократски сојуз (СДСМ) | СДСМ | `#E63946` |
| Демократска унија за интеграција (ДУИ) | ДУИ | `#F2C94C` |
| Левица | Левица | `#B71C1C` |
| ВЛЕН | ВЛЕН | `#27AE60` |
| Алтернатива | Алт. | `#27AE60` |
| Движење ЗНАМ | ЗНАМ | `#8E44AD` |
| Алијанса за Албанците | АА | `#1E90FF` |
| Демократска партија на Србите | ДПС | `#2C3E50` |
| БЕСА | БЕСА | `#00A878` |
| Независни пратеници | Незав. | `#7F8C8D` |
| Остани | — | — |

### Key Parties (2024–2028)

- **ВМРО-ДПМНЕ** — 55 seats, main right-wing opposition
- **СДСМ** — 15 seats, social-democrat, led opposition 2017–2024
- **ДУИ** — 10 seats, Albanian party, post-2001 war
- **Левица** — 6 seats, radical left, no EU/NATO绕道
- **ВЛЕН + Алтернатива** — combined 7 seats, government, Speaker from their ranks

---

## Simulation Terms

| Term | Definition |
|---|---|
| **MainCast** | The 7 MPs with full personas. Selected by softmax-weighted utility each turn. |
| **Chorus** | All other MPs. No individual identity. Emit pre-authored reaction lines. |
| **ChorusReaction** | A `SpeechKind` — short ambient line, no MP attribution. |
| **SessionOrchestrator** | Background service running the turn loop (45–60s cadence). |
| **ChorusEmitterService** | Parallel background service emitting Poisson-timed chorus bursts. |
| **Turn** | One MainCast speech. After `TurnsPerProposal` (12), proposal auto-concludes. |
| **UtilityCalculator** | Scores each MainCast MP per turn: `base + traitMatch - recencyPenalty`. |
| **SpeakerSelector** | Softmax over utilities → inverse-CDF sampling → picks the speaker. |
| **SpeechGenerator** | Streams LLM via OllamaSharp. Persona prompt = Core + overlay + signature moves. |

### Proposal Lifecycle

```
Queued → InDebate → Concluded
  ↑         ↓
  ← (after 12 turns)
```

`RssScraperService` polls time.mk/rss/all every 5 minutes, deduplicates by URL, inserts as `Queued`. `SessionOrchestrator.EnsureCurrentProposalAsync` promotes oldest `Queued` → `InDebate`. After 12 turns, `ConcludeAndPromoteAsync` marks it `Concluded` and promotes the next.

### SpeechKind Values

- `MainCastSpeech` — signed, attributed to a named MP
- `Interjection` — short reactive comment (future)
- `ChorusReaction` — unsigned ambient reaction line
- `SpeakerAction` — procedural (future)

---

## Technical Terms

| Term | Definition |
|---|---|
| **yak8b** | The `yak8b:q4km` Ollama model: `domestic-yak-8B-instruct` Q4_K_M. |
| **OllamaSharp** | `Microsoft.Extensions.AI` chat client for Ollama. Streams tokens in real-time. |
| **SignalR** | Real-time pub/sub. `ReceiveSpeech` (chunk), `ReceiveSpeechComplete` (full), `ReceiveChorusReaction` (chorus), `StateChange` (turn/proposal updates). |
| **SatireIntensity** | Config: `gentle` / `sharp` / `absurd`. Selects persona overlay register. |
| **LlmCallLog** | Per-call audit: model, prompt hash, tokens/sec, rejected flag. |

---

## Architecture

```
time.mk RSS
    ↓
RssScraperService → SQLite Proposals (Queued)
    ↓
SessionOrchestrator (turn loop, 45–60s cadence)
    ↓
UtilityCalculator → SpeakerSelector → SpeechGenerator → Ollama (yak8b:q4km)
    ↓
SignalR → SobranieHub → Angular SPA
    ↓
ChorusEmitterService → SQLite ChorusReaction Speeches
```

Orchestrator API (Kestrel, port 5000):
- `GET /api/smoke/speak` — smoke test, streaming text
- `POST /api/session/start` — start FSM loop
- `POST /api/session/stop` — stop FSM loop
- `GET /api/session/status` — {running, startedAt, turnsCompleted, lastError}
- `GET /health` — health check
- SignalR hub at `/hub`
