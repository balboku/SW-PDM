# DEV-014／015 合併與本機套用

日期：2026-10-26，Asia/Taipei。狀態：發布準備／驗證中，尚未切換服務。
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
