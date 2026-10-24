# ADR-004：完整選版套件與只改暫存副本的參照重寫

2026-10-22。狀態：DEV-014本輪工程契約確定，僅隔離實作／驗證，非部署授權。

## 目的與範圍

輸出可搬移的完整CAD套件，而非「部分檔案已下載」的ZIP。既有GET download-zip路由與useLatest／versionOverrides／includeDrawings參數保留。
不執行migration、不寫資料庫／歷史BOM、不改Vault原檔；不帶入原dirty工作區的套件或其他後端。前端批次／完整清單整合仍屬DEV-015。

## 決策與選版契約

- 預設沿用所選根版本及其BOM版本快照；useLatest對每個遇到的文件選目前版本，包含根；明確覆寫為sourceVersionId:targetVersionId且必須同一DocumentId。
  最新與明確覆寫不得混用；格式錯誤、重複來源、跨文件或未參與最終依賴圖的覆寫拒絕，不悄悄忽略。
- 每個選定版本都重新讀自己的BOM，包含抑制件，虛擬件保留在父檔內；不可將舊依賴清單換幾個VersionId就當成完整套件。
  原生Document Manager另檢查所有組態、工程圖與設計依賴；BOM與CAD不符、缺檔、缺件、循環、未知／不支援依賴、歧義或缺組態時阻擋。
- 工程圖預設不納入，includeDrawings=true沿用既有反查意圖，納入目前工程圖後也完整檢查其模型依賴。
  原生額外依賴只接受套件內明確版本／BOM路徑證據，或唯一精確來源路徑候選；不依單一同名檔猜版本，也不把新判定回填資料庫。
- 每個版本採唯一且確定的v<VersionId>__<安全檔名>，平鋪至同一資料夾，保留不同文件／版本的同名檔。
- 用StorageFileId唯讀讀取來源，限制在Vault內、拒絕traversal／reparse point；核對已保存的SHA256及可用的檔案大小。
  所有CAD開檔／重寫只針對新建GUID暫存目錄；限制1000檔、512MiB，失敗不回傳ZIP。
- Document Manager先讀參照，再ReplaceReference；元件專屬的舊快取則以原生SelectName2／SelectName完整occurrence與組態對應component.Replace，檢查其bool與Save回傳，不依短名稱／檔名猜測。
  以GetChangedReferences及GetReplacedComponents重建精確別名（identity條目不是循環）。重寫完關檔後搬移工作目錄，重新讀取每個副本，確定所有外部參照均在搬移後的套件內且對應所選版本；否則阻擋。
- ZIP附manifest.json，列出根選版、文件／版本、安全檔名、來源／輸出SHA、依賴與虛擬件；不包含來源絕對路徑、Vault路徑或金鑰。
  它是下載證據，不是系統稽核表或新資料模型。

## 失敗與相容性

過去缺檔仍出包、同名略過、無效覆寫忽略改為fail closed，是刻意的安全替代。成功仍application/zip與assembly_<rootVersionId>.zip。
錯誤使用繁中Problem與穩定code，不暴露伺服器路徑；暫存資料僅清理本請求建立的GUID目錄。未完成的串流大型套件／非CAD外部格式另需範圍決策。
本ADR為DEV-014權威，取代docs/assembly-download-package-flow.md的舊Google Drive示意與未驗證重寫提案；不改DEV-012解析／入庫或DEV-013鎖規則。

## QA／停止條件

驗證分層：合成HTTP契約＋SQLite／全新55433 PG、舊67項回歸；真實CAD副本以原生DM重開、搬移及雜湊證明，不能以fake parser取代原生證據。
主要負向：缺檔／錯SHA／路徑攻擊、同名、舊子組件換版後新增／移除依賴、覆寫非法／未使用、循環、未解析／抑制／虛擬／設計依賴、重寫失敗／Save失敗／搬移仍指向外部。
不因build或假測試通過標完整完成；缺可執行原生案例或需要SolidWorks完整重建／幾何驗證時清楚標限制，不放寬安全規則。

## 原生API依據

- [ReplaceReference](https://help.solidworks.com/2018/english/api/swdocmgrapi/solidworks.interop.swdocumentmgr~solidworks.interop.swdocumentmgr.iswdmdocument~replacereference.html)：先初始化參照，再以API回傳的完整路徑替換。
- [GetComponents](https://help.solidworks.com/2022/English/api/swdocmgrapi/SOLIDWORKS.Interop.swdocumentmgr~SOLIDWORKS.Interop.swdocumentmgr.ISwDMConfiguration2~GetComponents.html)：DM重寫後元件快取可能仍是舊路徑，需合併GetChangedReferences，不把快取舊值當真實重寫失敗或成功。
- [元件Replace](https://help.solidworks.com/2023/english/api/swdocmgrapi/SOLIDWORKS.Interop.swdocumentmgr~SOLIDWORKS.Interop.swdocumentmgr.ISwDMComponent6~Replace.html)與[GetReplacedComponents](https://help.solidworks.com/2012/english/api/swdocmgrapi/SolidWorks.Interop.swdocumentmgr~SolidWorks.Interop.swdocumentmgr.ISwDMConfiguration11~GetReplacedComponents.html)：以完整原生元件／組態證據處理專屬替換，保留組態並要求重接mates，且不能把DM成功當成完整SolidWorks幾何重建驗收。

部署、commit／push、完整SolidWorks幾何重建、跨實機工作站、Auth與DEV-015未在本輪；只有使用者後續要求才進入各自關卡。
