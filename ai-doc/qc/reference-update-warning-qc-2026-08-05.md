# 父文件舊版參照提示 QC 報告

驗證日期：2026-08-05
對應任務：`DEV-009`／`SWPDM-REFERENCE-UPDATE-WARNING-20260805`
結論：限制式通過；唯讀檢查、Vault 清單與詳情醒目提示、資料不變量、正式建置及三種 viewport 驗證均通過。

## 驗證範圍

- `GET /api/documents/{documentId}/reference-updates` 只檢查目前父版本的直屬 BOM occurrence。
- Assembly 與 Drawing 共用判斷，可回傳目前引用版本、子文件目前版本及受影響 occurrence 數。
- 圖檔中心在出入庫操作前顯示醒目的琥珀色警示、版本差異、下一步及歷史版本保留說明。
- 不自動改寫 CAD 內部參照、BOM、文件或歷史版本。

## API 與資料驗證

| Fixture | 類型／情境 | 已檢查 occurrence | 受影響 occurrence | 更新項目 | 結果 |
|---|---|---:|---:|---:|---|
| 文件 51 | Assembly 引用舊版子文件 | 2 | 2 | 1 | `hasUpdates=true`，V1 → V2 |
| 文件 42 | Drawing 引用舊版 Part | 1 | 1 | 1 | `hasUpdates=true`，V2／Rev. V03 → V3／Rev. V04 |
| 文件 53 | Drawing 無新版可更新 | 1 | 0 | 0 | `hasUpdates=false` |
| 文件 58 | Drawing 缺少 child link | 0 | 0 | 0 | `hasUpdates=false`，未誤判 |
| 文件 999999 | 不存在文件 | - | - | - | HTTP 404 |

- 連續執行 10 次唯讀 API 前後，`documents=63`、`versions=75`、`bom occurrences=99`，筆數均未改變。
- 無新版的 Part 在前端選取後，舊版參照警示數為 0，且未出現查詢失敗訊息。

## 前端與 RWD 驗證

測試路徑：`/documents`，搜尋料號 `100-00037`，選取 Drawing `CMP00199-01_V04_UDC旋轉頭`。

| Viewport | 警示結果 | 水平 overflow | 可見錯誤 | 證據 |
|---|---|---|---|---|
| 1440×900 | 完整顯示版本差異、occurrence、下一步與歷史保留說明 | 無，`scrollWidth=clientWidth=1440` | 0 | [桌面截圖](evidence/reference-update-desktop.png) |
| 1024×768 | 捲動至文件詳情後警示完整可見 | 無，`scrollWidth=clientWidth=1024` | 0 | [平板截圖](evidence/reference-update-tablet.png) |
| 390×844 | 卡片、版本標籤及 Check-in CTA 均未裁切 | 無，`scrollWidth=clientWidth=390` | 0 | [手機截圖](evidence/reference-update-mobile.png) |

- 三種 viewport 均能辨識「目前引用 Ver. 2／Rev. V03 → 子文件目前 Ver. 3／Rev. V04」。
- 三種 viewport 均顯示「歷史版本保留原參照，不會被自動改寫」與可執行的下一步。
- 瀏覽器 console `error`／`warn` 共 0 筆；可見錯誤提示 0 筆。

## 第二輪：Vault 清單直接顯示異常

UX 目標：工程人員進入圖檔中心、尚未選取任何文件時，即可在 5 秒內知道異常文件數量、
哪些文件需處理，以及選取警示列可查看版本差異與 SolidWorks 處理步驟。

### 搜尋 API

- `/api/documents/search` 新增 `referenceUpdateCount` 與 `affectedReferenceOccurrenceCount`，沿用相同的直屬 BOM 舊版判斷。
- 預設 50 筆結果中識別 5 份異常文件、6 個參照位置；測得回應時間 142 ms。
- 搜尋 `100-00037`：Part 為 `0／0`，Drawing 為 `1／1`，正反向資料一致。
- 搜尋正常 fixture `102-00003`：2 筆結果均無清單摘要或逐列警示，未誤報。

### 清單 UI 與 RWD

| Viewport | 未選取文件時的結果 | 水平 overflow | 證據 |
|---|---|---|---|
| 1440×900 | 首屏顯示「5 份文件／6 個位置」，前 5 列皆為置頂異常文件並顯示 `需更新` 徽章 | 無，`1440=1440` | [桌面清單](evidence/reference-update-vault-desktop.png) |
| 1024×768 | 摘要完整可見；檔名下方直接顯示異常數量，不需水平捲動才辨識 | 無，`1024=1024` | [平板清單](evidence/reference-update-vault-tablet.png) |
| 390×844 | 摘要及第一筆異常徽章均在首屏完整可見，文字與控制項未裁切 | 無，`390=390` | [手機清單](evidence/reference-update-vault-mobile.png) |

- 點選 `CMP00199-01_V04_UDC旋轉頭` 後，清單徽章與右側 V2／Rev. V03 → V3／Rev. V04
  詳情一致，且顯示下一步與歷史保留說明：[選取後證據](evidence/reference-update-vault-selected.png)。
- 2026-08-05 11:33:59 +08:00 執行 Visible Error Sweep：三種 viewport 的 `[role=alert]`、
  可見 HTTP／API 技術錯誤均為 0；console `error`／`warn` 為 0。

### Now What State Matrix

| State | 使用者問題 | 畫面先回答 | 下一步 | 結果 |
|---|---|---|---|---|
| 有異常、未選取 | 哪些文件要處理？ | 顯示異常文件與位置總數，異常列置頂 | 選取警示列 | Pass |
| 有異常、已選取 | 要更新到哪個版本？ | 顯示目前引用 → 子文件目前版本 | 到 SolidWorks 更新後重新入庫 | Pass |
| 無異常 | 是否仍需處理？ | 不顯示誤導性警示 | 維持一般 Vault 操作 | Pass |

### 人工 UX 檢查

| 問題 | 結果 | 證據 |
|---|---|---|
| 5 秒內是否知道頁面與異常狀態？ | Pass | 三種 viewport 清單截圖 |
| 是否知道需處理哪些文件？ | Pass | 異常列置頂、檔名下方 `需更新` 徽章 |
| 是否知道下一步？ | Pass | 摘要指向警示列；詳情指向 SolidWorks 更新與重新入庫 |
| 手機版是否不需左右滑動即可辨識異常？ | Pass | 390×844 首欄徽章完整可見 |

## 建置與工具結果

- `dotnet build SWPdm.sln --configuration Release`：通過，0 warnings、0 errors。
- `npm run build`：通過；TypeScript 與 Vite production build 完成。
- `git diff --check`：通過。
- `npm run lint`：未執行成功；既有專案宣告 lint script，但未安裝 `eslint` 相依套件。此為既有工具缺口，未掩蓋為通過。

## 風險邊界

- 本功能只根據已解析且具 child link 的直屬 BOM occurrence 提示新版，不修復 missing reference。
- 提示不等於自動更新；工程人員仍須在 SolidWorks 更新參照、儲存父文件，再重新入庫。
- 本輪未新增 migration，未 stage、commit、push 或部署至遠端主機。
