# SW-PDM DEV 任務

更新日期：2026-10-22
文件狀態：DEV-013限定migration與main本機套用已完成，未混入其他未完成後端或修改既有業務資料／歷史BOM；其餘階段限制與交付狀態保留為歷史紀錄。

最新本機套用：使用者已明確核准僅DEV-013 migration；5432/swpdm已套用20261022073552_AddCheckoutBatches，API5000已切至固定main 74cecca。
兩張新表、兩個主鍵、一個Restrict外鍵、一個查詢索引及一筆遷移紀錄新增完成；既有業務資料／歷史BOM、舊結構、序號、Vault及設定指紋不變。
同成品SQLite／PG各67/67、前端邏輯13/13、實際遷移後隔離HTTP驗證通過；[本次套用紀錄](reports/local-checkout-batch-main-2026-10-22.md)。
目前交付：DEV-013整批出庫安全復原後端隔離通過，原開發基準main 2f22791；原隔離開發階段未連線目前資料庫，migration未執行。
原隔離階段只新增模型與migration原始碼，本次後續已限定套用；不混入DEV-014／015、完整Auth或原dirty後端。
功能提交d5c910b及狀態提交ef40c1b已由codex/batch-checkout-recovery-20261022快轉合併main並push；本次後續套用已完成，紀錄尚未commit／push。
DEV-012本機套用紀錄已合併main並push；[套用與隔離驗證](reports/local-ingest-integrity-main-2026-10-22.md)
保留舊固定成品及歷史資料指紋；DEV-013只替換API，Web5174沿用與main相同的既有前端，沒有重啟。

歷史契約切片套用結果：先前工具拒絕經正式require_escalated／auto_review續作，桌面／平板／手機與錯誤恢復驗證通過；當時5000／5174使用main e40f3e4固定成品，DB／Vault及原WIP指紋不變。[當時紀錄](reports/local-checkout-reused-main-2026-10-22.md)保留切換異常、恢復與後驗證。該階段未執行migration；最新狀態以上方DEV-013為準。

## 總任務清單

- ✓ DEV-012 [交付點] [核心切片通過] [P0] [本機已套用／資料未變] 入庫版本、共用件與解析器整合
  - 摘要：WIP 同 Rev 建立新 Ver、相同身分與內容沿用、直接參照與設計依賴分流。
  - 來源 ID：2026-10-22 使用者同意先完成 WIP、共用件與解析器整合。
  - 下一步：DEV-013後端隔離已通過，可依其結果獨立續接DEV-014；本輪不延伸至其他後端或資料寫入。
  - 阻塞 / 恢復條件：除另行核准的DEV-013外，仍禁止其他migration、目前資料／歷史BOM修復；原隔離階段的不部署限制已由後續套用授權取代。
  - 證據：reports/local-ingest-integrity-main-2026-10-22.md；同成品SQLite／PG各41項與11份原生CAD。
  - 計入交付：是

- ✓ DEV-013 [交付點] [後端及本機套用通過] [P1] [限定遷移已套用] 整批出庫安全復原
  - 摘要：持久記錄當次新取得的鎖，依批次預覽並安全復原；既有鎖與新作業鎖不解除。
  - 來源 ID：2026-10-22 使用者指定接續 DEV-013，禁止正式套用、migration 與目前資料寫入。
  - 下一步：本次套用紀錄待使用者決定是否提交／push；批次畫面仍由DEV-015另行整合。
  - 阻塞 / 恢復條件：本次只核准DEV-013新增結構；其他migration、業務／歷史BOM修復仍未授權。
  - 證據：reports/local-checkout-batch-main-2026-10-22.md；同成品SQLite／PG各67項、前端邏輯13項及隔離HTTP。
  - 計入交付：是

- ○ DEV-014 [交付點] [待前置驗證] [P0] [後續獨立切片] 完整套件與副本參照重寫
  - 摘要：完整 CAD 預檢、選版依賴與跨工作站可攜性，不修改庫內原檔。
  - 下一步：入庫解析證據穩定後，獨立完成套件關卡；不因解析器通過標為完成。
  - 計入交付：是

- ○ DEV-015 [交付點] [待前置驗證] [P1] [後續獨立切片] Vault 完整清單與真實異常整合
  - 摘要：完整分頁、屬性與 CAD 異常提示；已完成的前端清晰度不重作。
  - 下一步：以驗證過的後端契約整合清單及真實 CAD 異常。
  - 計入交付：是

- ✓ DEV-001 [交付點] [實作完成] [P0] [本輪已完成] 試用人員回饋優化
  - 摘要：提供安全的改名入庫、可見的 2D/3D 雙向關聯、可包含工程圖的 Pack & Go，
    以及保留資料夾結構的 CAD 批次匯入。
  - 來源 ID：`SWPDM-TRIAL-FEEDBACK-20260729`
  - 父任務：無
  - 下一步：本輪main契約切片本機套用與隔離驗證已完成，未混入其他後端。
    真實CAD入庫與目前資料寫入測試不在本輪；固定成品啟動與回復方式見本輪紀錄。
  - 批次發版：`reports/local-release-2026-08-04.md`
  - 計入交付：是

- ✓ DEV-002 [開發點] [已完成] [P0] [本輪已完成] 入庫目標身分驗證
  - 摘要：將 Check-in 綁定目標文件 ID，允許改名但阻止品號或文件類型不符的檔案覆寫。
  - 來源 ID：`SWPDM-TRIAL-01`
  - 父任務：DEV-001
  - 下一步：已完成；不符身分 live 測試回傳 409，且目標文件仍維持出庫鎖
  - 計入交付：否

- ✓ DEV-003 [開發點] [已完成] [P0] [本輪已完成] 雙向工程圖關聯
  - 摘要：讓 Part 與 Assembly 明細可反查並顯示關聯 Drawing，包含檔名 fallback 狀態。
  - 來源 ID：`SWPDM-TRIAL-05`
  - 父任務：DEV-001
  - 下一步：已完成；Part 與 Assembly live fixture 均顯示反向 Drawing
  - 計入交付：否

- ✓ DEV-004 [開發點] [已完成] [P1] [本輪已完成] Pack & Go 包含工程圖
  - 摘要：在下載前顯示關聯工程圖，預設納入套件並允許使用者取消。
  - 來源 ID：`SWPDM-TRIAL-02`
  - 父任務：DEV-001
  - 下一步：已完成；含圖面 ZIP 比基準套件多 2 張 SLDDRW
  - 計入交付：否

- ✓ DEV-005 [開發點] [已完成] [P1] [本輪已完成] 安全批次匯入
  - 摘要：支援資料夾或多檔選取、相對路徑 staging、逐檔隔離處理與結果摘要。
  - 來源 ID：`SWPDM-TRIAL-03`
  - 父任務：DEV-001
  - 下一步：已完成；traversal、逐檔錯誤與暫存清理已驗證
  - 計入交付：否

- ◐ DEV-006 [QA/QC] [限制式通過] [P0] [本輪已完成] 試用回饋驗證
  - 摘要：驗證資料身分安全、關聯完整性、批次失敗隔離、建置與三種 viewport UI。
  - 來源 ID：`SWPDM-TRIAL-QA-20260729`
  - 父任務：DEV-001
  - 下一步：若進入 release，再補受控資料 mutation 的正向 Check-in 與 mixed-result batch fixture
  - 計入交付：否

- ✓ DEV-007 [交付點] [實作完成] [P0] [本輪已完成] 出庫後另存為新料號
  - 摘要：品號變更時建立新文件身分、保留來源稽核關聯，並禁止覆寫原文件與歷史 BOM。
  - 來源 ID：`SWPDM-PART-NUMBER-BRANCH-20260804`
  - 父任務：無
  - 下一步：本機 migration 與 production-mode smoke 已完成；遠端發版需另提供部署目標
  - 證據：`decisions/ADR-001-part-number-branch.md`、`qc/part-number-branch-qc-2026-08-04.md`
  - 批次發版：`reports/local-release-2026-08-04.md`
  - 計入交付：是

- ✓ DEV-008 [交付點] [實作完成] [P1] [本輪已完成] 圖檔中心顯示圖號
  - 摘要：從目前版本的 CAD「圖號」自訂屬性顯示圖號，並支援清單搜尋與明細辨識。
  - 來源 ID：`SWPDM-DRAWING-NUMBER-20260805`
  - 父任務：無
  - 下一步：已完成；納入 2026-08-26 GitHub release candidate
  - 證據：`qc/drawing-number-qc-2026-08-05.md`
  - 計入交付：是

- ✓ DEV-009 [交付點] [完成] [P1] [本輪已完成] 父文件舊版參照提示
  - 摘要：在圖檔中心清單直接辨識目前父版本引用的舊版子文件，並於文件詳情顯示完整更新指引。
  - 來源 ID：`SWPDM-REFERENCE-UPDATE-WARNING-20260805`
  - 父任務：無
  - 下一步：已完成；納入 2026-08-26 GitHub release candidate
  - 證據：`qc/reference-update-warning-qc-2026-08-05.md`
  - 計入交付：是

- ✓ DEV-010 [交付點] [完成] [P1] [本輪已完成] Vault 寬螢幕與資訊密度優化
  - 摘要：解除 Vault 的共用 1280px 寬度限制，利用頁首空間顯示結果、異常、已出庫與可用摘要，並擴充寬螢幕詳情區。
  - 來源 ID：`SWPDM-VAULT-SPACE-20260805`
  - 父任務：無
  - 下一步：已完成；納入 2026-08-26 GitHub release candidate
  - 證據：`qc/vault-space-utilization-qc-2026-08-05.md`
  - 計入交付：是

- ✓ DEV-011 [開發點] [完成] [P0] [本輪已完成] 內網圖面清單可見性修正
  - 摘要：修正前端可由同仁開啟、但 API 僅監聽 localhost 而無法載入圖面清單的問題，
    並在連線失敗時提供明確提示與重新連線動作。
  - 來源 ID：`SWPDM-LAN-VAULT-VISIBILITY-20260811`
  - 父任務：DEV-001
  - 下一步：請同仁以 `Ctrl+F5` 重新載入 LAN Vault，完成實際工作站使用者確認
  - 證據：`qc/lan-vault-visibility-qc-2026-08-11.md`
  - 計入交付：否

## DEV-012：2026-10-22 入庫核心隔離切片

狀態：本輪核心切片隔離 QC 通過；未正式套用。基準 main 40f7d70，分支 codex/ingest-integrity-20261022。
依使用者列出的三項成果，這輪先完成 DEV-012；DEV-013～015 為相依後續切片，不混入原工作區整包後端。
本輪取代下方歷史契約的「WIP／解析器／共用件不在本輪」，不放寬其他限制。

### 範圍、資料與相容性契約

- 文件身分仍為品號＋文件類型，不依來源路徑或檔名偷偷合併；衝突屬性／重複身分阻擋。
- 明確目標 Check-in 仍先檢查品號／類型／持鎖人與有效時間；相同內容及原始檔名 Reused，不遞迴、不寫庫、不解鎖。
- WIP 內容更新或明確改名：原因必填，可維持工程 Rev 建立遞增 Ver；成功只解除該文件鎖。
  InReview／Obsolete／未知狀態禁止更新；Released 需要非空且不同的 Rev，建立 WIP，原版本不變。
- 非明確目標的匯入／參照，相同身分及內容沿用目前版本，可使用不同本機檔名；
  不變更原檔名、版本、鎖或歷史 BOM。內容不同必須從 Vault 明確 Check-in，不允許批次覆寫。
- 參照以品號＋類型＋實際 SHA256 比對既有版本；沒有實體內容不能只憑檔名挑最新版。
  工程圖無原生模型參照時不偽造同品號關聯；新匯入永不回填其他歷史 BOM。
  BOM 查詢亦只能回傳儲存中的關聯，不依檔名動態偽裝為 Resolved。
- Drawing 使用 GetAllExternalReferences4；Assembly 目前儲存組態的直接元件保留 occurrence、
  虛擬／抑制與組態。Part 設計依賴及其他未分類依賴不扁平化成 BOM，回傳警告。
- 保留既有 CAD adapter、rootOutcome、files[].outcome 及所有取消出庫契約。
  使用現有 BOM 欄位；匹配說明回傳 issues，不加入欄位、migration 或新的批次資料表。
- 批次只允許選定 staging 清單遞迴匯入；未選檔只可唯讀比對。存在但解析失敗的子件使該根交易失敗。
  一個批次允許逐根結果，不宣稱整批單一交易。循環必須阻擋。
- 寫入使用 Serializable 交易；競爭更新／唯一性衝突回可重試 409，不自動重試或解除鎖。
- 目前執行的 API 5000、Web 5174、資料庫 5432 與 Vault 均不作為測試目標。
  SQLite 記憶體庫／獨立 PostgreSQL 55433 新測試庫只用 EnsureCreated；不執行任何 migration。
- 不在本輪：批次取消出庫、套件重寫、Vault 分頁、完整 Auth、歷史修復、前端版面、lint 工具整理、
  正式套用、commit／push。原 dirty 工作區保持不變。

### QA 計畫與停止條件

| 失效風險 | 優先級 | 驗收／證據 |
|---|---|---|
| 同 Rev 更新被拒或歷史被改寫 | P0 | WIP 正向新 Ver；舊版本、屬性與 BOM 快照不變 |
| 共用件被覆寫或解他人鎖 | P0 | 相同內容／改名沿用；不同內容拒絕；明確目標仍驗證持鎖人 |
| 虛擬／抑制／設計依賴誤匯入 | P0 | typed references、重複 occurrence、Drawing／Part 分流；可用原生 CAD 唯讀比對 |
| 解析失敗後部分根資料殘留 | P0 | 子件失敗／循環根交易回滾；同 scope 重試與檔案副作用記錄 |
| 無證據關聯到最新版本 | P0 | 身分＋內容精確匹配；缺檔／同名歧義不配對；歷史 missing-link 不回填 |
| 既有取消出庫／Reused 回歸 | P0 | 原 HTTP 契約測試及 UI 邏輯測試保持通過 |
| 競爭更新造成遺失版本 | P0 | PostgreSQL 隔離競爭操作；409 無內部 SQL／連線字串外洩 |

通過標準：focused 測試、既有契約回歸及建置通過；PostgreSQL 與原生 CAD 分別列出實際結果。
若原生 CAD 檔案或環境不足，列為未充分驗證，不把合成 parser 當成真實 SolidWorks 驗收。
需要 migration、正式寫入、歷史修復、放寬鎖保護或帶入其他未完成後端時停止，不自動擴大範圍。
本輪不變更文件身分／批次政策；沿用既有 ADR-001 與先前 ADR-002 的入庫決策，以上為目前切片的權威契約。

### QC 結果

SQLite HTTP 41/41、PostgreSQL 16.14 HTTP 41/41（0 略過）、既有前端邏輯 13/13 通過；
11 份真實 CAD 的原生參照／旗標比對、3 個根文件 typed BOM、同 Rev 真實內容更新與共用件沿用通過。
無 interop 假成功防護通過；Release API 0 警告／0 錯誤；原 dirty 指紋不變，未執行 migration。
完整命令、過程中失敗與排除範圍見 [QC](qc/ingest-integrity-qc-2026-10-22.md)。
不以本輪結果冒充跨工作站幾何／套件或 DEV-013～015 UI 整合通過。

## DEV-013：2026-10-22 整批出庫安全復原

狀態：後端契約隔離通過，後續限定migration與本機套用亦已完成；原開發基準main 2f22791。
分支 codex/batch-checkout-recovery-20261022，重用乾淨 checkout-reused-contract worktree。

- 使用者決策與範圍：新增批次取得鎖的永久紀錄、查詢、唯讀預覽與復原；只含必要模型／服務／端點／測試。
- 決策權威：[ADR-003](decisions/ADR-003-checkout-batch-recovery.md)。原單檔「拒絕批次」改為相容例外：
  只有明確 includeBatch=true 與有效 batchId 才可走批次；預設仍單檔且保持時間／版本保護。
- 取得：同一 Serializable 交易中取得鎖並寫入批次；只記錄新取得者，不納入先前本人鎖；他人衝突或不完整狀態整批回滾。
- 復原：只讀批次固定清單，不重算目前 BOM；每筆核對原 actor／微秒鎖定時間／version。
  已復原、已入庫、他人／新鎖、版本改變或文件消失者列出排除原因。符合者條件 UPDATE，競爭失敗則整筆交易回滾並409，不自動重試。
- API：既有checkout成功欄位保留，新增batchId與取得／沿用數；
  GET checkout-batches?actor=；GET undo-checkout-preview?actor=&includeBatch=true&batchId=；
  POST undo-checkout及舊checkin別名共用保護。缺欄位400、非本人403、找不到批次404、競爭409，重送只回0筆及排除原因。
- 資料：只新增兩張批次表的模型與未執行migration原始碼，不改既有文件／版本／BOM欄位。
  SQLite記憶體庫與127.0.0.1:55433全新contract cluster逐例EnsureCreated，不執行migration。
- 不在原實作階段：DEV-014套件、DEV-015清單與批次前端、parser／入庫規則、Auth、舊鎖推導／回填、部署、commit／push。
  使用者後續已分別核准本切片commit／push及合併main／push；再核准僅DEV-013 migration與本機套用，見下方後續套用結果。
  未記錄的歷史出庫仍只支援原單檔取消；actor是自填操作防護，不是登入授權。
- 原隔離階段停止條件：需要目前5432/swpdm、Vault、服務5000／5174或歷史BOM寫入、執行migration、放寬鎖保護或帶入其他dirty後端時停止；後續限定套用例外見下節。
- 驗收：新批次永久紀錄、排除既有鎖、本人／他人／新鎖／版本改變／已入庫／刪除／重送／併發與中途失敗回滾皆有HTTP證據；
  原41項契約及前端13項回歸、Release建置；SQLite與PG分列，不把API通過視為前端驗收。

### QA 計畫

| 失效模式 | 可能原因 | 使用者影響 | 偵測方式 | 優先級 | 對策／測試 |
|---|---|---|---|---|---|
| 解到先前本人鎖 | 依目前關聯重算 | 其他作業被中止 | 本人既有鎖、BOM後改 | P0 | 批次只存新取得ID |
| 解到新鎖／新版 | 只驗actor | 後續工作遺失 | time／version／owner變更 | P0 | 預覽與條件UPDATE雙重核對 |
| 中途失敗留下半批 | 多次更新無交易 | 不可預測鎖狀態 | 第二次UPDATE注入失敗 | P0 | 文件與稽核同交易回滾 |
| 競爭或重送重複復原 | 讀寫競爭 | 新鎖誤解 | 並行checkout／undo及重送 | P0 | Serializable＋CAS，409或0筆 |
| 舊單檔契約退化 | 共用端點分流 | 誤放寬解鎖 | 原HTTP負向與別名 | P0 | 預設單檔保護不變 |
| 測試碰目前資料庫 | 使用正式設定 | 測試污染 | fixture連線guard／環境設定 | P0 | 不讀正式設定；新GUID庫與Vault |

失敗時收集HTTP狀態／code、逐文件與批次快照、TRX及SHA；不得以重新跑成功隱藏首次失敗。
QC完成後再更新狀態；任何必測未通過不標完成。下一步為DEV-014獨立切片；DEV-015保留批次畫面整合驗收。

### QC 結果

SQLite與全新PostgreSQL16.14各67/67（原41＋新增26），前端純邏輯13/13，Release及UiFixture接線建置0警告／0錯誤。
保留首次58/61失敗及修復紀錄；離線EF模型一致、migration僅兩張新表原始碼，未執行。
臨時55433叢集已停止，測試庫後檢0個；未連線目前庫或操作正式服務。
後續Git指示已完成：功能與證據d5c910b及狀態ef40c1b已合併main並push；本次整合重用既有證據，不重跑測試或操作服務／資料庫。
詳見[QC與TRX雜湊](qc/batch-checkout-recovery-qc-2026-10-22.md)；本結果不是批次UI或正式schema套用驗收。

### 後續限定 migration 與本機套用結果

上述禁止部署／migration是原實作階段邊界；使用者後續明確核准僅20261022073552_AddCheckoutBatches及main本機套用。
已經隔離schema演練、同成品SQLite／PG各67項與實際API驗證後，於5432/swpdm單一交易新增兩表／必要約束索引／一筆紀錄。
既有七張業務表（含歷史BOM）內容、舊8筆遷移、72欄／20約束／23索引、7序號、Vault及設定指紋不變；兩張新表0筆。
API5000已使用main 74cecca固定成品，Web5174未變；本輪未執行其他migration或對目前庫呼叫寫入API。
驗收與停止條件：SQL只允許本migration；舊資料／來源／設定不同、隔離測試不過或未知服務owner即停止；失敗只回復API，不自動Down或修資料。
實際結果／回復方式／首次驗證腳本問題見[本機套用紀錄](reports/local-checkout-batch-main-2026-10-22.md)。本次紀錄未commit／push，DEV-015批次畫面仍未完成。

## DEV-001：2026-10-22 契約修正切片

狀態：本切片實作及隔離驗證完成；支援既有 DEV-001，不新增產品完成率。

- 使用者決策：只修正取消出庫與 Reused 前後端契約並隔離驗證，不正式套用，不更動目前資料庫，不混入其他後端變更。
- 基準：main `acbeee3`；獨立工作目錄與 `codex/checkout-reused-contract-20261022`。
- 範圍：單文件取消出庫預覽；POST 必須帶 actor、原樣鎖定時間、預期版本，由單一條件 UPDATE 保護；舊 checkin 解鎖別名採相同保護。
- Reused：僅明確 TargetDocumentId 的根文件，身分／持鎖人驗證後，相同 SHA256 與原始檔名才沿用原版本；不升版、不解鎖、不遞迴、不修歷史缺件。
- API：新增唯讀 `undo-checkout-preview?actor=`；POST 空本文、缺欄位及批次要求回 400，鎖／人／版本改變回 409 `LOCK_CHANGED`。新回應增加 rootOutcome 及 files[].outcome，既有欄位保留。
- 相容性：舊無本文解鎖請求刻意 fail closed；前後端日後需成套套用。舊成功入庫回應若無 outcome 維持 CreatedVersion；未知明示 outcome 不報成功。
- 不在本輪：批次復原、資料表／migration、WIP 同 Rev 規則、解析器修正、共用件策略、套件預檢／重寫、Vault 查詢、歷史 BOM 修復及正式部署。
- 停止條件：需要改 live schema／資料、放寬鎖定保護、依賴其他未完成功能，或原 dirty 工作目錄遭變更。
- ADR：不另建；這是局部契約修正，不更改文件身分／生命週期；單檔範圍刻意不實作原工作目錄的批次設計。

### UX 與模式矩陣

使用者要安全放棄修改或繼續持鎖，而非猜測「入庫成功」是否產生新版本。沿用既有 modal；主畫面保留範圍、操作人、鎖定狀態、下一步，安全限制降層。操作人是自填名稱，不是登入授權；不自動冒用顯示的持鎖人。

| 模式 | 來源／目標 | 範圍／安全策略 | 狀態與主要 CTA | 責任 |
|---|---|---|---|---|
| 取消出庫 | 本人操作／選取文件 | 1 份；伺服器人、時間、版本原子核對 | 預覽後確認解除；狀態變更則重新檢查 | 操作者 |
| Reused | 上傳內容／原文件版本 | 不寫版本或 BOM、不解鎖 | 預覽取消出庫；或保留出庫返回 | 操作者 |
| CreatedVersion | 上傳內容／新版本 | 保留原入庫身分與版次規則 | 入庫成功，重新載入清單 | 操作者 |

### QA 驗證計畫

| 失效模式 | 可能原因 | 使用者影響 | 偵測方式 | 優先級 | 對策／建議測試 |
|---|---|---|---|---|---|
| 誤解他人或新鎖 | 預覽後狀態改變 | 他人工作遭解鎖 | HTTP 負向與併發測試 | P0 | owner/time/version、重送只允許單一勝者 |
| 無本文或舊別名繞過 | 契約不一致 | 跳過保護 | 兩條 POST 負向測試 | P0 | 驗證 400／409 且資料不變 |
| Reused 被當新版本 | 混淆根與子件 outcome | 誤認入庫完成或鎖已解除 | HTTP 不變檢查、前端分流與 UI | P0 | 根結果優先、停留畫面與後續取消 |
| 隱藏限制或誤按批次 | 不清楚範圍與操作人 | 誤解其他關聯檔已解除 | 範圍提示與 disabled CTA | P1 | 批次要求拒絕、本人名稱與單檔提示 |
| 窄畫面溢出／錯誤不會恢復 | 新狀態與長字串 | 看不到按鈕或無法重試 | viewport／錯誤掃描 | P1 | 1440×900、1024×768、390×844 與重新檢查 |

- 測試資料：獨立 SQLite 記憶體庫、全合成圖檔／解析替身；另以專用 55433 PostgreSQL 臨時叢集驗證真實 SQL、微秒時間與競爭，不讀現有 5432。
- 通過條件：HTTP／單元測試、前端 build 通過；真實畫面 Reused 不自動關閉或解鎖，預覽與失敗恢復可操作，無非預期可見錯誤／破版。
- 證據：測試 TRX、指令結果、路由／viewport／截圖與 Now What 狀態紀錄；本段完成後補 QC 結果。

### QC 結果（2026-10-22）

- SQLite HTTP 13/13、PostgreSQL 16.14 專用臨時叢集 HTTP 13/13，均無略過；每例自己的測試庫已移除。
- 前端 build 通過；前端邏輯 13/13（既有 7＋新增 6）。UI fixture build 0 warnings／0 errors。
- Reused 與取消預覽的 1440×900、1024×768、390×844 實際畫面及錯誤恢復通過；最終同 URL 重新載入無可見錯誤，出庫計數 3→2 符合只解除文件58。
- 實作驗證交付時未 stage／commit／push；原工作目錄 tracked diff／index 指紋未變，main／origin/main 仍為 `acbeee3`。
- 使用者後續要求提交與 push 本切片。本輪僅發布隔離分支，不合併 main、不正式套用；提交前複查 SQLite HTTP 13/13、前端邏輯 13/13 及 build 均通過，沿用前輪 PostgreSQL／瀏覽器證據。
- 後續 main 合併：使用者再核准合併及 push，已將 main 從 `acbeee3` 快轉至修正提交 `0eb41e4`；新增提交僅記錄文件，產品／測試內容相同，因此重用既有測試證據，不重啟服務或測試資料庫。詳見 QC 的「main 合併紀錄」。
- 同一代理依序執行 RD／QA／QC，未宣稱獨立人員審查。
- 完整證據與限制：[契約修正 QC](qc/checkout-reused-contract-qc-2026-10-22.md)。

## DEV-001：試用人員回饋優化

狀態：實作完成；QC 限制式通過
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Medium；影響多檔、入庫資料身分及跨模組 API，但不需要 schema migration。

### 任務目標

工程人員可以：

1. 將已改名、但品號與類型正確的 CAD 檔安全入庫為目標文件的新版本。
2. 從 Part／Assembly 明細看到反向關聯工程圖及匹配方式。
3. 在 Pack & Go 前確認是否包含關聯工程圖。
4. 從同一資料夾批次匯入零件、組合件與工程圖，並看到逐檔成功／失敗結果。

### Current Phase RD Handoff Contract

#### Scope

- `IngestCadFileRequest` 新增可選 `TargetDocumentId`，維持舊用戶端相容。
- Check-in 必須由後端核對目標文件 ID、PartNumber、DocumentType 及 checkout lock。
- 新增唯讀 relations API；不修改現有 BOM schema。
- Pack & Go 以既有 package closure 為主，再反查目前版本的 Drawing。
- 新增同步 multipart 批次匯入；每個檔案使用獨立 DI scope／DB transaction。
- 前端沿用既有元件、色彩與明細側欄，不建立新設計系統。

#### Out of Scope

- production migration、既有資料批次修復、背景 queue、排程 hot-folder watcher。
- SolidWorks 內部參照改寫與壓縮檔碰撞命名策略重構。
- release、部署、PR、commit 或 push。

#### API / Transaction Contract

- `POST /api/ingest/cad`：`targetDocumentId` 為 optional；提供時身分不符回傳 `409`。
- `GET /api/documents/{documentId}/relations`：回傳 references、drawings、whereUsed。
- `GET /api/assemblies/{rootVersionId}/check-updates`：加回關聯 Drawing 預覽。
- `GET /api/assemblies/{rootVersionId}/download-zip`：新增 `includeDrawings`，預設 `false`
  以維持舊 API 相容；新 UI 預設傳 `true`。
- `POST /api/ingest/cad-batch`：multipart、最多 200 個 CAD、總量最多 1 GiB；
  每檔交易隔離，單檔失敗不回滾其他成功檔。
- 不新增資料表或 migration。

#### Failure Recovery

- Check-in 身分不符：不寫入版本、不解除目標文件鎖定，UI 顯示重新選檔。
- relations 載入失敗：不影響單檔下載或入出庫，UI 提供重新載入。
- batch 單檔失敗：保留其他成功結果，回應中列出檔名與錯誤。
- batch staging 一律限制在伺服器暫存根目錄；完成後清除。

#### Stop Conditions

- 發現必須更改正式 schema 或修復正式資料。
- 現有 API／文件出現無法相容的身分或版次語意衝突。
- 需要跳過 checkout lock 或允許品號不符覆寫。
- 使用者既有 dirty 檔案與本輪修改發生重疊。

### UX Intent

- 使用者：熟悉 CAD、但不應理解資料庫方向性的工程人員。
- 主要任務：安全入庫、看懂關聯、取得完整設計套件、一次匯入成套檔案。
- 成功狀態：畫面能指出處理數量、關聯狀態、下一個 CTA 與失敗檔案。
- 最可能誤解點：檔名警告被理解為禁止；3D 空白被理解為沒有 2D；批次部分成功被理解為全部成功。
- 高風險操作：建立新版本與批次寫入。
- 安全預設：Check-in 綁定文件 ID；Pack & Go 預設包含已找到 Drawing；
  batch 送出前顯示 CAD 數量與類型。
- 不能發生的誤操作：用另一品號檔案覆寫目前文件、路徑穿越 staging、隱藏批次失敗。

### 操作模式矩陣

| 模式 | 來源 | 目標 | 範圍 | 安全策略 | 狀態 | 主要 CTA | 責任 |
|---|---|---|---|---|---|---|---|
| Check-in | 單一 CAD | 已出庫文件 | 單一新版本 | 文件 ID＋品號＋類型＋鎖 | idle/error/success | 確認入庫 | 操作者 |
| 單檔入庫 | 單一 CAD | 新文件或已鎖定文件 | 單檔 | PartNumber 必填＋鎖 | parse/error/success | 直接入庫 | 操作者 |
| 批次匯入 | 資料夾／多檔 | 多文件 | 最多 200 檔 | 相對路徑、逐檔交易、結果摘要 | preview/error/partial/success | 匯入 N 個檔案 | 操作者 |
| Pack & Go | 系統版本 | ZIP | BOM＋可選 Drawing | 下載前預覽與勾選 | preview/download | 下載原簽入版本 | 操作者 |

### 驗收標準

- [ ] 改名檔案若目標文件 ID、PartNumber、DocumentType、checkout owner 均正確，可建立新版本。
  實作與編譯已完成；為避免修改既有試用資料，本輪未送出會建立新版本的正向 live Check-in。
- [x] 任一身分不符時回傳 `409`，不建立版本、不解除鎖。
- [x] Part 與 Assembly 明細均能顯示關聯 Drawing；沒有關聯時提供可執行下一步。
- [x] Pack & Go UI 顯示 Drawing 數量／名稱，勾選後 ZIP 查詢包含其目前版本。
- [x] 批次匯入保留相對路徑、拒絕 traversal、限制檔案數與總量。
- [ ] batch 部分失敗時回應成功／失敗數與逐檔原因。
  逐檔失敗回應及 staging 清理已 live 驗證；未以真實 CAD 製造一成一敗的 mixed-result 資料 mutation。
- [x] `dotnet build SWPdm.sln` 與前端 `npm run build` 通過。
- [x] `/documents`、`/ingest` 在 1440×900、1024×768、390×844 無明顯裁切或非預期水平 overflow。
- [x] 受影響頁面沒有可見 HTTP／API／Not Found／Internal Server Error。

### QA FMEA

| 失效模式 | 可能原因 | 使用者影響 | 偵測方式 | 優先級 | 對策 / 建議測試 |
|---|---|---|---|---|---|
| 錯檔覆寫 | 只依檔名或 PartNumber | 版本歷史污染 | targetDocumentId 負向測試 | P0 | 品號、類型、ID、鎖均須一致 |
| 2D 漏包 | 只沿 BOM 向下 | 現場缺工程圖 | package 查詢與 ZIP 清單 | P0 | 反查 Drawing current version |
| 關聯重複 | 多版本 Drawing 命中 | ZIP／UI 重複 | 依 DocumentId 去重 | P1 | 只取 CurrentVersionId |
| 批次半成品被隱藏 | 單檔例外中止 | 使用者誤以為全數成功 | mixed-result fixture | P0 | 每檔隔離並列出失敗 |
| 路徑穿越 | 惡意 relativePath | 覆寫暫存區外檔案 | `../` 負向測試 | P0 | FullPath 邊界檢查 |
| 手機版按鈕擠壓 | 長檔名／結果清單 | 無法完成操作 | 390×844 截圖 | P1 | wrap、truncate、可捲動清單 |

### QA / QC 證據

- 後端：solution build、API route／靜態契約檢查。
- 前端：TypeScript/Vite build、lint（若既有 lint 基線允許）。
- UI：實際瀏覽器三 viewport、Check-in modal、Pack & Go modal、Upload batch panel 截圖。
- Visible Error Sweep：`/documents`、`/ingest`。
- Git：回報本輪檔案與排除的使用者既有變更。

### Future Phase Capsule

- 大型批次背景 queue／hot-folder watcher。
- 恢復條件：同步請求超時、批次量經常超過 200 檔，或需要無人值守匯入。
- 未要求 production release；狀態為 `Future Phase Captured / Not Requested`。

### 規格治理結果

- Authoritative source：本文件與 `docs/assembly-download-package-flow.md`。
- API 均為 additive、舊參數預設維持原行為；不需要 migration。
- ADR 不建立：此次不改主資料唯一性或 schema，實作局部且可逆。
- 高影響 deferred scope 已以 Future Phase Capsule 保存。

## DEV-007：出庫後另存為新料號

狀態：實作完成；QC 限制式通過（第二輪已涵蓋 Vault 清單直接顯示異常；既有前端 lint 工具缺口）
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Medium；影響主資料身分、本地 schema、Check-in API 與高風險 UI 分流。

### 任務目標

當出庫檔案的 CAD 品號與原文件不同時，工程人員可以明確選擇「另存為新料號」，
建立可追溯的新文件，而不污染原文件歷史或自動改寫既有 BOM。

### Current Phase RD Handoff Contract

#### Scope

- 保留一般 Check-in 的品號／類型／文件 ID 驗證。
- `409` 回應提供結構化舊／新品號，讓 UI 顯示安全分流。
- 明確確認後，以單一資料庫交易建立新品號文件、版本與來源稽核關聯。
- 成功後解除原文件出庫鎖；原版本、生命週期與 BOM 不變。
- 分支不遞迴入庫參照 CAD，避免順帶建立其他文件的新版本。
- UI 顯示來源、目的、關聯工程圖與 where-used 數量，原因必填。
- 文件關聯 API 可查詢品號變更來源與衍生文件。

#### Out of Scope

- 自動改寫 SolidWorks 內部參照、父組立 BOM 或既有工程圖。
- 自動將原品號標記 Obsolete／Superseded。
- 品號輸入錯誤的管理員更正流程、核准工作流與正式環境 migration。
- release、部署、commit、push。

#### API / Data / Transaction Contract

- `POST /api/ingest/cad` 增加可選 `createNewDocumentForPartNumberChange`，預設 `false`。
- 只有目標文件已由同一操作者出庫、類型相同、品號確實不同、原因非空白、且新品號
  未被同類型文件使用時可建立。
- 新增 `pdm_document_identity_changes`，保存 source document/version、target document、
  old/new part number、reason、actor、timestamp；target document 僅能有一筆來源。
- 分支、版本、稽核關聯與原鎖解除必須在同一交易中完成；失敗時全部回滾。
- `GET /api/documents/{documentId}/relations` 增加 `identityOrigin` 與 `derivedDocuments`。

#### Failure Recovery / Stop Conditions

- 一般 mismatch：不寫資料、不解除鎖，UI 提供重新選檔或另存新品號。
- 建立新品號失敗：整筆交易回滾並保留原鎖，允許修正原因或重新嘗試。
- 文件類型改變、同品號、鎖持有人不符或新品號已存在：禁止確認動作。
- 若實作需要自動改寫歷史 BOM、遠端 migration 或正式資料修復，立即停止。

### UX Intent / Now What

- 使用者心智模型：同一設計修改後若成為新品號，是從原設計衍生的新文件。
- 主要 CTA：`另存為新料號`；一般錯檔仍可 `重新選擇檔案`。
- 狀態首句：`此檔案品號已變更，不能覆寫原文件。`
- 影響預覽：舊／新品號、文件類型、關聯工程圖數、where-used 數、原文件處置。
- 安全預設：不自動分支；原因未填、類型不符或新品號重複時按鈕 disabled。
- 成功：指出新文件 ID 與新品號，說明原文件歷史及 BOM 未變更。

### 驗收標準

- [x] mismatch 的 `409` 含舊／新品號及允許動作，且原文件維持鎖定。
- [x] 明確選擇分支後建立新的 DocumentId／Version 1／WIP，並寫入完整稽核關聯。
- [x] 分支成功後原文件解鎖，原版本、狀態與 BOM 不變。
- [x] 同品號、不同類型、非出庫持有人、空白原因與重複新品號均被拒絕。
- [x] 關聯 API 可從新文件查來源、從原文件查衍生文件。
- [x] UI 先顯示可執行下一步與影響摘要，不顯示裸露 HTTP／API 錯誤。
- [x] `dotnet build SWPdm.sln` 與前端 production build 通過。
- [ ] 前端 lint：現有專案未宣告／安裝 ESLint 執行檔及 config 相依套件；已記錄工具缺口。
- [x] `/documents` 的分流 modal 在 1440×900、1024×768、390×844 可操作且無 overflow。

### 規格治理結果

- Authoritative decision：`decisions/ADR-001-part-number-branch.md`。
- 與既有 mismatch 規則為 Compatible exception：只有明確旗標與完整前置條件才分支。
- 本輪需 migration，但不執行正式環境 migration；沒有產品決策 blocker。
- 品號更正核准流程為 Future Phase Captured / Not Requested；恢復條件是使用者要求處理
  既有主資料輸入錯誤，而非設計衍生新品號。

## DEV-008：圖檔中心顯示圖號

狀態：實作完成；QC 限制式通過（既有前端 lint 工具缺口）
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Low；新增唯讀欄位與搜尋條件，不改 schema、CAD 檔案或主資料。

### Current Phase RD Handoff Contract

#### Scope

- `/api/documents/search` 從目前版本、空白 configuration、精確屬性名稱「圖號」讀取圖號。
- 圖檔中心清單在檔名與料號之間顯示圖號；沒有圖號時顯示 `-`。
- 選取文件明細分別標示圖號與料號，避免兩者混淆。
- 關鍵字搜尋涵蓋檔名、圖號及料號。

#### Out of Scope

- 不以「工程圖號」替代「圖號」，不合併兩個不同 CAD 自訂屬性。
- 不新增、回填或修改圖號，不建立資料庫 migration。
- 不新增排序、批次編輯或唯一性規則。

### 驗收標準

- [x] 有「圖號」屬性的目前版本，在清單及明細顯示相同值。
- [x] 無「圖號」的文件顯示 `-`，既有料號仍在獨立欄位。
- [x] 以完整圖號搜尋可找到對應文件。
- [x] 後端及前端 production build 通過。
- [ ] 前端 lint：現有專案未提供可執行的 ESLint 相依套件；已記錄既有工具缺口。
- [x] `/documents` 在 1440×900、1024×768、390×844 無頁面級水平 overflow 或可見錯誤。

## DEV-009：父文件舊版參照提示

狀態：實作完成；QC 限制式通過（既有前端 lint 工具缺口）
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Low；新增唯讀檢查與前端提示，不改 schema、CAD 檔案、BOM 或歷史版本。

### Current Phase RD Handoff Contract

#### Scope

- 新增文件層級唯讀 API，以目前父版本的已解析直屬 BOM occurrence 判斷子文件是否已有較新目前版本。
- Assembly 與 Drawing 使用相同判斷；回傳舊／新版本、檔名、料號及受影響 occurrence 數。
- 圖檔中心文件詳情在出入庫操作前顯示醒目警示，並依出庫狀態提示下一步。
- 圖檔中心搜尋結果直接回傳每份文件的舊版參照項目數與受影響 occurrence 數。
- 清單上方顯示異常文件摘要，異常列在檔名附近顯示可掃描的「需更新」警示。
- 警示明確說明歷史版本不會自動改寫，需在 SolidWorks 更新父文件參照後重新入庫。

#### Out of Scope

- 不自動改寫既有 `pdm_bom_occurrences`、父文件 CAD 內部參照或任何歷史版本。
- 不將缺少 child link 的參照誤判為「有新版」；missing reference 修復另案處理。
- 不新增 migration、背景通知、批次更新或自動開啟 SolidWorks。

### 驗收標準

- [x] Assembly 與 Drawing 的目前父版本若引用舊版子文件，API 回傳正確的新舊版本及 occurrence 數。
- [x] 沒有舊版直屬參照的文件回傳 `hasUpdates=false`，不顯示誤導性警示。
- [x] 前端警示在出入庫按鈕前可見，並能辨識「目前引用 → 可更新」及下一步。
- [x] 歷史 BOM、版本與文件資料在檢查前後不變。
- [x] 後端及前端 production build 通過。
- [ ] 前端 lint：現有專案未安裝可執行的 ESLint 相依套件；已記錄既有工具缺口。
- [x] `/documents` 在 1440×900、1024×768、390×844 無頁面級水平 overflow 或可見錯誤。
- [x] 使用者不選取文件，也能從 Vault 清單摘要及逐列徽章辨識哪些文件有舊版子文件參照。
- [x] 選取異常列後，右側詳情仍顯示一致的新舊版本差異與下一步。

## DEV-010：Vault 寬螢幕與資訊密度優化

狀態：實作完成；QC 限制式通過（既有前端 lint 工具缺口）
節點類型：交付點
父交付點：無
是否計入產品交付完成：是
風險：Low；僅調整 Vault 路由的版面與既有搜尋資料呈現，不改 API、資料庫或其他頁面的資訊架構。

### UX Intent

- 使用者：需要快速掃描與處理大量 PDM 文件的工程人員。
- 主要任務：在寬螢幕充分利用可用空間，同時辨識結果數、異常、出庫狀態與文件詳情。
- 自然下一步：先由頁首摘要掌握目前搜尋結果，再於清單選取文件查看詳情。
- 資訊分層：頁首放短摘要、清單放可比較欄位、右側面板放完整關聯與處理操作。
- 安全預設：只擴充顯示，不改搜尋結果、文件狀態或任何寫入流程。

### Current Phase RD Handoff Contract

#### Scope

- 共用 Layout 提供可選的全寬內容模式，只讓 `/documents` 使用；Dashboard 與 Ingest 維持既有寬度策略。
- Vault 頁首右側顯示目前結果、參照異常、已出庫與可用文件數。
- 寬螢幕下擴大右側文件詳情面板，清單仍保留可用比較寬度。
- 版本欄同時顯示版本號與版次，利用新增空間提升判斷效率。

#### Out of Scope

- 不新增圖表、後端欄位、資料庫 migration 或持久化使用者版面偏好。
- 不重設 Dashboard、Ingest、導航或出入庫流程。
- 不以更多裝飾填滿空間；新增資訊必須支援掃描、比較或下一步判斷。

### 驗收標準

- [x] 1920×1080 下 Vault 不再受 1280px 上限限制，內容能使用主區域可用寬度且無頁面水平 overflow。
- [x] 頁首空白區顯示四個與目前搜尋結果一致的即時摘要數值。
- [x] 寬螢幕選取文件後，詳情面板較既有 384px 更寬，清單與詳情皆可操作。
- [x] 版本欄可同時辨識 VersionNo 與 RevisionLabel，未提供資料時安全顯示 `-`。
- [x] Dashboard 與 Ingest 的既有最大寬度行為不受影響。
- [x] `npm run build` 通過。
- [ ] 前端 lint：現有專案未安裝可執行的 ESLint 相依套件；已記錄既有工具缺口。
- [x] `/documents` 在 1920×1080、1440×900、1024×768、390×844 無重疊、裁切、頁面級水平 overflow 或可見錯誤。

### Stop Conditions / Evidence

- 若全寬模式影響其他路由、手機版資訊摘要造成水平 overflow，或選取詳情後清單不可操作，停止通過並回 RD 修正。
- 證據需包含寬度量測、四種 viewport 截圖、清單／詳情互動、Visible Error Sweep、build 與 Git boundary。

## DEV-011：內網圖面清單可見性修正

狀態：實作完成；QC 限制式通過（待同仁實際工作站確認）
節點類型：開發點
父交付點：DEV-001
是否計入產品交付完成：否
風險：Medium；需要修正本機 API 監聽位址並短暫重啟服務，但不修改資料庫、圖面或文件狀態。

### 根因證據

- 前端 `5174` 監聽 `0.0.0.0`，同仁可以開啟系統。
- API `5000` 目前只監聽 `127.0.0.1`／`::1`，啟動命令包含 `--urls http://localhost:5000`。
- 主機由 localhost 呼叫搜尋 API 可取得 50 份文件，但由 `192.168.20.62:5000` 呼叫失敗。
- Vault 捕捉載入錯誤後只寫入 console，畫面仍顯示「查無圖檔資料」，造成資料為空的誤判。

### UX Intent

- 使用者：從同一內網存取 SW-PDM 的工程同仁。
- 主要任務：開啟 Vault 後立即看到共享圖面；服務不可用時知道要重新連線或通知主機管理者。
- 成功狀態：LAN URL 回傳與主機一致的文件數，Vault 摘要不是非預期全零。
- 錯誤狀態首句：目前無法載入圖面清單。
- 自然下一步：確認主機服務後按「重新連線」。
- 安全預設：載入失敗不清除既有資料、不把故障誤報為真正的空清單。

### Current Phase RD Handoff Contract

#### Scope

- Windows 啟動腳本明確將 API 綁定 `0.0.0.0:5000`，避免 launch profile 或命令列回退到 localhost-only。
- Vault 搜尋載入失敗時顯示人類可理解的錯誤狀態、下一步與重新連線 CTA。
- 連線失敗時摘要數值顯示 `—`，真正空資料才顯示「查無圖檔資料」。
- 重建前後端並以目前主機 LAN IP 驗證 API、CORS 與 50 份既有文件。

#### Out of Scope

- 不新增登入、角色權限、VPN、反向代理、TLS 或遠端正式部署。
- 不修改 PostgreSQL 資料、Vault 圖檔、BOM、版本或出入庫狀態。
- 不開放任意 CORS origin；沿用目前明列的內網前端來源。

### 驗收標準

- [x] API 同時支援 localhost 與 `0.0.0.0:5000`，LAN URL 可取得與 localhost 相同的文件數。
- [x] LAN 前端來源通過 CORS，Vault 首次載入顯示 50 份既有文件。
- [x] API 不可用時不再顯示「查無圖檔資料」，而是顯示錯誤原因、替代下一步與「重新連線」。
- [x] API 恢復後按「重新連線」可回到正常清單，不需關閉頁面。
- [x] 後端與前端 production build 通過。
- [x] `/documents` 正常與錯誤狀態在 1440×900、1024×768、390×844 無重疊、裁切或頁面級水平 overflow。
- [ ] 前端 lint：現有專案未安裝可執行的 ESLint 相依套件；已記錄既有工具缺口。

### Stop Conditions / Evidence

- 若 LAN 綁定需要新增廣泛防火牆例外、變更公司網路策略，或服務重啟後 localhost smoke 失敗，停止並回報。
- 證據需包含監聽位址、localhost／LAN 文件數、CORS header、正常與錯誤畫面、重新連線流程、Visible Error Sweep、build 與 Git boundary。

## 變更紀錄

- 2026-07-29：依試用回饋建立 DEV-001～DEV-006 與本輪 RD/QA/QC 契約。
- 2026-07-29：完成 DEV-002～DEV-005；DEV-006 以不修改既有試用資料為前提限制式通過。
  詳細證據見 `qc/trial-feedback-qc-2026-07-29.md`。
- 2026-08-04：建立 DEV-007 與 ADR-001，進入新品號文件分支實作。
- 2026-08-04：完成 DEV-007；交易、資料不變量、API、建置及三種 viewport QC 通過，
  詳細證據見 `qc/part-number-branch-qc-2026-08-04.md`。
- 2026-08-04：DEV-001／DEV-007 已正式套用至本機運行環境；完成備份、Release build、
  migration 冪等檢查與 production-mode smoke，證據見 `reports/local-release-2026-08-04.md`。
- 2026-08-05：完成 DEV-008；圖號 API、搜尋、清單、明細、RWD、建置及三種 viewport QC
  通過，證據見 `qc/drawing-number-qc-2026-08-05.md`。
- 2026-08-05：完成 DEV-009；父文件舊版子文件參照 API、醒目前端提示、資料不變量、
  production build 及三種 viewport QC 通過，證據見 `qc/reference-update-warning-qc-2026-08-05.md`。
- 2026-08-05：依使用者回饋重開 DEV-009，將異常提示由文件詳情提升至 Vault 清單摘要及逐列狀態。
- 2026-08-05：完成 DEV-009 第二輪；搜尋 API 回傳異常數量，Vault 首屏摘要、異常列置頂、逐列徽章、
  正反向搜尋、詳情一致性、production build 及三種 viewport QC 通過。
- 2026-08-05：建立 DEV-010，依使用者回饋進行 Vault 寬螢幕空間利用與資訊密度優化。
- 2026-08-05：完成 DEV-010；1920×1080 的 Vault 內容寬度由 1280px 擴至 1600px、主區域利用率由
  76.9% 提升至 96.2%，新增四項搜尋結果摘要、版本／版次比較欄與擴充詳情面板；production build、
  四種 viewport、選取詳情及 Visible Error Sweep 通過，證據見 `qc/vault-space-utilization-qc-2026-08-05.md`。
- 2026-08-11：建立 DEV-011；確認同仁看不到圖面並非資料遺失，而是 Release API 被命令列參數限制為
  localhost-only，且 Vault 缺少可見的載入失敗狀態。
- 2026-08-11：完成 DEV-011；API 改為監聽 `0.0.0.0:5000`，localhost／LAN 均取得 50 份文件，
  Vault 新增可恢復的連線錯誤狀態；production build、三種正常／錯誤 viewport、同頁重新連線及
  Visible Error Sweep 通過，證據見 `qc/lan-vault-visibility-qc-2026-08-11.md`。
- 2026-08-26：整理 DEV-008～DEV-011 程式、文件及有效 QC 證據為 GitHub release candidate；
  排除未採納的錯誤尺寸截圖與 `scratch/db-backups/` 本機資料庫備份。
