import React, { useEffect, useRef, useState } from 'react';
import { Modal, Button } from './ui';
import { api, searchDocuments, undoCheckOutDocument } from '../lib/api';

type Props = { isOpen: boolean; documentId: number; fileName: string; onClose: () => void; onSuccess: () => void };

// Uses the already-published single-document endpoint, not the pending batch workflow.
export const CancelCheckoutModal: React.FC<Props> = ({ isOpen, documentId, fileName, onClose, onSuccess }) => {
  const [preview, setPreview] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [complete, setComplete] = useState(false);
  const requestId = useRef(0);

  const readSnapshot = async () => {
    // Published details omit lock fields; read those from the existing search response.
    const [detail, result] = await Promise.all([
      api.get(`/api/documents/${documentId}`), searchDocuments(fileName)
    ]);
    const rows = Array.isArray(result) ? result : result?.items;
    const row = rows?.find((item: any) => item.documentId === documentId);
    if (!row || !Object.prototype.hasOwnProperty.call(row, 'checkedOutBy'))
      throw new Error('無法確認這份圖檔的出庫狀態，請縮小搜尋範圍或重新整理清單。');
    return { ...detail.data, checkedOutBy: row.checkedOutBy, checkedOutAt: row.checkedOutAt };
  };

  const load = async () => {
    const request = ++requestId.current;
    setLoading(true); setError(''); setPreview(null);
    try {
      const snapshot = await readSnapshot();
      if (requestId.current === request) setPreview(snapshot);
    } catch {
      if (requestId.current === request) setError('出庫狀態尚未確認，請重新檢查後再解除鎖定。');
    } finally {
      if (requestId.current === request) setLoading(false);
    }
  };
  useEffect(() => {
    if (isOpen) { setComplete(false); void load(); }
    return () => { requestId.current++; };
  }, [isOpen, documentId]);

  const confirm = async () => {
    if (!preview?.checkedOutBy || loading || busy) return;
    setBusy(true); setError('');
    try {
      const latest = await readSnapshot();
      if (latest.checkedOutBy !== preview.checkedOutBy || latest.checkedOutAt !== preview.checkedOutAt
        || latest.currentVersionId !== preview.currentVersionId) {
        setPreview(null);
        setError('鎖定或版本已改變，未解除鎖定。請重新檢查再決定。');
        return;
      }
      await undoCheckOutDocument(documentId);
      setComplete(true);
      onSuccess();
    } catch {
      setPreview(null);
      setError('取消出庫未完成，請重新檢查狀態後再試。');
    } finally { setBusy(false); }
  };

  return <Modal isOpen={isOpen} onClose={() => !busy && onClose()} title="取消出庫（解除鎖定）">
    <div className="space-y-4 text-sm text-gray-200">
      <p className="break-all font-semibold text-white">{fileName}</p>
      <p>本次只解除這份圖檔的出庫鎖，不會處理其他關聯檔；本機尚未入庫的修改不會上傳，也不會刪除。</p>
      {loading && <p role="status">正在確認目前鎖定，請稍候…</p>}
      {error && <div role="alert" className="rounded border border-red-700 p-3 text-red-200">
        <p>{error}</p><Button className="mt-3" variant="secondary" onClick={load}>重新檢查出庫狀態</Button>
      </div>}
      {complete ? <div role="status">
        <p className="text-green-300">已解除這份圖檔的鎖定。其他關聯檔保持原狀。</p>
        <Button className="mt-3" onClick={onClose}>返回圖檔中心</Button>
      </div> : <>
        {preview && <p>{preview.checkedOutBy ? `目前由 ${preview.checkedOutBy} 出庫；確認後解除 1 份鎖定。` : '這份圖檔目前沒有出庫鎖，不需要再取消。'}</p>}
        <details className="text-xs text-amber-200">
          <summary className="cursor-pointer">既有 API 的限制</summary>
          <p className="mt-2">本版沒有批次復原及伺服器端競爭鎖驗證。送出前會重新讀取狀態，但不能取代伺服器的原子性保護。</p>
        </details>
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="secondary" disabled={busy} onClick={onClose}>返回，不解除</Button>
          <Button disabled={loading || busy || !preview?.checkedOutBy || Boolean(error)} onClick={confirm}>
            {busy ? '正在解除…' : '確認解除這份圖檔'}
          </Button>
        </div>
      </>}
    </div>
  </Modal>;
};
