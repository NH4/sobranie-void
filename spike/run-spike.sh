#!/usr/bin/env bash
#
# SOBRANIE_VOID — Spike #1: Ollama + domestic-yak-8B-instruct on Oracle A1 ARM64
#
# Target: Ubuntu 24.04 Minimal aarch64, 4 OCPU Ampere Altra (Neoverse-N1), 24GB RAM
#
# What this does:
#   1. Installs system deps (Ollama, jq, bc, curl, git)
#   2. Pulls domestic-yak-8B-instruct GGUF from HuggingFace (Q4_K_M)
#   3. Imports into Ollama via Modelfile with tuned parameters
#   4. Benchmarks prefill + generation TPS with 3 payload sizes
#   5. Runs 5 Macedonian persona test prompts (the real validation)
#   6. Measures RAM footprint under load
#   7. Prints a structured report you paste back to the orchestrator
#
# Idempotent: safe to re-run. Logs to ./spike.log.
#
# Usage:
#   chmod +x run-spike.sh
#   ./run-spike.sh 2>&1 | tee spike.log
#
# Expected wall-clock: ~20-40 minutes (model download dominates; ~4.6GB).

set -euo pipefail

# ---------- config ----------
MODEL_REPO="mradermacher/domestic-yak-8B-instruct-GGUF"         # community GGUF of LVSTCK/domestic-yak-8B-instruct (bartowski has no quant of this one)
MODEL_FILE="domestic-yak-8B-instruct.Q4_K_M.gguf"               # ~4.9GB (note the '.' separator in mradermacher's naming)
MODEL_TAG="yak8b:q4km"                                          # local ollama name
WORKDIR="${HOME}/sobranie-spike"
MODEL_DIR="${WORKDIR}/models"
REPORT="${WORKDIR}/REPORT.md"
OLLAMA_HOST_URL="http://127.0.0.1:11434"

mkdir -p "${MODEL_DIR}"
cd "${WORKDIR}"

log() { printf '\n\033[1;36m[spike]\033[0m %s\n' "$*"; }
warn() { printf '\n\033[1;33m[warn]\033[0m %s\n' "$*"; }
die() { printf '\n\033[1;31m[fatal]\033[0m %s\n' "$*" >&2; exit 1; }

# ---------- 0. Sanity ----------
log "Host sanity check"
uname -m | grep -q aarch64 || die "Not aarch64. Expected ARM64. Got: $(uname -m)"
NPROC=$(nproc)
MEM_GB=$(awk '/MemTotal/ {printf "%.0f", $2/1024/1024}' /proc/meminfo)
echo "  arch: $(uname -m)"
echo "  cores: ${NPROC}"
echo "  ram_gb: ${MEM_GB}"
echo "  kernel: $(uname -r)"
echo "  os: $(. /etc/os-release; echo "${PRETTY_NAME}")"
[ "${NPROC}" -ge 4 ] || warn "Fewer than 4 cores detected (${NPROC}). Results won't match spec."
[ "${MEM_GB}" -ge 20 ] || warn "Less than 20GB RAM (${MEM_GB}GB). Model load may fail."

# ---------- 1. Install deps ----------
log "Installing system packages (apt)"
export DEBIAN_FRONTEND=noninteractive
sudo apt-get update -qq
sudo apt-get install -y -qq curl jq bc git ca-certificates coreutils util-linux

# ---------- 2. Install Ollama ----------
if ! command -v ollama >/dev/null 2>&1; then
  log "Installing Ollama (arm64 official)"
  curl -fsSL https://ollama.com/install.sh | sh
else
  log "Ollama already installed: $(ollama --version 2>&1 | head -1)"
fi

# Configure Ollama env for our use case (single-model, keep loaded, q8 KV cache)
log "Configuring ollama systemd drop-in (persistent)"
sudo mkdir -p /etc/systemd/system/ollama.service.d
sudo tee /etc/systemd/system/ollama.service.d/override.conf >/dev/null <<'EOF'
[Service]
Environment="OLLAMA_KEEP_ALIVE=-1"
Environment="OLLAMA_NUM_PARALLEL=1"
Environment="OLLAMA_MAX_LOADED_MODELS=1"
Environment="OLLAMA_KV_CACHE_TYPE=q8_0"
Environment="OLLAMA_FLASH_ATTENTION=1"
Environment="OLLAMA_HOST=127.0.0.1:11434"
EOF
sudo systemctl daemon-reload
sudo systemctl enable --now ollama
sleep 3
sudo systemctl restart ollama
sleep 5

# Wait for daemon
for i in $(seq 1 20); do
  if curl -fsS "${OLLAMA_HOST_URL}/api/tags" >/dev/null 2>&1; then break; fi
  sleep 1
done
curl -fsS "${OLLAMA_HOST_URL}/api/tags" >/dev/null || die "Ollama daemon did not come up"

# ---------- 3. Download GGUF ----------
GGUF_PATH="${MODEL_DIR}/${MODEL_FILE}"
if [ ! -f "${GGUF_PATH}" ] || [ "$(stat -c%s "${GGUF_PATH}")" -lt 4000000000 ]; then
  log "Downloading GGUF from HuggingFace (${MODEL_REPO} / ${MODEL_FILE})"
  echo "  (this is the slow part — ~4.7GB)"
  curl -L --fail --progress-bar \
    -o "${GGUF_PATH}" \
    "https://huggingface.co/${MODEL_REPO}/resolve/main/${MODEL_FILE}?download=true"
else
  log "GGUF already present: ${GGUF_PATH} ($(du -h "${GGUF_PATH}" | cut -f1))"
fi

# ---------- 4. Import into Ollama ----------
MODELFILE="${WORKDIR}/Modelfile"
cat > "${MODELFILE}" <<EOF
FROM ${GGUF_PATH}

# Tuned for 4x Neoverse-N1, CPU-only
PARAMETER num_thread 4
PARAMETER num_ctx 4096
PARAMETER num_batch 128
PARAMETER temperature 0.8
PARAMETER top_p 0.9
PARAMETER repeat_penalty 1.1

# Llama-3 style chat template (domestic-yak is Llama-3.1 8B based)
TEMPLATE """{{ if .System }}<|start_header_id|>system<|end_header_id|>

{{ .System }}<|eot_id|>{{ end }}{{ range .Messages }}<|start_header_id|>{{ .Role }}<|end_header_id|>

{{ .Content }}<|eot_id|>{{ end }}<|start_header_id|>assistant<|end_header_id|>

"""

PARAMETER stop "<|eot_id|>"
PARAMETER stop "<|start_header_id|>"
PARAMETER stop "<|end_header_id|>"
EOF

if ! ollama list | grep -q "^${MODEL_TAG%:*}"; then
  log "Importing model into Ollama as ${MODEL_TAG}"
  ollama create "${MODEL_TAG}" -f "${MODELFILE}"
else
  log "Model ${MODEL_TAG} already imported (re-creating to pick up Modelfile changes)"
  ollama create "${MODEL_TAG}" -f "${MODELFILE}"
fi

# Warm up (load into RAM)
log "Warming up model (forcing load)"
curl -fsS "${OLLAMA_HOST_URL}/api/generate" \
  -d "{\"model\":\"${MODEL_TAG}\",\"prompt\":\"здраво\",\"stream\":false,\"options\":{\"num_predict\":8}}" \
  | jq -r '.response' >/dev/null

# ---------- 5. Benchmark TPS ----------
log "Benchmark: prefill + generation at 3 payload sizes"

bench() {
  local label="$1"
  local prompt="$2"
  local n_predict="$3"
  local result
  result=$(curl -fsS "${OLLAMA_HOST_URL}/api/generate" \
    -H 'Content-Type: application/json' \
    -d "$(jq -n --arg m "${MODEL_TAG}" --arg p "${prompt}" --argjson n "${n_predict}" \
       '{model:$m, prompt:$p, stream:false, options:{num_predict:$n, temperature:0.7}}')")
  local prompt_tokens eval_count prompt_dur eval_dur
  prompt_tokens=$(echo "${result}" | jq -r '.prompt_eval_count // 0')
  eval_count=$(echo "${result}"   | jq -r '.eval_count // 0')
  prompt_dur=$(echo "${result}"   | jq -r '.prompt_eval_duration // 1')   # ns
  eval_dur=$(echo "${result}"     | jq -r '.eval_duration // 1')          # ns
  local prefill_tps gen_tps
  prefill_tps=$(echo "scale=2; ${prompt_tokens} / (${prompt_dur} / 1000000000)" | bc -l)
  gen_tps=$(echo    "scale=2; ${eval_count}    / (${eval_dur}    / 1000000000)" | bc -l)
  echo "  [${label}] prompt_tokens=${prompt_tokens} prefill=${prefill_tps} t/s | gen_tokens=${eval_count} gen=${gen_tps} t/s"
  echo "${label}|${prompt_tokens}|${prefill_tps}|${eval_count}|${gen_tps}" >> "${WORKDIR}/bench.csv"
}

: > "${WORKDIR}/bench.csv"

SHORT="Напиши една реченица на македонски за демократијата."
MEDIUM="Ти си пратеник во Собранието на Република Македонија. Дискутираш за нов закон за работни односи. Твоето мислење е критично. Напиши краток говор од 3-4 реченици."
LONG_PROMPT=$(printf 'Ти си Димитар Апасиев. Твојата агресија е 0.85. Легализмот ти е 0.95. Користи го зборот "Устав" барем еднаш. Контекст на дебатата: Владата предлага зголемување на платите на функционерите за 40%%. Претходните говори: Мицкоски рече дека тоа е неприфатливо. Филипче одговори дека тоа е потребно. Сега ти го имаш зборот. Твојата животна мисија е борба против корупцијата. Говориш гласно, со правни аргументи. Напиши говор од 4-5 реченици.')

bench "short_8"    "${SHORT}"  8
bench "short_64"   "${SHORT}"  64
bench "medium_128" "${MEDIUM}" 128
bench "long_200"   "${LONG_PROMPT}" 200
bench "long_prefill_only" "${LONG_PROMPT}" 4

# ---------- 6. Persona quality probe (the real test) ----------
log "Persona probe: 5 Macedonian MP personas"

PERSONA_OUT="${WORKDIR}/persona_outputs.md"
: > "${PERSONA_OUT}"

probe() {
  local name="$1"
  local system="$2"
  local user="$3"
  echo "### ${name}" >> "${PERSONA_OUT}"
  echo "" >> "${PERSONA_OUT}"
  echo "**System:** ${system}" >> "${PERSONA_OUT}"
  echo "" >> "${PERSONA_OUT}"
  echo "**User:** ${user}" >> "${PERSONA_OUT}"
  echo "" >> "${PERSONA_OUT}"
  local resp
  resp=$(curl -fsS "${OLLAMA_HOST_URL}/api/chat" \
    -H 'Content-Type: application/json' \
    -d "$(jq -n --arg m "${MODEL_TAG}" --arg s "${system}" --arg u "${user}" \
       '{model:$m, stream:false, options:{num_predict:180, temperature:0.85},
         messages:[{role:"system",content:$s},{role:"user",content:$u}]}')")
  local content eval_count eval_dur tps
  content=$(echo    "${resp}" | jq -r '.message.content')
  eval_count=$(echo "${resp}" | jq -r '.eval_count // 0')
  eval_dur=$(echo   "${resp}" | jq -r '.eval_duration // 1')
  tps=$(echo "scale=2; ${eval_count} / (${eval_dur} / 1000000000)" | bc -l)
  echo "**Output (${eval_count} tokens @ ${tps} t/s):**" >> "${PERSONA_OUT}"
  echo "" >> "${PERSONA_OUT}"
  echo "${content}" >> "${PERSONA_OUT}"
  echo "" >> "${PERSONA_OUT}"
  echo "---" >> "${PERSONA_OUT}"
  echo "" >> "${PERSONA_OUT}"
  echo "  [${name}] ${tps} t/s, ${eval_count} tok"
}

probe "Apasiev — legalist, aggressive" \
  "Ти си Димитар Апасиев, пратеник од Левица. Аграсивен си (0.85), многу легалистичен (0.95), популизам 0.60. Секогаш цитираш Устав или закон. Говориш кратко и остро. Одговараш само на македонски." \
  "Владата предлага зголемување на платите на функционерите за 40%. Дај ти го своето мислење во 3-4 реченици."

probe "Mickoski — opposition leader, measured" \
  "Ти си Христијан Мицкоски, лидер на ВМРО-ДПМНЕ. Агресија 0.55, легализам 0.70, популизам 0.80. Говориш како државник, критикуваш владата, но со контролиран тон. Одговараш на македонски." \
  "Министерот за здравство објави нови правила за болниците. Коментирај."

probe "Filipche — defensive government official" \
  "Ти си Венко Филипче. Бранител си на владата. Агресија 0.40, легализам 0.60, популизам 0.50. Говориш со податоци и факти, не со емоции. Одговараш на македонски." \
  "Опозицијата те обвинува дека здравствениот систем е во колапс. Одбрани се во 3 реченици."

probe "Negative-sentiment reply (BeefMatrix trigger)" \
  "Ти си Димитар Апасиев. Агресија 0.90. Ги мразиш ВМРО-ДПМНЕ. Го нападнуваш лично говорникот." \
  "Мицкоски штотуку рече: 'Левица никогаш не разбрала што значи држава.' Одговори остро."

probe "Interjection at punctuation boundary" \
  "Ти си пратеник кој прекинува. Вика една реченица, максимум 12 зборови, во кирилица, со извичник." \
  "Прекини го говорникот кој рече: 'Оваа влада ги поправи сите проблеми во здравството.'"

# ---------- 7. Memory footprint ----------
log "Measuring RAM footprint with model loaded"
OLLAMA_PID=$(pgrep -f 'ollama serve' | head -1 || true)
RUNNER_PID=$(pgrep -f 'ollama runner' | head -1 || true)

rss_mb() {
  local pid="$1"
  [ -n "${pid}" ] && [ -d "/proc/${pid}" ] && awk '/VmRSS/ {printf "%.0f", $2/1024}' "/proc/${pid}/status" || echo "0"
}

OLLAMA_RSS=$(rss_mb "${OLLAMA_PID}")
RUNNER_RSS=$(rss_mb "${RUNNER_PID}")
TOTAL_RSS=$((OLLAMA_RSS + RUNNER_RSS))

# Free memory
FREE_MB=$(awk '/MemAvailable/ {printf "%.0f", $2/1024}' /proc/meminfo)

# Load averages
LOADAVG=$(cut -d' ' -f1-3 /proc/loadavg)

# ---------- 8. Build report ----------
log "Writing report to ${REPORT}"

{
  echo "# SOBRANIE_VOID Spike #1 — Report"
  echo ""
  echo "- **Date:** $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  echo "- **Host:** $(hostname)"
  echo "- **OS:** $(. /etc/os-release; echo "${PRETTY_NAME}")"
  echo "- **Kernel:** $(uname -r)"
  echo "- **Arch:** $(uname -m)"
  echo "- **Cores:** ${NPROC}"
  echo "- **RAM:** ${MEM_GB} GB"
  echo "- **Model:** ${MODEL_TAG} (${MODEL_FILE}, $(du -h "${GGUF_PATH}" | cut -f1))"
  echo "- **Ollama:** $(ollama --version 2>&1 | head -1)"
  echo ""
  echo "## 1. Throughput benchmark"
  echo ""
  echo "| Label | Prompt tokens | Prefill t/s | Gen tokens | Gen t/s |"
  echo "|---|---:|---:|---:|---:|"
  while IFS='|' read -r lbl pt pre gt gen; do
    echo "| ${lbl} | ${pt} | ${pre} | ${gt} | ${gen} |"
  done < "${WORKDIR}/bench.csv"
  echo ""
  echo "**Target acceptability:** gen >= 4 t/s on long_200 (which implies a ~35-45s speech cadence is feasible)."
  echo ""
  echo "## 2. Memory footprint (model loaded, post-inference)"
  echo ""
  echo "- ollama serve RSS: ${OLLAMA_RSS} MB"
  echo "- ollama runner RSS: ${RUNNER_RSS} MB"
  echo "- **Total: ${TOTAL_RSS} MB**"
  echo "- MemAvailable: ${FREE_MB} MB"
  echo "- loadavg (1/5/15m): ${LOADAVG}"
  echo ""
  echo "**Target:** total Ollama RSS < 7000 MB (leaves ~17GB for .NET, scraper, Piper, OS)."
  echo ""
  echo "## 3. Persona probe outputs"
  echo ""
  cat "${PERSONA_OUT}"
  echo ""
  echo "## 4. Manual quality checks (fill in after reading §3)"
  echo ""
  echo "For each probe, mark:"
  echo "- [ ] Output is in Cyrillic Macedonian (not Serbian, Bulgarian, English)"
  echo "- [ ] Persona traits are recognizable (aggression/legalism/populism show through)"
  echo "- [ ] Stays on topic"
  echo "- [ ] Length roughly matches request (no runaway generation)"
  echo "- [ ] No <|...|> template tokens leaking into output"
  echo ""
  echo "## 5. Go/no-go decision"
  echo ""
  echo "- [ ] gen t/s (long_200) >= 4.0  → cadence is viable"
  echo "- [ ] total RSS < 7GB            → memory is viable"
  echo "- [ ] >= 3/5 persona probes pass all 5 quality checks  → model is viable"
  echo ""
  echo "If ALL three boxes check: **GO** — proceed to build MVP per v1.1 spec."
  echo "If any fail: paste this report back and we adjust (smaller model, different quant, prompt engineering)."
} > "${REPORT}"

log "Done. Report: ${REPORT}"
echo ""
echo "=============================================="
cat "${REPORT}"
echo "=============================================="
