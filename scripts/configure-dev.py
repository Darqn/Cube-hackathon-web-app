#!/usr/bin/env python3
"""Create local-only credentials once. Preserve any existing developer file."""
import pathlib
import secrets
import os
root = pathlib.Path(__file__).resolve().parent.parent
os.umask(0o077)
password = secrets.token_hex(24)
try:
    with (root / '.env').open('x') as f:
        f.write(f"POSTGRES_PASSWORD='{password}'\n")
        f.write(f"ConnectionStrings__Cube='Host=127.0.0.1;Port=5432;Database=cube;Username=cube;Password={password}'\n")
        f.write("ASPNETCORE_ENVIRONMENT='Development'\n")
        f.write("ASPNETCORE_URLS='http://127.0.0.1:5080'\n")
        f.write("Files__AllowUnscannedDevelopmentUploads='true'\n")
        f.write("Notifications__WorkerEnabled='true'\n")
        f.write(f"Files__Root='{root}/.data/files'\n")
    print('Created ignored .env with local credentials; no values printed.')
except FileExistsError:
    print('Preserved existing .env.')
