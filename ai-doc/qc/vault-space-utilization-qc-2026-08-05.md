# Vault 寬螢幕與資訊密度 QC 報告

驗證日期：2026-08-05
對應任務：`DEV-010`／`SWPDM-VAULT-SPACE-20260805`
結論：限制式通過；Vault 寬螢幕利用率、頁首摘要、清單資訊密度、詳情寬度與四種 viewport 均符合驗收條件。

## 驗證範圍

- `/documents` 使用路由專屬全寬模式；Dashboard 與 Ingest 維持原本最大寬度。
- 頁首顯示目前結果、參照異常、已出庫及可用四項搜尋結果摘要。
- 清單版本欄同時顯示 VersionNo 與 RevisionLabel。
- 1920px 寬螢幕下文件詳情面板由原本 384px 擴充為 512px。
- 不修改 API、資料庫、文件狀態或出入庫流程。

## 寬螢幕空間量測

測試條件：`/documents`、1920×1080、主內容區寬度 1664px。

| 指標 | 改善前 | 改善後 | 結果 |
|---|---:|---:|---|
| Vault 內容 wrapper | 1280px | 1600px | +320px |
| 主內容區未使用寬度 | 384px | 64px | -320px |
| 主內容區利用率 | 76.9% | 96.2% | +19.3 個百分點 |
| 寬螢幕詳情面板 | 384px | 512px | +128px |

- 改善前：[1920px 基準](evidence/vault-space-before-1920.png)
- 改善後：[1920px 全寬 Vault](evidence/vault-space-after-1920.png)
- 選取文件：[1920px 清單與詳情並排](evidence/vault-space-selected-1920.png)

## 資料與互動驗證

- 頁首摘要顯示：目前結果 50、參照異常 5、已出庫 9、可用 41。
- 資料 sanity：`已出庫 9 + 可用 41 = 目前結果 50`；參照異常 5 與清單警示列數一致。
- 點選 `CMP00199-01_V04_UDC旋轉頭` 後，512px 詳情面板完整顯示版本差異、下一步及 Check-in CTA。
- 版本欄可同時辨識 `Ver. 2／Rev. V04`；缺少版次的文件安全顯示 `Rev. -`。
- 1920×1080 下 Dashboard 與 Ingest 共用 wrapper 仍為 1280px，未被 Vault 全寬模式影響。

## RWD 與 Visible Error Sweep

| Viewport | 摘要與主要內容 | 頁面水平 overflow | 可見錯誤 | 證據 |
|---|---|---|---|---|
| 1920×1080 | 摘要位於頁首右側；Vault 使用 1600px；清單欄位完整 | 無，`1920=1920` | 0 | [寬螢幕](evidence/vault-space-after-1920.png) |
| 1440×900 | 頁首與四項摘要同行；第一筆異常列在首屏 | 無，`1440=1440` | 0 | [桌面](evidence/vault-space-1440.png) |
| 1024×768 | 摘要改為獨立橫列；搜尋與清單仍可操作 | 無，`1024=1024` | 0 | [平板](evidence/vault-space-1024.png) |
| 390×844 | 摘要為 2×2；警示與第一筆異常列仍在首屏 | 無，`390=390` | 0 | [手機](evidence/vault-space-390.png) |

- Visible Error Sweep 時間：2026-08-05 11:49:27 +08:00。
- 三種既定 viewport 加 1920 寬螢幕的 `[role=alert]`、HTTP／API 技術錯誤皆為 0。
- 瀏覽器 console `error`／`warn` 為 0。
- 未發現重疊、裁切、頁面級水平 overflow、按鈕被擠壓或不可操作狀態。

## Now What 與人工 UX 檢查

| 狀態 | 畫面先回答 | 下一步 | 結果 |
|---|---|---|---|
| 進入 Vault | 目前有多少結果、異常、已出庫及可用文件 | 搜尋、篩選或選取文件 | Pass |
| 有參照異常 | 顯示異常數量並將異常列置頂 | 選取警示列查看版本差異 | Pass |
| 已選取文件 | 顯示完整屬性、版本、關聯與處理操作 | 依狀態出庫／入庫或查看關聯 | Pass |

| 人工問題 | 結果 | 備註 |
|---|---|---|
| 寬螢幕空白是否明顯減少？ | Pass | 內容增加 320px，利用率達 96.2% |
| 新增資訊是否支援操作判斷？ | Pass | 四項摘要、版本／版次與詳情均直接支援掃描或下一步 |
| 是否因填滿空間造成資訊疲勞？ | Pass | 頁首僅保留四個短摘要，完整資訊仍在清單及詳情 |
| 手機是否可不左右滑動讀取摘要與異常？ | Pass | 2×2 摘要及檔名下異常徽章完整可見 |

## 建置與工具結果

- `npm run build`：通過；TypeScript 與 Vite production build 完成。
- `git diff --check`：通過。
- `npm run lint`：未執行成功；既有專案未安裝 `eslint` 相依套件，此既有工具缺口未掩蓋為通過。

## Git 與交付邊界

- 本輪新增修改為 `App.tsx`、`components/ui.tsx`、`pages/Documents.tsx` 及 AI 任務／QC 文件。
- 工作目錄仍包含尚未提交的 DEV-008／DEV-009 變更；本輪未 stage、commit 或 push。
