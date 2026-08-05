# DEV-007 品號分支 QC 報告

驗證日期：2026-08-04
結論：限制式通過；功能、交易安全與 UI 均通過，前端 lint 為既有工具鏈缺口。

## 驗證範圍與結果

| 驗證面向 | 結果 | 證據摘要 |
| --- | --- | --- |
| 一般 Check-in 安全預設 | 通過 | 品號不符回傳結構化 `409 CAD_IDENTITY_MISMATCH`，不覆寫目標文件。 |
| 分支資格 | 通過 | 唯一新品號回傳 `canCreateNewDocument: true`；同類型重複品號回傳 `false` 與阻擋原因。 |
| 正向交易 | 通過 | 建立新 DocumentId／Version 1／WIP 與來源稽核關聯，成功後才解除來源鎖。 |
| 原資料不變量 | 通過 | 來源 current version、版本歷史與 BOM signature 均未改變。 |
| 參照檔邊界 | 通過 | 品號分支不遞迴入庫參照 CAD，只在新版本解析／連結既有參照。 |
| 關聯查詢 | 通過 | relations API 可回傳 `identityOrigin` 與 `derivedDocuments`。 |
| Schema | 通過 | `AddDocumentIdentityChanges` migration 僅新增稽核表、外鍵與索引；已套用本機開發 DB，未套用正式環境。 |
| 後端建置 | 通過 | `dotnet build SWPdm.sln --no-restore`：0 warnings、0 errors。 |
| 前端建置 | 通過 | TypeScript production build 與 Vite bundle 成功。 |
| 前端 lint | 工具缺口 | `npm run lint` 找不到 ESLint；現有 `package.json` 未宣告 config 所需 ESLint 套件。 |

正向交易使用臨時品號建立測試文件後，已刪除該文件的版本、屬性、BOM、稽核列與 vault
檔案；來源文件恢復無 checkout，relations 查無殘留衍生文件。測試不改動正式資料，也未
點擊 UI 的「另存為新料號」建立第二筆資料。

## UI／UX QC

流程顯示「不能覆寫原文件」的安全狀態、舊／新品號、文件類型、向下參照、關聯工程圖、
where-used 與「不自動修改歷史 BOM」說明。原因與影響資料完成前主要 CTA 保持 disabled；
完成後可操作，且預期的 409 不再寫入 console error。

| Viewport | 頁面水平溢出 | Dialog 邊界 | 結果 |
| --- | --- | --- | --- |
| 1440×900 | 無，scrollWidth 1440 | 512×855.8，完整位於 viewport | 通過 |
| 1024×768 | 無，scrollWidth 1024 | 512×736，內部可捲動 | 通過 |
| 390×844 | 無，scrollWidth 390 | 358.4×812，CTA 捲動後位於可視區 | 通過 |

- [桌面證據](evidence/part-number-branch-desktop.png)
- [平板證據](evidence/part-number-branch-tablet.png)
- [手機證據](evidence/part-number-branch-mobile.png)

最新乾淨流程沒有 console error；只保留 React Router v7 future flag 的既有升級警告。

## Now What 檢查

| 狀態 | 使用者看到什麼 | 可執行下一步 |
| --- | --- | --- |
| 錯選 CAD | 品號／類型不符與阻擋原因 | 重新選擇檔案 |
| 合法新品號 | 舊→新品號與影響範圍 | 填寫原因後另存為新料號 |
| 新品號重複 | 明確指出同類型品號已存在 | 重新檢查品號或選檔 |
| 分支成功 | 新文件 ID、WIP 與原 BOM 未變 | 前往新文件繼續流程 |

## Release 邊界

- 本輪只套用本機開發資料庫 migration；正式環境仍需獨立審核、備份與 smoke test。
- ESLint 工具鏈缺口需另列維護任務，不影響本輪 TypeScript production build，但 release
  gate 若要求 lint 必須先補齊依賴並處理全專案既有告警。
