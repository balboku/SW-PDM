# SW-PDM 開發文件入口

更新日期：2026-10-22

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
