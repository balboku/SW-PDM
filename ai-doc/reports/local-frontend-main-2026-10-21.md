# main 前端本機測試套用與驗證

日期：2026-10-21（Asia/Taipei），執行約18:00～18:12。依使用者明確要求，只套用main前端，不納入未完成後端、不更動現有資料庫。角色依序為部署/RD、QA門檻確認、QC事實驗證；未使用子代理。

## 結論與版本

- 本機前端套用成功，主要操作 smoke 通過；不是完整SolidWorks／CAD解析／入庫套件驗收。
- 套用時的main／origin/main：`e270736652dcf0f9c477b4ff01f7cfae8d7e0bc9`。由乾淨main工作區重新建置，未讀取原dirty工作區的React原始碼；後續文件提交不改變這次實際套用的程式版本。
- 現有地址維持 `http://localhost:5174`／`http://192.168.20.62:5174/documents`。
- 原5174 Vite dev（PID20284）已停止；新5174為固定production成品的Vite preview（PID2756），不是生產等級對外主機部署。
- 5000 API PID20184、啟動時間2026-10-15 10:59:09保留，未重啟／重建／替換後端。未執行start.ps1、migration、匯入或正式出入庫。
- 原工作區分支仍是codex/trial-feedback-20261016，HEAD 3d36d1c。Git狀態、工作差異摘要eb8bdd8fb2032a4a299f44bcc7696e57d216192f及空索引在前後相同；所有未完成後端／解析器仍保留。
- 套用階段只保存本地報告／截圖，當時未stage、commit或push任何新變更。後續依使用者要求，另以純文件提交存檔本報告、12張截圖及文件索引；不包含任何產品程式、本機成品／啟動器、設定、資料或未完成後端，亦不重新部署。

## 成品與重新啟動

固定成品目錄：
`C:/Users/user/Documents/GitHub/SW-PDM/src/SWPdm.Web/dist/local-main-e270736-20261021/site`

由main執行 `npm run build`，只將dist生成檔複製至此新目錄；原dist根下舊檔與原始碼保留。成品內沒有5010測試API位址／待完成套件預檢端點。API沿用瀏覽器主機的5000。

前端專用啟動器：
`C:/Users/user/Documents/GitHub/SW-PDM/src/SWPdm.Web/dist/local-main-e270736-20261021/Start-Web.ps1`

上述成品與啟動器是該測試主機的本地部署產物，不包含在本次GitHub紀錄提交；其他主機不可假定此絕對路徑存在。

此腳本只啟動前端，先驗證JS雜湊；5174若已被占用會拒絕，不會擅自終止其他程序。使用既有Node與Vite，背景視窗隱藏；stdout/stderr在啟動器同目錄，不在site公開根。重新啟動前先核對並停止該preview程序，再直接執行此腳本。未新增開機自啟或排程。

不要使用專案根的start.ps1來重啟這個限定範圍：該腳本會執行migration及啟動後端，不符合本次邊界。

回復方式：固定成品不覆蓋原檔；若新版前端發生問題，先停止核對過的preview PID，再明確選擇前一前端成品／原啟動方式。不要自動建置dirty後端、執行migration或修改資料庫。原開發服務是原工作區的 `node node_modules/vite/bin/vite.js`；重新啟用會再次讀取未提交前端，因此不是本次完成狀態。

| 成品／保留物 | SHA-256 |
|---|---|
| 已透過HTTP讀回的index-BbsnU4WV.js | D30F2F12743B1613000055F7AC2BC7263C08D7E3F8DAD874EF4CB022D2DBB69F |
| index-D8mquAUP.css | ABBC3B9CC80A375EC6C86C6CBF1A6EE641B2CB0DD583DDB24F2404F35271A55D |
| index.html | 46C24E093E17D563B9E0F37AA652078751C424352134C9F58289B3CDB2A7C6BC |
| 原API DLL（前後相同） | 558F31A7A1F2A03CFBE156B4ABC1C4DD049217F5381E6C1BA76F96427A0695FC |

HTTP 5174根／documents均200，HTML僅載入上述production bundle，不含@vite/client；已用HTTP取得JS並核對其SHA-256。

## QA邊界與風險

| 風險 | 門檻／驗證方法 |
|---|---|
| 套到未完成程式 | main來源與提交核對、固定成品、HTTP JS雜湊，不從dirty原始碼提供服務 |
| 不小心變更正式資料 | 本機只GET、資料庫連線強制default_transaction_read_only=on；所有鎖定寫入只在唯一SQLite記憶體fixture |
| 空庫與錯誤混淆 | 真實空庫、合成503、搜尋零結果各有不同提示；重試後可恢復 |
| 假成功或範圍不明 | 出庫需預覽／非空人員，取消只1份；成功後明細與鎖一致 |
| 彈窗溢出／焦點遺失 | 1440×900明細、1024×768入庫、390×844取消與錯誤；Esc返回與成功後返回 |
| 設定被誤當健康 | 真實首頁顯示資料庫實際連線通過；Vault／金鑰僅顯示已設定，不宣稱讀寫或解析通過 |

## 本機現有環境（唯讀）

原使用者分頁135043939重新載入同一地址，確認JS由5174固定成品供應。實際CSS viewport 1920×842。首頁、圖檔中心與首次匯入的可見alert／inline-error皆空；最後瀏覽器error紀錄為空。

- 首頁：連線檢測通過；Vault路徑／解析金鑰是「已設定」，驗證限制清楚。
- Vault：0份符合資料庫實況；缺失狀態為「未檢測」，不誤宣稱0異常。
- 搜尋QC-NO-MATCH＋Drawing篩選：顯示找不到符合條件，清除條件後恢復空庫入口。
- 首次匯入：單檔／資料夾多檔切換、展開品號說明、回圖檔中心皆正常。沒有選真實CAD或送出。
- 畫面可回答用途／下一步：空庫→首次匯入、搜尋零結果→清除條件、已出庫修改→選原文件。此次為實作者檢視，不是外部使用性研究。

## 隔離主要流程

隔離前端5175採相同main原始碼、API5010只使用唯一SQLite記憶體庫／暫存Vault，未載入正式連線字串。與部署成品的差異僅為測試API位址與dev服務；不是宣稱以真實CAD完成正式入庫。

| 案例 | 觀察與下一步 | 判定 |
|---|---|---|
| 清單／明細／版本歷史 | 60合成文件、舊契約顯示50非全部、初始鎖3；明細圖號、關聯與1筆版本可查看 | 通過 |
| 出庫前防呆 | 本圖1＋關聯0；人員留空確認disabled，填User才可送出 | 通過 |
| 出庫／取消 | 文件57出庫成功3→4，取消只解除57回3，明細改為可出庫；58另做穩定截圖取消後剩2，59／60鎖保持原狀 | 通過 |
| 修改後入庫 | 顯示一般入庫版次規則；未選檔無法確認，取消可回明細 | 通過（僅表單） |
| 下載失敗 | 57無實體檔回404；顯示「檔案尚未下載」與重試下載，不假成功 | 通過（預期錯誤） |
| 下載成功 | 56回UI-QC.txt，畫面指引查看下載紀錄；實際39 bytes與合成文字完全一致 | 通過 |
| 清單503與恢復 | 摘要為「—」、頁尾筆數未確認；提示重新載入，不要求重新匯入；normal後點重試恢復50且alert消失 | 通過 |
| RWD／返回 | 1440×900、1024×768、390×844根scrollWidth等於viewport；明細Esc返回、取消成功返回均可操作 | 通過 |

截圖工具偶有落後一幀：平板入庫與取消成功已在DOM穩定後另外補拍，證據以補拍檔為準。合成39-byte下載確認後只刪除該單一測試檔；不是CAD／Vault資料。

## 資料庫與Vault前後證據

兩次psql連線皆強制 `default_transaction_read_only=on`，只SELECT。業務7表皆0，內容摘要均d751713988987e9331980363e24189ce：
pdm_documents、pdm_document_versions、pdm_custom_properties、pdm_bom_occurrences、pdm_document_identity_changes、pdm_numbering_rules、pdm_number_sequences。

遷移表__EFMigrationsHistory為8筆，內容摘要be58dea1120e1a086ed7bc547f4a5d47，前後一致。Vault實體檔仍0份；5000清單仍200空陣列，連線canConnect=true。未備份、未資料修復、未變更歷史BOM／schema。

## 建置、證據與限制

- main TypeScript／Vite build通過（1423 modules，25.06秒）；7項UI邏輯測試全通過。既有Browserslist過期提示未因本次升級套件。
- [本機圖檔中心](../qc/local-frontend-2026-10-21/live-main-vault.png)、[首頁](../qc/local-frontend-2026-10-21/live-dashboard.png)、[搜尋零結果](../qc/local-frontend-2026-10-21/live-search-empty.png)、[匯入說明](../qc/local-frontend-2026-10-21/live-ingest-guide.png)。
- [隔離桌面明細](../qc/local-frontend-2026-10-21/fixture-desktop-detail.png)、[平板入庫](../qc/local-frontend-2026-10-21/fixture-tablet-checkin.png)、[手機取消預覽](../qc/local-frontend-2026-10-21/fixture-mobile-cancel-preview.png)、[成功](../qc/local-frontend-2026-10-21/fixture-mobile-cancel-success.png)。
- [下載404](../qc/local-frontend-2026-10-21/fixture-mobile-download-error.png)、[503](../qc/local-frontend-2026-10-21/fixture-mobile-search-error.png)、[恢復](../qc/local-frontend-2026-10-21/fixture-mobile-list-recovered.png)、[合成下載成功](../qc/local-frontend-2026-10-21/fixture-mobile-download-success.png)。
- 真實CAD匯入／Check-in、完整Pack & Go、SolidWorks解析、實體手機觸控與跨瀏覽器不在本次已驗證結論。
- 隔離5010／5175及預檢5176已關閉，瀏覽器viewport已還原、隔離分頁已關閉；保留原內網分頁與5174前端。
