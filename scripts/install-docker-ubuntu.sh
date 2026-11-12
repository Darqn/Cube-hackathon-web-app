#!/usr/bin/env bash
# Docker's official apt repository: https://docs.docker.com/engine/install/ubuntu/
set -euo pipefail
[[ "$EUID" -eq 0 ]] || { printf 'Run with sudo.\n' >&2; exit 1; }
if command -v docker >/dev/null; then
  docker compose version >/dev/null || { printf 'Docker exists but Compose v2 is missing. Install its matching Compose plugin first.\n' >&2; exit 1; }
  systemctl enable --now docker
  printf 'Existing Docker and Compose preserved.\n'
  exit 0
fi
source /etc/os-release
[[ "$ID" == ubuntu ]] || { printf 'This installer supports Ubuntu.\n' >&2; exit 1; }
apt-get update
apt-get install -y ca-certificates curl git python3
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: ${UBUNTU_CODENAME:-$VERSION_CODENAME}
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
docker compose version
