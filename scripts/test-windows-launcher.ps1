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

    Write-Host 'PASS: payload syntax, socket checks, configured ports, fallback, running database reuse, credential preservation, remote connection preservation.'
} finally {
    $listener.Stop()
    $env:ConnectionStrings__Cube = $savedConnection
    $env:CUBE_POSTGRES_PORT = $savedPort
}
