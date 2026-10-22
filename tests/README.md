# 隔離驗證

## 2026-10-22：DEV-012 入庫核心

契約與結果見 [DEV-012](../ai-doc/dev_task.md)、
[入庫完整性 QC](../ai-doc/qc/ingest-integrity-qc-2026-10-22.md)。
下方既有取消出庫／Reused 測試保留；明確目標的 WIP 改名現在需產生 CreatedVersion，不能當成 Reused。

- 清除 SWPDM_CONTRACT_PG 後執行同一 ContractTests 專案，使用 SQLite 記憶體庫。
- PostgreSQL 使用下節嚴格限定的 55433 contract 臨時叢集，逐例 EnsureCreated 新庫；不執行 migration。
- 原生驗證：tests/SWPdm.NativeIngestionVerifier 使用明示 SWPDM_DEV012_DM_KEY，
  唯一參數為系統 TEMP 下新建 swpdm_dev012_native_<GUID> 目錄，真實 CAD 副本放在 cad/。
  必須同時包含 Part／Assembly／Drawing；有原生 interop、COM 註冊及有效 Document Manager 授權才可執行。
  指令：dotnet run --project tests/SWPdm.NativeIngestionVerifier -- <測試目錄>。
  解析唯讀，參照與旗標由獨立原生 API 比對。內容更新只修改另外建立的私有測試副本；
  入庫資料在 SQLite 記憶體庫，Vault 在該唯一測試目錄內，不讀取正式連線設定。
- 無 interop 防偽成功驗證：dotnet run --project tests/SWPdm.NativeIngestionVerifier -c NoNative
  -p:SolidWorksDocumentManagerDll=C:/SWPDM-TEST-NO-INTEROP.dll -- --verify-no-native。
  此結果是拒絕假解析的測試，不是原生 CAD 驗證通過。
- 不使用舊 IntegrationVerifier／PostgresIntegrationTests：它們包含 migration，不屬本輪。
- 本輪不改前端介面，13 項前端邏輯測試只作既有契約回歸，不宣稱 UI／跨工作站 CAD 驗收。

## 2026-10-22：取消出庫／Reused 契約切片

只包含單文件取消出庫及明確目標 Check-in 的 Reused；不包含其他 pending 後端、migration 或批次復原。

- `dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj`：真實 HTTP 路由＋SQLite 記憶體庫，CAD 解析為合成替身，Vault 為本次唯一暫存資料夾。
- `node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs`：原前端邏輯回歸及根文件 outcome 分流。
- 若需重現 PostgreSQL 驗證，先以本機 PostgreSQL 16 的 `initdb` 建立全新臨時叢集（專用使用者 `contract`，僅監聽 `127.0.0.1:55433`），不能使用原 5432 或歷史 clone。設定 `SWPDM_CONTRACT_PG=Host=127.0.0.1;Port=55433;Database=postgres;Username=contract;Pooling=false` 後跑相同測試。測試器拒絕其他 host／port／user／admin DB；每例建立並移除自己的 `swpdm_contract_<GUID>` 測試庫。這不是正式 migration 驗證。
- 下方 UI fixture 已改用本切片的真實取消／入庫服務；`GET /health` 的 `noopUploadPath` 指向測試器產生的合成 CAD 文字檔，搭配文件58可重現 Reused。
- 新增情境 `fail-cancel-preview`、`stale-cancel`（在確認前模擬鎖定時間改變）；回到 `normal` 可重新檢查。僅適用 fixture5010。
- 日後正式套用必須前後端成套進行：原無本文 POST 會被安全拒絕，本輪沒有相容性降級解鎖。

## 前端清晰度歷史驗證

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
文件56下載回傳39-byte合成文字 `UI-QC.txt`，不是CAD；文件58有合成上傳文字檔，其餘無實體圖檔。取消出庫使用本切片受保護的單文件端點，測試不得呼叫正式環境。

完整結果與歷史／本次提交的證據邊界見 [前端QC](../ai-doc/qc/frontend-clarity-qc-2026-10-21.md)。
