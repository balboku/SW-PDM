# 試用回饋優化 QC 報告

日期：2026-07-29
結果：Conditional Pass
範圍：DEV-001～DEV-006；未執行 release、部署、commit、push。

## 結論

本輪功能已完成並通過建置、唯讀／負向 API 測試與實際瀏覽器 UI 驗證。兩個會修改既有
試用資料的案例未執行：正向改名 Check-in、真實 CAD mixed-result 批次匯入。

## 建置與靜態檢查

| 項目 | 結果 | 證據 |
|---|---|---|
| `dotnet build SWPdm.sln` | Pass | Debug：0 warnings / 0 errors |
| `dotnet build SWPdm.sln -c Release` | Pass | Release：0 warnings / 0 errors |
| `npm run build` | Pass | TypeScript＋Vite；1421 modules transformed |
| `git diff --check` | Pass | 僅既有 LF/CRLF 提示，無 whitespace error |
| `npm run lint` | Not Run | 專案有 lint script，但未安裝 `eslint` 執行檔／devDependency |

Git 邊界提醒：使用者既有 `.gitignore` 的 `*.json` 會排除
`src/SWPdm.Web/package.json`、`package-lock.json` 與一般 `tsconfig.json`。本輪未修改該
使用者檔案，TypeScript 設定改存為可見的 `tsconfig.build.jsonc`；正式提交前仍需由專案
負責人決定是否修正 JSON ignore 規則，否則乾淨 clone 無法取得前端 dependency manifest。

## API 與資料安全

| 案例 | 結果 | 實際觀察 |
|---|---|---|
| Check-in 身分不符 | Pass | 以不同 Part/Type 指向文件 53，回傳 HTTP 409 `CAD identity mismatch`；文件仍由 `User` 出庫 |
| Assembly 反向 Drawing | Pass | 文件 55：references 1、drawings 2、whereUsed 1 |
| Part 反向 Drawing | Pass | `CMP00022...SLDPRT` 顯示 1 張 `CMP00022-01...SLDDRW` |
| Pack & Go 不含 Drawing | Pass | ZIP 2 entries、0 SLDDRW |
| Pack & Go 包含 Drawing | Pass | ZIP 4 entries、2 SLDDRW |
| Batch path traversal | Pass | `../bad.sldprt` 回傳 HTTP 400 |
| Batch 逐檔失敗 | Pass | 2 個無效 CAD 回傳 total 2、failed 2，兩筆皆含各自錯誤 |
| Batch staging cleanup | Pass | 該次批次暫存目錄處理後不存在 |

## UI / UX 驗證

實際瀏覽器驗證 1440×900、1024×768、390×844：

- `/ingest`：單檔／資料夾多檔模式可切換；桌機、平板、手機均無全頁水平 overflow。
- `/documents`：Part／Assembly 可見反向 Drawing；Pack & Go 預設勾選 2 張工程圖。
- Check-in modal 可從出庫文件開啟；確認按鈕在未選檔時維持 disabled。
- visible error sweep 未發現 HTTP、API、Not Found、Internal Server Error 或可見 alert。
- QC 首輪發現手機固定側欄壓縮主內容，以及圖檔中心標題對比／篩選截斷；修正後重測通過。

## 截圖證據

- [批次匯入桌機](evidence/batch-import-desktop-viewport.png)
- [批次匯入平板](evidence/batch-import-tablet.png)
- [批次匯入手機（修正後）](evidence/batch-import-mobile-fixed.png)
- [圖檔中心手機（修正後）](evidence/documents-mobile-fixed.png)
- [Assembly 關聯工程圖](evidence/assembly-related-drawings-visible.png)
- [Pack & Go 工程圖預檢](evidence/pack-and-go-drawings-preflight.png)
- [Check-in 目標文件 modal](evidence/check-in-target-identity.png)

## 尚未充分驗證

1. 正向改名 Check-in：需要一份品號、DocumentType、checkout owner 正確且 Revision 已變更的
   受控 CAD；送出會建立新版本並解除鎖，本輪刻意未修改現有試用資料。
2. Mixed-result batch：逐檔 transaction 與錯誤收集已由程式結構及全失敗 live fixture 驗證，
   但未用真實 CAD 建立「一成功、一失敗」資料異動。

這兩項是 release 前的受控資料驗收項目，不阻擋本輪程式實作完成。
