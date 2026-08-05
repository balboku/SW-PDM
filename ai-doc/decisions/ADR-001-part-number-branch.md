# ADR-001：出庫後變更品號建立新文件身分

日期：2026-08-04
狀態：Accepted / Current Phase

## Context

一般 Check-in 以 `TargetDocumentId` 綁定原文件，且 CAD 解析出的 `PartNumber` 與
`DocumentType` 必須一致。工程變更有時會合法產生新品號；若直接放寬驗證並覆寫原
`DocumentId`，會污染歷史版本、BOM 與工程圖追溯。

## Decision

- 一般 Check-in 繼續禁止品號不一致覆寫。
- 使用者明確選擇「另存為新料號」後，系統建立新的 `PdmDocument` 與版本 1。
- 新文件以 `PdmDocumentIdentityChange` 記錄來源文件、來源版本、舊／新品號、原因、
  操作者與時間。
- 操作必須由原文件的出庫持有人執行，文件類型不可改變，原因必填，且新品號不可
  已被同類型文件使用。
- 成功後解除原文件出庫鎖；原文件、版本、生命週期及既有 BOM 均不修改。
- 分支不遞迴入庫參照 CAD；新版本只解析並連結系統中可辨識的既有參照，避免順帶
  建立其他文件的新版本。
- Where-used 與關聯工程圖只列入影響預覽，不自動改寫；需要變更時由各父文件另建新版。

## Options

1. 覆寫原文件品號：拒絕，會讓既有版本共用一個已改變意義的主資料身分。
2. 直接新增無來源的新文件：拒絕，無法稽核新品號從何而來。
3. 建立新品號文件並保留來源關聯：採用。

## Consequences

- 需要新增本地 schema 與 migration。
- `POST /api/ingest/cad` 增加可選的建立新品號旗標，舊用戶端行為不變。
- `409 CAD identity mismatch` 回應增加結構化舊／新品號資料，供 UI 顯示安全分流。
- 文件關聯 API 增加品號變更來源及衍生文件資訊。

## Compatibility / Migration

- 既有文件與版本不需資料修復。
- 新表只記錄功能啟用後的品號分支；migration 不會自動推送至正式環境。

## Amended Documents

- `ai-doc/dev_task.md`
- `ai-doc/documentation_map.md`
- `docs/pdm-blueprint.md`
