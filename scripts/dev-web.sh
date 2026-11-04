#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export Cors__Origins__0="${Cors__Origins__0:-http://localhost:8081}"
export Cors__Origins__1="${Cors__Origins__1:-http://127.0.0.1:8081}"
exec bash scripts/dev.sh run
