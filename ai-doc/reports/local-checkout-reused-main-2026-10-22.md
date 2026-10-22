# main 取消出庫／Reused 本機套用與隔離驗證

日期：2026-10-22。目前狀態：**已透過正式升權審查續作，完成隔離畫面驗證及本機5000／5174套用**。來源固定main `e40f3e4`，目前DB／Vault／未完成變更前後不變，未執行migration。下方另保留第一次遭拒及切換異常的歷史，避免誤把當時暫停當成目前狀態。

## GitHub 紀錄存檔範圍

使用者於同日另行核准將本次本機套用、正式核准及隔離畫面驗證紀錄提交並push。該存檔階段目標為獨立紀錄分支 `codex/local-checkout-reused-20261022`，以main `e40f3e4`為父提交，當時不合併main；後續新增的main合併授權與結果見下節。

- 僅含本報告、`ai-doc/dev_task.md`、`ai-doc/documentation_map.md`及本次 `ai-doc/qc/local-checkout-reused-2026-10-22/` 證據。
- 證據含16張有效截圖、13/13通過的TRX、UI量測及SHA256清單；09號裁切截圖不納入。
- 證據目錄內 `.gitattributes` 僅對 `*.trx`／`*.txt` 設定 `-text`，防止Git自動換行轉換改變原始位元組，保留既有雜湊；不改測試結果、圖片或產品設定。
- 不含產品程式、其他未完成後端、migration、設定檔、憑證、成品、啟動腳本、原始資料庫內容或Vault圖檔。
- 此存檔步驟不重跑測試、不重新部署、不重啟服務，也不連線或更動目前資料庫；沿用本報告已完成的驗證證據。

## 紀錄分支合併 main（2026-10-22）

- 使用者後續明確核准將 `codex/local-checkout-reused-20261022` 合併main並push，仍排除其他未完成後端、migration及目前資料庫操作。
- 重新抓取遠端兩個指定refs後，確認main為 `e40f3e4`，紀錄分支為 `8fe92c3dbc845e26f4bc9084cc6105cc4e48c0f0`，差異僅23個ai-doc文件／證據檔，且可快轉。
- main已從 `e40f3e4` 以 `--ff-only` 合併至 `8fe92c3`；其後僅補充本報告的合併紀錄，不修改產品或測試。
- 合併後18項證據雜湊全數吻合，`src`／`tests`／`lib`內容與原main基準一致；原工作目錄的未完成變更保留。
- 本次僅做Git合併、文件存檔與main推送；不重建、不重跑測試、不重新部署或重啟服務，不連線資料庫、不執行migration。遠端完成狀態以Git main ref及交付回報核對。

## 正式核准續作與最終結果

### 拒絕原因：可確認與不可確認

- 上次訊息是工具在命令執行前回傳 `rejected: blocked by policy`，不是Vite／API執行後報錯；整個含隔離前端啟動的指令未執行。
- 上次有效環境是 `never`／full-access，無法提出正式升權。**工具未揭露精確觸發規則，不能把此設定、命令長度、公司帳號、刪除或migration推定為拒絕根因。**
- 本次有效環境為workspace-write、網路受限、審查器auto_review；原獨立worktree位於沙箱可寫根目錄外，直接git讀取實際遇到Permission denied，改以正式唯讀升權後成功。此為本次沙箱限制證據，不是對上次拒絕規則的反推。
- 可見本機default.rules僅有兩條舊allow規則，未發現針對這次指令的deny；未修改任何規則／全域權限。可讀session紀錄未提供此次精確審查理由，不能宣稱已取得隱藏政策。

### 實際採用的正式流程

1. 保留使用者已確認的單一範圍，先核對成品、資料隔離、監聽位址與副作用。
2. 工具呼叫使用 `sandbox_permissions=require_escalated`，附具體用途及目標；交由當輪auto_review審查，不改用其他工具繞過。
3. 隔離啟動明確限127.0.0.1:5010／5175、全新SQLite記憶體及合成暫存Vault；正式升權呼叫成功執行，取得PID17176／25228。
4. 切換前後的唯讀DB核對、5001預檢及本機5000／5174切換分別透過同一正式核准機制執行。沒有新增廣泛prefix allow、沒有改設定或要求再次確認資料刪除。

官方文件說明審查針對具體核准請求，核准後才繼續執行；拒絕不應以間接執行繞過。`/approve`描述的是開源TUI的單次覆核入口，不能直接當成桌面版按鈕；本次已直接使用工具支援的正式升權，不依賴該入口。[OpenAI Auto-review](https://learn.chatgpt.com/docs/sandboxing/auto-review)

### 隔離畫面驗證（本次實測）

API／Sample DLL與待套用成品雜湊完全一致；前端同一來源，隔離variant僅將API URL固定至5010。現有資料寫入測試均未連5000或swpdm。

| 驗收點 | 實際結果與畫面證據 |
| --- | --- |
| 桌面1440×900、平板1024×768、手機390×844 | Reused提示與取消modal均可讀、CTA可操作，document寬度不超出viewport；[桌面Reused](../qc/local-checkout-reused-2026-10-22/02-desktop-reused.png)、[平板Reused](../qc/local-checkout-reused-2026-10-22/03-tablet-reused.png)、[手機Reused](../qc/local-checkout-reused-2026-10-22/04-phone-reused.png) |
| Reused | 上傳合成TEST-058同內容，清楚顯示未建立新版本、沿用原版本、鎖仍保留；不自動關閉，提供預覽取消及保留出庫返回 |
| 操作人變更 | 填OtherTester立即使確認失效；重新檢查顯示只能取消自己持有的鎖。[手機防呆](../qc/local-checkout-reused-2026-10-22/05-phone-other-actor-blocked.png) |
| 單文件預覽 | 清楚列出只影響1份、不含關聯檔、本機修改不會上傳或刪除；[手機預覽](../qc/local-checkout-reused-2026-10-22/06-phone-cancel-preview.png) |
| 過期預覽 | fixture在POST前變更鎖時間，回409；畫面顯示未解除、請重新檢查且確認disabled。[競態錯誤](../qc/local-checkout-reused-2026-10-22/07-phone-stale-lock-error.png) |
| 預覽服務503 | 清楚顯示尚未確認、重新檢查及未解除任何鎖；確認disabled。[服務錯誤](../qc/local-checkout-reused-2026-10-22/08-phone-preview-service-error.png) |
| 恢復與成功取消 | fixture恢復normal，重新檢查後成功解除58；59／60仍鎖定，清單已出庫3→2。[桌面成功](../qc/local-checkout-reused-2026-10-22/11-desktop-cancel-complete.png) |
| 已無鎖 | 58重新預覽顯示不需再取消，確認disabled。[平板無鎖](../qc/local-checkout-reused-2026-10-22/12-tablet-no-lock-recheck.png) |
| 新出庫與下載 | 合成56預覽只1份、出庫成功後給下載／修改後入庫的下一步；下載UI-QC.txt（明確非CAD），再於手機成功取消56。[平板出庫](../qc/local-checkout-reused-2026-10-22/13-tablet-checkout-preview.png)、[出庫成功](../qc/local-checkout-reused-2026-10-22/14-tablet-checkout-complete.png)、[手機取消完成](../qc/local-checkout-reused-2026-10-22/15-phone-fresh-checkout-cancel-complete.png) |
| 正常畫面錯誤掃描 | 除故障注入案例外，visible `.inline-error`／alert為0；最終清單及原Chrome重載皆無可見錯誤，browser logs無warn/error；故障訊息不以後續成功抹除，另留上述截圖 |
| 計數核對 | fixture有60份，依既有array契約最多顯示50份且標明非全部；初始出庫3，取消58後2，出庫56後3，再取消56後2；未把全部0當成有資料時的正常值 |

UI量測含URL／viewport／時間／錯誤文字／dialog座標，見[ui-metrics.txt](../qc/local-checkout-reused-2026-10-22/ui-metrics.txt)。共有16份有效截圖；09號viewport切換瞬間截圖有裁切，明確排除，不當作通過證據，平板穩定重測採12／13／14。一次瀏覽器CDP點擊逾時後先重新讀畫面再重試成功；未更改產品解決工具暫態。

Now What人工理解檢查：Reused→預覽取消或保留出庫返回；他人鎖→填正確操作人／聯絡持有人；競態→重新檢查；服務失敗→重新檢查／確認服務版本；成功→返回圖檔中心；無鎖→不需再取消；出庫成功→下載並修改後入庫。每一分支均有明確結論及下一步，故障時不可誤按確認。

### 本機套用與後驗證

- 固定來源仍是main `e40f3e473755b4e8611ca2450e4cab41b45d926c`；product `src/tests/lib`無tracked差異。沿用前次本輪13/13契約與13/13前端測試，以及同切片歷史PostgreSQL隔離證據，未重建或帶入原工作目錄WIP。
- API5000目前PID6992，執行獨立worktree的固定publish exe，使用原API目錄作content root，Development設定與原Vault絕對路徑；SolidWorks原生interop及license配置保留，不自動migration。
- Web5174目前PID15460，服務固定main成品 `index-DKZLPEHx.js`；實際LAN HTTP下載SHA256與下方成品表相同，不是原WIP dev server。
- 原Chrome分頁 `http://192.168.20.62:5174/documents`已重新載入，實際script URL為新版hash，1920×842、無水平溢出／可見錯誤；資料原本為空，顯示「圖庫尚無圖檔」與首次匯入CTA。[原Chrome套用後](../qc/local-checkout-reused-2026-10-22/17-live-chrome-main.png)
- GET5000 health=Healthy、database status canConnect=true、search=[]。不呼叫目前環境的checkout／cancel／ingest／migration POST。
- `resume-before.json`、`pre-cutover.json`、`resume-after.json`（忽略的成品父目錄）指紋一致；7業務表0筆、migration8筆、schema72欄、constraints20、sequences7含last_value、Vault0檔、設定hash完全不變；與下方第一次基線也一致。
- 原工作目錄tracked diff／index仍為下方相同指紋，short-status仍52筆；目前服務只載入固定main成品，不使用未完成後端原始碼。
- 本輪隔離PID17176／25228及預檢24424已按exe／命令列身分核對後停止。完成時僅5000／5174監聽，5001／5010／5175關閉。IAB測試分頁關閉、viewport重設；原Chrome分頁保留。

### 切換異常與受控恢復

首次切換已通過正式核准，但在舊API退出後中斷；回復腳本又因5174仍由舊前端2756持有而安全拒絕停止，遮蓋了原始例外。原始例外未被保留，不能把程序退出競態當成已證實根因。唯讀確認當時5000無監聽、5174仍是正確舊成品後，以兩個明確正式核准步驟先啟動已預檢新API，再核對舊Web命令列並等待退出、切換新Web；最後完整後驗證通過。此異常不是新的policy denial，期間未寫入資料。

一次性Apply-Services.ps1已停用並保留為事故證據，不可再跑；Rollback-Services.ps1已補上辨識並保留已回復的舊服務、等待新程序退出的防護，只做AST語法檢查，**未實際執行反向切換演練**。

### 後續啟動／回復邊界

- API固定啟動器：`C:\Users\user\.codex\worktrees\frontend-clarity-20261021\SW-PDM\src\SWPdm.Api\bin\local-main-e40f3e4-20261022\Start-Api.ps1`。
- Web固定啟動器：`C:\Users\user\Documents\GitHub\SW-PDM\src\SWPdm.Web\dist\local-main-e40f3e4-20261022\Start-Web.ps1`。
- 回復舊成品腳本：`C:\Users\user\Documents\GitHub\SW-PDM\src\SWPdm.Api\bin\local-main-e40f3e4-20261022\Rollback-Services.ps1`；會核對舊API SHA與port owner，未知程序一律拒絕停止。未執行回復，也未覆寫舊成品。
- 成品仍由獨立worktree提供，該worktree目前供服務使用，不可封存／清除；`start.ps1`會migration，不是本輪允許的啟動方式。
- 真實SolidWorks CAD解析／實際圖面入庫、新版次建立、完整批次／套件及其他未完成後端不在本次驗證；actor是操作防護不是登入身分驗證，未新增Auth。
- 套用／驗證階段只留下文件及證據於 `codex/local-checkout-reused-20261022`，當時未commit／push；後續獨立存檔範圍見上節。原工作目錄產品及index保持原樣。

## 授權與固定邊界

- 使用者核准將 main 的取消出庫／Reused 契約修正套用目前本機測試環境。
- 來源 main `e40f3e473755b4e8611ca2450e4cab41b45d926c`；產品修正 `0eb41e4`，不含原工作目錄尚未完成的後端。
- 只重建、驗證及切換 API 5000／Web 5174；不執行 migration、不呼叫目前資料庫的寫入端點、不改目前 Vault。
- 寫入操作只在 SQLite 記憶體 fixture／獨立暫存 Vault 驗證。CAD 解析使用合成 parser，不代表真實 SolidWorks 入庫驗收。
- 原套用／驗證階段不commit／push；後續使用者另外核准獨立文件分支存檔，再新增main合併與push授權，兩階段範圍及結果見上節。

## QA 計畫及停止條件（切換前建立）

1. 原工作目錄差異指紋、目前 PostgreSQL 表列數／內容雜湊／schema／migration／sequence，以及 Vault 檔案清單建立唯讀基線。
2. 從固定 main 建立 Release API（保留 SolidWorks 原生 interop）及 production Web；不執行 `start.ps1`。
3. 重跑契約測試、前端測試；隔離 fixture 驗證出庫、取消、Reused、錯誤與恢復，桌面／平板／手機實際渲染檢查。
4. 新 API 先在 5001 唯讀預檢；通過才切換 5000／5174。舊成品不覆寫，備有僅啟動服務的回復方式。
5. 切換後只讀檢查目前服務；再次比對資料指紋及原工作目錄差異，停止隔離服務，保留正式測試服務。

| 主要風險 | 防護與通過標準 |
| --- | --- |
| 混入原工作目錄 WIP | 固定 main 來源建置；原工作目錄 tracked diff／index 不變 |
| 意外 migration／資料寫入 | 啟動路徑審查無自動 migration；目前 DB 僅 read-only transaction／GET；前後資料指紋完全相同 |
| Vault 指向新 worktree | 明確設定原 Vault 絕對路徑；前後檔案清單／雜湊相同 |
| 取消解除錯誤鎖／Reused 誤顯成功 | 隔離 HTTP 契約＋UI 驗證 actor、版本、精確時間及無變更分支 |
| 發版中斷或設定遺失 | 保留舊成品，先預檢新成品；切換失敗則回復舊服務，不變更資料 |

任一來源不一致、資料指紋變更、migration 行為、關鍵測試失敗或成品缺失即停止切換，記錄事實，不擴張至其他後端修正。

## 第一次執行 QC 結果（歷史，正式補驗見上方）

| 項目 | 結果及證據 |
| --- | --- |
| API Release publish | 通過；.NET SDK 8.0.420，來源固定 e40f3e4，使用目前本機 interop DLL，不使用原工作目錄產品來源 |
| 原生解析編譯旗標 | `SOLIDWORKS_DOCUMENT_MANAGER` 存在；interop 雜湊與本機來源一致。未執行真實 CAD 解析 |
| HTTP 契約回歸 | SQLite 隔離測試 13/13，0 skip；[本輪 TRX](../qc/local-checkout-reused-2026-10-22/local-main-sqlite.trx) |
| 前端測試 | `node --test tests/ui-clarity.test.mjs tests/ingest-outcome.test.mjs` 13/13，0 skip |
| Web 建置 | TypeScript／Vite production 及僅 API URL 不同的隔離 variant 均通過；既有 Browserslist 資料過舊警告未擴張至依賴更新 |
| 獨立 fixture 主要操作 | 5010 合成圖檔相同內容 → `rootOutcome=Reused`、Ver.1／versionId58 不變，鎖保持 User；再帶原樣時間與版本取消 → `restoredCount=1`、只含58；59仍保持 User 鎖 |
| 新成品唯讀預檢 | 新 API 在127.0.0.1:5001啟動；GET health=Healthy、設定已配置、PostgreSQL可連線、search=[]。Vault 明確指向原絕對路徑；SolidWorks設定有效，reference search paths 與舊服務同為空陣列 |
| 本輪桌面／平板／手機 UI | 未執行：含隔離前端啟動的工具指令在執行前遭 `blocked by policy` 拒絕；5175未開始監聽。未將歷史截圖冒充本輪驗證 |
| 正式本機切換 | 未執行；原5000／5174保留，不覆寫舊成品 |
| 原資料與工作目錄 | 唯讀前後完全一致，詳下節 |

13項契約涵蓋單文件取消／重播、他人鎖與無鎖預覽、缺欄位與批次拒絕、owner／time／version競態、並行只能一方成功、Reused無DB／Vault變動、actor／identity／filename不符拒絕，以及既有版次政策下變更內容正常建立版本。

額外手動 fixture 請求第一次誤用了非契約的 `explicitTargetDocumentId`／`author`，回傳500同版次防護；查核契約後改用 `targetDocumentId`／`uploadedBy` 即通過。此為驗證請求錯誤，不修改產品或放寬既有版次政策；只接觸隔離 fixture。

## 本輪固定成品（忽略路徑，不提交成品與設定）

根目錄：`C:\Users\user\.codex\worktrees\frontend-clarity-20261021\SW-PDM`。

- API：`src/SWPdm.Api/bin/local-main-e40f3e4-20261022/api`。
- API啟動器：同級 `Start-Api.ps1`，核對下列雜湊、使用原設定content root及Vault絕對路徑；僅啟動固定exe，無migration／build／寫入端點。首次僅用於5001；正式續作已用於5000。
- Web：`src/SWPdm.Web/dist/local-main-e40f3e4-20261022/site`；隔離variant：同級 `fixture-site`，API固定5010。隔離前端首次遭拒，正式續作經升權啟動、完成驗證後停止，未使用規避途徑。
- SQLite fixture linked API／Sample DLL 與待套用成品的 SHA256 完全相同。

| 成品 | SHA256 |
| --- | --- |
| SWPdm.Api.dll | `0F92B9C0D98D95754B0CCAC036541FA170812356FC6FFEF9907CA4E00A756E20` |
| SWPdm.Sample.dll | `E5EC3E49AF1F0288FD3AC79CD25960A37BE6147A4B3DE94AEFE2E2133048C9E2` |
| SolidWorks.Interop.swdocumentmgr.dll | `F2DD975C978041E84C719EF5B522BB06F156A42A185EC619513779F816EAAF6F` |
| Web index-DKZLPEHx.js | `9BC4D54E4483C4D2185DC38FDE9915B18CB28F0D470CF8A0022CCB3091D0D661` |
| 隔離Web index-COZCBiZS.js | `7E4327CCDB2E9F1A9C77BA8A8DC2F1080A3307EF09467CFAD39D907D932B0729` |

## 第一次執行唯讀基線與完成時狀態（歷史）

資料庫檢查使用 `PGOPTIONS=-c default_transaction_read_only=on`、`BEGIN ... REPEATABLE READ READ ONLY`，回傳 `transaction_read_only=on`；憑證僅在子行程環境短暫使用，未寫進紀錄。`before.json`／`after.json` 在忽略的API成品父目錄，逐字比較一致。表格內容僅留雜湊，不匯出資料。

| 物件 | 前後列數 | 前後 MD5（相同） |
| --- | --- | --- |
| pdm_documents | 0 | `d751713988987e9331980363e24189ce` |
| pdm_document_versions | 0 | 同上 |
| pdm_custom_properties | 0 | 同上 |
| pdm_bom_occurrences | 0 | 同上 |
| pdm_document_identity_changes | 0 | 同上 |
| pdm_numbering_rules | 0 | 同上 |
| pdm_number_sequences | 0 | 同上 |
| __EFMigrationsHistory | 8 | `be58dea1120e1a086ed7bc547f4a5d47` |
| public columns | 72 | `b4daa84dfae94cd71efeeee487851c13` |
| public constraints | 20 | `0bb2836cf3795d110a975c3929aa5c28` |
| public sequences（含last_value） | 7 | `e24d75439b593dca6e75e1753e3efea8` |

- 目前Vault `C:\Users\user\Documents\GitHub\SW-PDM\vault_storage`：前後0檔；設定檔雜湊前後相同。
- 原工作目錄：52筆short-status，tracked diff指紋 `eb8bdd8fb2032a4a299f44bcc7696e57d216192f`、空index `e69de29bb2d1d6434b8b29ae775ad8c2e48c5391`，均與開工基線相同。
- 原API仍為 PID20184，舊exe `C:\Users\user\Documents\GitHub\SW-PDM\src\SWPdm.Api\bin\Debug\net8.0\SWPdm.Api.exe`，監聽5000。
- 原Web仍為 PID2756，固定舊main e270736成品，監聽5174；未切換至本輪Web。
- 自建API預檢PID26000及fixture PID4580已按完整exe路徑核對後停止。完成時5001／5010／5175均無監聽。
- 合成fixture暫存檔未另做刪除；未重試歷史遭拒的清理操作；不觸及目前Vault。

## 第一次執行政策限制與續接（歷史）

工具回覆只揭露 `rejected: blocked by policy`，未提供可歸因的具體規則。被拒的是一個同時包含成品雜湊讀取、`Start-Process node ... vite preview ... --port 5175`及預檢的指令；可確定整個指令未執行、5175未啟動，**不能斷言是哪個片段或內部規則觸發**。

本輪有效執行環境為 approval `never`／full-access，與AGENTS保存的on-request偏好不同；未更改設定、未提出不受支援的升權欄位，也未把同一被拒啟動改寫或改用其他工具執行。後續允許的唯讀診斷、已啟動隔離API驗證及自建程序停止不屬於重送被拒的啟動操作。

依 Dev PM／UI QC 的切換前驗證門檻，本輪保留舊服務；畫面驗證與服務切換均不能標示通過。續接需先經平台正式機制允許這項隔離前端啟動，再重新核對main來源／目前資料基線、完成UI與故障提示驗證，才可切換5000／5174並做後驗證。不需擴張資料庫權限或取得任何刪除授權。

歷史同一產品切片的UI與PostgreSQL隔離證據見 [契約QC](../qc/checkout-reused-contract-qc-2026-10-22.md)；僅作既有證據，不取代本輪未完成步驟。
