# 原工作區 52 項變更與 main 比對

盤點起始：2026-10-28；文件整理完成：2026-10-29（Asia/Taipei）。檔名保留起始日期。
狀態：分類與任務／驗收清單更新完成；僅文件，未實作或發布產品。
2026-10-30 文件 commit／push 的授權邊界另見文末；下方「本輪」未特別標日期者仍指 10/28～29 盤點。

## 基準與執行邊界

- 原工作區：`C:/Users/user/Documents/GitHub/SW-PDM`，分支 `codex/trial-feedback-20261016`，HEAD `3d36d1ca142959cd4dcf95b7a7f4f83c907f8752`。
- 比對基準：本機與 GitHub `main` 均為 `e218fdd5324b62a5f8a0540b55df026870f72364`；本機 main 在 `C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM`，開始盤點時乾淨。
- 已發布產品來源 `14de0f58717cfa58c274efa9e72e6ca475c4e4da`，後續 e218fdd 僅發版文件／證據。DEV-012～015 已提交、push、合併及本機套用，不重開這些發布待辦。
- 52 是 `git status --porcelain=v1` 的路徑項目，不是 52 個功能／故障。以 `git ls-files --others --exclude-standard` 展開其中目錄，合計 105 個 Git 可見檔案；不含被忽略的 DB、Vault、bin／obj 或私有預覽資料。
- 判定同時比較 bytes SHA-256、忽略 CRLF／LF 的文字差異、服務替代關係、API／模型契約及 main 的既有 QC 證據；不是只比較檔名。
- 本輪只改 `ai-doc/dev_task.md`、`ai-doc/documentation_map.md` 並新增本報告／雜湊清單。其餘原程式、migration、測試、截圖與歷史記錄不刪除、不覆寫、不合併。
- 未執行產品 build／tests／verifier、未啟停服務、未連資料庫或讀寫 Vault；不執行 migration，不改歷史 BOM。舊 verifier 的預設 migration 路徑尤其不能當成唯讀檢查。
- 未 stage、commit、push、切分支或修改 main。更新文件不代表核准下列未實作事項。

## 分類摘要

| 分類 | 路徑項目數 | 含義 |
|---|---:|---|
| 已取代 | 30 | 相同內容已在 main，或有已驗證的新實作取代。保留原檔不等於需要再次合併。 |
| 需保留 | 5 | 協作規則、原始決策、歷史測試／失敗與設計證據。不是待部署產品。 |
| 尚待處理 | 17 | 含本輪已更新的兩份文件，及仍有未納入片段／不相容模型／混合測試的 15 個路徑項目；不是 17 個待修 bug。 |

有 9 個單檔＋1 個含 20 檔的目錄與 main bytes 完全相同（合計 29 檔）；另有 4 個單檔僅 CRLF／LF 不同。
各項都是「本輪前」分類，02／03 的文件整理本輪已完成；其餘仍維持原 bytes。
建議保留新 main 的安全契約，僅對明確尚缺的片段另做最小實作，不整包移植原稿。

使用思考習慣：#差距分析、#證據品質、#變數控制

## 52 項逐項對照

`M` 及 `??` 的原狀態與逐檔 SHA 見 [盤點基線 TSV](../qc/wip-main-comparison-2026-10-28.tsv)。編號是本次清單列號，不是新增 DEV。

| 編號 | 原路徑 | 分類 | 比對結果／處置理由 |
|---|---|---|---|
| 01 | `SWPdm.sln` | 已取代 | 舊 solution 只新增 SWPdm.Tests 註冊；main 改以獨立 ContractTests／Native 驗證專案與明確命令執行，不移植過期測試註冊。 |
| 02 | `ai-doc/dev_task.md` | 尚待處理 | 文件落後於 main；本輪只更新最新狀態、驗收對照與分類入口，保留歷史記錄。 |
| 03 | `ai-doc/documentation_map.md` | 尚待處理 | 冷啟動入口落後；本輪追加目前 main 與下一步，不刪歷史入口。 |
| 04 | `src/SWPdm.Api/Configuration/SolidWorksDocumentManagerServiceFactory.cs` | 已取代 | DEV-012 已用 ICadIngestionSessionFactory 分離唯讀解析；DEV-014 另有套件 session，不能回復混合介面。 |
| 05 | `src/SWPdm.Api/Contracts/BatchIngestCadResponse.cs` | 已取代 | 與 main SHA-256 完全相同；Outcome 契約已納入。 |
| 06 | `src/SWPdm.Api/Contracts/IngestedFileResponse.cs` | 已取代 | 與 main SHA-256 完全相同；逐檔 Outcome 已納入。 |
| 07 | `src/SWPdm.Api/Contracts/SolidWorksParseResponse.cs` | 已取代 | 與 main SHA-256 完全相同；typed references 回應已納入。 |
| 08 | `src/SWPdm.Api/Endpoints/DocumentEndpoints.cs` | 尚待處理 | 搜尋／鎖服務已由 DEV-013／015 取代；舊 package-preview 與 expectedVersions 契約尚未納入，須另議完整預覽（見下表）。 |
| 09 | `src/SWPdm.Api/Endpoints/EndpointHelpers.cs` | 已取代 | main 保留巢狀 PG 競爭辨識，並增加套件 code／issues 與 LOCK_CHANGED；舊公開 helper 不需移植。 |
| 10 | `src/SWPdm.Api/Endpoints/IngestEndpoints.cs` | 已取代 | 與 main SHA-256 完全相同；批次 Outcome 回應已在 main。 |
| 11 | `src/SWPdm.Api/Program.cs` | 尚待處理 | DI 已被新版替換，但舊稿 Cors:AllowedOrigins 可設定行為未納入；目前 main 仍列固定三個 origin，跨網段需求另確認。 |
| 12 | `src/SWPdm.Api/Services/PdmIngestionService.cs` | 已取代 | DEV-012 已完成相同內容沿用／WIP 同 Rev／typed BOM，另增加失敗清理、owner 驗證與 RootOutcome；舊版不可回蓋。 |
| 13 | `src/SWPdm.Sample/Data/Entities/PdmBomOccurrence.cs` | 尚待處理 | 多出的 ReferenceMatchMethod／ReferenceIssue 欄位不在 main；是否持久保存匹配證據尚未決策，不等於現行缺失未提示。 |
| 14 | `src/SWPdm.Sample/Data/Migrations/PdmDbContextModelSnapshot.cs` | 尚待處理 | 舊快照含額外 BOM 欄位、舊批次模型；不能蓋掉 main 新快照，與 45／46 一併決定。 |
| 15 | `src/SWPdm.Sample/Data/Models/PdmBomLinkData.cs` | 尚待處理 | DTO 多出兩個持久欄位；main 異常快照不依賴它們，需先確認新增資料契約必要性。 |
| 16 | `src/SWPdm.Sample/Data/Models/PdmDocumentDetails.cs` | 已取代 | 舊 DTO 增加 CheckedOutBy／At；main 已用搜尋／integrity 及操作預覽取得鎖，不需只為欄位相異移植。 |
| 17 | `src/SWPdm.Sample/Data/PdmDbContext.cs` | 尚待處理 | 舊 EF mapping 同時引入 BOM 欄位與舊批次定義；main 已採 snake_case／Restrict／複合索引，禁止混用。 |
| 18 | `src/SWPdm.Sample/Data/Repositories/PdmRepository.cs` | 已取代 | 唯讀 BOM／不猜歷史連結已在 DEV-012；舊額外欄位與明細鎖投影已由新版讀取途徑替代，持久欄位決策見 13。 |
| 19 | `src/SWPdm.Sample/Services/SolidWorksDocumentManagerService.cs` | 已取代 | typed parser 的解析內容已整合；舊 RewriteReferences 混入 ingestion service，main 改用 SolidWorksPackageSession 精確重寫與搬移複驗。 |
| 20 | `src/SWPdm.Web/src/App.tsx` | 已取代 | 與 main SHA-256 完全相同；共用前端入口已發布。 |
| 21 | `src/SWPdm.Web/src/components/BatchUploadPanel.tsx` | 尚待處理 | main 未使用 batch files[].outcome，成功列僅顯示品號／版本；舊稿的新增／沿用標示仍是待補 UX，不能原樣套用未知 outcome 為新增。 |
| 22 | `src/SWPdm.Web/src/components/BomTreeView.tsx` | 已取代 | 舊缺失提示已被 DEV-015 的虛擬／抑制／安全來源檔名標示取代，避免虛擬件誤報缺檔。 |
| 23 | `src/SWPdm.Web/src/components/CheckInModal.tsx` | 尚待處理 | Reused 成功流程已被新版取代；舊稿依目標類型限制副檔名、WIP／原因說明尚未納入 main，屬前置提示缺口，非後端類型驗證失效。 |
| 24 | `src/SWPdm.Web/src/components/CheckOutModal.tsx` | 已取代 | DEV-015 已補預覽 documentId 驗證、where-used 不計鎖、batch 結果回傳；舊 modal 不移植。 |
| 25 | `src/SWPdm.Web/src/components/ui.tsx` | 已取代 | 與 main SHA-256 完全相同；共用 UI／焦點改善已發布。 |
| 26 | `src/SWPdm.Web/src/index.css` | 已取代 | 與 main SHA-256 完全相同；樣式改善已發布。 |
| 27 | `src/SWPdm.Web/src/lib/api.ts` | 尚待處理 | 新版 actor／time／version 與批次契約已取代；舊 package-preview／expectedVersions 尚未納入，不能單獨搬此 client。 |
| 28 | `src/SWPdm.Web/src/pages/Dashboard.tsx` | 已取代 | 與 main SHA-256 完全相同；設定不冒充健康狀態的前端已發布。 |
| 29 | `src/SWPdm.Web/src/pages/Documents.tsx` | 尚待處理 | 完整查詢、異常及批次 UI 已取代大部分；舊「實際套件預覽／確認」與新增依賴選版入口未納入，須和 8／27／38 成套評估。 |
| 30 | `src/SWPdm.Web/src/pages/Upload.tsx` | 已取代 | 與 main SHA-256 完全相同；匯入入口已發布。 |
| 31 | `AGENTS.md` | 需保留 | 只在原工作區存在的專案核准偏好；保留並遵守，不視為常駐刪除授權，也不自行提交。 |
| 32 | `ai-doc/decisions/ADR-002-trial-feedback-integrity.md` | 需保留 | 舊範圍與解析器／唯讀預覽決策歷史；新入庫／批次／套件／Vault 以 main 切片契約為準，不讓舊整包規格覆蓋新契約。 |
| 33 | `ai-doc/qc/frontend-clarity-2026-10-21/`（20 檔） | 已取代 | 目錄 20 個 Git 可見檔案全與 main bytes 相同；歷史截圖已攜入，不需重複合併。 |
| 34 | `ai-doc/qc/frontend-clarity-qc-2026-10-21.md` | 已取代 | main 已保留原始報告並補獨立提交／重新驗證章節；舊副本不應回蓋新版。 |
| 35 | `ai-doc/qc/trial-feedback-2026-10-16/`（15 檔） | 需保留 | 15 個早期 TRX／CAD 開啟／UI 截圖未在 main；需保留歷史失敗及限制證據，不算新版測試結果。 |
| 36 | `ai-doc/qc/trial-feedback-qc-2026-10-16.md` | 需保留 | 資料清空、工具拒絕與 3 筆旗標／候選的歷史紀錄；需保留，不能依舊 ID 修復新資料。 |
| 37 | `scratch/ui-clarity-review-20261021/`（10 檔） | 需保留 | 10 個早期檢視截圖，main 無對應；保留設計脈絡，不當作最新版驗收證據。 |
| 38 | `src/SWPdm.Api/Services/CadPackageService.cs` | 尚待處理 | 安全出包核心已由 DEV-014 取代；舊 PlanAsync 用於完整預覽的公開能力尚未納入。舊核心漏抑制件、拒絕同名且無新版 SHA／全組態護欄，不可搬回。 |
| 39 | `src/SWPdm.Api/Services/CadReferenceResolver.cs` | 已取代 | main 已取消檔名候選自動關聯／多同內容版本選最新；舊 resolver 仍有上述推斷，已被較嚴格 DEV-012 取代，禁止回蓋。 |
| 40 | `src/SWPdm.Api/Services/CadRepairPreview.cs` | 尚待處理 | 純唯讀歷史差異比較器不在 main；候選／虛擬旗標證據分級可供未來工具重用，但須先適配最新模型與新隔離資料。 |
| 41 | `src/SWPdm.Api/Services/CheckoutService.cs` | 已取代 | 整合式 CheckoutService 已由 CancelCheckoutService＋CheckoutBatchService 取代；舊版自動選最近批次、單檔缺版本條件，不移植。 |
| 42 | `src/SWPdm.Api/Services/PdmWorkflowException.cs` | 已取代 | 與 main 正規化換行後相同；僅 CRLF／LF 差異，不是新功能。 |
| 43 | `src/SWPdm.Api/Services/VaultSearchService.cs` | 已取代 | VaultSearchService 已由 VaultQueryService 取代，包含全結果摘要、穩定分頁、屬性來源、抑制異常與唯讀快照。 |
| 44 | `src/SWPdm.Sample/Data/Entities/PdmCheckoutBatch.cs` | 已取代 | 批次 entity 已由 DEV-013 取代；main VersionId 非空，舊版為可空，不能混用模型。 |
| 45 | `src/SWPdm.Sample/Data/Migrations/20261016024333_TrialFeedbackIntegrity.Designer.cs` | 尚待處理 | 舊 Designer 對應未採用 migration；含不相容批次映射及額外 BOM 欄位，禁止執行或回蓋新 Designer。 |
| 46 | `src/SWPdm.Sample/Data/Migrations/20261016024333_TrialFeedbackIntegrity.cs` | 尚待處理 | 舊 migration 會重建相同批次表名，使用 PascalCase 欄位／Cascade，且加兩個 BOM 欄；不能在已採 AddCheckoutBatches 的結構上重播。 |
| 47 | `src/SWPdm.Sample/Services/CadPropertyResolver.cs` | 已取代 | 與 main 正規化換行後相同；屬性解析已整合。 |
| 48 | `src/SWPdm.Sample/Services/CadReference.cs` | 已取代 | 與 main 正規化換行後相同；typed CadReference 已整合。 |
| 49 | `src/SWPdm.Sample/Services/ICadDocumentSession.cs` | 已取代 | 舊 Parse＋Rewrite 合併介面已由 ingestion／package 兩種 boundary 取代，不移植。 |
| 50 | `src/SWPdm.Web/src/components/UndoCheckoutModal.tsx` | 已取代 | 已由新版 CancelCheckoutModal 取代；新版要求明確批次、actor／time／version，舊 modal 不移植。 |
| 51 | `src/SWPdm.Web/src/lib/ui-model.ts` | 已取代 | 與 main 正規化換行後相同；共用 UI helper 已發布。 |
| 52 | `tests/`（12 檔） | 尚待處理 | 12 個可見檔案混合已取代回歸與未移植修復預覽工具，逐檔細分如下；default verifier 會執行 migration，禁止本輪啟動。 |

## 第 52 項 tests/ 的逐檔處置

| 原檔案（相對 tests/） | 分類 | 判定 |
|---|---|---|
| Fixtures/wrong-upload.sldprt | 需保留 | 123-byte 錯檔負向 fixture，不是真實 CAD 成功證據；目前 main 未攜入。 |
| README.md | 已取代 | main 已有 ContractTests／Native／UI 的新版命令與隔離邊界。 |
| SWPdm.IntegrationVerifier/Program.cs | 尚待處理 | 舊 clone 55432 護欄與預設 migration／up-down-up；只能靜態參考，不能接 main 執行。 |
| SWPdm.IntegrationVerifier/ReferenceAudit.cs | 需保留 | 舊原生比對 oracle 可作歷史分析參考；新版原生驗證器已分工，不當現行可執行入口。 |
| SWPdm.IntegrationVerifier/RepairPreviewRunner.cs | 尚待處理 | 唯讀預覽 runner 未移植；需獨立無 migration 的入口、最新模型及私有測試，不能只複製舊專案。 |
| SWPdm.IntegrationVerifier/SWPdm.IntegrationVerifier.csproj | 尚待處理 | 依賴舊 adapter／模型的整合 runner；不註冊到 main solution。 |
| SWPdm.Tests/PostgresIntegrationTests.cs | 已取代 | 新 ContractTests 已有隔離 PG／競爭與批次證據；舊 migration 測試不是待執行正式 migration。 |
| SWPdm.Tests/SWPdm.Tests.csproj | 已取代 | 舊測試接線由新版獨立專案取代；不將舊專案整包引入。 |
| SWPdm.Tests/TrialFeedbackTests.cs | 尚待處理 | 大部分回歸已取代；其中 repair-preview 純比較測試及舊 package-preview 斷言，僅在對應能力另獲實作指示時挑選改寫。 |
| SWPdm.UiFixture/Program.cs | 已取代 | main 使用真實 VaultQuery／批次服務，舊 fixture 不可回蓋。 |
| SWPdm.UiFixture/SWPdm.UiFixture.csproj | 已取代 | main 已有新版 UI 驗證接線。 |
| ui-clarity.test.mjs | 已取代 | main 已含此測試內容及額外 outcome／Vault 測試。 |

## 尚缺片段、優先順序與進入條件

這是現有 DEV 下的差距／未要求的後續 capsule，不新增產品交付點、不改既有完成率。這輪只記錄，不實作。

| 優先 | 歸屬 | 尚缺片段 | 處置／進入條件 | 後續驗收方向 |
|---|---|---|---|---|
| 1 | DEV-015／DEV-006 | 批次成功結果沒有明確顯示 CreatedVersion／Reused | 已確認 main UI 未讀 outcome；後續若要求修正，沿用 main client，不複製舊二分法 | 新增／沿用／未知結果各有清楚提示；沿用不被當成升版或解鎖，兼測真 CAD 一成一敗。 |
| 2 | DEV-015／DEV-012 | 修改後入庫的目標類型前置提示、WIP 同 Rev／變更原因說明 | 後端有類型／原因防護，不列資料覆寫 bug；後續只補 UX，不改入庫政策 | 錯類型選檔立即引導；正確類型正常；Reused／CreatedVersion／新品號分流保持。 |
| 3 | DEV-014／DEV-015 | 選版後「實際完整套件」預覽、變更後再確認與新依賴的選版入口 | main 下載時完整重算／阻擋已完成，但目前 check-updates 不是完整 PlanAsync 預覽。需另外確認新增公開預覽契約；不能採舊 PlanAsync／expectedVersions 當成原生完整性已通過 | 根／子組立換版造成依賴新增／移除、選版後再預覽、預覽後狀態改變、畫面與實際 ZIP／manifest 一致；不放寬 SHA／所有組態／碰撞／搬移重開護欄。 |
| 另行決策 | DEV-012／DEV-015 | 唯讀修復比較器／runner、ReferenceMatchMethod／Issue 持久欄位 | main 的資料庫快照異常提示已完成；新原生修復預覽工具及持久欄位是兩項不同決策。僅在新資料需審查時恢復，不沿用清空前 ID，不自動修歷史；任何 schema 需另確認 | 新隔離資料下多候選／旗標／組態／DB-only 都不自動修復；無 migration 的獨立唯讀入口；正式資料／原檔不變。 |
| 有需求再排 | DEV-011 | CORS origins 從設定讀取 | 目前固定 origins 的既有 LAN 已有通過紀錄，不當成現存故障。新增網址／網段時再確認 allowlist | 允許與拒絕的 origins、空設定／預設行為；不能改為任意來源。 |
| 本輪完成 | DEV-006 與文件入口 | 舊狀態／驗收待辦收斂 | 最新任務與驗收對照見 dev_task；不以檔案數估算完成率 | 52 項一一對應、分類總數與 SHA 可核對、兩份文件之外舊檔不變。 |

使用思考習慣：#真正問題、#差距分析、#限制條件

### 舊 migration 的具體風險

- 原稿 `20261016024333_TrialFeedbackIntegrity` 新增 `reference_match_method`／`reference_issue`，並建立 `pdm_checkout_batches` 與 `pdm_checkout_batch_items`。
- main 的 `20261022073552_AddCheckoutBatches` 已建立同名兩表，但採 `batch_id`／`root_document_id` 等 snake_case、非空 VersionId、Restrict 外鍵及 root＋created 查詢索引。
- 舊稿採 PascalCase 欄位、可空 VersionId、Cascade 外鍵與 root 單欄索引。直接套用會遇同名表／模型不相容；本輪未對資料庫試跑，也不將此風險宣稱已在 live 重現。
- 因此舊 migration／Designer／Snapshot 不能整包合併或重播。額外兩欄若未來確認需要，必須以當時 main 為基準另行設計，不因本報告而執行。

## 驗收清單與證據時點

| 驗收項 | 判定 | 證據／限制 |
|---|---|---|
| DEV-012 WIP 同 Rev、明確改名正向、共用件沿用、typed parser | 已有通過證據 | main 入庫 QC（10/22）；11 份真 CAD、41 項 SQLite／PG，不再沿用 7/29「完全未測」說法。 |
| DEV-013 安全批次／限定 migration／批次 UI | 已完成並本機套用 | 10/22 後端與限定 migration、10/26 UI／發布記錄；本輪不重跑。 |
| DEV-014 安全出包與副本參照搬移重開 | 已完成所列驗收 | 11 CAD／4 套件／22 重開／18 參照；不是完整 SolidWorks 幾何驗收。 |
| DEV-015 完整分頁／真實異常快照／批次 UI | 已完成並本機套用 | 10/26 SQLite／PG 各 134、前端 36、四種 viewport；快照 `nativeFileChecked=false`。 |
| 真 CAD 批次一成一敗＋實際前端結果連動 | 尚待驗證 | main 批次回歸已測兩筆成功（含 Reused），不能當作真 CAD mixed-result UI 證據。 |
| SolidWorks 強制重建／配合／工程圖、多組態／抑制／虛擬實例 | 尚待驗證 | Document Manager 重開或合成負向不等於完整涵蓋。 |
| 第二台實機 LAN 操作及套件開啟 | 尚待驗證 | 本機 LAN URL smoke 已有，不等於第二台工作站。 |
| 前端 lint | 工具缺口仍在 | package.json 有指令，eslint.config.js 有設定，但未宣告所需 ESLint 等套件；後續需相容性檢查，這輪不安裝。 |
| 實際 rollback／重啟恢復 | 未演練／未設定自動啟動 | 10/26 保留固定舊成品與腳本；本輪不停止服務或安排自動啟動。 |
| 完整 Auth／歷史資料修復 | 未要求的後續範圍 | actor 仍為自填；個人單人測試不擅自升格為多人正式系統。歷史 3 筆差異不是目前修復目標。 |

已讀取的 main 證據（固定 e218fdd，可供跨工作樹續接）：

- [已完成本機發布與限制](https://github.com/balboku/SW-PDM/blob/e218fdd5324b62a5f8a0540b55df026870f72364/ai-doc/reports/local-package-vault-main-2026-10-26.md)
- [DEV-012 原生入庫 QC](https://github.com/balboku/SW-PDM/blob/e218fdd5324b62a5f8a0540b55df026870f72364/ai-doc/qc/ingest-integrity-qc-2026-10-22.md)
- [DEV-014 契約](https://github.com/balboku/SW-PDM/blob/e218fdd5324b62a5f8a0540b55df026870f72364/ai-doc/decisions/ADR-004-package-copy-integrity.md)
- [DEV-015 契約／原生未檢查限制](https://github.com/balboku/SW-PDM/blob/e218fdd5324b62a5f8a0540b55df026870f72364/ai-doc/decisions/ADR-005-vault-integrity-ui.md)

## 本輪文件 QC

- 盤點前原 tracked diff：`eb8bdd8fb2032a4a299f44bcc7696e57d216192f`；index diff：`e69de29bb2d1d6434b8b29ae775ad8c2e48c5391`。
- 只允許原 105 檔中的 dev_task／documentation_map 改變；其餘 103 檔逐檔 SHA 必須等於盤點基線。
- 新增本報告與 TSV 不列回原 52 項統計；主清單列號 01～52 不重漏，tests/ 另展開 12 檔，目錄共 105 檔可重算。
- 分類是檔案內容與契約盤點，不宣稱本輪執行了任何新的產品測試、原生 CAD／DB 前後快照或獨立人員審核。
- 完成驗證結果見下方追加；任何原程式／證據 SHA 改變、main 變動或出現範圍外新檔均應停止，不修掉差異來通過。

### 完成後驗證（2026-10-29）

- TSV 共 105 筆、105 個唯一檔案路徑，涵蓋原 52 個狀態項目；主表列號 01～52 不重漏。
  三類分別 30／5／17，原樣 bytes 與 main 相等共 29 檔，與盤點摘要一致。
- 逐檔重算：原 105 檔只有獲准更新的 `ai-doc/dev_task.md`／`ai-doc/documentation_map.md` 改變；
  其餘 103 檔 SHA-256 全相同，沒有遺失檔案。main 對應檔案的 SHA／原先不存在狀態亦全相同。
- 原工作區分支／HEAD 不變，index diff 仍為 `e69de29bb2d1d6434b8b29ae775ad8c2e48c5391`（空）；未 stage。
  `git status` 由原 52 項增加為 54 項，只新增本報告與 TSV，沒有其餘新狀態項。
- 任務索引 DEV-001～016 各出現一次、沒有缺號或重複；DEV-006 保留待補驗，不把文件整理算成產品驗收。
  對兩份修改的 Markdown 執行 `git diff --check` 通過。
- 本機 main 工作樹仍乾淨，HEAD 與 `git ls-remote origin refs/heads/main` 皆為 `e218fdd5324b62a5f8a0540b55df026870f72364`。
- 本輪僅靜態文件／差異／雜湊檢查，沒有產品測試、資料庫連線、migration、歷史 BOM／Vault 操作、服務切換或發布。
  未取得現行 DB／Vault 前後指紋；不把「沒有連線／操作」寫成已執行 live 資料比對。

## 文件提交／push 邊界（2026-10-30）

- 使用者明確要求 commit／push 本次文件；不包含 main 合併、正式套用、原 dirty 後端或任何資料操作。
- 重用不承載目前服務的乾淨工作樹，從 main `e218fdd5324b62a5f8a0540b55df026870f72364` 建立 `codex/wip-main-comparison-20261030`。
  原 `codex/vault-integrity-20261026` 分支不移動；原工作區 HEAD／index／54 個狀態項亦不改。
- 僅四路徑：`ai-doc/dev_task.md`、`ai-doc/documentation_map.md`、本報告及 `ai-doc/qc/wip-main-comparison-2026-10-28.tsv`。
  兩個入口以 main 文件為基底選擇性更新，保留 main 歷次產品契約／QC／發布記錄，不整份複製舊分支文件。
- TSV 直接攜入原始基線；原報告的盤點內容及日期保留，只補本次提交邊界。原工作區四份來源文件不修改。
- 提交前檢查：分類 30／5／17、52 列與 105 檔、DEV-001～016 唯一索引、四路徑 allowlist、diff／機密格式檢查及原工作區 107 檔（105＋本報告／TSV）SHA 不變。
- 僅一般來源分支 push；不 force push、不合併 main、不建 PR、不呼叫產品測試或資料連線。最終提交 SHA／遠端核對結果見本次完成回報與分支 ref。
- 首次保護檢查的彙整器因 JSON 欄位大小寫不同誤報 Git 邊界變更；修正讀取欄位後，107 檔、HEAD、index 與 54 個狀態項全數不變。
  首次 staged diff 檢查攔下新增報告的 EOF 多餘空行，僅修正文檔格式後重檢；未修改原工作區或產品。
