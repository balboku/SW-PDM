# SW-PDM DEV 任務

更新日期：2026-08-04
文件狀態：DEV-001／DEV-007 本機正式套用完成；遠端 release target 未設定

## 總任務清單

- ✓ DEV-001 [交付點] [實作完成] [P0] [本輪已完成] 試用人員回饋優化
  - 摘要：提供安全的改名入庫、可見的 2D/3D 雙向關聯、可包含工程圖的 Pack & Go，
    以及保留資料夾結構的 CAD 批次匯入。
  - 來源 ID：`SWPDM-TRIAL-FEEDBACK-20260729`
  - 父任務：無
  - 下一步：本機正式套用完成；若要遠端發版，需另提供部署目標與正式連線指標
  - 批次發版：`reports/local-release-2026-08-04.md`
  - 計入交付：是

- ✓ DEV-002 [開發點] [已完成] [P0] [本輪已完成] 入庫目標身分驗證
  - 摘要：將 Check-in 綁定目標文件 ID，允許改名但阻止品號或文件類型不符的檔案覆寫。
  - 來源 ID：`SWPDM-TRIAL-01`
  - 父任務：DEV-001
  - 下一步：已完成；不符身分 live 測試回傳 409，且目標文件仍維持出庫鎖
  - 計入交付：否

- ✓ DEV-003 [開發點] [已完成] [P0] [本輪已完成] 雙向工程圖關聯
  - 摘要：讓 Part 與 Assembly 明細可反查並顯示關聯 Drawing，包含檔名 fallback 狀態。
  - 來源 ID：`SWPDM-TRIAL-05`
  - 父任務：DEV-001
  - 下一步：已完成；Part 與 Assembly live fixture 均顯示反向 Drawing
  - 計入交付：否

- ✓ DEV-004 [開發點] [已完成] [P1] [本輪已完成] Pack & Go 包含工程圖
  - 摘要：在下載前顯示關聯工程圖，預設納入套件並允許使用者取消。
  - 來源 ID：`SWPDM-TRIAL-02`
  - 父任務：DEV-001
  - 下一步：已完成；含圖面 ZIP 比基準套件多 2 張 SLDDRW
  - 計入交付：否

- ✓ DEV-005 [開發點] [已完成] [P1] [本輪已完成] 安全批次匯入
  - 摘要：支援資料夾或多檔選取、相對路徑 staging、逐檔隔離處理與結果摘要。
  - 來源 ID：`SWPDM-TRIAL-03`
  - 父任務：DEV-001
  - 下一步：已完成；traversal、逐檔錯誤與暫存清理已驗證
  - 計入交付：否

- ◐ DEV-006 [QA/QC] [限制式通過] [P0] [本輪已完成] 試用回饋驗證
  - 摘要：驗證資料身分安全、關聯完整性、批次失敗隔離、建置與三種 viewport UI。
  - 來源 ID：`SWPDM-TRIAL-QA-20260729`
  - 父任務：DEV-001
  - 下一步：若進入 release，再補受控資料 mutation 的正向 Check-in 與 mixed-result batch fixture
  - 計入交付：否

- ✓ DEV-007 [交付點] [實作完成] [P0] [本輪已完成] 出庫後另存為新料號
  - 摘要：品號變更時建立新文件身分、保留來源稽核關聯，並禁止覆寫原文件與歷史 BOM。
  - 來源 ID：`SWPDM-PART-NUMBER-BRANCH-20260804`
  - 父任務：無
  - 下一步：本機 migration 與 production-mode smoke 已完成；遠端發版需另提供部署目標
  - 證據：`decisions/ADR-001-part-number-branch.md`、`qc/part-number-branch-qc-2026-08-04.md`
  - 批次發版：`reports/local-release-2026-08-04.md`
  - 計入交付：是

## DEV-001：試用人員回饋優化

狀態：實作完成；QC 限制式通過
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Medium；影響多檔、入庫資料身分及跨模組 API，但不需要 schema migration。

### 任務目標

工程人員可以：

1. 將已改名、但品號與類型正確的 CAD 檔安全入庫為目標文件的新版本。
2. 從 Part／Assembly 明細看到反向關聯工程圖及匹配方式。
3. 在 Pack & Go 前確認是否包含關聯工程圖。
4. 從同一資料夾批次匯入零件、組合件與工程圖，並看到逐檔成功／失敗結果。

### Current Phase RD Handoff Contract

#### Scope

- `IngestCadFileRequest` 新增可選 `TargetDocumentId`，維持舊用戶端相容。
- Check-in 必須由後端核對目標文件 ID、PartNumber、DocumentType 及 checkout lock。
- 新增唯讀 relations API；不修改現有 BOM schema。
- Pack & Go 以既有 package closure 為主，再反查目前版本的 Drawing。
- 新增同步 multipart 批次匯入；每個檔案使用獨立 DI scope／DB transaction。
- 前端沿用既有元件、色彩與明細側欄，不建立新設計系統。

#### Out of Scope

- production migration、既有資料批次修復、背景 queue、排程 hot-folder watcher。
- SolidWorks 內部參照改寫與壓縮檔碰撞命名策略重構。
- release、部署、PR、commit 或 push。

#### API / Transaction Contract

- `POST /api/ingest/cad`：`targetDocumentId` 為 optional；提供時身分不符回傳 `409`。
- `GET /api/documents/{documentId}/relations`：回傳 references、drawings、whereUsed。
- `GET /api/assemblies/{rootVersionId}/check-updates`：加回關聯 Drawing 預覽。
- `GET /api/assemblies/{rootVersionId}/download-zip`：新增 `includeDrawings`，預設 `false`
  以維持舊 API 相容；新 UI 預設傳 `true`。
- `POST /api/ingest/cad-batch`：multipart、最多 200 個 CAD、總量最多 1 GiB；
  每檔交易隔離，單檔失敗不回滾其他成功檔。
- 不新增資料表或 migration。

#### Failure Recovery

- Check-in 身分不符：不寫入版本、不解除目標文件鎖定，UI 顯示重新選檔。
- relations 載入失敗：不影響單檔下載或入出庫，UI 提供重新載入。
- batch 單檔失敗：保留其他成功結果，回應中列出檔名與錯誤。
- batch staging 一律限制在伺服器暫存根目錄；完成後清除。

#### Stop Conditions

- 發現必須更改正式 schema 或修復正式資料。
- 現有 API／文件出現無法相容的身分或版次語意衝突。
- 需要跳過 checkout lock 或允許品號不符覆寫。
- 使用者既有 dirty 檔案與本輪修改發生重疊。

### UX Intent

- 使用者：熟悉 CAD、但不應理解資料庫方向性的工程人員。
- 主要任務：安全入庫、看懂關聯、取得完整設計套件、一次匯入成套檔案。
- 成功狀態：畫面能指出處理數量、關聯狀態、下一個 CTA 與失敗檔案。
- 最可能誤解點：檔名警告被理解為禁止；3D 空白被理解為沒有 2D；批次部分成功被理解為全部成功。
- 高風險操作：建立新版本與批次寫入。
- 安全預設：Check-in 綁定文件 ID；Pack & Go 預設包含已找到 Drawing；
  batch 送出前顯示 CAD 數量與類型。
- 不能發生的誤操作：用另一品號檔案覆寫目前文件、路徑穿越 staging、隱藏批次失敗。

### 操作模式矩陣

| 模式 | 來源 | 目標 | 範圍 | 安全策略 | 狀態 | 主要 CTA | 責任 |
|---|---|---|---|---|---|---|---|
| Check-in | 單一 CAD | 已出庫文件 | 單一新版本 | 文件 ID＋品號＋類型＋鎖 | idle/error/success | 確認入庫 | 操作者 |
| 單檔入庫 | 單一 CAD | 新文件或已鎖定文件 | 單檔 | PartNumber 必填＋鎖 | parse/error/success | 直接入庫 | 操作者 |
| 批次匯入 | 資料夾／多檔 | 多文件 | 最多 200 檔 | 相對路徑、逐檔交易、結果摘要 | preview/error/partial/success | 匯入 N 個檔案 | 操作者 |
| Pack & Go | 系統版本 | ZIP | BOM＋可選 Drawing | 下載前預覽與勾選 | preview/download | 下載原簽入版本 | 操作者 |

### 驗收標準

- [ ] 改名檔案若目標文件 ID、PartNumber、DocumentType、checkout owner 均正確，可建立新版本。
  實作與編譯已完成；為避免修改既有試用資料，本輪未送出會建立新版本的正向 live Check-in。
- [x] 任一身分不符時回傳 `409`，不建立版本、不解除鎖。
- [x] Part 與 Assembly 明細均能顯示關聯 Drawing；沒有關聯時提供可執行下一步。
- [x] Pack & Go UI 顯示 Drawing 數量／名稱，勾選後 ZIP 查詢包含其目前版本。
- [x] 批次匯入保留相對路徑、拒絕 traversal、限制檔案數與總量。
- [ ] batch 部分失敗時回應成功／失敗數與逐檔原因。
  逐檔失敗回應及 staging 清理已 live 驗證；未以真實 CAD 製造一成一敗的 mixed-result 資料 mutation。
- [x] `dotnet build SWPdm.sln` 與前端 `npm run build` 通過。
- [x] `/documents`、`/ingest` 在 1440×900、1024×768、390×844 無明顯裁切或非預期水平 overflow。
- [x] 受影響頁面沒有可見 HTTP／API／Not Found／Internal Server Error。

### QA FMEA

| 失效模式 | 可能原因 | 使用者影響 | 偵測方式 | 優先級 | 對策 / 建議測試 |
|---|---|---|---|---|---|
| 錯檔覆寫 | 只依檔名或 PartNumber | 版本歷史污染 | targetDocumentId 負向測試 | P0 | 品號、類型、ID、鎖均須一致 |
| 2D 漏包 | 只沿 BOM 向下 | 現場缺工程圖 | package 查詢與 ZIP 清單 | P0 | 反查 Drawing current version |
| 關聯重複 | 多版本 Drawing 命中 | ZIP／UI 重複 | 依 DocumentId 去重 | P1 | 只取 CurrentVersionId |
| 批次半成品被隱藏 | 單檔例外中止 | 使用者誤以為全數成功 | mixed-result fixture | P0 | 每檔隔離並列出失敗 |
| 路徑穿越 | 惡意 relativePath | 覆寫暫存區外檔案 | `../` 負向測試 | P0 | FullPath 邊界檢查 |
| 手機版按鈕擠壓 | 長檔名／結果清單 | 無法完成操作 | 390×844 截圖 | P1 | wrap、truncate、可捲動清單 |

### QA / QC 證據

- 後端：solution build、API route／靜態契約檢查。
- 前端：TypeScript/Vite build、lint（若既有 lint 基線允許）。
- UI：實際瀏覽器三 viewport、Check-in modal、Pack & Go modal、Upload batch panel 截圖。
- Visible Error Sweep：`/documents`、`/ingest`。
- Git：回報本輪檔案與排除的使用者既有變更。

### Future Phase Capsule

- 大型批次背景 queue／hot-folder watcher。
- 恢復條件：同步請求超時、批次量經常超過 200 檔，或需要無人值守匯入。
- 未要求 production release；狀態為 `Future Phase Captured / Not Requested`。

### 規格治理結果

- Authoritative source：本文件與 `docs/assembly-download-package-flow.md`。
- API 均為 additive、舊參數預設維持原行為；不需要 migration。
- ADR 不建立：此次不改主資料唯一性或 schema，實作局部且可逆。
- 高影響 deferred scope 已以 Future Phase Capsule 保存。

## DEV-007：出庫後另存為新料號

狀態：實作完成；QC 限制式通過（既有前端 lint 工具缺口）
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Medium；影響主資料身分、本地 schema、Check-in API 與高風險 UI 分流。

### 任務目標

當出庫檔案的 CAD 品號與原文件不同時，工程人員可以明確選擇「另存為新料號」，
建立可追溯的新文件，而不污染原文件歷史或自動改寫既有 BOM。

### Current Phase RD Handoff Contract

#### Scope

- 保留一般 Check-in 的品號／類型／文件 ID 驗證。
- `409` 回應提供結構化舊／新品號，讓 UI 顯示安全分流。
- 明確確認後，以單一資料庫交易建立新品號文件、版本與來源稽核關聯。
- 成功後解除原文件出庫鎖；原版本、生命週期與 BOM 不變。
- 分支不遞迴入庫參照 CAD，避免順帶建立其他文件的新版本。
- UI 顯示來源、目的、關聯工程圖與 where-used 數量，原因必填。
- 文件關聯 API 可查詢品號變更來源與衍生文件。

#### Out of Scope

- 自動改寫 SolidWorks 內部參照、父組立 BOM 或既有工程圖。
- 自動將原品號標記 Obsolete／Superseded。
- 品號輸入錯誤的管理員更正流程、核准工作流與正式環境 migration。
- release、部署、commit、push。

#### API / Data / Transaction Contract

- `POST /api/ingest/cad` 增加可選 `createNewDocumentForPartNumberChange`，預設 `false`。
- 只有目標文件已由同一操作者出庫、類型相同、品號確實不同、原因非空白、且新品號
  未被同類型文件使用時可建立。
- 新增 `pdm_document_identity_changes`，保存 source document/version、target document、
  old/new part number、reason、actor、timestamp；target document 僅能有一筆來源。
- 分支、版本、稽核關聯與原鎖解除必須在同一交易中完成；失敗時全部回滾。
- `GET /api/documents/{documentId}/relations` 增加 `identityOrigin` 與 `derivedDocuments`。

#### Failure Recovery / Stop Conditions

- 一般 mismatch：不寫資料、不解除鎖，UI 提供重新選檔或另存新品號。
- 建立新品號失敗：整筆交易回滾並保留原鎖，允許修正原因或重新嘗試。
- 文件類型改變、同品號、鎖持有人不符或新品號已存在：禁止確認動作。
- 若實作需要自動改寫歷史 BOM、遠端 migration 或正式資料修復，立即停止。

### UX Intent / Now What

- 使用者心智模型：同一設計修改後若成為新品號，是從原設計衍生的新文件。
- 主要 CTA：`另存為新料號`；一般錯檔仍可 `重新選擇檔案`。
- 狀態首句：`此檔案品號已變更，不能覆寫原文件。`
- 影響預覽：舊／新品號、文件類型、關聯工程圖數、where-used 數、原文件處置。
- 安全預設：不自動分支；原因未填、類型不符或新品號重複時按鈕 disabled。
- 成功：指出新文件 ID 與新品號，說明原文件歷史及 BOM 未變更。

### 驗收標準

- [x] mismatch 的 `409` 含舊／新品號及允許動作，且原文件維持鎖定。
- [x] 明確選擇分支後建立新的 DocumentId／Version 1／WIP，並寫入完整稽核關聯。
- [x] 分支成功後原文件解鎖，原版本、狀態與 BOM 不變。
- [x] 同品號、不同類型、非出庫持有人、空白原因與重複新品號均被拒絕。
- [x] 關聯 API 可從新文件查來源、從原文件查衍生文件。
- [x] UI 先顯示可執行下一步與影響摘要，不顯示裸露 HTTP／API 錯誤。
- [x] `dotnet build SWPdm.sln` 與前端 production build 通過。
- [ ] 前端 lint：現有專案未宣告／安裝 ESLint 執行檔及 config 相依套件；已記錄工具缺口。
- [x] `/documents` 的分流 modal 在 1440×900、1024×768、390×844 可操作且無 overflow。

### 規格治理結果

- Authoritative decision：`decisions/ADR-001-part-number-branch.md`。
- 與既有 mismatch 規則為 Compatible exception：只有明確旗標與完整前置條件才分支。
- 本輪需 migration，但不執行正式環境 migration；沒有產品決策 blocker。
- 品號更正核准流程為 Future Phase Captured / Not Requested；恢復條件是使用者要求處理
  既有主資料輸入錯誤，而非設計衍生新品號。

## 變更紀錄

- 2026-07-29：依試用回饋建立 DEV-001～DEV-006 與本輪 RD/QA/QC 契約。
- 2026-07-29：完成 DEV-002～DEV-005；DEV-006 以不修改既有試用資料為前提限制式通過。
  詳細證據見 `qc/trial-feedback-qc-2026-07-29.md`。
- 2026-08-04：建立 DEV-007 與 ADR-001，進入新品號文件分支實作。
- 2026-08-04：完成 DEV-007；交易、資料不變量、API、建置及三種 viewport QC 通過，
  詳細證據見 `qc/part-number-branch-qc-2026-08-04.md`。
- 2026-08-04：DEV-001／DEV-007 已正式套用至本機運行環境；完成備份、Release build、
  migration 冪等檢查與 production-mode smoke，證據見 `reports/local-release-2026-08-04.md`。
