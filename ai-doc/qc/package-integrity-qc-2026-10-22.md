# DEV-014：完整套件與副本參照重寫 QC

2026-10-22 隔離 QC 通過；當時未正式套用、提交或 push。2026-10-24 的分支提交指示與執行結果見文末；驗收限制不變。

## 授權、來源與邊界

使用者核准 DEV-014 隔離實作／驗證；不正式套用、不執行 migration、不更動目前業務資料、歷史 BOM 或 Vault 原檔。
基準 main `2e1e2b89cfcaf90aba47214fb4435eef041b69e3`，分支 `codex/package-integrity-20261022`。
使用不承載目前服務的 checkout-reused-contract 工作樹；不採用原 dirty checkout 的 pending 套件／批次／parser 程式。
契約見 [ADR-004](../decisions/ADR-004-package-copy-integrity.md)；本輪未 commit／push／merge／部署。

只改套件路由、服務、原生副本 adapter、Vault 專用安全讀取、必要 DI、測試及文件。
不改 entity、migration、模型 snapshot、repository、入庫／解析器、Auth、前端產品程式或依賴鎖檔。
RD／QA／QC 為同一代理分工檢查，並非獨立人員簽核。

## 實作與驗收

- 選定根／子版本後重新走其各自 BOM；支援快照、最新與同文件明確選版，拒絕非法／未使用覆寫。
- 原生 all-config、設計／抑制／虛擬件參照預檢；缺件、錯 SHA／大小、不安全來源、歧義、循環、類型／組態不符一律不出 ZIP。
- 版本 ID 前綴避免同名覆蓋。只重寫 GUID 暫存副本，要求 Save 成功，搬移後重開核對目標／組態／虛擬證據；不寫 DB 或原檔。
- ZIP 附無伺服器路徑的 manifest，列選版、依賴與來源／輸出 SHA；失敗使用繁中 Problem 與穩定 code，無部分成功 ZIP。
- 套件限制 1000 CAD／512 MiB；資料庫交易 PostgreSQL 為 RepeatableRead + READ ONLY；僅清理自己建立的精確 GUID 目錄。

| 關卡 | 實際結果 | 證據／範圍 |
|---|---|---|
| 首輪 SQLite | 103/103，0 略過 | 首輪 TRX 保留；後續補測不是隱藏失敗 |
| 中間 SQLite／PG | 各 105/105，再各 109/109 | 已完成；最終成品新增三項重寫一致性負向 |
| 最終 SQLite | 112/112，0 失敗／略過 | `dev014-release-sqlite.trx`；含舊 67 項回歸 |
| 最終 PostgreSQL 16.14 | 112/112，0 失敗／略過，8分34秒 | `dev014-release-postgres.trx`；僅全新 127.0.0.1:55433／contract，從未連線 5432 |
| 原生 CAD 最終 | 通過 | 11 來源、4 套件、22 次獨立重開、18 筆參照；同名／搬移／manifest／來源 SHA／私有 Vault／BOM 全通過 |
| 既有前端純邏輯 | 13/13 | 不宣稱畫面／RWD 驗收；本輪沒改產品前端 |
| Native verifier Release | 0 警告／0 錯誤 | 真實 interop／COM，不是 fake 成功 |
| UI fixture Release | 0 警告／0 錯誤 | 僅 DI 相容性建置，不啟動目前服務 |

套件測試額外核對缺檔／無 SHA／錯大小／traversal／絕對路徑、未解析 BOM、原生未知依賴、錯組態、循環、歧義、open／inspect／rewrite／no-op／重開失敗、重寫後組態／子組態／虛擬件證據改變、非法覆寫及三個併行請求隔離。
每例比較合成 documents／versions／BOM／批次／鎖定資料與私有 Vault SHA，並確認 package workspace 清理。

## 真實 CAD 證據與限制

取用先前私有 DEV-012 樣本的另一份副本，11 份 CAD 存於本輪 TEMP 私有 GUID 目錄；不從現行 Vault 取樣或開原檔寫入。
驗證器在 SQLite 記憶體庫建立合成身分／BOM 與私有 Vault；原生操作另複製到 package GUID 目錄。
四組案例為組立根、同一組立的零件同名碰撞、包含工程圖的另一組立及工程圖根。
碰撞案例僅將記憶體 fixture 的零件下載名稱設為相同，未改 CAD 位元組或目前系統 metadata。
每組套件解壓至新的私有目錄，再以獨立 COM session 的 GetAllExternalReferences4 檢查外部參照皆在解壓目錄，且與 manifest 目標集合相等。
原生結果保留來源 SHA 陣列及限制旗標，不存 CAD 二進位、金鑰、內部路徑或公司圖號。

沒有啟動完整 SolidWorks 作幾何／mates 重建，也沒有第二台實體工作站驗收。
抑制／虛擬／多組態的完整失效矩陣有合成契約測試，但本輪 11 份原生樣本不代表涵蓋所有真實 CAD 型態。
因此結論限於此受控樣本的原生參照重寫與可搬移性，不可宣稱全面幾何驗證或跨實機驗收。

## 過程問題與修正（不以重跑掩蓋）

1. 原生首次出現 `PACKAGE_REFERENCE_MISSING`：fixture 兄弟檔搜尋與原生變更快取影響精確路徑。
   fixture 改為分資料夾保存原生證據；產品加入 GetChangedReferences 精確別名，不改用全 Vault 同名猜配。
2. 原生 identity `A→A` 被誤判循環：只略過 identity；真循環仍阻擋，增加專門單元測試。
3. 第二組原生歷史快取出現 `PACKAGE_REWRITE_FAILED`：文件級 ReplaceReference 無法涵蓋元件專屬替換。
   以原生 occurrence／configuration 精確執行 component.Replace，要求其 bool 及 Save 回傳成功。
4. 隨後 `PACKAGE_RELOCATION_FAILED`：GetReplacedComponents 使用 qualified occurrence，而非 component.Name 短名。
   改用 SelectName2／SelectName 對應，搬移與獨立解壓重開才真正通過。此前拒絕均未回 ZIP。
5. 109 項 PG 還在執行時，誤啟 focused build，被測試程序鎖住輸出 DLL，MSB3026／3027／3021；該 focused 測試沒有執行，不能計為通過。
   等原 PG 完成（109/109）後，序列化重建／SQLite，PG 使用 `--no-build`；最終未以強制關閉程序或替換 DLL 繞過。

保留首輪 TRX 及去路徑的上述原生失敗摘要；含樣本原始路徑的 COM 診斷不公開封存。

## 重現方式

```powershell
$env:SWPDM_CONTRACT_PG=$null
dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj -c Release --no-restore --logger 'trx;LogFileName=dev014-release-sqlite.trx'
# 先建立專用 127.0.0.1:55433 / contract / UTF8 臨時叢集，絕不使用現行 5432。
$env:SWPDM_CONTRACT_PG='Host=127.0.0.1;Port=55433;Database=postgres;Username=contract;Pooling=false'
dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=dev014-release-postgres.trx'
node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs
dotnet build tests/SWPdm.NativePackageVerifier/SWPdm.NativePackageVerifier.csproj -c Release --no-restore
# 有效 SWPDM_DEV014_DM_KEY 只傳子程序環境；不將值放命令、文件或輸出。
dotnet tests/SWPdm.NativePackageVerifier/bin/Release/net8.0/SWPdm.NativePackageVerifier.dll <TEMP私有swpdm_dev014_native_GUID目錄>
dotnet build tests/SWPdm.UiFixture/SWPdm.UiFixture.csproj -c Release --no-restore
```

測試器嚴格限制 PG host／port／user／admin DB，每例 EnsureCreated 新 GUID 庫並移除；無 migration。
新目錄與測試程序透過正式 require_escalated／auto_review 核准執行，沒有更換手段繞過拒絕。
不能將本輪核准擴張為資料清空、部署或下次 migration 的常駐授權。

## 最後邊界檢查

原 dirty checkout 仍為 `codex/trial-feedback-20261016`／`3d36d1ca142959cd4dcf95b7a7f4f83c907f8752`，52 項狀態。
tracked diff 指紋 `eb8bdd8fb2032a4a299f44bcc7696e57d216192f`、index `e69de29bb2d1d6434b8b29ae775ad8c2e48c5391` 均與執行前一致。
承載服務的 main 工作樹仍為 `2e1e2b89cfcaf90aba47214fb4435eef041b69e3`，0 項變更；API5000 PID24072、Web5174 PID27392、PG5432 PID5560 不變。
API／ContractTests／NativePackageVerifier 三處產品 DLL 相同，見 [SHA256](package-integrity-2026-10-22/SHA256SUMS.md)。
最終 PG TRX 已封存；完成後查詢 pg_database 確認 `swpdm_contract_%` 為 0。
精確核對本輪 `tests/SWPdm.ContractTests/bin/dev014-pg-20261022/data`、postmaster.pid／55433 listener／postgres.exe command line 一致（PID24716），
再以 `pg_ctl -D <該精確目錄> -m fast -w stop` 停止；55433 已無 listener，5432／5560 未變。保留忽略的測試日誌／叢集，不刪除其他資料。
唯讀 allowlist 核對僅七個必要產品檔，未混入 entity／migration／parser／前端／其他後端；index 0 項，無 CAD／設定／金鑰封存，`git diff --check` 通過。
目前 5432/swpdm 未連線，故不虛構本輪現行資料庫前後快照；隔離庫／Vault 與受控 CAD 的前後一致性由測試斷言證明。
後續部署／GitHub 提交／DEV-015 需使用者另行指示；本輪不推進這些工作。

## 分支提交與 push（2026-10-24）

使用者後續明確要求提交並 push DEV-014 程式與隔離驗證紀錄；只限 `codex/package-integrity-20261022`，不合併 main、不部署、不執行 migration、不更動既有業務資料／歷史 BOM／Vault 原檔。
上方「未提交」是 2026-10-22 的隔離驗證時點，不是新的禁止提交指示。

提交前重建並重跑 SQLite 記憶體契約測試 112/112（0 失敗／略過，16 秒），既有前端純邏輯 13/13 通過。
API／Sample／interop 在 API、ContractTests、NativePackageVerifier 三處的 SHA，以及 ContractTests DLL SHA，全部與 2026-10-22 封存值相同。
故重用原 PostgreSQL 112/112 與原生 CAD 11 份／4 套件／22 次重開／18 筆參照證據；本次沒有重啟 PG、重跑原生 CAD 或讀取 Vault。
原始 TRX／JSON／TXT 的 SHA 與計數已再次核對；新 SQLite TRX 與 SHA 亦封存於同一證據目錄。

提交前 `git ls-remote` 確認遠端 main 仍 `2e1e2b89cfcaf90aba47214fb4435eef041b69e3`、DEV-014 遠端分支尚不存在；無啟用的本機 hooks，未加入 CI／部署設定。
使用明確檔案清單 staging，排除原 dirty checkout、entity／migration／模型／入庫解析器／其他後端與產品前端。
首次 staged whitespace 檢查將原生 TXT 的 CRLF 判為行尾空白；只補該證據目錄的 `cr-at-eol` 屬性，不轉換原始 TXT／TRX／JSON 位元組或更動其 SHA。
提交與遠端 SHA 核對完成後，再於此補實際結果；不將本次 push 當成合併或正式套用。
