# 圖檔中心圖號 QC 報告

日期：2026-08-05
對應任務：`DEV-008`
結論：限制式通過；產品功能、production build 與 UI 驗證通過，僅保留既有 ESLint 工具缺口。

## 驗證範圍

- 目前版本的空白 configuration、精確 CAD 自訂屬性「圖號」為唯一資料來源。
- 搜尋 API 回傳 `drawingNumber`，關鍵字可比對檔名、圖號與料號。
- 圖檔中心清單及選取明細分別顯示圖號與料號；缺值顯示 `-`。
- 1024px 以下採上下資訊流，避免清單、篩選器與明細互相擠壓。

## 事實驗證

| 項目 | 結果 | 證據 |
|---|---|---|
| 完整圖號搜尋 | 通過 | `CMP00005-01` 只回傳文件 53，圖號 `CMP00005-01`、料號 `100-00020`、類型 `Drawing` |
| 無圖號文件 | 通過 | Part 文件 62、60、59 的 `drawingNumber` 為空，UI 顯示 `-` |
| 清單欄位 | 通過 | 欄位順序為檔名、圖號、料號、類型、版次、狀態、更新時間 |
| 選取明細 | 通過 | 圖號 `CMP00005-01` 與料號 `100-00020` 各自有明確標籤 |
| 後端建置 | 通過 | `dotnet build SWPdm.sln --configuration Release --no-restore`：0 warnings、0 errors |
| 前端建置 | 通過 | `npm run build`：TypeScript 與 Vite production build 成功 |
| 前端 lint | 限制 | `npm run lint` 找不到 ESLint 可執行檔；為既有相依套件缺口 |

## UI QC

| Viewport | 頁面寬度 | 頁面 scrollWidth | 結果 | 證據 |
|---|---:|---:|---|---|
| 1440×900 | 1440 | 1440 | 搜尋、清單與明細通過；無可見錯誤或 console error | [桌機截圖](evidence/drawing-number-desktop.png) |
| 1024×768 | 1024 | 1024 | 篩選控制完整可見，清單全寬；無可見錯誤 | [平板截圖](evidence/drawing-number-tablet.png) |
| 390×844 | 390 | 390 | 無頁面級水平 overflow；表格可局部左右滑動且有提示 | [手機截圖](evidence/drawing-number-mobile.png) |

## 剩餘風險

- 本輪只讀取精確屬性「圖號」，不以「工程圖號」補值；若未來要合併兩者，必須先定義主資料權威與衝突規則。
- 本輪不修改 CAD 屬性、不回填缺值，也不新增圖號唯一性或編輯流程。
