$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
function Run($command, [string[]]$arguments) {
    & $command @arguments
    if ($LASTEXITCODE -ne 0) { throw "$command failed with exit code $LASTEXITCODE" }
}
if (-not (Test-Path .env)) { throw 'Run start-cube.bat once to configure local dependencies and .env.' }
foreach ($line in Get-Content .env -Encoding UTF8) {
    if ($line -match '^\s*(#|$)') { continue }
    if ($line -notmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Expected KEY=value in .env.' }
    $key = $matches[1]; $value = $matches[2].Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith("'") -and $value.EndsWith("'")) -or ($value.StartsWith('"') -and $value.EndsWith('"')))) { $value = $value.Substring(1, $value.Length - 2) }
    [Environment]::SetEnvironmentVariable($key, $value, 'Process')
}
if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development') { throw 'Demo seed requires ASPNETCORE_ENVIRONMENT=Development in .env.' }
if (-not $env:ConnectionStrings__Cube -or -not $env:POSTGRES_PASSWORD) { throw 'Missing database configuration in .env.' }
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection.set_ConnectionString($env:ConnectionStrings__Cube)
if ($connection['Host'] -notin @('localhost', '127.0.0.1', '::1')) { throw 'This helper only seeds the local Compose database.' }
$running = @(& docker.exe compose ps --status running --quiet db)
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose is unavailable. Start Docker Desktop first.' }
if ($running.Count) {
    $binding = & docker.exe compose port db 5432
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the running database port.' }
    $env:CUBE_POSTGRES_PORT = ($binding -split ':')[-1]
} elseif (-not $env:CUBE_POSTGRES_PORT) {
    $env:CUBE_POSTGRES_PORT = '5432'
    if ($connection.ContainsKey('Port')) { $env:CUBE_POSTGRES_PORT = [string]$connection['Port'] }
}
$connection['Port'] = $env:CUBE_POSTGRES_PORT
$env:ConnectionStrings__Cube = $connection.get_ConnectionString()
$localDotnet = Join-Path $root '.tools/dotnet-win'
if (Test-Path "$localDotnet/dotnet.exe") { $env:DOTNET_ROOT = $localDotnet; $env:Path = "$localDotnet;$env:Path" }
$credentialsPath = Join-Path $root '.data/demo-accounts.json'
New-Item -ItemType Directory -Force (Split-Path $credentialsPath) | Out-Null
if (-not (Test-Path $credentialsPath)) {
    $bytes = New-Object byte[] 18
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $credentials = @{ password = 'Demo!' + [Convert]::ToBase64String($bytes) + '9a'; logins = @('demo-admin','demo-staff','demo-tenant','demo-tenant-2','demo-pending') }
    [IO.File]::WriteAllText($credentialsPath, ($credentials | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
}
$env:Demo__Password = (Get-Content $credentialsPath -Raw | ConvertFrom-Json).password
try {
    Run 'docker.exe' @('compose','up','-d','--wait','--pull','never','db')
    Run 'dotnet.exe' @('build','src/Cube.Api','--configuration','Release','--no-restore')
    Run 'dotnet.exe' @('run','--project','src/Cube.Api','--configuration','Release','--no-build','--no-launch-profile','--','--migrate')
    Run 'dotnet.exe' @('run','--project','src/Cube.Api','--configuration','Release','--no-build','--no-launch-profile','--','--seed-demo')
    Write-Host "Demo account credentials: $credentialsPath"
    Write-Host 'Database is running. Start the application with start-cube.bat.'
} finally { Remove-Item Env:Demo__Password -ErrorAction SilentlyContinue }
