# SOBRANIE_VOID — Spike #1

Goal: validate in ~30 min of wall-clock (most of it model download) whether the **domestic-yak-8B-instruct Q4_K_M** model on your Oracle A1 (4 OCPU Ampere Altra, 24GB RAM, Ubuntu 24.04 aarch64) meets three bars:

1. **Throughput**: generation >= 4 tokens/sec on a ~1000-token prompt + 200-token output. Below this, the simulation cadence gets painful.
2. **Memory**: total Ollama RSS < 7GB, leaving ~17GB for .NET + scraper + Piper + OS.
3. **Persona quality**: the model can hold a Macedonian MP persona for 3+ sentences without drifting into English/Serbian/Bulgarian or leaking chat-template tokens.

If all three pass → we build the v1.1 MVP.
If any fail → we iterate (smaller quant, different model, or prompt engineering) *before* writing any C#.

## What to run

```bash
# On the A1 VM, as your non-root user (must have sudo):
scp run-spike.sh ubuntu@<a1-public-ip>:~/
ssh ubuntu@<a1-public-ip>
chmod +x ~/run-spike.sh
~/run-spike.sh 2>&1 | tee ~/spike.log
```

Expected wall-clock:
- apt install: ~30s
- Ollama install: ~30s
- GGUF download (~4.7GB): **slowest step, 5-30 min** depending on your bandwidth to HuggingFace's CDN from Oracle's region
- Model import + warmup: ~1-2 min
- Benchmark + 5 persona probes: ~5-10 min

Total: **15-45 min**, dominated by download.

## What you paste back

The final report, which the script writes to `~/sobranie-spike/REPORT.md` and also dumps to stdout at the end. It's a self-contained markdown file with:

- Host fingerprint (OS, cores, RAM)
- Throughput table (prefill t/s, gen t/s) for 5 payload sizes
- RAM footprint measurement
- Full Macedonian outputs from 5 persona probes
- Go/no-go checklist

Paste the whole `REPORT.md` contents into the chat.

## What I'll do with the report

- **TPS numbers** → firm up the speech-cadence math (5s? 20s? 40s?). This drives the FSM timer constants.
- **Persona outputs** → we read them together. If the aggression/legalism dials are clearly visible in the Macedonian text, we keep the prompting strategy as designed. If the outputs are bland/generic, we either switch models or add few-shot persona exemplars per MP.
- **RAM** → confirm we can add Piper TTS and .NET without swapping. If Ollama eats >8GB, we drop to Q4_K_S quant.

## If something breaks

Paste the last ~100 lines of `spike.log`. Most likely failure modes, ranked:

1. **HuggingFace 401 on the GGUF download** — HF returns 401 (not 404) for non-existent repos when you're unauthenticated. First spike attempt hit this because `bartowski` never quantized this specific model. Script now uses `mradermacher/domestic-yak-8B-instruct-GGUF` which has the full quant range. If mradermacher ever disappears, the fallback is converting from the ungated `LVSTCK/domestic-yak-8B-instruct` safetensors ourselves (adds ~15 min and ~16GB transient RAM for conversion, but doesn't require an HF token).
2. **Ollama install script fails on Ubuntu 24.04 Minimal** — "Minimal" images sometimes lack `systemctl` or have no default user in the `sudoers` file. Workaround: install Ollama manually from the tarball.
3. **Model import fails with "unsupported architecture"** — the GGUF metadata might claim a template Ollama doesn't auto-detect. The Modelfile in the script pins an explicit Llama-3 template, which should handle this, but if it fails we adjust.
4. **num_predict=200 never returns** — means generation is so slow the benchmark appears hung. Let it run; it may take 60+ seconds for the long_200 test. If it exceeds 3 minutes, abort and paste the log — we have a much bigger problem than prompt engineering.

## Cleanup

If you want to abort and reclaim disk:

```bash
ollama rm yak8b:q4km
rm -rf ~/sobranie-spike
sudo systemctl stop ollama
sudo snap remove ollama   # if installed via snap
# or: sudo rm /usr/local/bin/ollama /usr/local/lib/ollama
```

The Ollama systemd drop-in at `/etc/systemd/system/ollama.service.d/override.conf` is the only other artifact.
