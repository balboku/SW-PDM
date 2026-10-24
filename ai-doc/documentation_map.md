# SW-PDM 開發文件入口

更新日期：2026-10-24

## 最新隔離完成：DEV-014 完整套件與副本參照重寫

使用者已核准DEV-014隔離實作／驗證，基準main 2e1e2b8，獨立分支codex/package-integrity-20261022。
契約與QA入口見[DEV-014](dev_task.md#dev-0142026-10-22-完整套件與副本參照重寫)與[ADR-004](decisions/ADR-004-package-copy-integrity.md)。
只修改必要套件、原生副本adapter與測試；不部署、不執行migration、不連線目前5432/swpdm、不改歷史BOM／Vault原檔或其他dirty後端。
下方DEV-013是先前完成紀錄；承載API5000／Web5174的frontend-clarity工作樹不動，不重新build或切換服務。
最終SQLite／PostgreSQL16.14各112/112、前端邏輯13/13；11份CAD、4套件、22次獨立原生重開與18筆參照通過，含同名碰撞。
成品SHA一致，55433測試叢集已停止，原dirty 52項／main乾淨狀態／現行服務PID未變。
2026-10-24使用者僅核准本分支提交／push；提交前SQLite112/112、純邏輯13/13通過，重建成品SHA與原PG／原生證據相同。
見[QC、原生失敗修復、原始證據與Git紀錄](qc/package-integrity-qc-2026-10-22.md)；本次不合併main／部署／執行migration或更動目前資料。
本輪僅DM參照與可攜性，不冒充完整SolidWorks幾何重建、跨實機或DEV-015前端驗收。

## 最新完成：DEV-013 限定 migration 與 main 本機套用

使用者後續明確核准僅DEV-013兩張批次表、必要索引與遷移紀錄；已成功套用20261022073552_AddCheckoutBatches。
API5000已切至固定main 74cecca（PID24072）；Web5174仍為PID27392，前端與main無差異，未重啟。
同成品SQLite／PG各67/67、前端邏輯13/13及實際migration後的隔離HTTP均通過；目前庫只執行該筆migration，其餘操作均唯讀。
既有業務資料／歷史BOM、舊結構、序號、舊8筆遷移、Vault及設定指紋未變；新表0筆、歷史紀錄共9筆。
見[本機套用、正式核准與驗證紀錄](reports/local-checkout-batch-main-2026-10-22.md)。
紀錄分支codex/local-batch-preflight-20261022（證據6dde08d、狀態550ce47）已快轉合併main並push，遠端SHA已核對；原dirty工作區52項變更不動。
此次合併只含15個文件／證據檔案；重用既有驗證、不連線資料庫、不執行migration或切換服務，詳見報告的main合併紀錄。
DEV-014、DEV-015（含批次畫面）、其他migration與歷史BOM修復仍不在本次範圍；不得封存正在服務的本worktree。

## 前次預檢：DEV-013 當時因 schema 授權不足暫停

2026-10-22使用者要求套用main 74cecca，但仍禁止migration或更動目前庫。
強制READ ONLY查詢確認5432/swpdm缺少pdm_checkout_batches與pdm_checkout_batch_items，對應遷移紀錄0筆。
未切換API5000／Web5174，仍保留DEV-012固定成品；不以手動建表或其他儲存繞過限制。
本次main SQLite67/67、前端純邏輯13/13通過，PostgreSQL沿用既有67/67，未重跑／重啟PG。
詳細SQL、TRX與限制見[本機套用預檢](qc/batch-checkout-recovery-qc-2026-10-22.md#本機套用預檢2026-10-22未切換)。
紀錄位於codex/local-batch-preflight-20261022；本段是當時狀態，後續精確schema授權與套用已完成，見上節。

## 已完成：DEV-013 整批出庫安全復原後端隔離通過

基準main 2f22791，分支codex/batch-checkout-recovery-20261022，重用checkout-reused-contract乾淨worktree。
只做批次鎖快照、預覽與安全復原，見[DEV-013](dev_task.md#dev-0132026-10-22-整批出庫安全復原)
及[ADR-003](decisions/ADR-003-checkout-batch-recovery.md)。資料模型／migration原始碼只離線準備，測試庫僅EnsureCreated。
不部署、不執行migration、不讀寫目前5432/swpdm或Vault；原dirty工作區與執行中固定成品不動。
DEV-014與DEV-015（含批次畫面）未混入；來源ef40c1b（功能d5c910b）已快轉合併main並push，遠端SHA核對一致；未正式套用。
SQLite／PostgreSQL16.14各67/67、前端純邏輯13/13、Release建置通過；臨時55433叢集已停止。
詳見[QC／首次失敗修復／TRX與雜湊](qc/batch-checkout-recovery-qc-2026-10-22.md)。
下一步可獨立接DEV-014；本次不代表新表已套用或批次UI已完成。

## 已完成：DEV-012 固定 main 成品已本機套用

使用者後續核准套用main DEV-012並隔離驗證，已以固定49f98a6成品啟動API5000，
前端5174沿用未變固定成品；SQLite41/41、PG41/41、真實CAD11份與邏輯13/13通過。
目前5432/swpdm、migration、Vault、設定及原dirty工作區指紋前後一致；不執行migration。
詳見[本機套用與隔離驗證](reports/local-ingest-integrity-main-2026-10-22.md)。
紀錄444a92e及狀態提交749e201已由codex/local-ingest-integrity-20261022快轉合併main並push，遠端SHA已核對。
此worktree內固定成品正供服務使用，不可封存／清除；不使用會migration的start.ps1。

## 前階段：DEV-012 入庫核心合併 main（當時未套用）

使用者指定 DEV-012～015，並明確要求先完成 WIP 同工程版次更新、共用件沿用與解析器整合。
本輪依序先切出 DEV-012，基準 main 40f7d70，分支 codex/ingest-integrity-20261022；
不整包帶入原工作區的批次取消、套件、Vault 查詢或 schema 變更。
契約、QA 與後續切片入口見 [DEV-012](dev_task.md#dev-0122026-10-22-入庫核心隔離切片)，
實際命令、修正及驗證限制見 [入庫完整性 QC](qc/ingest-integrity-qc-2026-10-22.md)。
本輪僅隔離實作與測試，不部署、不執行 migration、不連接目前 5432 業務庫，不改寫歷史 BOM。
使用者先核准提交並 push：產品修正及驗證紀錄以 4bbed8d 推送至上述分支，文件紀錄為 4e9bdc3。
本次再核准合併並 push，main 已由 40f7d70 快轉至 4e9bdc3，與遠端 main 核對一致；
只再補充本次文件紀錄，產品與測試內容未改。未正式套用，詳見同份 QC 的 main 合併紀錄。
下方是已套用的舊契約切片紀錄，不表示本輪新入庫規則已套用。

## 本機契約切片已套用（2026-10-22）

使用者已核准main契約切片本機套用；固定成品建置、13/13契約與13/13前端測試、隔離API操作及新成品唯讀預檢均通過。
先前工具拒絕已透過正式require_escalated／auto_review續作；隔離桌面／平板／手機及錯誤恢復驗證通過，**已切換5000／5174至固定main e40f3e4成品**。
目前DB／migration／Vault／原工作目錄前後一致，未執行migration；見 [套用結果、核准、截圖與啟動方式](reports/local-checkout-reused-main-2026-10-22.md)。
服務目前依賴本獨立worktree固定成品，不可封存／清除。下節「不部署」是先前合併階段的歷史邊界，不代表目前狀態。

## 本輪隔離契約修正（2026-10-22）

只處理取消出庫與 Reused，基準 main `acbeee3`，不帶入原工作目錄的其他後端變更。
範圍、契約、QA 與續接見 [DEV-001 契約修正切片](dev_task.md#dev-0012026-10-22-契約修正切片)。
實作與隔離驗證完成，見 [契約修正 QC](qc/checkout-reused-contract-qc-2026-10-22.md)。
不部署、不執行 migration、不更動目前 PostgreSQL／Vault；批次復原與完整套件仍在本輪外。
本切片已以 `0eb41e4` 獨立提交並 push；使用者再核准合併 main 與 push，詳見 QC 的「main 合併紀錄」。
本機 main 已從 `acbeee3` 快轉至 `0eb41e4`，合併後只補充文件紀錄，產品與測試內容不再修改。
本輪仍不正式套用、不更動資料庫或 Vault；正式套用需後續指示，以下「歷史下一步」不是本輪操作指令。

## 本機前端套用與驗證（2026-10-21）

已將 main `e270736` 的固定前端成品套用至本機5174；原5000 API、未完成後端與資料庫保持不動。
主要操作在現有環境唯讀檢查，出庫／取消與故障恢復使用隔離記憶體fixture驗證。
詳見 [本機套用報告與截圖](reports/local-frontend-main-2026-10-21.md)，含成品雜湊、重新啟動方式及未驗證範圍。
本次存檔僅含報告、截圖與本索引，不代表重新部署或通過真實CAD入庫驗收。

## 前端獨立提交（2026-10-21）

圖檔中心響應式抽屜、操作引導、錯誤恢復及焦點改善獨立於後端提交。
詳見 [前端清晰度 QC](qc/frontend-clarity-qc-2026-10-21.md) 的「本次 GitHub 提交範圍」；
只沿用已提交後端，未納入批次復原、套件預檢、解析器或遷移。以下交付／部署狀態為歷史紀錄。

## 目前交付點

- `DEV-001`：試用回饋優化，涵蓋安全入庫、雙向 2D/3D 關聯、Pack & Go 工程圖及批次匯入。
- `DEV-007`：出庫後另存為新料號，建立新文件身分並保留來源稽核關聯。
- `DEV-008`：圖檔中心顯示目前版本的 CAD「圖號」，並支援清單搜尋與明細辨識。
- `DEV-009`：辨識父文件目前版本所引用的舊版子文件，並在圖檔中心顯示醒目更新提示。
- `DEV-010`：擴充圖檔中心寬螢幕版面，補入搜尋結果摘要、版本／版次資訊與更寬的文件詳情區。
- `DEV-011`（支援修正）：讓內網同仁可載入共享圖面清單，並補上可恢復的 API 連線錯誤狀態。
- 執行邊界：本地產品程式、相容性 API、技術文件、建置、UI 驗證與 GitHub 發版。
- 不在本輪：正式環境部署、正式資料修復與資料庫 migration。

## 必讀文件

- [DEV 任務與 RD/QA/QC 契約](dev_task.md)
- [試用回饋 QC 報告](qc/trial-feedback-qc-2026-07-29.md)
- [組合件下載套件流程](../docs/assembly-download-package-flow.md)
- [PDM 後端藍圖](../docs/pdm-blueprint.md)
- [品號分支架構決策](decisions/ADR-001-part-number-branch.md)
- [品號分支 QC 報告](qc/part-number-branch-qc-2026-08-04.md)
- [圖檔中心圖號 QC 報告](qc/drawing-number-qc-2026-08-05.md)
- [父文件舊版參照提示 QC 報告](qc/reference-update-warning-qc-2026-08-05.md)
- [Vault 空間利用優化 QC 報告](qc/vault-space-utilization-qc-2026-08-05.md)
- [內網圖面清單可見性 QC 報告](qc/lan-vault-visibility-qc-2026-08-11.md)
- [本機正式套用報告](reports/local-release-2026-08-04.md)

## 歷史下一步（2026-08-26）

1. `DEV-011` 已完成；Release API 現在監聽 `0.0.0.0:5000`，LAN Vault 可載入 50 份文件，請同仁以 `Ctrl+F5` 做實際工作站確認。
2. `DEV-008`／`DEV-009`／`DEV-010` 已完成並保留既有 QC 證據；本機 Release API／preview 已更新。
3. `DEV-001`／`DEV-007` 已正式套用至本機 PostgreSQL、Release API 與前端 preview。
4. DEV-008～DEV-011 已納入 2026-08-26 GitHub release candidate；正式環境仍未設定，若要部署至其他主機需另行提供目標。

## 已知 Git 邊界

- DEV-008～DEV-011 的 GitHub release candidate 以 `codex/swpdm-release-20260805` 為來源分支，
  目標為快轉合併至 `main`。
- 發版範圍包含圖號／舊版參照 API、圖檔中心與共用 Layout、LAN 啟動設定、README，
  以及 AI 開發文件／有效 QC 證據。
- `scratch/db-backups/` 與未採納的錯誤尺寸截圖不納入版本控制；本機 stash／復原分支僅作發版安全備份。
