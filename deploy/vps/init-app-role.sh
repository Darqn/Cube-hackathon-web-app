#!/usr/bin/env bash
set -euo pipefail
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=ON_ERROR_STOP=1 --set=app_password="$CUBE_APP_PASSWORD" <<'SQL'
CREATE ROLE cube_app LOGIN PASSWORD :'app_password';
GRANT CONNECT ON DATABASE cube TO cube_app;
GRANT USAGE ON SCHEMA public TO cube_app;
ALTER DEFAULT PRIVILEGES FOR ROLE cube_owner IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO cube_app;
ALTER DEFAULT PRIVILEGES FOR ROLE cube_owner IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO cube_app;
SQL
