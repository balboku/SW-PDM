# SW-PDM 開發文件入口

更新日期：2026-08-04

## 目前交付點

- `DEV-001`：試用回饋優化，涵蓋安全入庫、雙向 2D/3D 關聯、Pack & Go 工程圖及批次匯入。
- `DEV-007`：出庫後另存為新料號，建立新文件身分並保留來源稽核關聯。
- 執行邊界：本地產品程式、相容性 API、技術文件、建置與 UI 驗證。
- 不在本輪：正式環境部署、正式資料修復、資料庫 migration、Git commit／push。

## 必讀文件

- [DEV 任務與 RD/QA/QC 契約](dev_task.md)
- [試用回饋 QC 報告](qc/trial-feedback-qc-2026-07-29.md)
- [組合件下載套件流程](../docs/assembly-download-package-flow.md)
- [PDM 後端藍圖](../docs/pdm-blueprint.md)
- [品號分支架構決策](decisions/ADR-001-part-number-branch.md)
- [品號分支 QC 報告](qc/part-number-branch-qc-2026-08-04.md)
- [本機正式套用報告](reports/local-release-2026-08-04.md)

## 下一步

1. `DEV-001`／`DEV-007` 已正式套用至本機 PostgreSQL、Release API 與前端 preview。
2. 遠端正式環境尚未設定；若要發版至其他主機，需提供部署目標與正式連線指標。
3. Git commit／push 尚未執行，現有 dirty worktree 邊界記錄於本機正式套用報告。

## 已知 Git 邊界

- 使用者既有變更：`.gitignore`、`src/SWPdm.Sample/Data/PdmDbContextDesignFactory.cs`、
  `start.ps1`、`start-system.cmd`。
- 本輪不得回復、覆寫或混入上述檔案。
