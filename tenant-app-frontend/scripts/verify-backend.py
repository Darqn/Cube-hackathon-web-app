"""Run frontend browser tests with a disposable database; never touches cube data."""
import os, pathlib, secrets, shlex, subprocess, time, urllib.request, tempfile, json
root = pathlib.Path(__file__).resolve().parents[2]
env = os.environ.copy()
for line in (root / '.env').read_text().splitlines():
    if not line.strip() or line.lstrip().startswith('#') or '=' not in line: continue
    k, v = line.split('=', 1); values = shlex.split(v); env[k] = values[0] if values else ''
env.update(DOTNET_ROOT='/workspace/.tools/dotnet', DOTNET_CLI_HOME='/workspace/.tools/dotnet-home', NUGET_PACKAGES='/workspace/.tools/nuget')
env['PATH'] = env['DOTNET_ROOT'] + ':' + env['PATH']
db = 'cube_frontend_' + secrets.token_hex(6)
password = secrets.token_urlsafe(24) + 'Aa1!'
env.update(Bootstrap__Login='frontend-admin', Bootstrap__Email='frontend-admin@example.test', Bootstrap__Password=password, ASPNETCORE_URLS='http://127.0.0.1:5081', Cors__Origins__0='http://127.0.0.1:8082', Notifications__WorkerEnabled='false')
env['ConnectionStrings__Cube'] = env['ConnectionStrings__Cube'].replace('Database=cube;', 'Database=' + db + ';')
env.update(CUBE_E2E_ADMIN=password, CUBE_E2E_PASSWORD=secrets.token_urlsafe(24)+'Aa1!', CUBE_E2E_API='http://127.0.0.1:5081/api/v1', EXPO_PUBLIC_API_URL='http://127.0.0.1:5081/api/v1', EXPO_OFFLINE='1')
def sql(query): subprocess.run(['docker','compose','exec','-T','db','psql','-U','cube','-d','postgres','-c',query],cwd=root,check=True,stdout=subprocess.DEVNULL)
def ready(url):
    for _ in range(60):
        try:
            with urllib.request.urlopen(url,timeout=2): return
        except Exception: time.sleep(.5)
    raise RuntimeError('Service did not become ready: '+url)
api = web = None
with tempfile.TemporaryDirectory(prefix='cube-frontend-') as tmp:
    env['Files__Root'] = tmp+'/files'
    sql('CREATE DATABASE '+db)
    try:
        with open(tmp+'/backend.log','w') as log:
            command=['dotnet','run','--project','src/Cube.Api','--configuration','Release','--no-build']
            for action in ['--migrate','--bootstrap-admin']:
                subprocess.run(command+['--',action],cwd=root,env=env,check=True,stdout=log,stderr=log)
            api=subprocess.Popen(command,cwd=root,env=env,stdout=log,stderr=log)
            ready('http://127.0.0.1:5081/health/ready')
            with open(tmp+'/web.log','w') as web_log:
                subprocess.run(['npx','expo','export','--platform','web','--clear','--output-dir',tmp+'/web'],cwd=root/'tenant-app-frontend',env=env,stdout=web_log,stderr=web_log,check=True)
                web=subprocess.Popen(['python3','scripts/serve-web.py',tmp+'/web','8082'],cwd=root/'tenant-app-frontend',env=env,stdout=web_log,stderr=web_log,start_new_session=True)
                ready('http://127.0.0.1:8082')
                suites = sorted((root/'tenant-app-frontend/tests/browser').glob('*.spec.ts'))
                for index, suite in enumerate(suites):
                    # Each suite uses production rate limits. Restart the API to isolate counters;
                    # database authorization and persistence remain real across requests.
                    if index:
                        api.terminate(); api.wait()
                        api=subprocess.Popen(command,cwd=root,env=env,stdout=log,stderr=log)
                        ready('http://127.0.0.1:5081/health/ready')
                    subprocess.run(['npx','playwright','test',suite.name,'--output',str(pathlib.Path('test-results')/suite.stem)],cwd=root/'tenant-app-frontend',env=env,check=True)
                print('All browser suites passed.', flush=True)
    finally:
        if web:
            import signal
            os.killpg(web.pid, signal.SIGTERM); web.wait()
        if api: api.terminate(); api.wait()
        sql('DROP DATABASE '+db+' WITH (FORCE)')
