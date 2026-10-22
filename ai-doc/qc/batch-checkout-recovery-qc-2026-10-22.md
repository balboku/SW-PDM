# DEV-013 整批出庫安全復原：後端隔離驗證

日期：2026-10-22，Asia/Taipei。判定：後端契約隔離通過；未正式套用、未執行任何migration，未連線目前5432/swpdm。
基準main 2f22791b4a739c8e4543dc4b42ae8dbcad784d45；分支codex/batch-checkout-recovery-20261022。
工作目錄：C:/Users/user/.codex/worktrees/checkout-reused-contract-20261022/SW-PDM。
本輪未commit／push／合併main，產品與必要測試／文件變更仍在此專用分支。

## 邊界與實作

契約與FMEA見[DEV-013](../dev_task.md#dev-0132026-10-22-整批出庫安全復原)，
決策見[ADR-003](../decisions/ADR-003-checkout-batch-recovery.md)。同一代理分階段執行RD、QA、QC，再由PM整理紀錄；未宣稱是另一位獨立審查者。

- 新增CheckoutBatchService，checkout端點委派該服務；Program及兩個fixture只增加必要DI接線。
- 同一Serializable交易以條件UPDATE取得鎖，永久寫入batch及新取得項目；先前本人鎖不加入，衝突整批回滾。
- 對所選BOM子件按文件身分取得目前版本鎖，避免current-version JOIN漏件；工程圖仍限定目前版本，不擴大到where-used父組件。
- 批次清單查詢限定root／actor；預覽必須明確includeBatch=true及batchId，不自動採最新批次或重算新BOM。
- 執行重新評估每筆owner／精確微秒lock time／version；已復原、缺件、已解鎖、他人、新鎖與新版分別列出code及繁中原因。
  有效者解除鎖並寫restored_at；競爭或中途失敗整筆交易回滾，不留下部分解鎖／稽核。
- 預設單檔preview／undo及舊checkin別名仍維持原actor/time/version保護；批次必須明確選擇，不放寬舊空本文請求。
- 只新增PdmCheckoutBatch／Item、兩個DbSet、EF映射及AddCheckoutBatches原始碼／designer／snapshot。
  不帶入原dirty工作區的CheckoutService、套件、VaultSearch、parser、ingestion、BOM欄位或其他migration。

## QC 結果

| 項目 | 結果／證據 |
|---|---|
| Release API／Sample／ContractTests建置 | 0警告、0錯誤 |
| SQLite記憶體庫HTTP | 67/67，0失敗／0跳過；[TRX](batch-checkout-recovery-2026-10-22/dev013-sqlite.trx) |
| 全新PostgreSQL16.14 HTTP | 67/67，0失敗／0跳過，約3分59秒；[TRX](batch-checkout-recovery-2026-10-22/dev013-postgres.trx) |
| 原契約回歸 | 上述67項包含原41項取消／Reused／DEV-012測試及新增26項批次案例 |
| 前端純邏輯 | node兩檔13/13，0失敗／0跳過；未改前端原始碼 |
| UiFixture DI接線建置 | Release 0警告、0錯誤，未啟動fixture或正式服務 |
| 模型與snapshot | ef migrations has-pending-model-changes：無差異；僅離線核對 |
| migration範圍 | 記憶體檢查Up只含2個CreateTable＋1個CreateIndex；Down只指兩張新表 |
| SQL離線產生 | script只新增兩張批次表、索引及對應遷移紀錄SQL；未執行，輸出留在ignored bin |
| 測試／建置成品一致性 | 測試器載入API與Sample DLL SHA等同各專案Release輸出；[雜湊清單](batch-checkout-recovery-2026-10-22/sha256.txt) |
| 隔離清理 | 55433測試庫swpdm_contract_*後檢0個；已核對目錄／PID25036／port後停止本輪臨時叢集 |

新增HTTP驗收包含：根文件單獨出庫、根／子件／工程圖新鎖、先前本人鎖保留、他人／不完整鎖／缺目前版本拒絕、
舊版BOM參照以目前文件版本鎖定、不鎖父件／無關件／歷史工程圖、actor／root／batch／範圍負向、舊鎖單檔相容、
預覽後owner／1微秒時間／version／已解鎖／文件消失變更、BOM後改不擴大、重送不解新批次、新批次不接收舊鎖、
兩種POST別名一致、第二筆更新拋錯或回0整批回滾，以及8次並行checkout單一成功／8次並行undo累計只復原4筆。

## 首次失敗與修復證據

- 最初原41項回歸先通過。
- 新增案例第一輪61項為58通過、3失敗；保留[首次TRX](batch-checkout-recovery-2026-10-22/dev013-first-sqlite.trx)，不是最終通過證據。
- 實際產品缺口1項：關聯文件缺目前版本時，原current-version JOIN會默默漏掉，checkout仍200。
  修為所選BOM文件身分納入檢查，缺版本整批409；另補舊BOM指向舊版本但文件有新版的正向測試。
- 測試器缺口2項：第二筆UPDATE故障注入只匹配未加引號的SQL，SQLite未觸發而錯誤收到200。
  修正為辨識UPDATE及明確pdm_documents表；加入回0的競爭模擬，強制核對已執行第2次更新且整批／audit快照不變。
- 修正後61/61通過，再擴充至67/67；同一Release測試成品在SQLite與PG皆通過。
  未將重跑成功用來刪除或隱藏上述失敗。

## 可重現指令與隔離限制

```text
dotnet build tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj -c Release --no-restore --verbosity quiet
dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=dev013-sqlite.trx"
dotnet test tests/SWPdm.ContractTests/SWPdm.ContractTests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=dev013-postgres.trx"
node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs
dotnet build tests/SWPdm.UiFixture/SWPdm.UiFixture.csproj -c Release --no-restore --verbosity quiet
```

SQLite指令前清除SWPDM_CONTRACT_PG；PG指令前只設為Host=127.0.0.1;Port=55433;Database=postgres;Username=contract;Pooling=false。
此叢集本輪以initdb新建於tests/SWPdm.ContractTests/bin/dev013-pg-c04a2ca9616142d18a31b91030e6526c，僅監聽loopback、身分contract。
每例新建swpdm_contract_GUID資料庫與私有暫存Vault／合成CAD，EnsureCreated後結束即刪除；不是既有庫複本或正式備份。
臨時叢集已停止，空測試叢集與log保留在ignored bin；重現時應依tests/README新建自己的叢集，不使用舊庫或目前5432。

離線EF只用PdmDbContextDesignFactory，PDM_DB_PROVIDER=PostgreSql，PDM_DB_CONNECTION_STRING刻意設127.0.0.1:1/offline_dev013。
先產生AddCheckoutBatches原始碼，再做has-pending-model-changes及script；未呼叫database update、Migrate或執行產生的SQL。
未執行會migration的啟動、整合測試或舊IntegrationVerifier腳本，未讀正式設定或金鑰。

## 未套用與後續邊界

- 目前API5000、Web5174、5432/swpdm、Vault及其設定未作為測試目標；沒有查詢、指紋重取、重啟或寫入。
  本輪不宣稱重新驗證目前執行中的服務或目前庫內容；先前部署證據保持原樣。
- 原codex/trial-feedback-20261016工作區保留52項未提交變更，tracked diff為eb8bdd8fb2032a4a299f44bcc7696e57d216192f、
  index為e69de29bb2d1d6434b8b29ae775ad8c2e48c5391；不stage／複製整包dirty後端。
- 新表尚未在目前資料庫建立，此分支不可直接正式套用；schema套用與部署須後續明確指令及獨立驗證。
  原8筆歷史migration原始碼不變，本輪沒有既有資料回填／歷史BOM修復。
- actor是現有自填操作人，不是登入授權；完整Auth需另一範圍決策，不將本次防誤解鎖當成身分安全保證。
- DEV-014套件與DEV-015批次UI整合未實作；本輪無畫面變更／截圖、未重驗真實SolidWorks或跨工作站幾何，不宣稱端到端UI通過。
