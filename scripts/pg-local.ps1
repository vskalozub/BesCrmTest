<#
.SYNOPSIS
    Управление локальным portable PostgreSQL (без установки службы).

.EXAMPLE
    .\scripts\pg-local.ps1 init     # один раз: создать кластер
    .\scripts\pg-local.ps1 start
    .\scripts\pg-local.ps1 status
    .\scripts\pg-local.ps1 stop
    .\scripts\pg-local.ps1 reset    # удалить данные и создать кластер заново
    .\scripts\pg-local.ps1 psql     # консоль psql
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('init', 'start', 'stop', 'status', 'reset', 'psql')]
    [string]$Command
)

$ErrorActionPreference = 'Stop'

$PgHome   = if ($env:PG_HOME)     { $env:PG_HOME }     else { 'C:\Tools\pgsql' }
$PgData   = if ($env:PG_DATA)     { $env:PG_DATA }     else { 'C:\Tools\pgdata' }
$Port     = if ($env:PG_PORT)     { $env:PG_PORT }     else { '5432' }
$User     = 'postgres'
$Password = if ($env:PG_PASSWORD) { $env:PG_PASSWORD } else { 'postgres' }

$Bin = Join-Path $PgHome 'bin'
$Log = Join-Path $PgData 'server.log'

if (-not (Test-Path (Join-Path $Bin 'pg_ctl.exe'))) {
    throw "pg_ctl.exe не найден в '$Bin'. Укажите PG_HOME."
}

function Invoke-Init {
    if (Test-Path (Join-Path $PgData 'PG_VERSION')) {
        Write-Host "Кластер уже существует: $PgData"
        return
    }
    $pwFile = New-TemporaryFile
    try {
        Set-Content -Path $pwFile -Value $Password -NoNewline -Encoding ascii
        & (Join-Path $Bin 'initdb.exe') -D $PgData -U $User --pwfile=$pwFile `
            -E UTF8 --no-locale -A scram-sha-256
        if ($LASTEXITCODE -ne 0) { throw "initdb завершился с кодом $LASTEXITCODE" }
    }
    finally {
        Remove-Item $pwFile -Force
    }
    Write-Host "Кластер создан: $PgData (user=$User, port=$Port)"
}

function Invoke-Start {
    & (Join-Path $Bin 'pg_ctl.exe') -D $PgData -l $Log -o "-p $Port -c listen_addresses=localhost" -w start
    if ($LASTEXITCODE -ne 0) { throw "Не удалось запустить сервер, см. $Log" }
    Write-Host "Host=localhost;Port=$Port;Username=$User;Password=$Password"
}

function Invoke-Stop {
    & (Join-Path $Bin 'pg_ctl.exe') -D $PgData -m fast -w stop
}

switch ($Command) {
    'init'   { Invoke-Init }
    'start'  { Invoke-Start }
    'stop'   { Invoke-Stop }
    'status' { & (Join-Path $Bin 'pg_ctl.exe') -D $PgData status }
    'reset'  {
        & (Join-Path $Bin 'pg_ctl.exe') -D $PgData status *> $null
        if ($LASTEXITCODE -eq 0) { Invoke-Stop }
        if (Test-Path $PgData) { Remove-Item $PgData -Recurse -Force }
        Invoke-Init
    }
    'psql'   {
        $env:PGPASSWORD = $Password
        & (Join-Path $Bin 'psql.exe') -h localhost -p $Port -U $User
    }
}
