#!/usr/bin/env bash
set -euo pipefail

# 取得腳本所在的目錄（專案根目錄）
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=========================================================="
echo "    🚀 啟動 SolidWorks PDM 系統 (Local Storage 版) "
echo "=========================================================="
echo ""

echo "▶ 1. 更新資料庫結構 (執行 Entity Framework Migrations)..."
# 這裡會讀取 appsettings.Development.json 中的設定（如果存在），或是環境變數。
if bash "$ROOT_DIR/scripts/db-update.sh"; then
    echo "✅ 資料庫更新成功！"
else
    echo "❌ 資料庫更新失敗，請檢查 scripts/db-init-postgres.sql 是否已執行並啟動 Postgres。"
    exit 1
fi
echo ""

echo "▶ 2. 啟動 PDM API 伺服器..."
echo "✅ API 預計將運行在 http://localhost:5000"
echo "請按 Ctrl+C 來停止伺服器。"
echo "----------------------------------------------------------"

# 直接執行 API 啟動腳本
bash "$ROOT_DIR/scripts/run-api.sh"
