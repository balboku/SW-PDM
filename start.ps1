# SolidWorks PDM System Startup Script for Windows

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    🚀 啟動 SolidWorks PDM 系統 (Windows 版) " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. 確認 Docker 容器是否運行
Write-Host "▶ 1. 正在檢查 PostgreSQL 容器..." -ForegroundColor Yellow
$containerStatus = docker inspect --format='{{.State.Running}}' swpdm-postgres 2>$null
if ($containerStatus -ne "true") {
    Write-Host "⚠️  swpdm-postgres 容器未運行。正在嘗試啟動..." -ForegroundColor Magenta
    docker start swpdm-postgres
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 無法啟動容器，請確認 Docker Desktop 已啟動且容器名稱正確。" -ForegroundColor Red
        exit 1
    }
}
Write-Host "✅ PostgreSQL 容器已就緒 (Port: 5433)" -ForegroundColor Green
Write-Host ""

# 2. 啟動 Backend API
Write-Host "▶ 2. 正在啟動 Backend API..." -ForegroundColor Yellow
$apiPath = Join-Path $PSScriptRoot "src\SWPdm.Api"
Start-Process dotnet -ArgumentList "run --project `"$apiPath`"" -NoNewWindow
Write-Host "✅ API 正在啟動，預計運行於 http://localhost:5000" -ForegroundColor Green
Write-Host ""

# 3. 啟動 Frontend Web
Write-Host "▶ 3. 正在啟動 Frontend Web..." -ForegroundColor Yellow
$webPath = Join-Path $PSScriptRoot "src\SWPdm.Web"
Start-Process npm -ArgumentList "run dev" -WorkingDirectory $webPath -NoNewWindow
Write-Host "✅ Frontend 正在啟動，預計運行於 http://localhost:5174" -ForegroundColor Green
Write-Host ""

Write-Host "----------------------------------------------------------"
Write-Host "系統啟動完成！請開啟瀏覽器瀏覽上述網址。" -ForegroundColor Cyan
Write-Host "按下 Ctrl+C (或關閉視窗) 停止服務。"
Write-Host "----------------------------------------------------------"
