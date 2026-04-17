# SolidWorks PDM System Startup Script for Windows

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    🚀 Starting SolidWorks PDM System (Windows) " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check Docker PostgreSQL container
Write-Host "▶ 1. Checking PostgreSQL container..." -ForegroundColor Yellow
$containerStatus = docker inspect --format='{{.State.Running}}' swpdm-postgres 2>$null
if ($containerStatus -ne "true") {
    Write-Host "⚠️  swpdm-postgres container is not running. Starting..." -ForegroundColor Magenta
    docker start swpdm-postgres
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to start container. Check Docker config." -ForegroundColor Red
        exit 1
    }
}
Write-Host "✅ PostgreSQL container is ready (Port: 5433)" -ForegroundColor Green
Write-Host ""

# 2. Start Backend API
Write-Host "▶ 2. Starting Backend API..." -ForegroundColor Yellow
$apiPath = Join-Path $PSScriptRoot "src\SWPdm.Api"
Start-Process dotnet -ArgumentList "run --project `"$apiPath`"" -NoNewWindow
Write-Host "✅ API is starting at http://localhost:5000" -ForegroundColor Green
Write-Host ""

# 3. Start Frontend Web
Write-Host "▶ 3. Starting Frontend Web..." -ForegroundColor Yellow
$webPath = Join-Path $PSScriptRoot "src\SWPdm.Web"
Start-Process npm.cmd -ArgumentList "run dev" -WorkingDirectory $webPath -NoNewWindow
Write-Host "✅ Frontend is starting at http://localhost:5174" -ForegroundColor Green
Write-Host ""

Write-Host "----------------------------------------------------------"
Write-Host "System startup complete! Opening browser..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C or close window to stop services."
Write-Host "----------------------------------------------------------"

Start-Sleep -Seconds 3
Start-Process "http://localhost:5174"
