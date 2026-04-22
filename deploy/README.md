# Deployment to Oracle A1 (Ubuntu 24.04 ARM64)

Target: `Canonical-Ubuntu-24.04-Minimal-aarch64`, 4 OCPU Neoverse-N1, 24 GB RAM.

## Prerequisites on the A1 instance

Ollama already installed and running as a systemd service (from the spike). Verify:

```bash
systemctl status ollama
curl -s http://127.0.0.1:11434/api/tags
```

Install .NET 10 runtime (ASP.NET Core, not SDK — we publish framework-dependent):

```bash
# Microsoft package feed
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt update
sudo apt install -y aspnetcore-runtime-10.0
dotnet --list-runtimes   # expect Microsoft.AspNetCore.App 10.x
```

## First deploy

```bash
sudo mkdir -p /opt/sobranie-void
sudo chown ubuntu:ubuntu /opt/sobranie-void
cd /opt/sobranie-void

git clone https://github.com/vancojordanovski/sobranie-void.git src
cd src

# Pull the model if not already present
ollama pull hf.co/mradermacher/domestic-yak-8B-instruct-GGUF:Q4_K_M
ollama cp hf.co/mradermacher/domestic-yak-8B-instruct-GGUF:Q4_K_M yak8b:q4km

# Publish framework-dependent for linux-arm64
dotnet publish src/Sobranie.Orchestrator/Sobranie.Orchestrator.csproj \
    -c Release \
    -r linux-arm64 \
    --no-self-contained \
    -o /opt/sobranie-void/publish

mkdir -p /opt/sobranie-void/data /opt/sobranie-void/logs

# Install systemd unit
sudo cp deploy/sobranie.service /etc/systemd/system/sobranie.service
sudo systemctl daemon-reload
sudo systemctl enable --now sobranie.service

# Verify
systemctl status sobranie
curl -s http://127.0.0.1:5000/
journalctl -u sobranie -f
```

## Subsequent deploys (git pull)

```bash
cd /opt/sobranie-void/src
git pull --ff-only

dotnet publish src/Sobranie.Orchestrator/Sobranie.Orchestrator.csproj \
    -c Release -r linux-arm64 --no-self-contained \
    -o /opt/sobranie-void/publish

sudo systemctl restart sobranie
```

## Exposing the web UI

The Kestrel listener binds to `127.0.0.1:5000` only. Reverse-proxy via Caddy or
nginx and terminate TLS externally. Do **not** expose Kestrel directly.

Oracle Cloud VCN: open port 443 ingress; keep 11434 and 5000 firewalled.

## Troubleshooting

- `systemctl status ollama` — confirm model is warm (`OLLAMA_KEEP_ALIVE=-1`).
- `journalctl -u sobranie -n 200` — app logs (Serilog also writes to `/opt/sobranie-void/logs`).
- First token latency > 5s: check `nvidia-smi`-equivalent `top` — if Ollama RSS < 4 GB, the model was evicted.
