@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
set "CUBE_LAUNCHER=%~f0"
set "CUBE_PROJECT=%~dp0"
if not "%~1"=="" set "CUBE_PROJECT=%~f1"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$s=[IO.File]::ReadAllText($env:CUBE_LAUNCHER); $m='# POWERSHELL'+' PAYLOAD'; & ([ScriptBlock]::Create($s.Substring($s.IndexOf($m)+$m.Length)))"
set "CUBE_EXIT=%errorlevel%"
if not "%CUBE_EXIT%"=="0" pause
exit /b %CUBE_EXIT%
# POWERSHELL PAYLOAD
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$workers = New-Object 'System.Collections.Generic.List[System.Diagnostics.Process]'
$databaseStarted = $false
function Check($name) { Write-Host "[CHECK] $name" -ForegroundColor Cyan }
function StopProject {
    Write-Host 'Stopping project...'
    foreach ($worker in $workers) {
        # The retained PowerShell parent owns this server's entire process tree.
        if (-not $worker.HasExited) {
            Probe 'taskkill.exe' @('/PID', [string]$worker.Id, '/T', '/F')
            if ($probeExit -ne 0 -and -not $worker.HasExited) {
                Write-Warning "Could not stop server process $($worker.Id). Stop it with Task Manager."
            }
        }
        $worker.Dispose()
    }
    $workers.Clear()
    if ($databaseStarted) {
        Probe 'docker.exe' @('compose','stop','db')
        if ($probeExit -ne 0) { Write-Warning 'Could not stop PostgreSQL. Run docker compose stop db from the project directory.' }
    }
}
# Windows PowerShell 5 reports native stderr as ErrorRecord, even for expected misses.
function Probe($command, [string[]]$arguments) {
    $ErrorActionPreference = 'Continue'
    & $command @arguments 2>&1 | Out-Null
    $script:probeExit = $LASTEXITCODE
}
[Console]::OutputEncoding = New-Object Text.UTF8Encoding
function Confirm($message) {
    $answer = Read-Host "$message [y/N]"
    if ($answer -notmatch '^(y|yes|д|да)$') { throw 'Операция отменена пользователем. Ничего не скачано на этом шаге.' }
}
function Run($command, [string[]]$arguments) {
    & $command @arguments
    if ($LASTEXITCODE -ne 0) { throw "Ошибка команды $command (код $LASTEXITCODE)." }
}
function DownloadFile($url, $destination) {
    # Stream to disk with a percentage display; retain normal TLS verification.
    $response = $null; $inputStream = $null; $outputStream = $null
    $complete = $false
    try {
        $request = [Net.HttpWebRequest]::Create($url)
        $request.Timeout = 30000
        $request.ReadWriteTimeout = 60000
        $response = $request.GetResponse()
        $total = $response.ContentLength
        $inputStream = $response.GetResponseStream()
        $outputStream = [IO.File]::Create($destination)
        $buffer = New-Object byte[] 131072
        [long]$received = 0
        $lastPercent = -1
        if ($total -le 0) { Write-Host 'Downloading SDK (server did not provide total size)...' }
        while (($count = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $outputStream.Write($buffer, 0, $count)
            $received += $count
            if ($total -gt 0) {
                $percent = [int][Math]::Min(99, [Math]::Floor(100.0 * $received / $total))
                if ($percent -ne $lastPercent) {
                    Write-Host -NoNewline ("`rDownloading SDK: {0,3}%" -f $percent)
                    $lastPercent = $percent
                }
            }
        }
        if ($total -ge 0 -and $received -ne $total) { throw 'SDK download was incomplete.' }
        $complete = $true
        Write-Host "`rDownloading SDK: 100%"
    } finally {
        if ($outputStream) { $outputStream.Dispose() }
        if ($inputStream) { $inputStream.Dispose() }
        if ($response) { $response.Close() }
        if (-not $complete) {
            Write-Host ''
            if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination }
        }
    }
}
function RefreshPath {
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User') + ';' + $env:Path
}
function InstallWinget($id, $label) {
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) { throw "Для установки $label установите App Installer (winget) из Microsoft Store и повторите запуск." }
    Confirm "Скачать и установить $label через winget? Установщик может запросить права администратора"
    Run 'winget.exe' @('install','--id',$id,'--exact','--source','winget','--accept-source-agreements','--accept-package-agreements')
    RefreshPath
}
function WaitHttp($url) {
    for ($i=0; $i -lt 120; $i++) {
        try { $r=Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 3; if ($r.StatusCode -eq 200) { return } } catch {}
        Start-Sleep -Seconds 1
    }
    throw "Сервис не готов: $url. Логи сервера: $state"
}
function FreePort($port) {
    if (-not (TestPort $port)) { throw "Порт $port уже занят или недоступен. Остановите использующий его процесс и повторите запуск." }
}
function TestPort([int]$port) {
    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, $port)
    try {
        $listener.Server.ExclusiveAddressUse = $true
        $listener.Start()
        return $true
    } catch [Net.Sockets.SocketException] {
        return $false
    } finally { $listener.Stop() }
}
function ConfigureDatabasePort([string]$runningPort) {
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    # Use accessors: PowerShell treats this builder as a dictionary.
    $connection.set_ConnectionString($env:ConnectionStrings__Cube)
    $localConnection = $connection['Host'] -in @('127.0.0.1','localhost','::1')
    $port = 5432
    if ($localConnection -and $connection.ContainsKey('Port')) { $port = [int]$connection['Port'] }
    if ($env:CUBE_POSTGRES_PORT) { $port = [int]$env:CUBE_POSTGRES_PORT }
    if ($port -lt 1 -or $port -gt 65535) { throw 'CUBE_POSTGRES_PORT must be between 1 and 65535.' }
    if ($runningPort) {
        $port = [int]$runningPort
    } elseif (-not (TestPort $port)) {
        if ($env:CUBE_POSTGRES_PORT) { throw "Configured PostgreSQL port $port is unavailable." }
        $unavailablePort = $port
        $port = 5433
        while ($port -le 5532 -and -not (TestPort $port)) { $port++ }
        if ($port -gt 5532) { throw 'No available local PostgreSQL port in the range 5432-5532.' }
        Write-Host "Port $unavailablePort is unavailable; using PostgreSQL port $port for this run."
    }
    # Change only the local Compose connection; preserve credentials and the .env file.
    if ($localConnection) {
        $connection['Port'] = [string]$port
        $env:ConnectionStrings__Cube = $connection.get_ConnectionString()
    }
    $env:CUBE_POSTGRES_PORT = [string]$port
}
function HashFiles($paths) { return (($paths | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash }) -join '-') }
function OpenWorker($name, $body) {
    $file = Join-Path $state "$name.ps1"
    $prefix = '$ErrorActionPreference="Stop"; Set-Location -LiteralPath ' + "'" + $root.Replace("'","''") + "';`r`n"
    [IO.File]::WriteAllText($file, $prefix + $body, (New-Object Text.UTF8Encoding($true)))
    $worker = Start-Process powershell.exe -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $state "$name.log") -RedirectStandardError (Join-Path $state "$name.error.log") -ArgumentList @('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',('"'+$file+'"'))
    $workers.Add($worker)
}
try {
    $root = (Resolve-Path -LiteralPath $env:CUBE_PROJECT).Path
    Set-Location -LiteralPath $root
    foreach ($file in @('global.json','Cube.sln','compose.yaml','tenant-app-frontend/package-lock.json')) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Не найден $file. Положите BAT в корень проекта либо передайте путь: start-cube.bat C:\Projects\Cube-hackathon-web-app" }
    }
    Write-Host "Проект: $root"
    if (Get-Command git.exe -ErrorAction SilentlyContinue) {
        $branch = & git branch --show-current
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Текущая ветка: $branch"
        }
    }
    $state=Join-Path $root '.data/windows-launcher'
    New-Item -ItemType Directory -Force -Path $state | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Check 'Node.js (version 20 or newer)'
    if (-not (Get-Command node.exe -ErrorAction SilentlyContinue)) { InstallWinget 'OpenJS.NodeJS.LTS' 'Node.js LTS и npm' }
    $nodeVersion = & node.exe -p 'process.versions.node'
    if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion.Split('.')[0]) -lt 20) {
        InstallWinget 'OpenJS.NodeJS.LTS' 'Node.js LTS'
        $nodeVersion = & node.exe -p 'process.versions.node'
    }
    if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion.Split('.')[0]) -lt 20) { throw 'Требуется Node.js >=20; рекомендуется проверенная версия 24.22.1. Обновите Node.js и перезапустите BAT.' }
    Check 'npm'
    if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { InstallWinget 'OpenJS.NodeJS.LTS' 'Node.js LTS (includes npm)' }
    if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'npm не найден. Восстановите установку Node.js.' }
    Run 'npm.cmd' @('--version')
    Write-Host "Node.js $nodeVersion"
    Check 'Docker Desktop and Linux container engine'
    if (-not (Get-Command docker.exe -ErrorAction SilentlyContinue)) { InstallWinget 'Docker.DockerDesktop' 'Docker Desktop' }
    if (-not (Get-Command docker.exe -ErrorAction SilentlyContinue)) { throw 'Docker установлен. Перезапустите Windows, настройте WSL2/Linux containers в Docker Desktop и повторите запуск.' }
    Probe 'docker.exe' @('info','--format','{{.OSType}}')
    if ($probeExit -ne 0) {
        $desktop=Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'
        if (Test-Path $desktop) { Start-Process -FilePath $desktop -WindowStyle Hidden | Out-Null }
        Write-Host 'Ожидание Docker Desktop (до 120 секунд)...'
        for ($i=0; $i -lt 60; $i++) {
            Probe 'docker.exe' @('info','--format','{{.OSType}}')
            if ($probeExit -eq 0) { break }
            Start-Sleep -Seconds 2
        }
    }
    $ErrorActionPreference = 'Continue'
    $osType = & docker.exe info --format '{{.OSType}}' 2>$null
    $dockerExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($dockerExit -ne 0 -or $osType -ne 'linux') { throw 'Docker не готов. Включите виртуализацию/WSL2, запустите Docker Desktop в режиме Linux containers. После первой установки может требоваться перезагрузка.' }
    Check 'Docker Compose v2'
    Run 'docker.exe' @('compose','version')
    Check '.NET SDK version pinned in global.json'
    $sdk=(Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    $localDotnet=Join-Path $root '.tools/dotnet-win'
    if (Test-Path "$localDotnet/dotnet.exe") { $env:DOTNET_ROOT=$localDotnet; $env:Path="$localDotnet;$env:Path" }
    $hasSdk=$false
    if (Get-Command dotnet.exe -ErrorAction SilentlyContinue) { $hasSdk = [bool]((& dotnet.exe --list-sdks) | Where-Object { $_ -match ('^'+[regex]::Escape($sdk)+'\s') }) }
    if (-not $hasSdk) {
        Confirm "Скачать .NET SDK $sdk с Microsoft и установить локально в .tools/dotnet-win?"
        $arch=[Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
        if ($arch -notin @('x64','arm64')) { throw "Неподдерживаемая архитектура: $arch" }
        $channel=($sdk.Split('.')[0..1] -join '.')
        $metadata=Invoke-RestMethod "https://builds.dotnet.microsoft.com/dotnet/release-metadata/$channel/releases.json"
        $files=@($metadata.releases | ForEach-Object { $_.sdks } | Where-Object version -eq $sdk | ForEach-Object { $_.files } | Where-Object { $_.rid -eq "win-$arch" -and $_.name.EndsWith('.zip') })
        if ($files.Count -ne 1) { throw 'Не найден однозначный официальный архив SDK.' }
        $zip=Join-Path $state 'dotnet-sdk.zip'
        DownloadFile $files[0].url $zip
        if ((Get-FileHash $zip -Algorithm SHA512).Hash -ne $files[0].hash) { Remove-Item $zip; throw 'SHA512 архива SDK не совпадает.' }
        New-Item -ItemType Directory -Force $localDotnet | Out-Null
        Expand-Archive -LiteralPath $zip -DestinationPath $localDotnet -Force
        Remove-Item $zip
        $env:DOTNET_ROOT=$localDotnet; $env:Path="$localDotnet;$env:Path"
    }
    Run 'dotnet.exe' @('--version')
    Check 'Local database and API configuration (.env)'
    # Preserve existing shell-compatible .env; never execute its contents.
    if (-not (Test-Path .env)) {
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create(); $bytes=New-Object byte[] 24; $rng.GetBytes($bytes); $rng.Dispose()
        $password=([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant()
        $lines=@("POSTGRES_PASSWORD='$password'", "ConnectionStrings__Cube='Host=127.0.0.1;Port=5432;Database=cube;Username=cube;Password=$password'", "ASPNETCORE_ENVIRONMENT='Development'", "ASPNETCORE_URLS='http://127.0.0.1:5080'", "Files__AllowUnscannedDevelopmentUploads='true'", "Notifications__WorkerEnabled='true'", "Files__Root='.data/files'")
        [IO.File]::WriteAllLines((Join-Path $root '.env'),$lines,(New-Object Text.UTF8Encoding($false)))
        Write-Host 'Создан локальный .env со случайным паролем. Значения не выводятся.'
    }
    foreach ($line in Get-Content .env -Encoding UTF8) {
        if ($line -match '^\s*(#|$)') { continue }
        if ($line -notmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Неподдерживаемый формат .env: ожидается KEY=value.' }
        $key=$matches[1]; $value=$matches[2].Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith("'") -and $value.EndsWith("'")) -or ($value.StartsWith('"') -and $value.EndsWith('"')))) { $value=$value.Substring(1,$value.Length-2) }
        [Environment]::SetEnvironmentVariable($key,$value,'Process')
    }
    if (-not $env:POSTGRES_PASSWORD -or -not $env:ConnectionStrings__Cube) { throw 'В .env отсутствует POSTGRES_PASSWORD или ConnectionStrings__Cube.' }
    $env:ASPNETCORE_ENVIRONMENT='Development'; $env:ASPNETCORE_URLS='http://127.0.0.1:5080'
    $env:Cors__Origins__0='http://localhost:8081'; $env:Cors__Origins__1='http://127.0.0.1:8081'
    $env:EXPO_PUBLIC_API_URL='http://127.0.0.1:5080/api/v1'; $env:EXPO_PUBLIC_USE_MOCK='false'; $env:EXPO_OFFLINE='1'
    FreePort 5080; FreePort 8081
    $runningDatabase = @(& docker.exe compose ps --status running --quiet db)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Compose database.' }
    $runningPort = $null
    if ($runningDatabase.Count -gt 0) {
        $binding = & docker.exe compose port db 5432
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the running PostgreSQL port.' }
        $runningPort = ($binding -split ':')[-1]
    }
    ConfigureDatabasePort $runningPort
    Check 'Required Docker images'
    $images=@(& docker.exe compose config --images)
    if ($LASTEXITCODE -ne 0) { throw 'Ошибка compose.yaml/.env.' }
    foreach ($image in $images) {
        Probe 'docker.exe' @('image','inspect',$image)
        if ($probeExit -ne 0) { Confirm "Скачать Docker-образ $image?"; Run 'docker.exe' @('pull',$image) }
    }
    Check 'Each NuGet package in the restored dependency graph'
    $backendHash=HashFiles @('global.json','src/Cube.Api/Cube.Api.csproj','src/Cube.Api/packages.lock.json','tests/Cube.Tests/Cube.Tests.csproj','tests/Cube.Tests/packages.lock.json')
    $backendStamp=Join-Path $state 'backend.sha256'
    $needRestore= -not (Test-Path $backendStamp)
    if (-not $needRestore) { $needRestore=((Get-Content $backendStamp -Raw).Trim() -ne $backendHash) }
    foreach ($project in @('src/Cube.Api','tests/Cube.Tests')) {
        $assets=Join-Path $project 'obj/project.assets.json'
        if (-not (Test-Path $assets)) { $needRestore=$true; continue }
        $a=Get-Content $assets -Raw | ConvertFrom-Json
        foreach ($lib in $a.libraries.PSObject.Properties) {
            if ($lib.Value.type -ne 'package') { continue }
            $found=$false
            foreach ($folder in $a.packageFolders.PSObject.Properties.Name) { if (Test-Path (Join-Path $folder $lib.Value.path)) { $found=$true } }
            if (-not $found) { Write-Host ('[MISSING] NuGet: ' + $lib.Name); $needRestore=$true }
        }
    }
    if ($needRestore) {
        Confirm 'Разрешить dotnet restore --locked-mode и скачивание недостающих NuGet-пакетов?'
        Run 'dotnet.exe' @('restore','Cube.sln','--locked-mode')
        Set-Content -LiteralPath $backendStamp -Value $backendHash
    }
    Check 'Frontend packages, including transitive dependencies'
    Push-Location tenant-app-frontend
    try {
        $frontendHash=HashFiles @('package.json','package-lock.json')
        $frontendStamp=Join-Path $state 'frontend.sha256'
        $needNpm= -not (Test-Path $frontendStamp)
        if (-not $needNpm) { $needNpm=((Get-Content $frontendStamp -Raw).Trim() -ne $frontendHash) }
        if (-not (Test-Path node_modules)) { $needNpm=$true } else { Probe 'npm.cmd' @('ls','--all','--offline'); if ($probeExit -ne 0) { $needNpm=$true } }
        if ($needNpm) {
            Confirm 'Разрешить npm ci: восстановить node_modules и скачать недостающие пакеты по package-lock.json?'
            Run 'npm.cmd' @('ci','--no-audit','--no-fund')
            Set-Content -LiteralPath $frontendStamp -Value $frontendHash
        }
        Probe 'npm.cmd' @('ls','--all','--offline')
        if ($probeExit -ne 0) { throw 'Frontend dependency validation failed. Run npm ls --all --offline in tenant-app-frontend for details.' }
        Run 'npm.cmd' @('run','typecheck')
    } finally { Pop-Location }
    Run 'dotnet.exe' @('build','Cube.sln','--configuration','Release','--no-restore')
    Check 'PostgreSQL readiness'
    $databaseStarted = ($runningDatabase.Count -eq 0)
    Run 'docker.exe' @('compose','up','-d','--wait','--pull','never','db')
    Run 'dotnet.exe' @('run','--project','src/Cube.Api','--configuration','Release','--no-build','--no-launch-profile','--','--migrate')
    OpenWorker 'backend' 'dotnet.exe run --project src/Cube.Api --configuration Release --no-build --no-launch-profile'
    WaitHttp 'http://127.0.0.1:5080/health/ready'
    OpenWorker 'frontend' "Set-Location tenant-app-frontend; npm.cmd run web -- --port 8081"
    WaitHttp 'http://127.0.0.1:8081'
    Write-Host "Проект запущен. Логи API и Expo: $state"
    Write-Host 'Веб: http://localhost:8081   API: http://127.0.0.1:5080'
    Write-Host 'Type q in THIS window and press Enter to stop API, Expo and PostgreSQL. Database data is retained.'
    Write-Host 'Администратор автоматически не создаётся; используйте штатную команду --bootstrap-admin с защищёнными переменными Bootstrap__*.'
    Start-Process 'http://localhost:8081'
    do {
        if ([Console]::IsInputRedirected) { $command = [Console]::ReadLine() }
        else { $command = Read-Host 'q = stop project' }
    } while ($null -ne $command -and $command.Trim() -ine 'q')
    exit 0
} catch {
    Write-Host ("ОШИБКА: " + $_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    StopProject
}
