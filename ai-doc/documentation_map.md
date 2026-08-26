# SW-PDM 開發文件入口

更新日期：2026-08-26

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

## 下一步

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
