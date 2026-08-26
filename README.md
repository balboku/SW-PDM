# SolidWorks PDM API (本機儲存版)

完全獨立運作的 Web-based 3D CAD 檔案管理系統。提供強大的 SolidWorks 解析功能，並使用 PostgreSQL 和本機儲存（Local Storage）作為後端。

## ✅ 核心功能

1. **圖檔上傳入庫 (Ingest)**：自動解析 `SLDPRT` / `SLDASM` 的屬性、材質與 BOM 關係，寫入資料庫，並將實體圖檔存放於本機儲存區。
2. **屬性與 BOM 查詢**：透過 API 讀取圖檔最新版本與關聯樹（BOM）。
3. **遞迴打包與 ZIP 下載**：下載組合件時，會自動透過 CTE 查詢出所有的參考零件，並打包成不包含路徑層級的 zip 檔（Flat Structure），確保解壓縮後 SolidWorks 能夠無痛開啟並正確識別所有的參考零件。

## 🚀 快速啟動

本專案提供了一鍵啟動腳本。Windows 主機在專案根目錄下執行：

```powershell
.\start.ps1
```

Linux／macOS 環境可執行：

```bash
./start.sh
```

此腳本將自動：
1. 更新/套用最新的 Entity Framework Core 資料庫遷移 (Migration)。
2. 啟動 `SWPdm.Api` 服務。

- 主機本機 Web：`http://localhost:5174`
- 主機本機 API：`http://localhost:5000`
- 同一內網的同仁：使用 `http://<PDM 主機 IPv4>:5174` 開啟系統。

Windows 啟動腳本會讓 API 監聽 `0.0.0.0:5000`，供同一內網的 Web 用戶端載入圖面。
請只在受信任的公司內網使用，並確認 Windows 防火牆允許 `.NET Host` 的入站連線。

## 🛠 開發與測試

我們提供了一套完整的測試腳本，能在真實環境下驗證 API 的呼叫流程：

```bash
./scripts/test-ingest.sh
```

## ⚙️ 系統部署架構

- **Web Server**: ASP.NET Core 8
- **Database**: PostgreSQL 15+ (可透過 `./scripts/db-init-postgres.sql` 初始化)
- **ORM**: Entity Framework Core
- **Storage**: Local File System (本機資料夾，預設：`vault_storage/`)
- **CAD Parsing**: SolidWorks Document Manager API (`SolidWorks.Interop.swdocumentmgr.dll`)

## 🔑 設定檔

請將設定寫在 `src/SWPdm.Api/appsettings.Development.json`。
若無此檔案，可複製 `appsettings.json` 並確保完成設定，包含：
- 連線字串 `"Database:ConnectionString"`
- 本機儲存路徑 `"LocalStorage:VaultPath"`
- SolidWorks DM 金鑰 `"SolidWorksDocumentManager:LicenseKey"`

*(注意： `.gitignore` 已排除對應的本地開發設定檔與檔案儲存庫，避免敏感資料外流。)*
