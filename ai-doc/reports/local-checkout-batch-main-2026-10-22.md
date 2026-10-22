# DEV-013 限定 migration 與 main 本機套用

日期：2026-10-22（Asia/Taipei）。判定：本機套用及隔離驗證完成，既有業務資料與歷史BOM未變。
固定來源main：74cecca15d800bb92b731757651f26046e7d7d83（功能d5c910b）；同一代理依序執行PM／QA／QC，非獨立人員審查。
紀錄分支：codex/local-batch-preflight-20261022，保留前次預檢紀錄；證據提交6dde08d47b06fcb52b6bb27812c43821f4c02947已push，遠端SHA已核對。
尚未合併main；本段發布狀態以後續純文件提交保存，最終分支SHA以遠端ref辨識。

## 明確授權、驗收與停止條件

使用者已明確同意只執行DEV-013 migration，新增兩張批次表、必要索引及遷移紀錄，套用main至本機並以隔離資料驗證。
只授權20261022073552_AddCheckoutBatches；不是允許整套database update或其他尚未完成的後端變更。
不更新／刪除／回填既有業務資料或歷史BOM；不修改原dirty工作區、Vault實體圖檔、設定、其他schema或前端。

套用關卡為：固定來源與成品SHA、隔離回歸、現況schema隔離演練、實際API隔離操作、現況唯讀指紋、精確SQL交易、5001唯讀預檢、核對服務owner後切換、後指紋與LAN檢查。
任一測試失敗、來源或舊資料指紋不同、非預期migration／SQL或未知port owner即停；必要時只回復舊API，不自動Down或修資料。

## Migration 實際範圍

唯讀檢查確認原有8筆EF紀錄，兩張批次表不存在；七張業務表均0筆、Vault0份。這是實測基準，不假設未來仍空。
從固定main離線產生上一版20260804090408_AddDocumentIdentityChanges到本版的SQL；連線刻意設為127.0.0.1:1/offline_dev013，未執行EF database update。
生成SQL的Up本文與實際執行的[護欄SQL](../qc/batch-checkout-recovery-2026-10-22/dev013-local-migration.sql)逐字比對一致（只統一CRLF）。

- pdm_checkout_batches：GUID主鍵、根文件ID、操作人、UTC建立時間。
- pdm_checkout_batch_items：(batch_id,document_id)複合主鍵，版本ID、鎖時間、復原時間。
- 只有items.batch_id對batch表的ON DELETE RESTRICT外鍵；不對舊文件／版本／BOM新增外鍵或cascade。
- idx_checkout_batches_root_created查詢索引，另兩個主鍵各自有唯一索引。
- __EFMigrationsHistory只INSERT本筆ID，ProductVersion=8.0.11；總數8→9，舊8筆指紋相同。

交易先檢查精確目標、舊遷移ID清單、兩表不存在與實測空業務基準；鎖住舊業務表防止交易期間競爭寫入，設定5秒lock timeout／30秒statement timeout。
僅2個CREATE TABLE、1個CREATE INDEX、1個history INSERT，含必要PK／FK；同交易驗證新表0筆及history9筆後COMMIT。
沒有ALTER舊表、業務DML、migration端點呼叫、權限變更或其他migration；無自動失敗重試。
實際SQL SHA256：C29F6DF6ED1762FFBF35D5D8061BE4CC3DE8C5887DB149C91B33E319CF8A2089。

## 隔離驗證結果

| 驗證 | 結果 |
|---|---|
| API publish／ContractTests Release build | 0警告、0錯誤，獨立輸出，不覆寫舊成品 |
| 同成品SQLite HTTP | [67/67、0失敗／0跳過](../qc/batch-checkout-recovery-2026-10-22/dev013-local-main-sqlite.trx) |
| 同成品PostgreSQL16.14 HTTP | [67/67、0失敗／0跳過](../qc/batch-checkout-recovery-2026-10-22/dev013-local-main-postgres.trx)，約3分18秒 |
| 前端純邏輯 | 13/13、0跳過，前端原始碼未改 |
| Migration演練 | 目前庫只schema唯讀匯出到新55433/dev013_schema_verification，舊history只置入8筆metadata；未複製業務資料或執行其他migration |
| 遷移後實際固定API | loopback5002搭配遷移後隔離庫與私有Vault，見[合成HTTP結果](../qc/batch-checkout-recovery-2026-10-22/dev013-local-isolated-http.json) |
| 新鎖／沿用鎖 | 4份合成文件中根／子件／工程圖共新取得3鎖，另1份先前本人鎖沿用、不列入批次 |
| 預覽／取消 | 預覽僅ID1,3,4；錯誤actor回403；正確批次復原3筆，既有ID2鎖不變 |
| 重送／後續鎖／單檔 | 重送0筆；新批次取得根鎖後重送舊批次仍0筆；原單檔預覽及取消成功1筆 |
| 歷史不變 | 隔離版本4筆、BOM3筆與先前本人鎖的前後指紋相同；隔離Vault0檔 |
| 清理 | 各contract GUID庫後檢0個；本次合成schema庫已移除；5001／5002及55433叢集已停止，無業務資料刪除 |

67項包含原41項取消出庫／Reused／DEV-012與新增26項批次案例；涵蓋併發、中途失敗／CAS0整批回滾、owner/time/version改變、BOM後改及舊批次新鎖保護。
契約測試使用TestServer與新GUID庫（EnsureCreated），另一次實際5002測試使用真實migration後schema，兩種證據不混稱。
CAD解析為合成替身；沒有重新驗證原生SolidWorks幾何、跨工作站或DEV-014套件。
結果JSON為PowerShell解析後的彙整（顯示時區可能為+08），不是逐字HTTP封包；單檔請求實際保留伺服器原樣UTC字串。

可重現的建置／測試命令（PG叢集須另建自己的隔離實例）：

```text
dotnet publish src/SWPdm.Api/SWPdm.Api.csproj -c Release --no-restore --no-self-contained --output src/SWPdm.Api/bin/local-main-74cecca-20261022/api --verbosity quiet
dotnet build tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj -c Release --no-restore --output src/SWPdm.Api/bin/local-main-74cecca-20261022/contract-tests --verbosity quiet
dotnet vstest src/SWPdm.Api/bin/local-main-74cecca-20261022/contract-tests/SWPdm.ContractTests.dll --Logger:"trx;LogFileName=dev013-local-main-postgres.trx" --ResultsDirectory:src/SWPdm.Api/bin/local-main-74cecca-20261022/test-results
node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs
```

實際執行Logger參數有以引號保護分號；PG測試只設SWPDM_CONTRACT_PG=Host=127.0.0.1;Port=55433;Database=postgres;Username=contract;Pooling=false；SQLite執行前清除。
測試器載入的API／Sample／interop與publish SHA完全相同；[完整SHA清單](../qc/batch-checkout-recovery-2026-10-22/sha256.txt)。

## 既有資料與本機服務後驗證

目前5432/swpdm的快照採REPEATABLE READ READ ONLY，PGOPTIONS另強制default_transaction_read_only=on；設定密碼只在程序內使用，不輸出或存入證據。
[之前指紋](../qc/batch-checkout-recovery-2026-10-22/dev013-local-before.json)與[之後指紋](../qc/batch-checkout-recovery-2026-10-22/dev013-local-after.json)只含計數／雜湊，非資料備份。

- 七張舊業務表均維持0筆，逐表內容MD5相同，包括pdm_bom_occurrences；沒有歷史BOM修復／重建。
- 舊8筆migration雜湊be58dea1120e1a086ed7bc547f4a5d47不變；只新增本筆紀錄。
- 舊72欄／20約束／23索引／7個序號（含last_value）指紋相同；不將新表結構算進舊結構比較。
- Vault仍0份；appsettings.json及appsettings.Development.json SHA不變。
- migration後、切換前、切換後三份快照均SHA=5EDF929790F04008BD451ED7302A9385932B0E708093436D7F9695193A86E1A0；兩張新表均0筆。
- API5000由確認過的舊PID23348切至新PID24072，來源為本worktree下src/SWPdm.Api/bin/local-main-74cecca-20261022/api/SWPdm.Api.exe。
- 5001預檢強制唯讀；5000恢復既有操作能力，但本輪只呼叫GET health／database status／config status／search／批次清單，不對目前庫做出入庫測試。
- loopback與192.168.20.62的health及search均200；前端http://192.168.20.62:5174/documents回200。
- Web5174仍PID27392、既有local-main-e40f3e4-20261022/site；與main前端來源無差異，JS SHA仍9BC4D54E4483C4D2185DC38FDE9915B18CB28F0D470CF8A0022CCB3091D0D661。沒有重啟／覆寫前端。

原codex/trial-feedback-20261016保持52項status，tracked diff=eb8bdd8fb2032a4a299f44bcc7696e57d216192f，index=e69de29bb2d1d6434b8b29ae775ad8c2e48c5391；未stage或帶入其他變更。
main／src／tests／lib／solution無產品變更，本次dirty只含DEV-013文件、證據與前次預檢紀錄。

## 正式核准、遇到的問題與回復方式

受限的worktree／CIM／服務／資料庫操作均透過require_escalated與auto_review正式核准；未切換工具、編碼或間接繞過拒絕。
最初未提權的CIM／worktree讀取受限，改走正式核准後成功；本次migration與cutover正式核准均通過，無政策阻塞殘留。

保留首次問題，不把最終通過當成首次即成功：

1. pg_dump初次將host／port合併的參數格式被拒，未匯出；分開參數後唯讀匯出成功，正式資料未變。
2. 第一次隔離schema還原遇到新DB已有空public schema，僅在明確55433/dev013_schema_verification移除空schema後還原；沒有對5432刪除schema。
3. 實際API隔離驗證最後的單檔取消，PowerShell自動把UTC JSON轉為本地DateTime再送+08，回400，鎖未解除；修正驗證器原樣送UTC token，僅重設自建合成fixture後重驗通過，產品碼未改。
4. 5001預檢第一次路徑護欄因斜線格式不同停止，尚未切換；正規化絕對路徑並顯示實際owner後通過。

固定成品及本機輔助腳本均保留在本worktree的src/SWPdm.Api/bin/local-main-74cecca-20261022（ignored），不可封存本worktree。
Start-Api.ps1只驗證SHA並啟動固定成品；Port5001只唯讀、Port5002只隔離、Port5000使用既有設定；不build／migration。
勿使用原dirty工作區的start.ps1重建／套用，避免把其他未完成後端及migration混入。
Rollback-Api.ps1只允許停止本固定exe的5000，再啟動舊49f98a6固定成品；未知owner拒絕。不自動刪新表或移除遷移紀錄；本輪未實際演練rollback，不宣稱已演練。

## 邊界與下一步

本輪受開發PM／QA/QC規則影響，先通過精確migration與資料保護關卡再切換服務；沒有把Git合併視為已部署。
本機DEV-013後端已可用；批次操作畫面仍屬DEV-015，現有前端只保留原單檔取消，不能宣稱整批畫面已完成。
actor仍為自填操作人，不是完整登入授權；DEV-014／015、其他migration、原dirty後端與業務／歷史BOM修復均未混入。
原套用交付時未commit／push；使用者後續已核准發布，現已完成下節紀錄分支push。合併main需後續指示，不自動執行。

## 紀錄提交與 GitHub 發布（2026-10-22）

- 使用者明確要求提交並push DEV-013 migration、本機套用與隔離驗證紀錄；不混入其他後端、不執行其他migration或更動業務資料／歷史BOM。
- [證據提交6dde08d](https://github.com/balboku/SW-PDM/commit/6dde08d47b06fcb52b6bb27812c43821f4c02947)已正常push至
  [codex/local-batch-preflight-20261022](https://github.com/balboku/SW-PDM/tree/codex/local-batch-preflight-20261022)，git ls-remote核對一致。
- 僅選擇性stage 15個ai-doc文件／證據：ADR執行狀態、任務／文件入口、QC／本報告、3份TRX、3份JSON指紋／隔離彙整、實際護欄SQL、SHA與證據目錄規則。
  SQL只是保存先前已執行內容，本次沒有再次執行；沒有src／tests／lib／solution、設定或其他migration原始碼差異。
- 既有10份證據SHA全部吻合；本機套用的SQLite／PG與前次預檢TRX皆67/67、0失敗／0跳過。
  保存的舊資料／BOM／結構／序號／設定／Vault指紋相符，隔離HTTP彙整通過；本輪不重新連線目前庫，不把歷史證據當成新的現況快照。
- stage檢查發現Windows JSON的CRLF被Git當成尾端空白；僅調整該目錄.gitattributes識別cr-at-eol，未正規化原始證據。
  diff檢查及暫存blob與工作檔位元組核對通過，原SHA不變；無啟用的Git hook或push workflow。
- 原dirty工作區52項status及tracked／index指紋不變；main仍74cecca15d800bb92b731757651f26046e7d7d83。
  未合併main、未force push或建立PR；只操作Git／文件，未執行測試／建置／SQL／migration、未重啟服務或改Vault。
- 本節與入口的發布狀態僅以後續純文件提交保存，不新增產品或驗證結果；分支最終SHA以遠端ref與完成回報為準。
