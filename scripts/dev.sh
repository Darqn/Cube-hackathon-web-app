#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -d /workspace/.tools/dotnet ]]; then
  export DOTNET_ROOT=/workspace/.tools/dotnet
  export PATH="$DOTNET_ROOT:$PATH"
fi
python3 scripts/configure-dev.py
set -a
source .env
set +a
case "${1:-run}" in
  setup)
    docker compose up -d --wait
    dotnet restore Cube.sln --locked-mode
    dotnet build Cube.sln --no-restore --configuration Release
    dotnet run --project src/Cube.Api --configuration Release --no-build -- --migrate
    ;;
  run) exec dotnet run --project src/Cube.Api --configuration Release --no-build ;;
  test) export CUBE_TEST_DATABASE="$ConnectionStrings__Cube"; exec dotnet test Cube.sln --configuration Release --no-restore --logger 'trx;LogFileName=backend.trx' ;;
  bootstrap) exec dotnet run --project src/Cube.Api --configuration Release --no-build -- --bootstrap-admin ;;
  *) echo 'Usage: scripts/dev.sh setup|run|test|bootstrap' >&2; exit 2 ;;
esac
