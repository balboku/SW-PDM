# DEV-015 Vault 完整清單、參照／屬性與批次 UI：隔離 QC

2026-10-26。結論：本輪隔離開發／驗證通過；未提交、push、合併 main 或正式套用。
來源：使用者「繼續完成剩餘任務」。接續 DEV-014 已發布分支，不擴大為資料修復或部署授權。
契約：[ADR-005](../decisions/ADR-005-vault-integrity-ui.md)。

## 範圍與角色

- 分支 `codex/vault-integrity-20261026`，基準 `c12b852f12d206d2bb2e416edb8e811ad57bb44c`。
  DEV-014 的 `codex/package-integrity-20261022` 保持原 SHA；main 保持 `2e1e2b89cfcaf90aba47214fb4435eef041b69e3`。
- RD：唯讀查詢／integrity service、DTO／預覽補正、Vault／屬性／批次取消介面、必要測試。
  QA：先建立 ADR-005 的資料、範圍、安全與 viewport 契約，再實作。
  QC：真實 HTTP、兩種資料庫、實際瀏覽器及前後 Git／程序邊界核對。
  三角色由同一代理分階段執行，不宣稱獨立人員驗收。
- 使用 hcs、dev-pm、agent-role-playbook、ui-ux-design-principles 與 computer-use。
  UX 決策著重任務範圍、可行下一步、錯誤／未知不冒充成功；沿用既有 drawer／modal，沒有重做整套視覺設計。
- 未改 entity／模型／migration、repository、入庫／parser、批次核心、套件後端或原生 adapter。
  `ContractTests.cs` 僅註冊新 service；`CheckoutBatchTests.cs` 只新增唯讀預覽整合案例。

## 完成內容

1. 完整分頁、穩定排序、篩選結果全體摘要；搜尋目前版本屬性（含組態），不把前50份當全部。
   字面處理特殊字元，頁碼／大小／條件驗證；翻頁不套用草稿，失敗重試保留最後送出的條件。
2. Vault 逐列顯示缺失／待確認、新版參照；明細列安全檔名、屬性來源／衝突、組態及解析狀態。
   虛擬件不標成缺檔，抑制未解析仍需確認。明示快照與未重驗實體 CAD／設計依賴，不回填歷史 BOM。
3. 批次復原必須本人名稱、明確批次與成功預覽；不自動選第一批、不推算歷史批次。
   顯示可解除／保留數及原因，送出時仍以後端重新核對為準；完成後重新讀取根圖真實鎖狀態。
4. 出庫預覽補齊 documentId，以文件身分查目前鎖（即使父檔引用舊版），排除歷史工程圖；where-used 父文件不算鎖定範圍。
   此為唯讀查詢補正，不更改批次取得／復原規則。
5. 套件下載失敗保留原 code／issues 及可行下一步；不產出不完整 ZIP，也不把下載稱為幾何驗收。

## 測試與成品

| Gate | 最終結果 | 原始證據 |
|---|---|---|
| SQLite 真實產品 HTTP／EF | 134/134，0失敗、0略過 | [TRX](vault-integrity-2026-10-26/dev015-preview-sqlite.trx) |
| PostgreSQL 16.14 真實產品 HTTP／EF | 134/134，0失敗、0略過（9分6秒） | [TRX](vault-integrity-2026-10-26/dev015-preview-postgres.trx) |
| 前端純邏輯 | 36/36，含原13項 | [原始輸出](vault-integrity-2026-10-26/frontend-logic.txt) |
| 前端 production build | 通過，1426 modules | 最終 JS `index-CAFec57o.js`；下方 SHA |
| UI fixture build | 0錯誤、0警告 | Release，與測試 API DLL SHA 相同 |
| 真實瀏覽器 | 四種viewport、39組紀錄；首次失敗保留並修正複驗 | [DOM／計數稽核](vault-integrity-2026-10-26/browser-audit.json)與同名JPEG |
| 邊界／清理 | 原dirty／main／現行服務不變；隔離程序已停止 | 下節具體SHA／PID |

新增22項 HTTP案例（21項Vault＋1項預覽整合），加原112項。Vault測試有72份合成文件、完整掃頁無重漏、同時間穩定排序、全體摘要、特殊字元、歷史屬性排除、缺目前版本、虛擬／抑制／Broken、來源衝突與版次回退；唯讀測試比較前後文件、版本、BOM與屬性快照。
預覽整合額外確認舊引用仍顯示同一文件目前鎖、排除歷史工程圖、保留where-used純查閱，並與實際出庫4份新鎖／1份沿用對照。

API DLL（產品／ContractTests／UiFixture三者相同）：
`1F93F45B0146DEB6E4AA09A3D61C5B319347624134D9B396109AB70BA05F0D16`。
Sample DLL：`2861E2672B7EB51E64B10231A936CFCBDD2CA2756DC36C3EC2E1A4FFCA620ED2`。
最終 Web JS：`98BD1E9FA2BE20C38CE4D0E77607DDAA32AEFDE0803A97B8D47E9228AC20C5A4`。

前端僅有既有 Browserslist 資料較舊提示，未升級依賴；契約測試新增 Assert.Single 寫法有 xUnit2031 分析建議，不影響134項執行或判定。

## 實際 UI 驗收

39組是畫面／DOM紀錄，不是39項全部一次通過。首輪的3個非預期產品問題列於下一節；錯誤注入本身的alert是預期結果，不冒稱正常無錯。
全部紀錄的 document.scrollWidth 不超過 viewport；四種viewport與最終正常／恢復畫面均0非預期alert。

| 操作與尺寸 | 實際結果 | 代表證據（同目錄有完整DOM） |
|---|---|---|
| 桌面1440×900完整分頁 | 60總數、50／10兩頁；草稿輸入不影響翻頁；Missing篩選1份、3處 | [第二頁](vault-integrity-2026-10-26/desktop-final-page2.jpg)、[篩選](vault-integrity-2026-10-26/desktop-final-missing-filter.jpg) |
| 桌面異常明細 | 目前快照、舊版提醒、人工待確認／抑制參照及下一步可見 | [明細](vault-integrity-2026-10-26/desktop-final-integrity.jpg) |
| 平板1024×768出庫 | 根1＋必要2＝3；58既有鎖正確標示；送出取得2、沿用1 | [預覽](vault-integrity-2026-10-26/tablet-checkout-preview-fixed.jpg) |
| 平板768×1024批次競態 | 預覽2；確認前根鎖改變後，只解除59、保留60；返回後仍可「修改後入庫」 | [實際結果](vault-integrity-2026-10-26/tablet-batch-race-result.jpg)、[新鎖保留](vault-integrity-2026-10-26/tablet-root-new-lock-retained.jpg) |
| 手機390×844批次 | 不自動選批次；舊批次0不可送出；active批次解除2、保留3（他人／已解鎖／新鎖） | [預覽](vault-integrity-2026-10-26/mobile-batch-preview.jpg)、[結果](vault-integrity-2026-10-26/mobile-batch-result.jpg) |
| 單圖取消 | 只解除根圖1份，其餘保留；全庫已出庫數回3 | [結果](vault-integrity-2026-10-26/tablet-single-restore.jpg) |
| 屬性搜尋／衝突 | TOL-0.05找到58目前Ver2與Alternate組態；54衝突不任選品名 | [組態](vault-integrity-2026-10-26/tablet-current-configuration-properties.jpg)、[衝突](vault-integrity-2026-10-26/tablet-property-conflict.jpg) |
| 參照／批次清單失敗恢復 | 明細失敗停用出庫，重試後恢復；批次失敗不能確認，重試無批次引導單份 | [明細失敗](vault-integrity-2026-10-26/mobile-integrity-error.jpg)、[恢復](vault-integrity-2026-10-26/mobile-integrity-recovered.jpg)、[無批次](vault-integrity-2026-10-26/mobile-no-batch-recovered.jpg) |
| 清單失敗／畸形回應 | 顯示筆數未確認及重試，摘要為—；不冒充空圖庫 | [HTTP失敗](vault-integrity-2026-10-26/mobile-search-error.jpg)、[格式失敗](vault-integrity-2026-10-26/mobile-malformed-page-blocked.jpg) |
| 舊服務／空庫／查無結果 | 舊服務50非全部且異常篩選停用；空庫引導匯入、查無結果引導清除 | [舊服務](vault-integrity-2026-10-26/mobile-legacy-retry-fixed.jpg)、[空庫](vault-integrity-2026-10-26/mobile-empty-vault.jpg)、[無結果](vault-integrity-2026-10-26/mobile-no-matches.jpg) |
| 完整套件阻擋 | 真實PACKAGE_BOM_UNRESOLVED及安全檔名顯示；不提供不完整ZIP | [錯誤細節](vault-integrity-2026-10-26/tablet-package-error-details.jpg) |
| 鍵盤／長檔名 | Escape只關閉最上層modal，焦點回原觸發鈕；再Escape關drawer。手機長檔名換行、modal可捲動，不遮住可達CTA | 同批次與錯誤畫面及DOM |

最終平板橫／直清單與手機正常清單另存 `tablet-landscape-final-list.jpg`、`tablet-portrait-final-list.jpg`、`mobile-final-list.jpg`。
最後Web重建僅改查詢失敗重試與舊版分頁文字；其後重新驗證手機重試、舊API與空庫、桌面分頁／草稿／篩選、兩種平板清單及批次失敗恢復。批次核心／契約成品未再改。

## 首次失敗與修復（不刪證據）

- API首編譯：Api沒有SQLite擴充方法，改依ProviderName判別讀取交易隔離層級；未放寬PostgreSQL READ ONLY。
- 首次20項新測試6通過14失敗：fixture的OccurrencePath唯一鍵重複；修正後仍14失敗是使用不存在的Ambiguous狀態。
  fixture依既有schema改用Broken，未修改schema或正式資料。保留 `dev015-first-sqlite.trx`、`dev015-fixed-sqlite.trx`；修正後20/20，完整132/132兩種DB。
- 版次回退紅測試：預期B、實得快取D；缺版本Rev時恢復唯一文件層Revision優先，衝突則不猜值，保留快取與衝突提示。
  保留 `dev015-revision-first.trx`；修正後133/133兩種DB，最後新增預覽案例為134/134。
- 首次畫面把虛擬件顯示缺檔、無連結來源顯示Unknown File：結構樹改用旗標與安全來源檔名，新增純邏輯測試。
  首次 `desktop-integrity-drawer.jpg`；複驗 `desktop-integrity-fixed.jpg`與最終明細。
- 正常出庫預覽被停用（`mobile-checkout-preview.jpg`）：API缺documentId，且按引用版本查鎖會漏報目前鎖；另可能列入不會取得鎖的歷史工程圖。
  唯讀DTO／查詢補正＋新增HTTP案例，畫面複驗根1＋必要2，新取得2／沿用1。前端也保留本地格式錯誤的具體原因。
- 清除條件的請求失敗後，重試誤用上一組成功條件：提交時保存查詢條件，分頁仍不套草稿。
  首次 `mobile-retry-filter-first-failure.jpg`；複驗 `mobile-legacy-retry-fixed.jpg`從0錯誤條件恢復50份有限清單。
- 瀏覽器初次連線曾timeout，重新取得已存在頁籤後正常；不是產品通過／失敗數。
  sandbox曾拒絕對5010的唯讀socket及外部worktree Git存取；依正式require_escalated核准續作，未更換工具繞過，也未改平台政策。

## 隔離、清理與原環境不變證據

- PostgreSQL只使用新建 `tests/SWPdm.ContractTests/bin/dev015-pg-20261026/data`，127.0.0.1:55433、contract使用者；每例建立／移除自己的GUID庫，測試只EnsureCreated、不Migrate。
  完成後`swpdm_contract_%`資料庫0個；核對PID12964、命令列與資料目錄後pg_ctl fast stop成功，postmaster.pid不存在。
  PG測試目錄／log保留在ignored bin，不刪歷史或現行資料。
- UI只用5010與5175、唯一SQLite記憶體庫／合成文字Vault。最終fixture7472、preview21216核對命令列／port後停止；僅移除本輪兩個GUID合成TEMP資料夾。
  瀏覽器viewport已reset，暫用tab已關閉。55433／5010／5175均無listener。
- 現行服務仍為API5000 PID24072、Web5174 PID27392、PG5432 PID5560；未重建服務中的main成品，未讀取／寫入swpdm資料庫。
- 原工作區仍 `codex/trial-feedback-20261016`、HEAD `3d36d1ca142959cd4dcf95b7a7f4f83c907f8752`、52項porcelain。
  tracked diff hash仍 `eb8bdd8fb2032a4a299f44bcc7696e57d216192f`，index diff hash仍 `e69de29bb2d1d6434b8b29ae775ad8c2e48c5391`。
  main工作樹仍乾淨、HEAD `2e1e2b89cfcaf90aba47214fb4435eef041b69e3`；DEV-014分支仍c12b852。
- 本輪git diff --check通過，沒有stage／commit／push。證據原始bytes以局部gitattributes保留；SHA清單見同目錄 `evidence.sha256`。
  本輪沒有取得現行資料庫／Vault前後指紋，不能冒稱做過該項比對；保護證據是未連線、未寫入、Git／程序邊界不變。

## 剩餘工作與限制

本輪DEV-015開發驗證無待修阻塞；未完成的是另行授權的提交／push、DEV-014／015合併與本機套用。
完整SolidWorks幾何重建、第二台實機開啟、完整Auth與歷史資料修復仍是既有範圍外驗收；快照正常、DM重開或HTTP綠燈均不能代替。
若後續要求發版，依DEV-014＋DEV-015相依性成套處理，保留其他dirty後端，不執行未授權migration。

使用思考習慣：#可驗證性、#限制條件、#溝通設計

## 後續發布指示（2026-10-26）

使用者後續明確要求提交／push、合併與正式套用。上方未提交／未部署是隔離階段時點；後續結果與共用關卡另見[批次發版紀錄](../reports/local-package-vault-main-2026-10-26.md)。
原始隔離證據、首次失敗與驗收限制不改；資料／BOM／Vault修復與其他migration仍未納入。
後續結果：功能14de0f5已提交／push、快轉合併main並成套套用；同固定成品SQLite／PG各134、前端36、原生11CAD／4套件／22重開／18參照及四viewport後驗證通過。詳見同一批次報告，不將歷史未部署時點當現況。
