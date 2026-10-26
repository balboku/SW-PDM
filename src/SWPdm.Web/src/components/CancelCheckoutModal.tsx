import React, { useEffect, useRef, useState } from 'react';
import { Modal, Button } from './ui';
import { getCancelCheckoutPreview, getCheckoutBatches, getBatchCheckoutPreview, undoCheckOutDocument } from '../lib/api';
import { isBatchId, isBatchPreview } from '../lib/vault-model';

type Props = { isOpen: boolean; documentId: number; fileName: string; initialActor?: string;
  onClose: () => void; onSuccess: () => void };

export const CancelCheckoutModal: React.FC<Props> = ({ isOpen, documentId, fileName, initialActor, onClose, onSuccess }) => {
  const [actor, setActor] = useState(initialActor || 'User');
  const [mode, setMode] = useState<'single' | 'batch'>('single');
  const [batches, setBatches] = useState<any[] | null>(null);
  const [batchId, setBatchId] = useState('');
  const [preview, setPreview] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const [error, setError] = useState('');
  const [complete, setComplete] = useState<any>(null);
  const requestId = useRef(0);
  const invalidate = () => { requestId.current++; setPreview(null); setError(''); setLoading(false); };

  const load = async (requestedActor = actor, requestedMode = mode, selectedBatch = batchId) => {
    if (!requestedActor.trim() || (requestedMode === 'batch' && !isBatchId(selectedBatch))) return;
    const request = ++requestId.current;
    setLoading(true); setError(''); setPreview(null);
    try {
      const snapshot = requestedMode === 'single'
        ? await getCancelCheckoutPreview(documentId, requestedActor.trim())
        : await getBatchCheckoutPreview(documentId, requestedActor.trim(), selectedBatch);
      const valid = requestedMode === 'batch' ? isBatchPreview(snapshot, documentId, selectedBatch, requestedActor)
        : snapshot?.scope === 'SingleDocument' && snapshot?.documentId === documentId &&
          snapshot?.actor === requestedActor.trim() && typeof snapshot?.canRestore === 'boolean' &&
          (!snapshot.canRestore || (snapshot.checkedOutAt && snapshot.currentVersionId));
      if (!valid) throw new Error('Invalid preview');
      if (requestId.current === request) setPreview(snapshot);
    } catch {
      if (requestId.current === request) setError('出庫狀態尚未確認，請重新檢查。若持續失敗，請確認服務版本或操作人；未解除任何鎖。');
    } finally { if (requestId.current === request) setLoading(false); }
  };
  const loadBatches = async () => {
    const request = ++requestId.current;
    setLoading(true); setPreview(null); setBatches(null); setBatchId(''); setError('');
    try {
      const result = await getCheckoutBatches(documentId, actor.trim());
      if (!Array.isArray(result) || !result.every(b => isBatchId(b.batchId) && b.rootDocumentId === documentId &&
          b.actor?.toLowerCase() === actor.trim().toLowerCase() && Number.isSafeInteger(b.acquiredCount) && b.acquiredCount > 0))
        throw new Error('Invalid batch list');
      if (requestId.current === request) setBatches(result);
    } catch { if (requestId.current === request) setError('批次清單尚未載入，請重新載入；不會自動推測或改用其他批次。'); }
    finally { if (requestId.current === request) setLoading(false); }
  };
  useEffect(() => {
    if (isOpen) {
      const nextActor = initialActor || 'User';
      setActor(nextActor); setMode('single'); setBatches(null); setBatchId(''); setComplete(null);
      void load(nextActor, 'single', '');
    }
    return () => { requestId.current++; };
  }, [isOpen, documentId, initialActor]);

  const confirm = async () => {
    if (!preview?.canRestore || preview.actor !== actor.trim() || loading || busyRef.current || error ||
        (mode === 'batch' && !isBatchPreview(preview, documentId, batchId, actor))) return;
    busyRef.current = true; setBusy(true); setError('');
    try {
      // Keep the exact microsecond timestamp for single-document requests.
      const request = mode === 'single'
        ? { actor: preview.actor, expectedLockTime: preview.checkedOutAt, expectedVersionId: preview.currentVersionId, includeBatch: false as const }
        : { actor: preview.actor, includeBatch: true as const, batchId: preview.batchId };
      const result = await undoCheckOutDocument(documentId, request);
      if (result?.scope !== preview.scope || !Number.isSafeInteger(result.restoredCount) || result.restoredCount < 0 ||
          (mode === 'batch' && (result.batchId !== batchId || !Array.isArray(result.skipped)))) throw new Error('Unknown result');
      setComplete(result); setPreview(null); onSuccess();
    } catch (failure: any) {
      setPreview(null);
      setError(failure.response?.data?.code === 'LOCK_CHANGED'
        ? '鎖定或版本已改變，未解除鎖定。請重新檢查，再決定是否取消。'
        : '取消出庫未確認完成，請重新檢查狀態後再試；不要直接重複送出。');
    } finally { busyRef.current = false; setBusy(false); }
  };

  return <Modal isOpen={isOpen} onClose={() => !busyRef.current && onClose()} title="取消出庫（解除鎖定）">
    <div className="space-y-4 text-sm text-gray-200">
      <p className="break-all font-semibold text-white">{fileName}</p>
      <p>{mode === 'single' ? '本次只解除這份圖檔，其他關聯檔保持原狀。' : '只解除指定批次當時新取得且現在仍符合條件的鎖；先前已有的鎖與後續新鎖保持原狀。'}本機修改不會上傳或刪除。</p>
      {!complete && <>
        <fieldset className="space-y-2 rounded border border-gray-600 p-3" disabled={busy}>
          <legend className="px-1">解除範圍</legend>
          <label className="flex items-center gap-2"><input type="radio" name="restore-scope" checked={mode === 'single'} onChange={() => {
            invalidate(); setMode('single'); setBatches(null); setBatchId(''); void load(actor, 'single', '');
          }} />只解除這份圖檔</label>
          <label className="flex items-center gap-2"><input type="radio" name="restore-scope" checked={mode === 'batch'} onChange={() => {
            invalidate(); setMode('batch'); setBatches(null); setBatchId('');
          }} />復原這次出庫批次（先選批次並預覽）</label>
        </fieldset>
        <div className="space-y-2">
          <label htmlFor="cancel-checkout-actor" className="block">操作人（請填自己的出庫名稱）</label>
          <input id="cancel-checkout-actor" className="w-full rounded border border-gray-600 bg-gray-900 p-2 text-white focus:border-blue-500 focus:outline-none"
            value={actor} maxLength={100} disabled={busy} onChange={event => {
              invalidate(); setActor(event.target.value); setBatches(null); setBatchId('');
            }} />
          {mode === 'batch' && <>
            <Button variant="secondary" disabled={loading || busy || !actor.trim()} onClick={loadBatches}>載入我的出庫批次</Button>
            {batches && (batches.length ? <>
              <label htmlFor="restore-batch" className="block">選擇要復原的批次（不會自動選取）</label>
              <select id="restore-batch" value={batchId} disabled={busy || loading} onChange={event => {
                invalidate(); setBatchId(event.target.value);
              }} className="w-full min-w-0 rounded border border-gray-600 bg-gray-900 p-2 text-white">
                <option value="">請選擇批次</option>
                {batches.map(b => <option key={b.batchId} value={b.batchId}>
                  {new Date(b.createdAt).toLocaleString('zh-TW')} · 新鎖 {b.acquiredCount}／已復原 {b.restoredCount} · {b.batchId.slice(0, 8)}
                </option>)}
              </select>
            </> : <p role="status">沒有可辨識的本人批次。請改用「只解除這份圖檔」；舊出庫不會推算為新批次。</p>)}
          </>}
          <Button variant="secondary" disabled={loading || busy || !actor.trim() || (mode === 'batch' && !batchId)} onClick={() => load()}>
            重新檢查出庫狀態
          </Button>
        </div>
      </>}
      {loading && <p role="status">正在確認目前鎖定，請稍候…</p>}
      {error && <div role="alert" className="rounded border border-red-700 p-3 text-red-200">{error}</div>}
      {complete ? <div role="status" className="space-y-3">
        <p className="text-green-300">{complete.restoredCount > 0 ? '已解除 ' + complete.restoredCount + ' 份圖檔的鎖定。' : '這次沒有解除任何鎖；請重新查看狀態。'}未列入解除的圖檔保持原狀。</p>
        {(complete.skipped || []).map((item: any) => <p key={item.documentId} className="break-words text-amber-200">保留 {item.fileName}：{item.reason}</p>)}
        <Button onClick={onClose}>返回圖檔中心</Button>
      </div> : <>
        {preview && <div role="status" className="space-y-2 rounded border border-gray-600 p-3">
          {mode === 'single' ? <>
            <p>{preview.reason}</p>
            {preview.checkedOutBy && <p className="break-all">目前出庫人：{preview.checkedOutBy}</p>}
            {preview.canRestore && <p>本次確認範圍：1 份圖檔，不包含其他關聯檔。</p>}
          </> : <>
            <p className="font-semibold">可解除 {preview.restorableCount} 份；保留 {preview.items.length - preview.restorableCount} 份。</p>
            {!preview.canRestore && <p>沒有符合條件的鎖，不需再次復原；可返回或改選批次。</p>}
            <ul className="max-h-56 space-y-2 overflow-y-auto overscroll-contain">
              {preview.items.map((item: any) => <li key={item.documentId} className="break-words rounded border border-gray-700 p-2">
                <p className="font-medium">{item.canRestore ? '將解除：' : '將保留：'}{item.fileName}</p><p className="mt-1 text-xs text-gray-400">{item.reason}</p>
              </li>)}
            </ul>
          </>}
        </div>}
        <details className="text-xs text-amber-200">
          <summary className="cursor-pointer">安全檢查與限制</summary>
          <p className="mt-2">伺服器核對操作人、鎖定時間及版本。批次只處理該次新取得的鎖；操作人名稱不是登入驗證，請勿代填他人名稱。</p>
        </details>
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="secondary" disabled={busy} onClick={onClose}>返回，不解除</Button>
          <Button disabled={loading || busy || !preview?.canRestore || preview?.actor !== actor.trim() || Boolean(error)} onClick={confirm}>
            {busy ? '正在解除…' : mode === 'single' ? '確認解除這份圖檔' : '確認解除 ' + (preview?.restorableCount || 0) + ' 份圖檔'}
          </Button>
        </div>
      </>}
    </div>
  </Modal>;
};
