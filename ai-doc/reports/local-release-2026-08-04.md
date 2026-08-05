# SW-PDM 本機正式套用報告

套用日期：2026-08-04
範圍：DEV-001、DEV-007
結果：本機正式套用成功；遠端 deployment target 未設定。

## Release Target

- API：`http://localhost:5000`，ASP.NET Core `Production` environment，Release DLL。
- Web：`http://localhost:5174`，Vite preview 提供 production build 成品。
- Database：本機 PostgreSQL `localhost:5432/swpdm`。
- Storage：repo 的 `vault_storage/`。

repo 中沒有遠端 production 設定、容器／主機部署描述或 CI/CD；因此本次沒有推定未知
環境，也沒有執行遠端 migration、commit 或 push。

## Backup / Rollback

- 套用前 DB backup：`scratch/db-backups/swpdm-pre-local-release-20260804-182107.backup`
- 格式：PostgreSQL custom archive；`pg_restore --list` 驗證 78 筆 TOC entry。
- SHA-256：`4D6D234C2FE8DB4EBF50AC1970748136C0854ACE278B7AF3F19E37104D74FA34`
- 回復方式：停止 API，另建還原驗證 DB，以 `pg_restore` 驗證後再決定是否切回；不得在
  未確認時直接覆蓋現有 DB。

## Build / Migration Evidence

- `dotnet build SWPdm.sln -c Release --no-restore`：0 warnings、0 errors。
- `npm run build`：TypeScript 與 Vite production build 通過，1421 modules。
- `dotnet ef database update --configuration Release --no-build -- --environment Production`：
  database already up to date，無重複 migration。
- `pdm_document_identity_changes` 存在；套用時稽核資料 0 筆，沒有 QC 殘留。
- API DLL SHA-256：`098FB4F1ED62CB09D9CB5309EBBC13CF839B7FAB87C030D40D29BE6A3A61DEA6`
- Web JS SHA-256：`DE01AFAA181ED702639AF8EBE6C0C388D3BB3C66E6C11E3A0DEE3909D0DB39B7`

## Production-mode Smoke

| 檢查 | 結果 |
| --- | --- |
| `/health` | 200 |
| `/api/config/status` | DB、Local Storage、SolidWorks DM 均已設定 |
| `/api/database/status` | 200，`canConnect: true` |
| `/api/documents/search` | 200 |
| `/api/documents/53/relations` | 200，含 `identityOrigin`／`derivedDocuments` |
| Web `/` | 200，production app root 正常 |

首次 API 啟動因 content root 指向 repo 根目錄而未載入設定，smoke gate 立即攔截；該程序
已停止，改從 `src/SWPdm.Api` 啟動後全部重測通過。

## Known Limits / Git Boundary

- 前端 `npm run lint` 仍因 repo 未安裝 ESLint config 相依套件而無法執行；production build
  與既有三 viewport UI QC 已通過。
- 目前 branch 為 `main`，基準 commit `65694a2`，worktree 含 DEV-001、DEV-007 以及使用者
  既有未提交修改；本次未 stage、commit 或 push，避免混入未確認 Git scope。
- API 與 Web 目前保持運行；遠端部署需建立獨立 release gate。
