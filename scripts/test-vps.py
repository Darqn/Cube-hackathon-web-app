"""Real container/TLS/OCR smoke test with isolated, temporary Docker resources."""
import http.client
import json
import os
import pathlib
import secrets
import ssl
import subprocess
import tempfile
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    project = 'cube-vps-test-' + secrets.token_hex(5)
    with tempfile.TemporaryDirectory(prefix=project) as directory:
        temporary = pathlib.Path(directory)
        owner_password = secrets.token_hex(32)
        app_password = secrets.token_hex(32)
        admin_password = secrets.token_urlsafe(24) + 'Aa1!'
        settings = temporary / '.env'
        settings.write_text(f'CUBE_API_HOST=localhost\nCUBE_OWNER_PASSWORD={owner_password}\nCUBE_APP_PASSWORD={app_password}\n')
        settings.chmod(0o600)
        env = os.environ.copy()
        env.update(CUBE_API_HOST='localhost', CUBE_BIND_IP='127.0.0.1', CUBE_HTTP_PORT='0', CUBE_HTTPS_PORT='0')
        compose = ['docker', 'compose', '-p', project, '--env-file', str(settings), '-f', str(ROOT / 'deploy/vps/compose.yaml')]

        def run(*args, extra=None, capture=False):
            return subprocess.run(compose + list(args), cwd=ROOT, env=env | (extra or {}), check=True,
                                  text=True, stdout=subprocess.PIPE if capture else None).stdout

        try:
            run('config', '--quiet')
            run('up', '-d', '--wait', 'db')
            owner_connection = f'Host=db;Port=5432;Database=cube;Username=cube_owner;Password={owner_password}'
            run('run', '--rm', '--no-deps', '-e', 'ConnectionStrings__Cube', 'api', '--migrate', extra={'ConnectionStrings__Cube': owner_connection})
            run('run', '--rm', '--no-deps', '-e', 'Bootstrap__Login', '-e', 'Bootstrap__Email', '-e', 'Bootstrap__Password', 'api', '--bootstrap-admin',
                extra={'Bootstrap__Login': 'vps-test-admin', 'Bootstrap__Email': 'vps-admin@example.test', 'Bootstrap__Password': admin_password})
            run('up', '-d', '--wait', 'api', 'gateway')
            port = int(run('port', 'gateway', '443', capture=True).strip().rsplit(':', 1)[1])
            http_port = int(run('port', 'gateway', '80', capture=True).strip().rsplit(':', 1)[1])
            certificate = temporary / 'caddy-root.crt'
            run('cp', 'gateway:/data/caddy/pki/authorities/local/root.crt', str(certificate))
            tls = ssl.create_default_context(cafile=str(certificate))
            base = f'https://localhost:{port}'
            token = None

            def call(path, data=None, body=None, content_type=None, expected=200):
                headers = {'Accept': 'application/json'}
                if token:
                    headers['Authorization'] = 'Bearer ' + token
                if data is not None:
                    body = json.dumps(data).encode()
                    content_type = 'application/json'
                if content_type:
                    headers['Content-Type'] = content_type
                request = urllib.request.Request(base + path, data=body, headers=headers)
                try:
                    response = urllib.request.urlopen(request, context=tls, timeout=30)
                except urllib.error.HTTPError as error:
                    response = error
                with response:
                    payload = response.read()
                    assert response.status == expected, f'{path}: status {response.status}, expected {expected}'
                    return json.loads(payload) if payload else None

            assert call('/health/ready')['status'] == 'Ready'
            call('/api/v1/auth/me', expected=401)
            call('/openapi/v1.json', expected=404)
            connection = http.client.HTTPConnection('127.0.0.1', http_port, timeout=10)
            connection.request('GET', '/health/live', headers={'Host': 'localhost'})
            response = connection.getresponse()
            assert response.status in (301, 302, 307, 308)
            assert response.getheader('Location').startswith('https://localhost')
            connection.close()
            token = call('/api/v1/auth/login', data={'loginName': 'vps-test-admin', 'password': admin_password})['accessToken']

            def multipart(path, expected):
                boundary = 'cube-' + secrets.token_hex(12)
                image = (ROOT / 'meter-example.png').read_bytes()
                body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="meter.png"\r\nContent-Type: image/png\r\n\r\n'.encode()
                        + image + f'\r\n--{boundary}--\r\n'.encode())
                return call(path, body=body, content_type='multipart/form-data; boundary=' + boundary, expected=expected)

            assert multipart('/api/v1/meters/recognize', 200) == {'value': '12480', 'message': None}
            tenant = call('/api/v1/admin/tenants', data={'name': 'VPS Test', 'inn': '7707083893', 'contactName': 'Test', 'email': 'tenant@example.test', 'phone': '1'}, expected=201)
            upload = multipart('/api/v1/files?tenantId=' + tenant['id'], 201)
            assert upload['state'] == 'Quarantined'
            call('/api/v1/files/' + upload['id'], expected=404)
            flags = run('exec', '-T', 'db', 'psql', '-U', 'cube_owner', '-d', 'cube', '-At', '-c',
                        "SELECT rolsuper, rolcreatedb, rolcreaterole FROM pg_roles WHERE rolname='cube_app'", capture=True).strip()
            assert flags == 'f|f|f'
            key = run('exec', '-T', 'db', 'psql', '-U', 'cube_owner', '-d', 'cube', '-At', '-c',
                      'SELECT "Key" FROM "Files" LIMIT 1', capture=True).strip()
            assert len(key) == 32 and all(c in '0123456789abcdef' for c in key)
            run('exec', '-T', 'api', 'test', '-s', '/var/lib/cube/files/' + key)
            api_container = run('ps', '-q', 'api', capture=True).strip()
            uid = run('exec', '-T', 'api', 'id', '-u', capture=True).strip()
            assert uid != '0'
            ports = json.loads(subprocess.check_output(['docker', 'inspect', '--format', '{{json .HostConfig.PortBindings}}', api_container], env=env, text=True))
            assert not ports, 'API must not publish a host port'
            run('up', '-d', '--force-recreate', '--wait', 'api', 'gateway')
            port = int(run('port', 'gateway', '443', capture=True).strip().rsplit(':', 1)[1])
            base = f'https://localhost:{port}'
            assert call('/health/ready')['status'] == 'Ready'
            assert call('/api/v1/auth/me')['userName'] == 'vps-test-admin'
            run('exec', '-T', 'api', 'test', '-s', '/var/lib/cube/files/' + key)
            assert multipart('/api/v1/meters/recognize', 200)['value'] == '12480'
            print('PASS: real TLS, HTTP redirect, production routing/auth, PostgreSQL migrations and limited app role, '
                  'native OCR, quarantine, non-root API, private API port, and session/file persistence after recreation.', flush=True)
        finally:
            run('down', '--volumes', '--remove-orphans')  # Only this unique, temporary test project.


if __name__ == '__main__':
    main()
