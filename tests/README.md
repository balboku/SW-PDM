# 前端清晰度驗證

本提交只包含前端與隔離 UI fixture，不包含尚未提交的解析器、批次取消出庫、套件預檢或資料庫變更。

## 建置與單元測試

在 `src/SWPdm.Web` 執行 `npm ci --ignore-scripts`、`npm run build`；回到根目錄執行 `node --test tests/ui-clarity.test.mjs`。
套件清單與鎖檔沿用本機既有版本，未升級依賴。

## 隔離瀏覽器驗證

1. 根目錄執行 `dotnet run --project tests/SWPdm.UiFixture/SWPdm.UiFixture.csproj -c Feedback`，API 固定監聽 `http://127.0.0.1:5010`。
2. 在另一個 PowerShell 的 `src/SWPdm.Web` 執行 `$env:VITE_PDM_API_URL='http://127.0.0.1:5010'`，再執行 `npm run dev -- --host 127.0.0.1 --port 5175 --strictPort`。
3. 開啟 `http://127.0.0.1:5175/documents`，驗證 1440×900、1024×768、768×1024、390×844 的清單與抽屜、空庫、搜尋無結果及錯誤重試。
4. 可用 `Invoke-RestMethod -Method Post http://127.0.0.1:5010/fixture/scenario/empty` 切換情境；切回 `normal` 後重新整理。其他情境：`fail-config`、`fail-search`、`fail-detail`（文件57）、`fail-relations`、`fail-checkout`、`fail-bom`、`conflict`。

fixture 使用唯一 SQLite 記憶體庫與暫存 Vault，不載入正式連線字串。種入60份合成文件，58～60已出庫、60有一個缺失參照；原版 API 最多回傳50筆，畫面須顯示非全部資料的提示，不得把50當成總數。關閉程序即捨棄資料。
原版查詢使用 PostgreSQL ILike 與時間排序；fixture 僅為 SQLite 增加搜尋契約轉接及時間值映射。這不驗證 PostgreSQL SQL 查詢，且不修改產品後端。
文件56下載回傳39-byte合成文字 `UI-QC.txt`，不是CAD；其他檔案無實體圖檔。取消出庫只使用原版單文件端點，測試不得呼叫正式環境。

完整結果與歷史／本次提交的證據邊界見 [前端QC](../ai-doc/qc/frontend-clarity-qc-2026-10-21.md)。
