# DEV-014／015 合併與本機套用

日期：2026-10-26，Asia/Taipei。狀態：提交／push、main合併、本機成套套用與後驗證完成。
使用者已明確要求「提交、push、合併&正式套用」，本輪目標為既有個人本機測試環境API5000／Web5174。
此指示取代先前隔離階段的不提交／不合併／不部署限制；不擴張為其他未完成後端、migration、資料修復、刪除或Vault原檔修改。

## 範圍、依賴與風險

- DEV-015基於已發布DEV-014 c12b852；main原為2e1e2b8，將以fast-forward一次納入兩者，不另外帶入原dirty checkout。
- 只選擇性提交DEV-015產品、測試與證據；正常push來源分支，核對遠端後合併main、正常push並核對SHA。無force push、無刪分支、無無關重構。
- 本次屬可回復的程式／UI成套切換，沒有schema／設定／權限／資料遷移；依風險採Medium lane。
  deployment-release-gate技能未提供，使用專案既有固定成品／唯讀預檢／rollback／smoke流程，保留同等安全邊界。
- 不執行原dirty start.ps1、database update、migration端點或IntegrationVerifier；Program.cs啟動不含migration。
- 原生幾何重建、第二台實機、完整Auth與歷史資料修復維持既有範圍外限制。

## 本輪關卡與停止條件

1. 提交前核對53份證據SHA、134項SQLite／PG既有證據、36項前端與四viewport畫面；重跑SQLite／前端。
2. 提交DEV-015並push；來源／遠端main無未知變更時才fast-forward main並push。
3. 從固定合併commit建置新的API／Web成品；契約與原生測試載入的產品DLL需與待套用成品一致。
   SQLite／私有55433 PostgreSQL隔離回歸、私有CAD副本驗證；不以合成CAD冒充原生證據。
4. 目前5432/swpdm只在READ ONLY交易擷取計數／雜湊、結構／序號／migration metadata；Vault／設定只雜湊，不存原內容。
   加入兩張批次表資料／結構核對，確認必要schema已存在；不執行migration或修資料。
5. 新API先於5001唯讀預檢；目前服務owner、舊成品SHA、前端位置與新成品皆確認後，才切換5000／5174。
6. 主要寫入流程只在隔離資料驗證；目前環境只做GET／畫面smoke，核對實際資料數，桌面／平板／手機可見錯誤與overflow。
7. 後快照與前快照一致、LAN／loopback皆可用後完成；封存證據、停止私有測試程序，提交／push本次套用紀錄。

未知port owner、測試／原生／資料雜湊／必要schema不符、非預期migration或正常畫面alert即停止；不以放寬安全檢查換取通過。
若切換後失敗，只停止經核對的本次程序並恢復原固定API／Web；不Down schema、不回填資料、不改Vault。

## 回復目標

- API：原PID24072，frontend-clarity工作樹的`src/SWPdm.Api/bin/local-main-74cecca-20261022/api/SWPdm.Api.exe`；既有Start-Api.ps1只啟固定成品，不build／migration。
- Web：原PID27392，Vite preview5174，固定`src/SWPdm.Web/dist/local-main-e40f3e4-20261022/site`。
- 只切換本次明確5000／5174 owner；PG5432 PID5560不重啟。舊成品不覆寫、不刪除。

## 證據入口

- [DEV-014 QC與原生限制](../qc/package-integrity-qc-2026-10-22.md)
- [DEV-015 QC與首次失敗／複驗](../qc/vault-integrity-qc-2026-10-26.md)
- 本次新增證據集中`../qc/local-package-vault-2026-10-26/`，完成後補實際SHA、PID、驗證與切換結果。

使用思考習慣：#限制條件、#可驗證性、#變數控制

## 實際 Git 發布結果

- DEV-015產品／測試／原始證據與本次發布計畫，經明確allowlist選擇性stage，共78檔；53份原始證據Git blob與工作檔bytes一致，diff檢查通過。
- [功能提交14de0f5](https://github.com/balboku/SW-PDM/commit/14de0f58717cfa58c274efa9e72e6ca475c4e4da)已push至`codex/vault-integrity-20261026`；包含DEV-014祖先78492f8與c12b852。
- main由2e1e2b8快轉至14de0f58717cfa58c274efa9e72e6ca475c4e4da並正常push，git ls-remote確認來源／main相同。
  無merge conflict、force push、刪分支、PR或CI／部署hook；未納入原dirty checkout。
- 後續本次紀錄以純文件／證據提交至main並push，不改已測產品或重建服務中成品；最終main ref見完成回報與遠端。

## 固定成品與驗證結果

來源固定main `14de0f58717cfa58c274efa9e72e6ca475c4e4da`。以下目錄位於承載服務的frontend-clarity工作樹，均為新輸出，沒有覆寫舊成品：

- API／測試／啟動與回復輔助：`src/SWPdm.Api/bin/local-main-14de0f5-20261026/`（ignored，正在服務，勿封存此worktree）。
- Web：`src/SWPdm.Web/dist/local-main-14de0f5-20261026/site`；不固定loopback API，沿用host動態5000，支援既有LAN。
- [64份API／5份Web成品SHA](../qc/local-package-vault-2026-10-26/artifact-sha.json)；Start-Api／Start-Web每次啟動都核對固定bytes，不build、不migration。

| 驗證 | 實際結果／證據 |
|---|---|
| 提交前SQLite | [134/134](../qc/local-package-vault-2026-10-26/release-precommit-sqlite.trx)，0失敗／略過 |
| 固定發布API SQLite | [134/134](../qc/local-package-vault-2026-10-26/release-main-sqlite.trx)，0失敗／略過，19秒 |
| 同成品PostgreSQL16.14 | [134/134](../qc/local-package-vault-2026-10-26/release-main-postgres.trx)，0失敗／略過，8分23秒 |
| 前端邏輯 | [36/36](../qc/local-package-vault-2026-10-26/frontend-logic.txt)，0失敗／略過 |
| Web build | 通過；1426 modules，JS `index-tPhSW7r-.js`；僅既有Browserslist資料較舊提示，未升級依賴 |
| Native／UI fixture build | 0錯誤／0警告；ContractTests有既有xUnit2031分析建議，134項均實際執行 |
| 原生CAD同成品 | [11來源／4套件／22獨立重開／18參照](../qc/local-package-vault-2026-10-26/native-package-result.json)；同名碰撞、搬移、manifest、來源／私有Vault／合成BOM均通過 |
| 目前schema隔離API5002 | [完整結果](../qc/local-package-vault-2026-10-26/isolated-http.json)：分頁全體4份、新取得3鎖／沿用1、復原3、重送0、錯actor403、後續新鎖保護與單圖復原1 |
| 套件負向 | 5002合成版本故意無SHA，實際409／PACKAGE_CHECKSUM_MISSING，沒有不完整ZIP；不是原生成功證據 |
| 目前API5001 | 強制DB READ ONLY，health與新分頁查詢通過；0份與唯讀快照一致，未執行寫入探測 |
| 正式畫面 | [10組切換前後畫面／DOM](../qc/local-package-vault-2026-10-26/browser-audit.json)，含四種viewport、搜尋／清除、匯入入口與概覽完成狀態；0可見alert、0橫向溢出、console error 0 |
| LAN／loopback | API5000與Web5174均200，既有LAN origin的CORS正確；未改網路／防火牆設定 |

API DLL在發布、ContractTests、NativePackageVerifier、UiFixture四處SHA相同：`531CA31C1912F724A85B30B0922183D64A8874B5EBF52E7C522FB11F261A3117`。
Sample四處皆`03E07B9118243626DFD46CB1927FE011C997D28F212A18A3F33C0E8C8CF27B67`；interop為`F2DD975C978041E84C719EF5B522BB06F156A42A185EC619513779F816EAAF6F`。
Web JS SHA=`3DD2AF6FE4D66BE54F293203A6470087821217DBFE6C416FF08FC591BD0B2E34`。
因固定commit重建成品SHA與隔離開發階段不同，本次重新執行PG與原生驗證，未冒用舊DLL的同成品證據。

schema演練只用pg_dump --schema-only唯讀匯出目前結構，還原至全新55433/release_http_20261026；無業務資料複製或正式DDL。
合成4份文件／4版本／3BOM由[受目標護欄的SQL](../qc/local-package-vault-2026-10-26/seed-isolated.sql)建立；不是對目前資料修復或執行migration。
原生樣本取自先前私有測試樣本的全新GUID副本，未從現行Vault取樣或寫原檔；金鑰只在子程序環境使用，未封存或輸出。

## 切換、資料保護與畫面判定

[切換結果](../qc/local-package-vault-2026-10-26/cutover.json)：2026-10-26 13:34:49（Asia/Taipei）完成。
API5000：PID24072→24528；Web5174：PID27392→3308。PostgreSQL5432仍PID5560，不重啟。
新程序均核對命令列／port owner／固定SHA後啟動；API先health成功，再切換Web；沒有呼叫migration端點或自動migration。

[之前](../qc/local-package-vault-2026-10-26/before.json)、[切換前](../qc/local-package-vault-2026-10-26/precutover.json)、[之後](../qc/local-package-vault-2026-10-26/after.json)三份摘要bytes SHA皆為：
`2BF12E9D6D89EF5478F378164D0BE59C490F554A113261F0EBCB94834DDEE230`。

- 九張業務／批次表均0筆，逐表MD5相同，包括BOM與批次紀錄；未寫入既有業務資料。
- migration維持9筆及原雜湊eef960f638ec6682b52803f19d81e5c3；81欄、23約束、26索引、7序號指紋相同。
- Vault維持0檔，路徑／大小／內容聚合SHA相同；appsettings兩檔SHA不變，未封存內容。
- 快照用REPEATABLE READ READ ONLY及程序PGOPTIONS強制唯讀，只有計數／雜湊／metadata，不是業務內容備份。
- 目前環境只做GET／畫面smoke，不匯入／出庫／取消／下載原圖測試。全部0是已核實空庫基準，不是載入失敗。
  主流程寫入只在合成私有資料完成；不聲稱已驗收目前真實CAD資料。

實際同一瀏覽器頁在切換前後reload複驗，1440×900、1024×768、768×1024、390×844均無可見失敗／溢出。
「舊API最多50筆／未檢測」已改為新分頁／異常篩選；空庫有首次匯入CTA，搜尋無結果提供清除。
概覽在loading紀錄後另保存完成狀態，確認DB連線通過、Vault路徑與原生金鑰已設定；不把設定成功等同實際解析通過。
代表畫面：[最終Vault](../qc/local-package-vault-2026-10-26/after-live-final-vault.jpg)、[手機](../qc/local-package-vault-2026-10-26/after-live-mobile.jpg)、[概覽完成](../qc/local-package-vault-2026-10-26/after-live-overview-settled.jpg)。

## 正式核准、遇到的問題與回復

外部worktree／網路／CIM／資料庫與服務切換均經require_escalated／auto_review正式核准，沒有更換工具或繞過拒絕；無政策阻塞殘留。
同一代理執行PM／QA／QC，不宣稱獨立人員審核。

1. main工作樹首次NativePackageVerifier --no-restore缺project.assets.json，建置停止；只還原既有套件後重建成功，無產品／依賴版本修改。
2. 證據SHA彙整的工具回應處理初次失敗，未建立或修改成品；之後確認exit code並成功建立完整manifest。
3. 隔離HTTP驗證器首次把application/problem+json的byte[]直接當JSON，末項拒絕判定未通過；實際API已正確409／PACKAGE_CHECKSUM_MISSING。
   僅修正驗證器UTF8解碼後重跑，全項通過；未改產品或放寬拒絕。當時單圖已復原，重跑仍只追加私有批次測試紀錄。
4. 清理護欄因Windows命令列反斜線先停止，沒有誤停正式程序；讀取真實owner後正規化路徑再完成清理。
   同步補正Rollback的Web owner路徑正規化；沒有為通過而移除owner／SHA護欄。
5. main checkout將舊證據的.gitattributes／.gitignore兩個控制檔換行標準化；TRX／JSON／TXT／JPEG原始證據均未變。
   僅補控制檔本身的-text規則並更新這兩筆metadata雜湊，重新核對53份；新發版證據亦鎖定PS1／SQL與控制檔bytes，避免後續checkout誤報。

本次沒有觸發實際rollback，不能宣稱已演練；已保留且核對舊API／WebSHA，回復腳本只允許本固定新程序或既有舊程序，不碰schema／資料／Vault。
本機輔助腳本保留於固定API根目錄並封存同bytes供續接：[Start-Api](../qc/local-package-vault-2026-10-26/Start-Api.ps1)、[Start-Web](../qc/local-package-vault-2026-10-26/Start-Web.ps1)、[Rollback](../qc/local-package-vault-2026-10-26/Rollback.ps1)、[唯讀Snapshot](../qc/local-package-vault-2026-10-26/Snapshot.ps1)。
請從ignored固定成品根目錄執行對應腳本，不在QC證據目錄直接啟動；勿用原dirty start.ps1重建／migration。

## 清理、最終邊界與限制

- 5001 PID19620、5002 PID27708、5175 PID15404已核對後停止；私有PG25184已停止，55433無listener。
- contract GUID庫後檢0；自建release_http_20261026及本輪新GUID CAD副本已移除，原樣本未刪；僅清理本次自有測試資料。
  PG忽略的日誌／叢集、固定新舊成品與證據保留。瀏覽器viewport reset、暫用tab關閉。
- 原工作區仍codex/trial-feedback-20261016、3d36d1ca142959cd4dcf95b7a7f4f83c907f8752、52項。
  tracked diff=eb8bdd8fb2032a4a299f44bcc7696e57d216192f，index=e69de29bb2d1d6434b8b29ae775ad8c2e48c5391；來源DEV-015 worktree乾淨。
- 本機目前僅新API5000 PID24528、Web5174 PID3308與既有PG5432 PID5560。未增加自動開機／Windows服務設定。
- 本輪DEV-014／015提交／合併／套用工作完成；完整SolidWorks幾何、第二台實機、完整Auth與歷史資料修復仍不在本輪。
  DB空庫所以不虛構目前資料的入庫或CAD成功案例；本輪不要求改現有資料來製造畫面證據。
