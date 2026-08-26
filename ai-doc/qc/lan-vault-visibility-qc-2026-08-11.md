# DEV-011 內網圖面清單可見性 QC 報告

驗證日期：2026-08-11
對應任務：`DEV-011`／`SWPDM-LAN-VAULT-VISIBILITY-20260811`
結論：限制式通過；API 已由 localhost-only 改為內網可達，Vault 能從 LAN URL 載入 50 份文件，錯誤狀態與重新連線流程通過。尚待同仁實際工作站重新整理後做最後使用者確認。

## 根因與修正

| 項目 | 修正前 | 修正後 |
|---|---|---|
| 前端監聽 | `0.0.0.0:5174` | 不變 |
| API 監聽 | `127.0.0.1:5000`／`::1:5000` | `0.0.0.0:5000` |
| API 啟動命令 | `--urls http://localhost:5000` | `--urls http://0.0.0.0:5000` |
| localhost 文件數 | 50 | 50 |
| LAN 文件數 | 無法連線 | 50 |
| Vault 載入失敗 | console error 後顯示「查無圖檔資料」 | 顯示連線錯誤、摘要 `—` 與「重新連線」 |

Windows 啟動腳本已明確帶入 `0.0.0.0:5000`，避免日後被 launch profile 或人工啟動參數限制為 localhost-only。現行 Windows 防火牆已有 `.NET Host` 入站允許規則。

## API 與 LAN Smoke

驗證來源：主機 `192.168.20.62`，前端 origin `http://192.168.20.62:5174`。

| 檢查 | 實際結果 | 判定 |
|---|---|---|
| `GET http://127.0.0.1:5000/api/documents/search` | HTTP 200、50 份文件 | Pass |
| `GET http://192.168.20.62:5000/api/documents/search` | HTTP 200、50 份文件 | Pass |
| CORS `Access-Control-Allow-Origin` | `http://192.168.20.62:5174` | Pass |
| API listener | `0.0.0.0:5000` | Pass |
| 資料不變量 | 驗證期間未修改 PostgreSQL、Vault 檔案、版本、BOM 或出入庫狀態 | Pass |

## UI / RWD 驗證

路由：`http://192.168.20.62:5174/documents`

### 正常狀態

| Viewport | 文件列 | 摘要 | Page width / scrollWidth | 結果 |
|---|---:|---|---|---|
| 1440×900 | 50 | 50／5／9／41 | 1440／1440 | Pass |
| 1024×768 | 50 | 50／5／9／41 | 1024／1024 | Pass |
| 390×844 | 50 | 50／5／9／41 | 390／390 | Pass |

### API 不可用錯誤狀態

受控暫停 API 後重新載入同一路由，並在驗證後立即恢復 API。

| Viewport | 可見判定 | 下一步 CTA | 誤顯示「查無圖檔資料」 | Page width / scrollWidth | 結果 |
|---|---|---|---|---|---|
| 1440×900 | 目前無法載入圖面清單 | 重新連線 | 否 | 1440／1440 | Pass |
| 1024×768 | 目前無法載入圖面清單 | 重新連線 | 否 | 1024／1024 | Pass |
| 390×844 | 目前無法載入圖面清單 | 重新連線 | 否 | 390／390 | Pass |

API 恢復後，在同一錯誤畫面按「重新連線」，警示消失並重新顯示 50 份文件與摘要 50／5／9／41，不需關閉頁面。

## Now What State Matrix

| State | 使用者問題 | 首句 | 下一步 | 結果 |
|---|---|---|---|---|
| loading | 圖面正在載入嗎？ | 摘要顯示 `…`、篩選停用 | 等待載入完成 | Pass |
| empty | 真的沒有符合條件的圖面嗎？ | 查無圖檔資料 | 調整搜尋或篩選 | Pass |
| error | 為什麼看不到圖面？ | 目前無法載入圖面清單 | 確認主機服務後按「重新連線」；仍失敗則通知管理者 | Pass |
| recovered | 服務恢復後要重開系統嗎？ | 警示消失並顯示 50 份文件 | 直接繼續搜尋或選取文件 | Pass |

## Visible Error Sweep

- 最終正常狀態：`.inline-error=[]`、`[role=alert]=[]`。
- 未出現可見 `HTTP 4xx/5xx`、`Not Found`、`Internal Server Error` 或 `/api/...` 技術文字。
- 新開正常頁面的 browser console error／warning：`[]`。
- 關鍵計數：目前結果 50、參照異常 5、已出庫 9、可用 41；不是非預期全零。
- 錯誤狀態中的 `[role=alert]` 為本案例刻意驗證的預期 UI，恢復後已在同一頁確認消失。

## 建置與靜態檢查

- `dotnet build SWPdm.sln --configuration Release --no-restore`：通過，0 warning、0 error。
- `npm run build`：通過，1421 modules transformed。
- `start.ps1` PowerShell parser：通過。
- `npm run lint`：未執行成功；現有專案未安裝可執行的 ESLint 相依套件，屬既有工具缺口。

## 證據

- `evidence/lan-vault-normal-1440.png`
- `evidence/lan-vault-normal-1024.png`
- `evidence/lan-vault-normal-390.png`
- `evidence/lan-vault-error-390.png`
- 1440／1024 錯誤狀態另有 DOM 尺寸、alert 邊界與 overflow 量測；截圖工具在切換較大 viewport 時未可靠輸出完整指定寬度，因此不採用該檔案作為 viewport 截圖證據。

## Git 與殘留風險

- 本輪新增修改：`start.ps1`、`README.md`、`src/SWPdm.Web/src/pages/Documents.tsx`、DEV/QC 文件及上述證據。
- 工作樹仍包含 DEV-008～DEV-010 尚未提交的既有變更；本輪未 stage、commit 或 push。
- API 目前已在本機以 Release DLL 監聽 `0.0.0.0:5000`，前端 preview 維持 `0.0.0.0:5174`。
- 最後限制：尚未直接控制同仁的實際工作站；請同仁以 `Ctrl+F5` 開啟 `http://192.168.20.62:5174/documents` 做最終確認。
