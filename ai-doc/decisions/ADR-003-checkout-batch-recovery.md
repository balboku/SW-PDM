# ADR-003：以取得鎖快照支援整批出庫安全復原

日期：2026-10-22。狀態：本輪契約確定，只允許隔離實作／驗證，非部署核准。

## 背景與選擇

main的單檔取消以actor、鎖定時間與目前版本保護；關聯出庫未保存當次取得範圍。
使用目前BOM或同一操作人查詢整批解鎖會誤解先前／後續作業的鎖，故不採用。
選擇在同一交易內保存批次與新取得項目，不沿用原dirty工作區整包後端或migration。

## 決策

- pdm_checkout_batches保存GUID、根文件ID、actor與UTC微秒建立時間；pdm_checkout_batch_items以(batch_id,document_id)為鍵，
  保存當次version_id、lock_time及restored_at。文件／版本ID為歷史值，不加向文件的刪除連動外鍵，保留刪除後的排除證據。
  項目對批次採Restrict；沒有刪除／覆寫批次API。
- checkout保留子件／目前工程圖的關聯方向、不鎖父組件，新增批次ID與計數。
  相容性修正：所選BOM子件依文件身分取得目前版本鎖，不因舊版參照或缺少目前版本而被JOIN默默排除；缺版本整批拒絕。
  原先本人鎖保留且不加入批次；他人／不完整鎖或版本衝突整批回滾。
- 批次預覽與執行都要求明確batchId與相符根文件／actor；只用不可變項目，不推導舊鎖或依最新BOM擴大。
- 預覽分列每筆可復原及排除代碼／原因；執行重新評估，已變更者安全略過，符合者原子條件UPDATE。
  所有成功解鎖及restored_at同一Serializable交易；條件更新競爭失敗或例外全部回滾，不自動重試。
- 已復原項目再次送出回0筆與AlreadyRestored；不是取得新鎖的權限。actor沿用現有自填名稱，非身分認證。
- 原單檔預設與checkin別名仍保護actor/time/version；只有includeBatch=true與batchId可進批次。

## 相容性與遷移

兩張新表只含本功能資料；舊文件／版本／BOM欄位及現有遷移不變。
新migration只產生原始碼與離線檢查，不執行；隔離庫僅EnsureCreated。
因此此分支不可直接套用目前資料庫；部署及schema套用需後續明確指令及獨立關卡。
既有單檔前端繼續可用；批次前端屬DEV-015，不能把本次API驗收當畫面通過。

## 文件權威與後續

修改dev_task的DEV-013範圍；相容延伸既有取消／Reused單檔契約，不推翻DEV-012入庫政策或歷史驗證。
實作、驗收、停止條件及高影響deferred scope見dev_task的DEV-013。
DEV-014套件、DEV-015畫面及完整Auth未要求於本輪執行；前兩者已有獨立DEV，Auth須另有人類範圍決策。
