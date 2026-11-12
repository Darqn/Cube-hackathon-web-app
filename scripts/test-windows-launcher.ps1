$ErrorActionPreference = 'Stop'
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../start-cube.bat'))
$marker = '# POWERSHELL PAYLOAD'
$payload = $source.Substring($source.IndexOf($marker) + $marker.Length)
$tokens = $null; $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput($payload, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
# Load only function declarations, never the launcher's startup block.
foreach ($statement in $ast.EndBlock.Statements) {
    if ($statement -is [Management.Automation.Language.FunctionDefinitionAst]) {
        . ([ScriptBlock]::Create($statement.Extent.Text))
    }
}
function Assert($condition, $message) { if (-not $condition) { throw $message } }
function AssertPort($expected) {
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.set_ConnectionString($env:ConnectionStrings__Cube)
    Assert ($env:CUBE_POSTGRES_PORT -eq [string]$expected) 'Compose port mismatch'
    Assert ($builder['Port'] -eq [string]$expected) 'API port mismatch'
    Assert ($builder['Password'] -eq 'test;quoted') 'Password was changed'
}
$savedConnection = $env:ConnectionStrings__Cube
$savedPort = $env:CUBE_POSTGRES_PORT
$networkVariables = @('CUBE_EXPO_HOST','CUBE_LAN_IP','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','EXPO_PUBLIC_API_URL','EXPO_PUBLIC_USE_MOCK','EXPO_OFFLINE','REACT_NATIVE_PACKAGER_HOSTNAME','Cors__Origins__0','Cors__Origins__1','Cors__Origins__2')
$savedNetwork = @{}
foreach ($name in $networkVariables) { $savedNetwork[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('cube-launcher-' + [Guid]::NewGuid().ToString('N'))
$listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
try {
    $listener.Start()
    $occupied = $listener.LocalEndpoint.Port
    Assert (-not (TestPort $occupied)) 'Occupied port was reported available'
    $listener.Stop()
    Assert (TestPort $occupied) 'Released port was reported occupied'

    # Deterministic availability for fallback and configuration checks.
    function TestPort([int]$port) { return $port -ne 5432 }
    $env:ConnectionStrings__Cube = 'Host=127.0.0.1;Port=15432;Database=cube;Username=cube;Password="test;quoted"'
    $env:CUBE_POSTGRES_PORT = $null
    ConfigureDatabasePort
    AssertPort 15432

    $env:ConnectionStrings__Cube = 'Host=localhost;Port=5432;Password="test;quoted"'
    $env:CUBE_POSTGRES_PORT = $null
    ConfigureDatabasePort
    AssertPort 5433

    $env:CUBE_POSTGRES_PORT = '15433'
    ConfigureDatabasePort
    AssertPort 15433

    $env:CUBE_POSTGRES_PORT = '5432'
    $rejected = $false
    try { ConfigureDatabasePort } catch { $rejected = $true }
    Assert $rejected 'Explicit occupied port must fail'

    ConfigureDatabasePort '5432'
    AssertPort 5432

    $env:ConnectionStrings__Cube = 'Host=remote.example;Port=6432;Password=test'
    $env:CUBE_POSTGRES_PORT = $null
    ConfigureDatabasePort
    Assert ($env:ConnectionStrings__Cube -eq 'Host=remote.example;Port=6432;Password=test') 'Remote connection was changed'

    foreach ($address in @('192.168.1.20','10.20.1.5','172.16.0.1','172.31.255.254')) {
        Assert (TestLanIPv4 $address) "Private IPv4 rejected: $address"
    }
    foreach ($address in @('127.0.0.1','0.0.0.0','8.8.8.8','169.254.1.2','172.32.0.1','::1','invalid;command')) {
        Assert (-not (TestLanIPv4 $address)) "Unsafe or non-LAN address accepted: $address"
    }
    $script:lanAddresses = @('192.168.1.20')
    function GetLanIPv4Addresses { return $script:lanAddresses }
    function GetLocalIPv4Addresses { return @('192.168.1.20','10.20.1.5') }
    $env:CUBE_EXPO_HOST = $null
    $env:CUBE_LAN_IP = $null
    $connectionBeforeLan = $env:ConnectionStrings__Cube
    ConfigureExpoNetwork
    Assert ($env:CUBE_EXPO_HOST -eq 'lan') 'Default Expo mode must be LAN'
    Assert ($env:ASPNETCORE_URLS -eq 'http://127.0.0.1:5080;http://192.168.1.20:5080') 'API must bind loopback and the selected LAN adapter only'
    Assert ($env:EXPO_PUBLIC_API_URL -eq 'http://192.168.1.20:5080/api/v1') 'Phone must use a reachable API address'
    Assert ($env:REACT_NATIVE_PACKAGER_HOSTNAME -eq '192.168.1.20') 'QR must advertise the same LAN adapter'
    Assert ($env:Cors__Origins__2 -eq 'http://192.168.1.20:8081') 'LAN web origin missing'
    Assert ($env:ConnectionStrings__Cube -eq $connectionBeforeLan) 'LAN mode changed database configuration'

    $env:CUBE_LAN_IP = '10.20.1.5'
    ConfigureExpoNetwork
    Assert ($env:EXPO_PUBLIC_API_URL -eq 'http://10.20.1.5:5080/api/v1') 'Explicit LAN adapter ignored'
    foreach ($address in @('8.8.8.8','192.168.1.250','invalid;command')) {
        $env:CUBE_LAN_IP = $address
        $rejected = $false
        try { ConfigureExpoNetwork } catch { $rejected = $true }
        Assert $rejected 'Invalid or unassigned LAN override must fail'
    }
    $env:CUBE_LAN_IP = $null
    $env:CUBE_EXPO_HOST = 'tunnel'
    $rejected = $false
    try { ConfigureExpoNetwork } catch { $rejected = $true }
    Assert $rejected 'Unsupported connection mode must not silently install a tunnel provider'

    $env:CUBE_EXPO_HOST = 'localhost'
    ConfigureExpoNetwork
    Assert ($env:ASPNETCORE_URLS -eq 'http://127.0.0.1:5080') 'Local mode must retain loopback binding'
    Assert ($env:EXPO_PUBLIC_API_URL -eq 'http://127.0.0.1:5080/api/v1') 'Local API URL incorrect'
    Assert (-not $env:Cors__Origins__2) 'Local mode leaked the prior LAN CORS origin'
    Assert ($env:REACT_NATIVE_PACKAGER_HOSTNAME -eq '127.0.0.1') 'Local mode leaked the prior LAN QR host'
    $script:lanAddresses = @()
    $env:CUBE_EXPO_HOST = 'lan'
    ConfigureExpoNetwork
    Assert ($env:CUBE_EXPO_HOST -eq 'localhost') 'Missing LAN adapter must fall back with a diagnostic'

    $body = GetFrontendWorkerBody
    $workerTokens = $null; $workerErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseInput($body, [ref]$workerTokens, [ref]$workerErrors)
    Assert ($workerErrors.Count -eq 0) 'Frontend worker syntax invalid'
    Assert ($body -match '(?m)^\$env:CI\s*=\s*\$null\s*$') 'Inherited CI must not suppress Expo interactivity'
    Assert ($body -match 'npm\.cmd run start -- --web --port 8081 --clear \$expoHostArgument') 'Interactive Expo command or cache reset missing'

    # Capture Windows process options without starting any server or native process.
    function Start-Process {
        param([string]$FilePath, [string]$WindowStyle, [switch]$PassThru,
            [string]$RedirectStandardOutput, [string]$RedirectStandardError, [string[]]$ArgumentList)
        $script:workerOptions = @{}
        foreach ($key in $PSBoundParameters.Keys) { $script:workerOptions[$key] = $PSBoundParameters[$key] }
        return [Diagnostics.Process]::GetCurrentProcess()
    }
    $root = Join-Path $testRoot "project's & directory"
    $state = Join-Path $root '.data/windows-launcher'
    New-Item -ItemType Directory -Force -Path $state | Out-Null
    $workers = New-Object 'System.Collections.Generic.List[System.Diagnostics.Process]'
    OpenWorker 'frontend' $body -Interactive
    Assert ($workerOptions['WindowStyle'] -eq 'Normal') 'Expo console must be visible'
    Assert (-not $workerOptions.ContainsKey('RedirectStandardOutput')) 'Redirected stdout suppresses the QR'
    Assert (-not $workerOptions.ContainsKey('RedirectStandardError')) 'Interactive stderr must stay in the Expo console'
    Assert ($workerOptions['ArgumentList'] -contains '-NoExit') 'Retain the parent for process-tree cleanup and diagnostics'
    $frontendFile = Join-Path $state 'frontend.ps1'
    Assert ($workerOptions['ArgumentList'][-1] -eq ('"'+$frontendFile+'"')) 'Worker path must be quoted'
    $generated = [IO.File]::ReadAllText($frontendFile)
    Assert ($generated.Contains($root.Replace("'","''"))) 'Project path apostrophes must be escaped'
    $null = [Management.Automation.Language.Parser]::ParseInput($generated, [ref]$workerTokens, [ref]$workerErrors)
    Assert ($workerErrors.Count -eq 0) 'Generated worker syntax invalid for a path containing spaces/apostrophes'
    $bytes = [IO.File]::ReadAllBytes($frontendFile)
    Assert ($bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) 'Windows PowerShell worker must retain UTF-8 BOM'

    OpenWorker 'backend' 'Write-Host backend'
    Assert ($workerOptions['WindowStyle'] -eq 'Hidden') 'Backend logging behavior changed'
    Assert ($workerOptions['RedirectStandardOutput'] -eq (Join-Path $state 'backend.log')) 'Backend stdout log missing'
    Assert ($workerOptions['RedirectStandardError'] -eq (Join-Path $state 'backend.error.log')) 'Backend stderr log missing'
    Assert ($workers.Count -eq 2) 'Both server parents must be tracked for cleanup'
    $script:cleanupCommands = New-Object 'System.Collections.Generic.List[object]'
    function Probe($command, [string[]]$arguments) {
        $script:cleanupCommands.Add([PSCustomObject]@{ Command = $command; Arguments = $arguments })
        $script:probeExit = 0
    }
    $databaseStarted = $true
    StopProject
    $kills = @($cleanupCommands | Where-Object Command -eq 'taskkill.exe')
    Assert ($kills.Count -eq 2) 'Cleanup must stop both retained process trees'
    foreach ($kill in $kills) { Assert ($kill.Arguments -contains '/T' -and $kill.Arguments -contains '/F') 'Process tree cleanup flags missing' }
    Assert (@($cleanupCommands | Where-Object Command -eq 'docker.exe').Count -eq 1) 'Owned database must be stopped'
    $cleanupCommands.Clear()
    $databaseStarted = $false
    StopProject
    Assert ($cleanupCommands.Count -eq 0) 'A reused database must be left running'

    Write-Host 'PASS: payload syntax, sockets/database ports, LAN/local configuration, adapter validation, visible unredirected Expo console, CI reset, path quoting/encoding, and owned-process/database cleanup.'
} finally {
    $listener.Stop()
    $env:ConnectionStrings__Cube = $savedConnection
    $env:CUBE_POSTGRES_PORT = $savedPort
    foreach ($name in $networkVariables) { [Environment]::SetEnvironmentVariable($name, $savedNetwork[$name], 'Process') }
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
