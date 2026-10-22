# 取消出庫／Reused 契約修正 QC

日期：2026-10-22，Asia/Taipei。結論：本切片實作與隔離驗證通過；未正式套用。

## 範圍與 Git 邊界

- 基準 main `acbeee3d1ecc42e481835b9a5c719b8f0c23e2b0`。
- 分支 `codex/checkout-reused-contract-20261022`，獨立工作目錄 `C:/Users/user/.codex/worktrees/checkout-reused-contract-20261022/SW-PDM`；前輪實作交付時保留 dirty，未 stage、commit、push 或合併 main。後續提交範圍見下節。
- 只含單文件取消出庫、明確目標 Check-in 的 Reused 及必要測試／文件。沒有複製原 dirty 工作目錄的批次服務、CAD parser、套件服務、VaultSearch、Sample model／repository／migration 或 solution 變更。
- 原工作目錄的 tracked diff 指紋前後均為 `eb8bdd8fb2032a4a299f44bcc7696e57d216192f`；index 均為空指紋 `e69de29bb2d1d6434b8b29ae775ad8c2e48c5391`。原未追蹤變更也未由本輪工具寫入。
- main／origin/main 仍為上述基準；現行 API5000 PID20184（2026-10-15 啟動）、Web5174 PID2756（2026-10-21 啟動）未重啟。本輪測試不讀取 5432 連線或現有 Vault，沒有呼叫正式 migration。
- 同一代理先實作、再依 QA 執行 QC；不是獨立人員審查，也不等於所有待整合後端已完成。

## GitHub 獨立提交（2026-10-22）

- 使用者後續明確要求提交並 push「取消出庫與 Reused 契約修正及驗證紀錄」，不混入其他未完成後端，也不正式套用或更動目前資料庫。
- 發布範圍採 35 個檔案的明確 allowlist：本切片 API／前端、窄 CAD ingestion 測試接點、ContractTests／UI fixture／前端測試、文件與測試結果忽略規則，以及11張合成截圖、2份TRX。沒有加入暫存測試叢集、現有 Vault、CAD 原檔、連線設定、憑證或建置產物。
- 提交基準仍為 `acbeee3`；只推送 [同名分支](https://github.com/balboku/SW-PDM/tree/codex/checkout-reused-contract-20261022)，不合併／推送 main、不建 PR、不部署。可用此文件所在提交識別本次範圍；遠端完成狀態以 push 後的分支 SHA 核對與本輪回報為準。
- 提交前重新執行：強制 SQLite 記憶體模式的 HTTP 契約測試13/13、前端邏輯13/13、前端 production build，全部通過；`git diff --check` 通過。沒有重新啟動 PostgreSQL、API 或 UI fixture；沿用前輪 PostgreSQL13/13與瀏覽器截圖證據，不將它們誤稱為本輪重跑。
- 這次提交準備未再修改已驗證的產品程式；僅補充本節與任務／文件入口的提交邊界。main／origin/main 及原 dirty 工作目錄指紋仍與前輪一致。

## 實作契約

### 取消出庫

- `GET /api/documents/{id}/undo-checkout-preview?actor=User` 回傳單文件範圍、操作人、目前持鎖人、原始時間字串、目前版本、canRestore 與下一步說明。
- `POST /api/documents/{id}/undo-checkout` 及舊 `/checkin` 解鎖別名，都必須帶 `actor`、`expectedLockTime`、`expectedVersionId`；`includeBatch` 固定 false。
- 單一 SQL conditional UPDATE 同時匹配 documentId／owner／lock time／current version。無本文／缺欄位／批次要求回 400；狀態不符或重送回 409 `LOCK_CHANGED`。未放寬保護來相容舊無本文請求。
- 前端保留伺服器時間字串，不經 JS Date 截短微秒。操作人編輯會作廢舊預覽；預覽失敗、他人名稱、已無鎖及競爭失敗都禁止直接確認。
- 只改指定文件的兩個 lock 欄位，不改其他圖檔、版本、BOM 或 UpdatedAt。
- 操作人仍是自填名稱，這是既有測試環境的操作防護，不是身分驗證／權限系統；畫面安全限制明示，未新增 Auth。

### Reused

- 明確 TargetDocumentId 的根文件通過品號／類型與持鎖人檢查後，只有目前版本 SHA256 及原始檔名都相同才沿用。Web staging 的 GUID 前綴會移除後比較。
- 回應保留原欄位，增加 `rootOutcome` 與 `files[].outcome`，值為 `Reused` 或 `CreatedVersion`。
- Reused 不新增版本、不解鎖、不遞迴匯入、不上傳 Vault、不執行舊 missing-link heal，避免 no-op 修改歷史 BOM。
- UI 優先判斷根結果，不能因子件 Reused 將新根版本誤判為 no-op。舊 API 沒有 outcome 時維持原成功流程；明示未知 outcome 不報成功。
- Reused 不呼叫會清鎖的成功 callback、不自動關閉。畫面顯示「內容未變更，沿用原版本」「未建立新版本，出庫鎖仍保留」，可選「預覽取消出庫」或「保留出庫，返回圖檔中心」。
- 改名入庫、改內容、改品號維持 main 既有規則；沒有帶入待完成的 WIP 同 Rev／共用料／批次策略。CAD production parser 未修改，只增加窄的 ingestion session 測試接點。

此契約替換舊無本文取消出庫方式；未另建 ADR，因為是局部、可逆的保護與回應修正，不改文件身分或生命週期。日後需前後端成套套用，不可只換其中一端。

## 自動驗證

| 執行 | 結果 | 證據 |
|---|---|---|
| `dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj` | SQLite HTTP 13 通過，0 失敗，0 略過 | [TRX](checkout-reused-2026-10-22/contracts-sqlite.trx) |
| 相同測試，SWPDM_CONTRACT_PG 指向專用臨時叢集 | PostgreSQL 16.14 HTTP 13 通過，0 失敗，0 略過 | [TRX](checkout-reused-2026-10-22/contracts-postgres.trx) |
| `npm run build` | 通過；僅既有 Browserslist 資料老舊提示 | 工作目錄 `src/SWPdm.Web/dist` 成品 |
| `node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs` | 13 通過（原 7＋新增 6），0 失敗 | 根 outcome 優先、子件隔離、legacy／unknown 分流 |
| `dotnet build tests/SWPdm.UiFixture/SWPdm.UiFixture.csproj --no-restore` | 0 warnings／0 errors | 隔離 fixture |
| `git diff --check` | 通過 | 原工作目錄／main 不變核對如上 |

HTTP 涵蓋：單文件解除與其他鎖不變、相同確認重送、owner／1 微秒 lock time／version 改變、兩條 POST 的空本文／缺欄位／批次要求、他人／無鎖／不存在文件，以及 8 次競爭確認恰好 1 次 200、其餘 7 次 409。真實 ingestion 路由驗證 Reused 不變、GUID 檔名、身分／持鎖／改名不繞過，以及改內容＋既有新 Rev 可正常建新版本。

前輪實作驗證的 PostgreSQL 測試使用新建叢集 `127.0.0.1:55433`／使用者 contract；每例建立自己的 `swpdm_contract_<GUID>`。測試器硬性拒絕其他 host／port／username／admin DB；結束後查詢剩餘測試庫為 0。未使用原 5432，也沒有執行 production migration。

CAD 解析使用合成替身，資料庫操作與 HTTP 路由則為產品實作。因此不宣稱 licensed SolidWorks、真實 CAD 內容或跨工作站套件已驗證。

### 過程失敗與修正

- SQLite 初跑 11 通過／2 失敗：測試的 DateTimeOffsetToBinaryConverter 截短時間精度；改用 UTC ticks 的測試映射，重跑 13/13，並以 PostgreSQL 驗證真實微秒比較。
- 前端首次完整編譯發現引用不存在的 Input export；改用既有樣式的原生 input，重跑 build 與 13 項前端邏輯通過。
- UI fixture 重建曾被本輪已啟動的舊測試程序鎖住執行檔；核對專屬程序路徑後停止、重建及啟動最新 fixture。沒有停止現行 API5000。

## 實際瀏覽器與畫面證據

- 瀏覽器：Codex in-app browser；URL `http://127.0.0.1:5175/documents`，API 僅指向 `http://127.0.0.1:5010`。
- 全合成 SQLite 記憶體庫，60 文件；既有查詢只回 50，所以 UI 顯示「已載入（非全部）」而非假稱全部。初始持鎖 3 份，解除文件58後為 2；59／60維持持鎖，58仍為 Ver.1。
- 以 fixture 產生的 `TEST-058_試用回饋驗證圖面.sldprt` 合成文字檔經真實上傳及 ingestion 端點取得 Reused。此檔不是 CAD，未使用正式 Vault 圖檔。
- 截圖時間為 2026-10-22 09:07:15～09:10:35（Asia/Taipei）。

| 狀態 | Viewport | body scrollWidth | modal clientWidth／scrollWidth | 垂直範圍 | 截圖 |
|---|---|---:|---|---|---|
| Reused | 1440×900 | 1440 | 510／510 | 128～772 | [桌面](checkout-reused-2026-10-22/reused-1440.png) |
| Reused | 1024×768 | 1024 | 510／510 | 62～706 | [平板／低高度](checkout-reused-2026-10-22/reused-1024.png) |
| Reused | 390×844 | 390 | 365／365 | 86～758 | [手機](checkout-reused-2026-10-22/reused-390.png) |
| 取消預覽 | 1440×900 | 1440 | 510／510 | 188～712 | [桌面](checkout-reused-2026-10-22/cancel-preview-1440.png) |
| 取消預覽 | 1024×768 | 1024 | 510／510 | 122～646 | [平板／低高度](checkout-reused-2026-10-22/cancel-preview-1024.png) |
| 取消預覽 | 390×844 | 390 | 365／365 | 158～686 | [手機](checkout-reused-2026-10-22/cancel-preview-390.png) |

代理目視檢查：各尺寸主 CTA、保留出庫／返回、範圍與風險皆可讀可操作，沒有觀察到重疊、裁切或水平溢出。這是代理依文案與操作做的 5 秒理解檢查，不是真人使用者研究。

### Now What 與錯誤恢復

| 狀態 | 使用者要知道什麼 | 畫面／下一步 | 判定與證據 |
|---|---|---|---|
| Reused | 是否升版或解鎖？ | 明示未升版、鎖仍在；可預覽取消或保留出庫返回 | 通過；跨多次畫面操作後仍未自動關閉，見三尺寸截圖 |
| 取消預覽 | 會影響哪些圖檔？ | 明示只解除1份；本人操作人＋伺服器預覽後確認 | 通過；見取消預覽截圖 |
| 改操作人／他人 | 能直接解鎖嗎？ | 編輯即作廢預覽；他人結果提示確認本人名稱或聯絡持有人，確認鈕 disabled | 通過；[他人阻擋](checkout-reused-2026-10-22/cancel-other-390.png) |
| 預覽503 | 現在可確認嗎？ | 未確認狀態，保持禁止；重新檢查後恢復 | 通過預期錯誤案例；[錯誤提示](checkout-reused-2026-10-22/cancel-preview-error-390.png) |
| 預覽後鎖變更409 | 是否誤解新鎖？ | 明示未解除；舊預覽作廢、重新檢查再決定 | 通過預期競爭案例；[狀態改變](checkout-reused-2026-10-22/cancel-stale-390.png) |
| 完成 | 是否還有關聯檔？ | 明示這份已解除、其他保持原狀；返回圖檔中心 | 通過；[完成](checkout-reused-2026-10-22/cancel-complete-390.png) |
| 已無鎖 | 是否還要取消？ | 明示不需再取消，確認鈕 disabled，可返回 | 通過；[免處理](checkout-reused-2026-10-22/cancel-already-unlocked-390.png) |

Visible error sweep：正常 Reused、取消預覽及完成狀態無 `[role=alert]`／`.inline-error`，無可見 HTTP4xx／5xx、Not Found、Internal Server Error 或 `/api/...` 錯誤文字。503／409是明確注入的負向案例，沒有用後續成功抹除：在同一視窗重新檢查恢復、確認完成，再 hard reload 同一 URL，錯誤歸零；已載入50、已出庫2、有新版0符合預期，缺失仍顯示未檢測。console 只有兩個既有 React Router future-flag warning，未觀察到產品 JS exception。

## 尚未執行與後續邊界

- 本輪只授權提交與 push 本切片；未合併 main 或正式部署，不更動現有資料庫或歷史 BOM。
- 本輪不新增 schema 或 migration；批次復原、WIP 同 Rev、解析器、套件真實參照預檢與跨工作站驗證仍不在這個分支。
- 不將本切片通過當作所有 DEV-012～015 的驗收通過；合併或正式套用仍需後續指示。
- 隔離服務與暫存測試資料的收尾狀態於結束時核對，見下方收尾。

## 收尾

- 已還原瀏覽器 viewport、關閉隔離分頁；沒有操作使用者原本5174分頁。
- 停止與刪除暫存目錄的合併指令在建立程序前遭工具 `blocked by policy`，沒有執行其中步驟；未嘗試改用工具、編碼或其他方式重試刪除。
- 隨後只執行不同的、非刪除收尾：核對專屬路徑後停止 fixture PID10412、Vite PID24296，`pg_ctl -D <本輪叢集> -m fast -w stop` 回報 server stopped、exit0。現有 API5000／Web5174 未動。
- 最後 TCP 核對：5010／5175／55433 已無監聽；5000／5174 仍為原 PID20184／2756。
- 13 個 PostgreSQL 自建測試庫已由測試流程逐例移除，最後查詢剩餘為0；這與下列叢集資料目錄未刪除是不同層次。
- 保留本輪合成暫存目錄（不是現有 Vault，不含使用者 CAD）：
  - `C:/Users/user/AppData/Local/Temp/swpdm_ui_fixture_1a4f8a8000e44b46a826c5c09f5272ed`
  - `C:/Users/user/AppData/Local/Temp/swpdm_ui_fixture_4d00ab65206f48ff8c991589cb71b7de`
  - `tests/SWPdm.ContractTests/bin/pg-contract-bc2b12291a5e4b1aa900106f454bbf54`（已停止的測試叢集，bin/ 忽略）
- 刪除暫存目錄不是產品契約驗收的必要條件；不因收尾限制將未合併／未部署狀態誤報為已正式套用。
