# SolidWorks PDM System startup script for Windows.

$ErrorActionPreference = "Stop"

$ApiPort = 5000
$WebPort = 5174
$PostgresPort = 5432
$ApiUrl = "http://localhost:$ApiPort"
$ApiListenUrl = "http://0.0.0.0:$ApiPort"
$WebUrl = "http://localhost:$WebPort"
$ApiPath = Join-Path $PSScriptRoot "src\SWPdm.Api"
$WebPath = Join-Path $PSScriptRoot "src\SWPdm.Web"
$LogDir = Join-Path $PSScriptRoot "scratch\logs"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Step {
    param([string] $Message)
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
}

function Test-PortListening {
    param([int] $Port)

    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Wait-ForPort {
    param(
        [int] $Port,
        [string] $Name,
        [int] $TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-PortListening -Port $Port) {
            Write-Host "$Name is ready on port $Port." -ForegroundColor Green
            return $true
        }

        Start-Sleep -Seconds 2
    }

    Write-Host "$Name did not start on port $Port within $TimeoutSeconds seconds." -ForegroundColor Red
    return $false
}

function Require-Command {
    param(
        [string] $CommandName,
        [string] $InstallHint
    )

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if (-not $command) {
        Write-Host "$CommandName was not found." -ForegroundColor Red
        Write-Host $InstallHint -ForegroundColor Red
        exit 1
    }

    return $command
}

function Show-LogTail {
    param([string[]] $Paths)

    foreach ($path in $Paths) {
        if (Test-Path $path) {
            Write-Host ""
            Write-Host "Last log lines from $path" -ForegroundColor DarkYellow
            Get-Content $path -Tail 40
        }
    }
}

Write-Host "=========================================================="
Write-Host "    Starting SolidWorks PDM System"
Write-Host "=========================================================="

Require-Command -CommandName "dotnet" -InstallHint "Install .NET 8 SDK/runtime, then run this script again." | Out-Null
$npmCommand = Get-Command "npm.cmd" -ErrorAction SilentlyContinue
if (-not $npmCommand) {
    $npmCommand = Require-Command -CommandName "npm" -InstallHint "Install Node.js, then run this script again."
}

Write-Step "1. Checking local PostgreSQL"
$postgresService = Get-Service -Name "postgresql-x64-16" -ErrorAction SilentlyContinue
if (-not $postgresService) {
    $postgresService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
}

if ($postgresService -and $postgresService.Status -ne "Running") {
    Write-Host "Starting PostgreSQL service: $($postgresService.Name)"
    try {
        Start-Service -Name $postgresService.Name
        $postgresService.WaitForStatus("Running", "00:00:30")
    }
    catch {
        Write-Host "Could not start PostgreSQL automatically." -ForegroundColor Red
        Write-Host "Start the PostgreSQL service manually, then run this script again." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-PortListening -Port $PostgresPort)) {
    Write-Host "PostgreSQL is not listening on localhost:$PostgresPort." -ForegroundColor Red
    Write-Host "Start the local PostgreSQL service and try again." -ForegroundColor Red
    exit 1
}
Write-Host "PostgreSQL is ready on port $PostgresPort." -ForegroundColor Green

Write-Step "2. Starting Backend API"
$apiOutLog = Join-Path $LogDir "api-$RunStamp.out.log"
$apiErrLog = Join-Path $LogDir "api-$RunStamp.err.log"

if (Test-PortListening -Port $ApiPort) {
    Write-Host "Backend API is already running on $ApiUrl." -ForegroundColor Green
}
else {
    $apiPathForCommand = $ApiPath.Replace("'", "''")
    $apiListenUrlForCommand = $ApiListenUrl.Replace("'", "''")
    $apiCommand = "& { `$env:ASPNETCORE_ENVIRONMENT = 'Development'; dotnet run --project '$apiPathForCommand' --urls '$apiListenUrlForCommand' }"
    Start-Process -FilePath "powershell.exe" `
        -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $apiCommand) `
        -WorkingDirectory $PSScriptRoot `
        -RedirectStandardOutput $apiOutLog `
        -RedirectStandardError $apiErrLog `
        -WindowStyle Hidden

    if (-not (Wait-ForPort -Port $ApiPort -Name "Backend API" -TimeoutSeconds 90)) {
        Show-LogTail -Paths @($apiOutLog, $apiErrLog)
        exit 1
    }
}

Write-Step "3. Starting Frontend Web"
$webOutLog = Join-Path $LogDir "web-$RunStamp.out.log"
$webErrLog = Join-Path $LogDir "web-$RunStamp.err.log"

if (Test-PortListening -Port $WebPort) {
    Write-Host "Frontend Web is already running on $WebUrl." -ForegroundColor Green
}
else {
    Start-Process -FilePath $npmCommand.Source `
        -ArgumentList @("run", "dev") `
        -WorkingDirectory $WebPath `
        -RedirectStandardOutput $webOutLog `
        -RedirectStandardError $webErrLog `
        -WindowStyle Hidden

    if (-not (Wait-ForPort -Port $WebPort -Name "Frontend Web" -TimeoutSeconds 60)) {
        Show-LogTail -Paths @($webOutLog, $webErrLog)
        exit 1
    }
}

Write-Step "4. Opening browser"
Start-Process $WebUrl

Write-Host ""
Write-Host "System is ready." -ForegroundColor Green
Write-Host "Frontend: $WebUrl"
Write-Host "API:      $ApiUrl"
Write-Host "Logs:     $LogDir"
