#!/usr/bin/env bash
set -euo pipefail
CUBE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CUBE_ENV="$CUBE_ROOT/deploy/vps/.env"
compose() { docker compose --env-file "$CUBE_ENV" -f "$CUBE_ROOT/deploy/vps/compose.yaml" "$@"; }

case "${1:-help}" in
  init)
    python3 - "$CUBE_ENV" "${2:-}" <<'PY'
import ipaddress, os, pathlib, re, secrets, sys
target, hostname = pathlib.Path(sys.argv[1]), sys.argv[2].lower()
if not re.fullmatch(r"(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?", hostname):
    raise SystemExit("Supply a DNS name, for example: api.203.0.113.10.sslip.io (use your own public IP).")
try:
    ipaddress.ip_address(hostname)
except ValueError:
    pass
else:
    raise SystemExit("Use a DNS name, not a bare IP: api.YOUR_PUBLIC_IP.sslip.io")
os.umask(0o077)
try:
    with target.open('x') as file:
        file.write(f"CUBE_API_HOST={hostname}\n")
        file.write(f"CUBE_OWNER_PASSWORD={secrets.token_hex(32)}\n")
        file.write(f"CUBE_APP_PASSWORD={secrets.token_hex(32)}\n")
        file.write("CUBE_WEB_ORIGIN=http://localhost:8081\n")
except FileExistsError:
    raise SystemExit("Existing deploy/vps/.env preserved. Edit it deliberately; do not regenerate database passwords.")
print(f"Created private settings. API address: https://{hostname}/api/v1")
PY
    ;;
  start)
    compose build api
    compose up -d --wait db
    # Only the maintenance container receives the database owner credential.
    (
      set -a
      source "$CUBE_ENV"
      set +a
      export ConnectionStrings__Cube="Host=db;Port=5432;Database=cube;Username=cube_owner;Password=$CUBE_OWNER_PASSWORD"
      compose run --rm --no-deps -e ConnectionStrings__Cube api --migrate
    )
    compose up -d --wait api gateway
    printf 'Containers are running. Check HTTPS readiness using the API hostname in deploy/vps/.env.\n'
    ;;
  bootstrap)
    read -r -p 'Administrator login: ' Bootstrap__Login
    read -r -p 'Administrator email: ' Bootstrap__Email
    read -r -s -p 'Administrator password (12+ characters, upper/lowercase, digit, symbol): ' Bootstrap__Password
    printf '\n'
    read -r -s -p 'Repeat password: ' CUBE_PASSWORD_REPEAT
    printf '\n'
    [[ "$Bootstrap__Password" == "$CUBE_PASSWORD_REPEAT" ]] || { printf 'Passwords differ.\n' >&2; exit 1; }
    export Bootstrap__Login Bootstrap__Email Bootstrap__Password
    trap 'unset Bootstrap__Login Bootstrap__Email Bootstrap__Password CUBE_PASSWORD_REPEAT' EXIT
    compose run --rm --no-deps -e Bootstrap__Login -e Bootstrap__Email -e Bootstrap__Password api --bootstrap-admin
    ;;
  status) compose ps ;;
  logs) compose logs --tail=100 --follow api gateway ;;
  stop) compose stop ;;
  *) printf 'Usage: sudo bash scripts/vps.sh init HOSTNAME | start | bootstrap | status | logs | stop\n' ;;
esac
