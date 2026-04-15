#!/usr/bin/env bash
# =============================================================
#  SWPdm API — 真實寫入流程驗證腳本
#  使用方式：
#    chmod +x scripts/test-ingest.sh
#    ./scripts/test-ingest.sh
#
#  前置條件：
#    1. PostgreSQL 已啟動，且已執行 scripts/db-init-postgres.sql
#    2. 已執行 ./scripts/db-update.sh （套用 EF migrations）
#    3. 已執行 ./scripts/run-api.sh  （API 在 localhost:5000 監聽）
#    4. appsettings.Development.json 所有欄位已填妥
# =============================================================

set -euo pipefail

BASE_URL="${API_BASE_URL:-http://localhost:5000}"
# 替換為你機器上真實存在的 .SLDPRT / .SLDASM 檔案路徑
SAMPLE_SLDPRT="${SAMPLE_SLDPRT:-/tmp/sample_part.SLDPRT}"
SAMPLE_SLDASM="${SAMPLE_SLDASM:-/tmp/sample_assembly.SLDASM}"
# 替換為已共用給服務帳戶的 Google Drive 資料夾 ID
DRIVE_FOLDER_ID="${DRIVE_FOLDER_ID:-YOUR_GOOGLE_DRIVE_FOLDER_ID}"

LINE="─────────────────────────────────────────────────────────"
PASS="✅ PASS"
FAIL="❌ FAIL"

pass_count=0
fail_count=0

check() {
  local label="$1"
  local http_code="$2"
  local expected="$3"
  if [ "$http_code" = "$expected" ]; then
    echo "$PASS  [$label]  HTTP $http_code"
    ((pass_count++))
  else
    echo "$FAIL  [$label]  HTTP $http_code (expected $expected)"
    ((fail_count++))
  fi
}

echo ""
echo "$LINE"
echo "  SWPdm API 整合測試"
echo "  Target: $BASE_URL"
echo "$LINE"
echo ""

# ─── 1. Health Check ─────────────────────────────────────────
echo "▶ 1. Health Check"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/health")
check "GET /health" "$code" "200"
echo ""

# ─── 2. 根路徑（列出所有 endpoint）────────────────────────────
echo "▶ 2. 根路徑 — 取得 endpoint 清單"
response=$(curl -s "$BASE_URL/")
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/")
check "GET /" "$code" "200"
echo ""

# ─── 3. 設定狀態驗證 ──────────────────────────────────────────
echo "▶ 3. 設定狀態驗證（/api/config/status）"
response=$(curl -s "$BASE_URL/api/config/status")
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/config/status")
check "GET /api/config/status" "$code" "200"
echo ""

# ─── 4. 資料庫狀態驗證 ────────────────────────────────────────
echo "▶ 4. 資料庫連線驗證（/api/database/status）"
response=$(curl -s "$BASE_URL/api/database/status")
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/database/status")
check "GET /api/database/status" "$code" "200"
echo ""

# ─── 5. 直接觸發 EF Migration（可選）─────────────────────────
echo "▶ 5. 透過 API 套用 Migration（/api/database/migrate）"
response=$(curl -s -X POST "$BASE_URL/api/database/migrate" \
  -H "Content-Type: application/json")
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/database/migrate" \
  -H "Content-Type: application/json")
check "POST /api/database/migrate" "$code" "200"
echo ""

# ─── 6. 參數驗證（缺少 LocalFilePath）────────────────────────
echo "▶ 6. 參數驗證 — 缺少 LocalFilePath（應回 400）"
code=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "$BASE_URL/api/ingest/cad" \
  -H "Content-Type: application/json" \
  -d '{
    "localFilePath": "",
    "ingestReferencedFiles": false,
    "additionalSearchPaths": []
  }')
check "POST /api/ingest/cad (empty path)" "$code" "400"
echo ""
# ─── 8. 階段 B：上傳測試檔案至 Local Storage（/api/storage/upload）"
response=$(curl -s -X POST "$BASE_URL/api/storage/upload" \
  -H "Content-Type: application/json" \
  -d '{
    "localFilePath": "'$SAMPLE_SLDPRT'"
  }')
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/storage/upload" \
  -H "Content-Type: application/json" \
  -d '{
    "localFilePath": "'$SAMPLE_SLDPRT'"
  }')
check "POST /api/storage/upload" "$code" "200"
echo ""

FILE_ID=$(echo "$response" | python3 -c "import sys, json; print(json.load(sys.stdin).get('storageFileId', ''))" 2>/dev/null)
if [ -n "$FILE_ID" ]; then
  echo "▶ 9. 階段 B：從 Local Storage 下載檔案（/api/storage/download）"
  # 此處只檢查執行是否成功，檔案會下載到 /tmp
  response=$(curl -s -X POST "$BASE_URL/api/storage/download" \
    -H "Content-Type: application/json" \
    -d '{
      "fileId": "'$FILE_ID'",
      "destinationFilePath": "/tmp/swpdm_test_download.sldprt"
    }')
  echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/storage/download" \
    -H "Content-Type: application/json" \
    -d '{
      "fileId": "'$FILE_ID'",
      "destinationFilePath": "/tmp/swpdm_test_download.sldprt"
    }')
  check "POST /api/storage/download" "$code" "200"
  echo ""
else
  echo "⚠️ 略過下載測試，因為沒有取得 storageFileId。"
fi
# ─── 7. 主要測試：單一 SLDPRT Ingest ─────────────────────────
if [ -f "$SAMPLE_SLDPRT" ]; then
  echo "▶ 7. Ingest 單一零件檔（SLDPRT）"
  response=$(curl -s -X POST "$BASE_URL/api/ingest/cad" \
    -H "Content-Type: application/json" \
    -d "{
      \"localFilePath\": \"$SAMPLE_SLDPRT\",
      \"ingestReferencedFiles\": false,
      \"additionalSearchPaths\": []
    }")
  echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$BASE_URL/api/ingest/cad" \
    -H "Content-Type: application/json" \
    -d "{
      \"localFilePath\": \"$SAMPLE_SLDPRT\",
      \"ingestReferencedFiles\": false,
      \"additionalSearchPaths\": []
    }")
  check "POST /api/ingest/cad (SLDPRT)" "$code" "200"
else
  echo "⚠️   跳過 SLDPRT 測試：檔案不存在 → $SAMPLE_SLDPRT"
  echo "     請設定環境變數 SAMPLE_SLDPRT=/path/to/real/file.SLDPRT"
fi
echo ""

# ─── 8. 主要測試：組件 Ingest（含參考檔）──────────────────────
if [ -f "$SAMPLE_SLDASM" ]; then
  echo "▶ 8. Ingest 組件（SLDASM）含參考檔遞迴"
  response=$(curl -s -X POST "$BASE_URL/api/ingest/cad" \
    -H "Content-Type: application/json" \
    -d "{
      \"localFilePath\": \"$SAMPLE_SLDASM\",
      \"ingestReferencedFiles\": true,
      \"additionalSearchPaths\": [\"$(dirname "$SAMPLE_SLDASM")\"]
    }")
  echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$BASE_URL/api/ingest/cad" \
    -H "Content-Type: application/json" \
    -d "{
      \"localFilePath\": \"$SAMPLE_SLDASM\",
      \"ingestReferencedFiles\": true,
      \"additionalSearchPaths\": [\"$(dirname "$SAMPLE_SLDASM")\"]
    }")
  check "POST /api/ingest/cad (SLDASM + refs)" "$code" "200"
else
  echo "⚠️   跳過 SLDASM 測試：檔案不存在 → $SAMPLE_SLDASM"
  echo "     請設定環境變數 SAMPLE_SLDASM=/path/to/real/assembly.SLDASM"
fi
echo ""

# ─── 9. 讀回剛寫入的文件（假設 documentId=1）─────────────────
echo "▶ 9. 讀回 Document（/api/documents/1）"
response=$(curl -s "$BASE_URL/api/documents/1")
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/documents/1")
# 有資料回 200，無資料回 404，兩者皆可接受
if [ "$code" = "200" ] || [ "$code" = "404" ]; then
  echo "$PASS  [GET /api/documents/1]  HTTP $code"
  ((pass_count++))
else
  echo "$FAIL  [GET /api/documents/1]  HTTP $code"
  ((fail_count++))
fi
echo ""

# ─── 10. 讀回 Version + Children ──────────────────────────────
echo "▶ 10. 讀回 Version（/api/versions/1）"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/versions/1")
if [ "$code" = "200" ] || [ "$code" = "404" ]; then
  echo "$PASS  [GET /api/versions/1]  HTTP $code"
  ((pass_count++))
else
  echo "$FAIL  [GET /api/versions/1]  HTTP $code"
  ((fail_count++))
fi

code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/versions/1/children")
check "GET /api/versions/1/children" "$code" "200"
echo ""

# ─── 11. 階段 D：ZIP 打包下載（package-closure + 下載） ───────
echo "▶ 11. package-closure 查詢（/api/assemblies/1/package-closure）"
response=$(curl -s "$BASE_URL/api/assemblies/1/package-closure")
echo "$response" | python3 -m json.tool 2>/dev/null || echo "$response"
code=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/assemblies/1/package-closure")
# 有資料回 200，無資料回 404，兩者皆可接受
if [ "$code" = "200" ] || [ "$code" = "404" ]; then
  echo "$PASS  [GET /api/assemblies/1/package-closure]  HTTP $code"
  ((pass_count++))
else
  echo "$FAIL  [GET /api/assemblies/1/package-closure]  HTTP $code"
  ((fail_count++))
fi
echo ""

echo "▶ 12. 階段 D：ZIP 下載（/api/assemblies/1/download-zip）"
ZIP_OUTPUT="/tmp/swpdm_test_assembly.zip"
code=$(curl -s -o "$ZIP_OUTPUT" -w "%{http_code}" \
  "$BASE_URL/api/assemblies/1/download-zip")
# 有資料回 200，無 BOM 回 404，兩者皆可接受
if [ "$code" = "200" ]; then
  ZIP_SIZE=$(wc -c < "$ZIP_OUTPUT" | tr -d ' ')
  echo "$PASS  [GET /api/assemblies/1/download-zip]  HTTP $code  (${ZIP_SIZE} bytes)"
  echo "     ZIP 已儲存至：$ZIP_OUTPUT"
  # 驗證 ZIP 格式是否有效
  if python3 -c "import zipfile; zipfile.ZipFile('$ZIP_OUTPUT').testzip()" 2>/dev/null; then
    echo "     ✅ ZIP 格式驗證通過"
    python3 -c "
import zipfile
with zipfile.ZipFile('$ZIP_OUTPUT') as z:
    names = z.namelist()
    print(f'     ZIP 內容({len(names)} 個檔案):')
    for n in names:
        print(f'       - {n}')
"
  else
    echo "     ⚠️  ZIP 格式驗證失敗（可能是空 ZIP 或損毀）"
  fi
  ((pass_count++))
elif [ "$code" = "404" ]; then
  echo "$PASS  [GET /api/assemblies/1/download-zip]  HTTP $code (尚無 BOM 資料，屬正常行為)"
  ((pass_count++))
else
  echo "$FAIL  [GET /api/assemblies/1/download-zip]  HTTP $code"
  ((fail_count++))
fi
echo ""

# ─── 結果摘要 ─────────────────────────────────────────────────
echo "$LINE"
echo "  測試結果：$pass_count 通過 / $fail_count 失敗"
echo "$LINE"
echo ""

if [ "$fail_count" -gt 0 ]; then
  exit 1
fi
