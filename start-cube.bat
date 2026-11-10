@echo off
setlocal
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
    throw "Сервис не готов: $url. Посмотрите ошибку в открытом окне сервера."
}
function FreePort($port) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Порт $port уже занят. Остановите использующий его процесс и повторите запуск." }
}
function HashFiles($paths) { return (($paths | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash }) -join '-') }
function OpenWorker($name, $body) {
    $file = Join-Path $state "$name.ps1"
    $prefix = '$ErrorActionPreference="Stop"; Set-Location -LiteralPath ' + "'" + $root.Replace("'","''") + "';`r`n"
    [IO.File]::WriteAllText($file, $prefix + $body, (New-Object Text.UTF8Encoding($true)))
    Start-Process powershell.exe -ArgumentList @('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',('"'+$file+'"')) | Out-Null
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
            if ($branch -ne 'dev' -and $branch -ne 'dev/web-design') { Confirm "Ожидалась dev или dev/web-design. Продолжить на текущей ветке $branch?" }
        }
    }
    $state=Join-Path $root '.data/windows-launcher'
    New-Item -ItemType Directory -Force -Path $state | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (-not (Get-Command node.exe -ErrorAction SilentlyContinue)) { InstallWinget 'OpenJS.NodeJS.LTS' 'Node.js LTS и npm' }
    $nodeVersion = & node.exe -p 'process.versions.node'
    if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion.Split('.')[0]) -lt 20) { throw 'Требуется Node.js >=20; рекомендуется проверенная версия 24.22.1. Обновите Node.js и перезапустите BAT.' }
    if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'npm не найден. Восстановите установку Node.js.' }
    Write-Host "Node.js $nodeVersion"
    if (-not (Get-Command docker.exe -ErrorAction SilentlyContinue)) { InstallWinget 'Docker.DockerDesktop' 'Docker Desktop' }
    if (-not (Get-Command docker.exe -ErrorAction SilentlyContinue)) { throw 'Docker установлен. Перезапустите Windows, настройте WSL2/Linux containers в Docker Desktop и повторите запуск.' }
    Probe 'docker.exe' @('info','--format','{{.OSType}}')
    if ($probeExit -ne 0) {
        $desktop=Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'
        if (Test-Path $desktop) { Start-Process -FilePath $desktop | Out-Null }
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
    Run 'docker.exe' @('compose','version')
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
        Invoke-WebRequest -UseBasicParsing $files[0].url -OutFile $zip
        if ((Get-FileHash $zip -Algorithm SHA512).Hash -ne $files[0].hash) { Remove-Item $zip; throw 'SHA512 архива SDK не совпадает.' }
        New-Item -ItemType Directory -Force $localDotnet | Out-Null
        Expand-Archive -LiteralPath $zip -DestinationPath $localDotnet -Force
        Remove-Item $zip
        $env:DOTNET_ROOT=$localDotnet; $env:Path="$localDotnet;$env:Path"
    }
    Run 'dotnet.exe' @('--version')
    # Preserve existing shell-compatible .env; never execute its contents.
    if (-not (Test-Path .env)) {
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create(); $bytes=New-Object byte[] 24; $rng.GetBytes($bytes); $rng.Dispose()
        $password=([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant()
        $lines=@("POSTGRES_PASSWORD='$password'", "ConnectionStrings__Cube='Host=127.0.0.1;Port=5432;Database=cube;Username=cube;Password=$password'", "ASPNETCORE_ENVIRONMENT='Development'", "ASPNETCORE_URLS='http://127.0.0.1:5080'", "Files__AllowUnscannedDevelopmentUploads='true'", "Notifications__WorkerEnabled='true'", "Files__Root='.data/files'")
        [IO.File]::WriteAllLines((Join-Path $root '.env'),$lines,(New-Object Text.UTF8Encoding($false)))
        Write-Host 'Создан локальный .env со случайным паролем. Значения не выводятся.'
    }
    foreach ($line in Get-Content .env) {
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
    $images=@(& docker.exe compose config --images)
    if ($LASTEXITCODE -ne 0) { throw 'Ошибка compose.yaml/.env.' }
    foreach ($image in $images) {
        Probe 'docker.exe' @('image','inspect',$image)
        if ($probeExit -ne 0) { Confirm "Скачать Docker-образ $image?"; Run 'docker.exe' @('pull',$image) }
    }
    Run 'docker.exe' @('compose','up','-d','--wait','--pull','never')
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
            if (-not $found) { $needRestore=$true }
        }
    }
    if ($needRestore) {
        Confirm 'Разрешить dotnet restore --locked-mode и скачивание недостающих NuGet-пакетов?'
        Run 'dotnet.exe' @('restore','Cube.sln','--locked-mode')
        Set-Content -LiteralPath $backendStamp -Value $backendHash
    }
    Push-Location tenant-app-frontend
    try {
        $frontendHash=HashFiles @('package.json','package-lock.json')
        $frontendStamp=Join-Path $state 'frontend.sha256'
        $needNpm= -not (Test-Path $frontendStamp)
        if (-not $needNpm) { $needNpm=((Get-Content $frontendStamp -Raw).Trim() -ne $frontendHash) }
        if (-not (Test-Path node_modules)) { $needNpm=$true } else { Probe 'npm.cmd' @('ls','--depth=0','--offline'); if ($probeExit -ne 0) { $needNpm=$true } }
        if ($needNpm) {
            Confirm 'Разрешить npm ci: восстановить node_modules и скачать недостающие пакеты по package-lock.json?'
            Run 'npm.cmd' @('ci','--no-audit','--no-fund')
            Set-Content -LiteralPath $frontendStamp -Value $frontendHash
        }
        Run 'npm.cmd' @('run','typecheck')
    } finally { Pop-Location }
    Run 'dotnet.exe' @('build','Cube.sln','--configuration','Release','--no-restore')
    Run 'dotnet.exe' @('run','--project','src/Cube.Api','--configuration','Release','--no-build','--no-launch-profile','--','--migrate')
    OpenWorker 'backend' 'dotnet.exe run --project src/Cube.Api --configuration Release --no-build --no-launch-profile'
    WaitHttp 'http://127.0.0.1:5080/health/ready'
    OpenWorker 'frontend' "Set-Location tenant-app-frontend; npm.cmd run web -- --port 8081"
    WaitHttp 'http://127.0.0.1:8081'
    Write-Host 'Проект запущен. Окна API и Expo должны оставаться открытыми.'
    Write-Host 'Веб: http://localhost:8081   API: http://127.0.0.1:5080'
    Write-Host 'Остановка: Ctrl+C в обоих окнах. PostgreSQL: docker compose stop'
    Write-Host 'Администратор автоматически не создаётся; используйте штатную команду --bootstrap-admin с защищёнными переменными Bootstrap__*.'
    Start-Process 'http://localhost:8081'
    exit 0
} catch {
    Write-Host ("ОШИБКА: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Уже запущенные сервисы автоматически не останавливаются. Их ошибки видны в отдельных окнах.'
    exit 1
}
