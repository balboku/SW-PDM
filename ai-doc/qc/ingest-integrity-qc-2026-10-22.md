# DEV-012 入庫核心隔離驗證

日期：2026-10-22（Asia/Taipei）。本輪未正式套用、未執行 migration、未連接目前 5432 業務庫。
基準 main 40f7d70；分支 codex/ingest-integrity-20261022。

最新 Git 狀態：DEV-012 來源 4e9bdc3 已快轉合併 main 並 push；僅補充合併紀錄，未正式套用。
以下隔離實作及分支推送段落保留各階段當時狀態，main 合併結果見文末。

## 範圍與判定

本輪先完成使用者列出的 WIP 同 Rev 更新、共用件沿用及解析器整合，支援 DEV-013～015 的相依後續切片。
不將四個 DEV 全部標完成；批次取消、套件重寫／跨機可攜性、Vault 分頁與真實異常 UI 未納入。
契約與 QA 風險表在 dev_task.md，本報告只保存可重跑事實與限制。

| 檢查 | 實際結果 |
|---|---|
| SQLite 記憶體庫＋HTTP 契約 | 最終 41/41 通過，0 略過 |
| PostgreSQL 16.14 臨時叢集 | 55433／contract；含最終 BOM 查詢修正的 41/41 通過，0 略過 |
| 原生 Document Manager | 11 真實 CAD：8 Part、2 Assembly、1 Drawing；17 路徑、8 直接 occurrence 比對通過 |
| 原生入庫整合 | 3 個 Assembly／Drawing 根文件 typed BOM 通過；同 Rev 改名與內容變更、共用副本沿用通過 |
| 真實共用件負向 | 1 份同品號不同內容的組合件被安全阻擋，版本計數不變 |
| 來源樣本完整性 | 11 份私有輸入 CAD 解析／入庫前後 SHA256 相同；只修改另外建立的測試副本 |
| 缺少原生 DLL | NoNative 組態回傳 NO_NATIVE_FAIL_CLOSED_PASS，不產生假品號或假解析成功 |
| 既有前端邏輯 | 13/13 通過，0 略過；未修改前端 |
| Release API 建置 | 最終 0 warning／0 error |
| 差異格式 | git diff --check 通過（既有 LF／CRLF 轉換提醒不是內容錯誤） |

PostgreSQL 後檢確認版本 16.14、剩餘 swpdm_contract_* 測試庫為 0；
本輪專用 GUID 臨時叢集已停止，55433 已無監聽。未停止其他 PostgreSQL 程序。

## 產品行為

- 明確目標仍檢查身分與持鎖人；相同 bytes＋原始檔名 Reused，不解鎖、不遞迴。
- WIP 同 Rev 在原因必填且持鎖條件成立時建立新 Ver；明確改名亦為新 Ver。
  原生正向測試新增私有 CAD 自訂屬性，不改工程 Rev，確認實際 bytes 改變後成功建立 Ver.3。
  不宣稱測過幾何變更或工程核准。
- Released 需新 Rev；InReview／Obsolete／未知狀態禁止更新；既有非空 Rev 不可清空。
- 非明確目標的相同身分＋內容沿用目前版本，可不同本機檔名，不改原檔名或他人鎖。
  內容不同回 409 EXISTING_FILE_CHANGED；重複身分、屬性衝突不可自動選一筆。
- 解析 Drawing 模型參照、Assembly 目前儲存組態的直接元件，保留 occurrence／virtual／suppressed／組態。
  Part 設計／未分類依賴只警告，不建立 BOM；虛擬／抑制件不遞迴匯入。
- 參照以實際內容、身分及類型匹配；多個歷史候選或僅有檔名不能挑最新版。
  工程圖無原生模型參照時不製造同品號 BOM。
- 移除新匯入後回填全庫歷史缺件的行為；BOM 查詢亦忠實反映已儲存的 ChildVersionId／status。
- 批次只遞迴處理當次 staging 名單；存在但解析失敗的子件讓該根交易回滾。
  批次仍是逐根結果，不宣稱整批單一交易。
- 使用 Serializable；兩個相同文件的同時 Check-in 只一個成功，另一個 409；
  巢狀 PostgreSQL serialization／deadlock 例外不外洩內部 SQL 或連線資訊。
- 根交易失敗時清除 tracker，清理本請求已完成的新上傳，不刪既有或 dedup 檔案。
  COMMIT 結果不明時不刪可能已提交的檔案；未新增孤兒檔自動掃除或歷史修復。

## 過程中失敗與修正

1. 舊 filename 負向測試假設「同 Rev 改名必須失敗」。新契約刻意改為持鎖＋原因下產生新 Ver，
   已改驗 CreatedVersion、版本加一及他人鎖不變；actor／identity 負向未放寬。
2. 原生驗證器第一次重跑固定副本檔名衝突；改為每次 GUID 輸出檔與私有 Vault，未覆寫來源 CAD。
3. 兩份 ZIP 包含同一組合件身分、不同已儲存參照，產品依共用件政策拒絕。
   已納入真實負向案例；每個根文件的 BOM 正向傳遞用獨立記憶體庫驗證，未為測試放寬規則。
4. 補驗歷史 BOM 查詢時，DB ChildVersionId 仍是 null，但 repository 依檔名動態回傳 version 2。
   單一回歸先重現失敗，再只移除該動態補配；最終 SQLite 全套重跑通過，未修改任何歷史列。

## 重跑方式

1. dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj --no-restore
   （未設定 SWPDM_CONTRACT_PG 時為 SQLite）。
2. 建立全新 initdb 叢集，僅監聽 127.0.0.1:55433、使用者 contract。
   設 SWPDM_CONTRACT_PG=Host=127.0.0.1;Port=55433;Database=postgres;Username=contract;Pooling=false，
   跑同一測試。測試器拒絕其他目標，逐例建立／移除 swpdm_contract_<GUID>，僅 EnsureCreated。
3. 原生：將既有測試 ZIP 解壓至系統 TEMP 下新建 swpdm_dev012_native_<GUID>/cad/，
   明示 SWPDM_DEV012_DM_KEY 後執行 dotnet run --project tests/SWPdm.NativeIngestionVerifier -- <測試目錄>。
   不輸出授權金鑰、不載入正式 DB 設定。缺少任一 CAD 類型或必要環境不能判通過。
4. node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs。
5. dotnet build src/SWPdm.Api/SWPdm.Api.csproj -c Release --no-restore。
6. NoNative 防偽成功測試見 tests/README.md；不可混同原生驗證結果。

原生來源為原工作區既有兩份測試 ZIP，未新增備份、未把 CAD 或授權加入 Git：

- assembly-renamed-sample.zip SHA256 603A3570A785337E0AD2F2214729A98B14AE8128FA87CCA7F9E7BD85B337A017
- drawing-assembly-sample.zip SHA256 9D68C49513EBCB0B16B7CCF2910272F16AB0BDA9B6A5D7DB9DA548325900D108

## Git／正式環境邊界與限制

- 原工作區仍有 52 個 status 項目；tracked diff 指紋 eb8bdd8fb2032a4a299f44bcc7696e57d216192f，
  index 指紋 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391，與本輪前一致。
- 隔離實作階段 main 工作區保持乾淨，當時沒有 commit／push；後續提交與 push 見下節。
  兩階段均沒有合併、部署或操作既有服務。
- 沒有修改 DbContext、Entities、Models、Migrations、Program、solution 或任何前端檔案。
  Data/Repositories 僅修正本輪已重現的 BOM 讀取誤配，不包含其他 repository 草稿。
- 未接觸目前資料庫，不把「未連接」冒充本輪全表雜湊核對；Vault 唯讀檔案計數仍為 0。
- 現有 API 的 related-drawings／候選提示、套件完整性與跨工作站開啟、其他組態策略及 UI 整合，
  仍由 DEV-014／015 處理，不因本輪傳遞 typed references 而標通過。
- 不會把原有 3 筆旗標差異或清空前資料 ID 當作現存資料修復目標。
- 任何正式 schema／資料修復／套用須另進共用正式套用關卡。

## 2026-10-22：提交與 GitHub 推送紀錄

- 使用者明確核准只提交並 push 本次 DEV-012 入庫核心及隔離驗證，不混入其他未完成後端；
  不合併 main、不正式套用、不執行 migration、不更動目前資料庫。
- 產品及驗證提交：4bbed8d7a9e0db4960d70cf4fd72bb8307e39680，
  fix: complete DEV-012 ingestion integrity slice，21 個白名單檔案。
- git push --set-upstream origin HEAD:refs/heads/codex/ingest-integrity-20261022 成功；
  git ls-remote 確認遠端分支 SHA 與本機上述提交一致，遠端 main 仍為 40f7d70af6a5d9c6148e42e8ce849d543bae54b0。
- 提交前另外重跑 SQLite HTTP 41/41 與前端邏輯 13/13，均無略過。
  PostgreSQL／原生 CAD／NoNative／Release 證據沿用上一節，這次沒有重新啟動 PostgreSQL 或重跑真實 CAD。
- git diff --check、21 檔 stage 白名單、排除路徑及新增內容憑證模式檢查通過；
  無 DbContext／Entities／Models／Migrations／Program／前端／solution 差異。
  沒有 CAD、授權、設定或測試庫檔案納入提交；沒有有效 Git hooks 或 .github 自動工作流程。
- 本節與入口狀態將以獨立 docs 提交同步至同一分支；docs 不改產品或測試內容。
- 原工作區 tracked diff 指紋及空 index 與前述相同，未提交其餘 52 個 status 項目。

## 2026-10-22：main 合併與推送紀錄

- 使用者核准將 codex/ingest-integrity-20261022 合併 main 並 push；
  仍禁止混入其他未完成後端、正式套用、migration 或目前資料庫寫入。
- 合併前：本機／遠端 main 為 40f7d70af6a5d9c6148e42e8ce849d543bae54b0，
  來源分支／遠端為 4e9bdc3369081fdfe6a29462c0308e53a5101a4d，兩個工作區乾淨。
- 已核對唯一新增提交為 4bbed8d（產品＋隔離驗證）及 4e9bdc3（分支推送紀錄），21 檔白名單相符；
  DbContext／Entities／Models／Migrations／Program／solution／前端均無差異。
- main 使用 git merge --ff-only 4e9bdc3369081fdfe6a29462c0308e53a5101a4d 成功快轉，無衝突。
  合併後完整 tree 與指定來源相同；沒有重建、改寫或混合原工作區的變更。
- git push origin HEAD:refs/heads/main 成功；git ls-remote 確認遠端 main 為 4e9bdc3369081fdfe6a29462c0308e53a5101a4d，
  來源分支亦維持該 SHA。合併後只新增本段及入口狀態的 docs 紀錄，不變更產品／測試。
- 本次沿用已通過的 SQLite 41/41、PG 41/41、前端邏輯 13/13、原生 CAD 11 份及建置證據；
  因產品／測試 tree 完全相同，沒有重跑或把歷史驗證冒充本次新執行。
- 合併前已確認沒有 .github 自動工作流程、有效 Git hooks，亦未發現 main 來源的 dotnet watch／Vite dev 監看。
  Git 合併不等於正式套用；沒有啟動、停止或重新啟動 API／Web／PostgreSQL，也沒有執行 migration 或連接目前業務庫。
- 原工作區仍保留原 52 項 status 與 tracked diff／index 指紋；未 stage、提交或合併那些尚未完成變更。
