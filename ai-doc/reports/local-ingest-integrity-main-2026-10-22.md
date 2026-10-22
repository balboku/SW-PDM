# main DEV-012 本機套用與隔離驗證

日期：2026-10-22（Asia/Taipei）。狀態：已完成本機 API5000 套用及固定前端5174啟動，
目前資料庫／Vault／設定及原未完成工作區指紋前後一致，未執行 migration。
固定來源 main 49f98a6f57dc01f6575434c4b141648d2bed4c41，產品提交 4bbed8d。
本輪只保存本機紀錄，尚未 commit／push；紀錄分支 codex/local-ingest-integrity-20261022。

## 使用者邊界與 QA 契約

- 只套用 main 已合併的 DEV-012：WIP 同工程 Rev 更新、共用件沿用、解析器整合。
  不帶入原工作區的批次復原、套件、Vault 查詢、schema 或其他未完成後端。
- 真正寫入只允許全新 SQLite 記憶體庫、全新 127.0.0.1:55433 contract 測試庫及私有 Vault／CAD 副本。
  測試庫僅 EnsureCreated，不執行 migration，不使用舊 clone 或目前 5432/swpdm。
- 目前 swpdm 只允許 READ ONLY 指紋及 GET 健康／清單核對，不呼叫入庫、出庫、取消、storage 或 migrate POST。
- 先建置新固定成品、比較測試器與成品 SHA、完成隔離驗證，再5001強制唯讀預檢，最後啟動5000。
  舊成品不覆寫，未知 port owner 不停止；任何指紋不同、測試失敗、來源混入或需要 migration 時停止套用。

| 風險 | 本輪關卡 |
|---|---|
| 把原 dirty 後端一併部署 | main 固定來源，產品／測試與4bbed8d相同；排除 schema／前端／Program 差異 |
| WIP 新 Ver 或共用件沿用誤解鎖 | Release HTTP 契約41項，含其他鎖／歷史不變與競爭操作 |
| 假 parser 掩蓋原生差異 | 真實11份 CAD、獨立原生 API 路徑／旗標比對及同 Rev 內容更新 |
| 測試碰到目前資料庫 | 嚴格55433／contract guard；現有5432只READ ONLY；前後完整指紋一致 |
| 啟動失敗或成品改變 | 新成品 SHA 啟動防護、5001預檢、保留舊固定成品與僅API復原腳本 |

## 本輪實際執行結果

| 檢查 | 結果與證據 |
|---|---|
| Release API publish | 從49f98a6新建獨立輸出，不覆寫舊成品 |
| SQLite HTTP | 41/41，0 failed／0 skipped；[sqlite.trx](../qc/local-ingest-integrity-2026-10-22/sqlite.trx) |
| PostgreSQL 16.14 HTTP | 41/41，0 failed／0 skipped；[postgres.trx](../qc/local-ingest-integrity-2026-10-22/postgres.trx) |
| 測試／成品一致性 | 契約測試器及原生驗證器載入的API／Sample／interop與待套用publish SHA完全相同 |
| 真實 CAD | 8 Part、2 Assembly、1 Drawing；17參照路徑、8直接occurrence原生比對通過 |
| 原生入庫整合 | 3個Assembly／Drawing根文件typed BOM、共用副本沿用、同Rev改名及實際內容更新通過 |
| 真實負向案例 | 同品號不同內容的組合件1筆被阻擋，未產生新版本 |
| 來源 CAD 完整性 | 11份私有輸入CAD前後SHA不變；內容更新只改另外建立的GUID副本 |
| 既有前端邏輯 | node --test 的13/13通過，未更動前端 |
| 5001唯讀預檢 | PID25832，health Healthy、DB可連、原生／storage已配置、search=[] |
| 本機5000 | PID23348，新固定49f98a6 API；health Healthy、DB可連、search=[] |
| 本機5174 | PID27392，既有固定main前端；HTTP200，實際HTTP下載JS SHA與固定成品相同 |
| 目前資料不變 | before／pre-cutover／after指紋檔完全同SHA，見下節 |
| 清理 | 已停止本輪5001與55433；臨時PG的swpdm_contract_*庫後檢為0；僅保留5000／5174 |

原生驗證器本輪輸出：

```text
NATIVE_PASS files=11; Part=8; Assembly=2; Drawing=1; paths=17; occurrences=8; bomRoots=3; changedSharedBlocked=1; ingestion/sharedReuse/sameRevRename/sameRevContentUpdate/sourceHashes=PASS
```

原生正向更新以自訂屬性修改造成實際bytes改變、工程Rev不變，再建立Ver.3；不宣稱已驗證幾何變更或工程核准。
actor仍是操作防護，不是完整登入授權。

### 目前 swpdm／Vault 前後核對

唯讀腳本以 REPEATABLE READ READ ONLY 核對既有5432/swpdm；PGOPTIONS亦強制default_transaction_read_only。
三次指紋檔SHA均為 24D9D2A66FF6E6927568E8A11C93A6C3EA9AF417C194ED448FF7552F8D00B98F：

- 7張業務表全部0筆，內容雜湊一致。
- EF遷移紀錄8筆且雜湊一致；schema72欄、constraints20、sequences7（含last_value）雜湊一致。
- Vault實體檔案0份；設定檔appsettings.json及appsettings.Development.json雜湊不變。
- 只保存指紋，沒有備份原始業務資料、增加帳號權限、變更schema或執行migration。
- 5001預檢連線另加唯讀選項；5000保留既有功能與連線權限，未改設定檔。
  本輪對5000只做GET，並未在目前庫實際建立測試文件。

原工作區仍為codex/trial-feedback-20261016、52項status；
tracked diff指紋eb8bdd8fb2032a4a299f44bcc7696e57d216192f，
index指紋e69de29bb2d1d6434b8b29ae775ad8c2e48c5391，前後相同。

## 執行環境限制與過程紀錄

- 一般沙箱讀CIM回報無法存取，改用正式require_escalated唯讀申請後成功；沒有繞過工具限制或改全域權限。
- 開始時5000／5174都未監聽、原5432正常，因此本輪沒有停止先前API／Web，也沒有把舊PID當成現況。
- main首次建置NativeIngestionVerifier時缺project.assets.json；還原既有依賴後重建，
  沒有升級或新增套件、沒有修改產品／測試原始碼；最終Release建置0 warning／0 error。
- 前端首次25秒就緒輪詢逾時。隨後獨立唯讀重測HTTP200，
  下載JS SHA吻合，PID27392的Node命令列確認使用固定site；stdout有正常5174位址，stderr為空。
  沒有為消除逾時而改產品或重新啟動；具體暫態原因未被證實，不宣稱是已確認的啟動競態。
- 本輪不涉及UI變更，未重跑桌面／平板／手機畫面，不以HTTP或舊截圖冒充UI視覺驗收。

## 固定成品、啟動與復原

成品與測試輸出父目錄：
C:\Users\user\.codex\worktrees\frontend-clarity-20261021\SW-PDM\src\SWPdm.Api\bin\local-main-49f98a6-20261022

| 成品 | SHA256 |
|---|---|
| SWPdm.Api.dll | 19F1B68FF2572BB5B9B6EED5237ADFB623C032AB16576CD8062F0AF5E31E7554 |
| SWPdm.Sample.dll | DB8375AE6228BB5B412415912DFD80B63AFAA0A0911A579F26E224F5C0A420AF |
| SolidWorks interop | F2DD975C978041E84C719EF5B522BB06F156A42A185EC619513779F816EAAF6F |
| 沿用前端index-DKZLPEHx.js | 9BC4D54E4483C4D2185DC38FDE9915B18CB28F0D470CF8A0022CCB3091D0D661 |

API用上述目錄的Start-Api.ps1啟動，先核對三份DLL SHA、空閒port、目前配置的明確host／port／DB，
使用原API目錄作content root、固定Vault絕對路徑；不執行dotnet run／build、EF或寫入端點。
5000輸出記錄於api-5000.stdout.log／stderr.log。

前端沿用：
C:\Users\user\Documents\GitHub\SW-PDM\src\SWPdm.Web\dist\local-main-e40f3e4-20261022\Start-Web.ps1
它服務main worktree既有site，與本輪main前端來源相同，不使用原dirty前端原始碼。

API復原：上述新成品父目錄的Rollback-Api.ps1。
只允許停止新exe在5000的確切owner，再啟動已核對SHA的前一固定e40f3e4 API；
保留不變的前端、資料庫與Vault，不用原dirty Debug成品。
啟動／復原腳本AST通過；本輪未實際執行反向復原演練，不宣稱復原流程已完整實測。
原先停用的Apply-Services.ps1與會migration的start.ps1皆未執行，不得拿來續接本次套用。

本機網址：http://192.168.20.62:5174/documents。
固定成品仍位於此worktree，後續不得封存／清除；Git分支切換不等於切換執行中的固定成品。

## Git 邊界與未驗證範圍

- 本輪紀錄僅dev_task、documentation_map、本報告、既有QC入口狀態及TRX／SHA文字；
  產品、設定、依賴宣告與測試原始碼均與main49f98a6相同。未commit／push。
- CAD、金鑰、目前資料庫／Vault、固定執行檔、啟動腳本與本機指紋JSON不納入Git。
  [證據雜湊](../qc/local-ingest-integrity-2026-10-22/sha256.txt)，.gitattributes避免TRX換行轉換。
- 提交前一般diff檢查將原始TRX的CRLF判為行尾空白；僅對本證據目錄的TRX宣告cr-at-eol，
  保留其他空白檢查，未改TRX內容。暫存blob與原檔雜湊一致，並重新核對兩份41/41計數。
- DEV-013～015仍未完成；批次取消、完整套件／跨工作站SolidWorks開啟、其他組態及真實異常UI不在本輪。
- 本輪新建測試PG及私有CAD副本不是正式資料修復，不使用清空前資料ID、不改歷史BOM。
