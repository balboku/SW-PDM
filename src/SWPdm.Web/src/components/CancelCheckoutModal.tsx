import React, { useEffect, useRef, useState } from 'react';
import { Modal, Button } from './ui';
import { getCancelCheckoutPreview, undoCheckOutDocument } from '../lib/api';

type Props = { isOpen: boolean; documentId: number; fileName: string; initialActor?: string;
  onClose: () => void; onSuccess: () => void };

export const CancelCheckoutModal: React.FC<Props> = ({ isOpen, documentId, fileName, initialActor, onClose, onSuccess }) => {
  const [actor, setActor] = useState(initialActor || 'User');
  const [preview, setPreview] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [complete, setComplete] = useState(false);
  const requestId = useRef(0);

  const load = async (requestedActor = actor) => {
    const request = ++requestId.current;
    setLoading(true); setError(''); setPreview(null);
    try {
      const snapshot = await getCancelCheckoutPreview(documentId, requestedActor.trim());
      if (snapshot?.scope !== 'SingleDocument' || snapshot?.documentId !== documentId ||
        snapshot?.actor !== requestedActor.trim() || typeof snapshot?.canRestore !== 'boolean' ||
        (snapshot.canRestore && (!snapshot.checkedOutAt || !snapshot.currentVersionId)))
        throw new Error('無法辨識伺服器的鎖定預覽，請確認服務版本後重試。');
      if (requestId.current === request) setPreview(snapshot);
    } catch {
      if (requestId.current === request) setError('出庫狀態尚未確認，請重新檢查。若持續失敗，請確認前後端服務版本；未解除任何鎖。');
    } finally {
      if (requestId.current === request) setLoading(false);
    }
  };
  useEffect(() => {
    if (isOpen) {
      const nextActor = initialActor || 'User';
      setActor(nextActor); setComplete(false); void load(nextActor);
    }
    return () => { requestId.current++; };
  }, [isOpen, documentId, initialActor]);

  const confirm = async () => {
    if (!preview?.canRestore || preview.actor !== actor.trim() || loading || busy || error) return;
    setBusy(true); setError('');
    try {
      // Preserve the exact timestamp string: JS Date would truncate PostgreSQL microseconds.
      await undoCheckOutDocument(documentId, { actor: preview.actor, expectedLockTime: preview.checkedOutAt,
        expectedVersionId: preview.currentVersionId, includeBatch: false });
      setComplete(true); setPreview(null); onSuccess();
    } catch (failure: any) {
      setPreview(null);
      setError(failure.response?.data?.code === 'LOCK_CHANGED'
        ? '鎖定或版本已改變，未解除鎖定。請重新檢查，再決定是否取消。'
        : '取消出庫未確認完成，請重新檢查狀態後再試；不要直接重複送出。');
    } finally { setBusy(false); }
  };

  return <Modal isOpen={isOpen} onClose={() => !busy && onClose()} title="取消出庫（解除鎖定）">
    <div className="space-y-4 text-sm text-gray-200">
      <p className="break-all font-semibold text-white">{fileName}</p>
      <p>本次只解除這份圖檔的出庫鎖，其他關聯檔保持原狀。本機尚未入庫的修改不會上傳，也不會刪除。</p>
      {!complete && <div className="space-y-2">
        <label htmlFor="cancel-checkout-actor" className="block">操作人（請填自己的出庫名稱）</label>
        <input id="cancel-checkout-actor" className="w-full rounded border border-gray-600 bg-gray-900 p-2 text-white focus:border-blue-500 focus:outline-none" value={actor} maxLength={100} disabled={busy} onChange={event => {
          requestId.current++; setActor(event.target.value); setPreview(null); setLoading(false); setError('');
        }} />
        <Button variant="secondary" disabled={loading || busy || !actor.trim()} onClick={() => load()}>重新檢查出庫狀態</Button>
      </div>}
      {loading && <p role="status">正在確認目前鎖定，請稍候…</p>}
      {error && <div role="alert" className="rounded border border-red-700 p-3 text-red-200">{error}</div>}
      {complete ? <div role="status">
        <p className="text-green-300">已解除這份圖檔的鎖定。其他關聯檔保持原狀。</p>
        <Button className="mt-3" onClick={onClose}>返回圖檔中心</Button>
      </div> : <>
        {preview && <div role="status" className="space-y-2 rounded border border-gray-600 p-3">
          <p>{preview.reason}</p>
          {preview.checkedOutBy && <p className="break-all">目前出庫人：{preview.checkedOutBy}</p>}
          {preview.canRestore && <p>本次確認範圍：1 份圖檔，不包含其他關聯檔。</p>}
        </div>}
        <details className="text-xs text-amber-200">
          <summary className="cursor-pointer">安全檢查與限制</summary>
          <p className="mt-2">伺服器會同時核對操作人、鎖定時間及版本，任一改變即停止。本版不支援批次復原；操作人名稱不是登入驗證，請勿代填他人名稱。</p>
        </details>
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="secondary" disabled={busy} onClick={onClose}>返回，不解除</Button>
          <Button disabled={loading || busy || !preview?.canRestore || preview?.actor !== actor.trim() || Boolean(error)} onClick={confirm}>
            {busy ? '正在解除…' : '確認解除這份圖檔'}
          </Button>
        </div>
      </>}
    </div>
  </Modal>;
};
