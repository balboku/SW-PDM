import React, { useEffect, useRef, useState } from 'react';
import { Modal } from './ui';
import { getCheckoutReferences, checkOutDocument } from '../lib/api';
import { uniqueRelatedFiles } from '../lib/ui-model';
import { AlertCircle, CheckCircle2, FileText, Package, Layout, Loader2, Lock, Unlock } from 'lucide-react';

interface CheckOutModalProps {
  isOpen: boolean;
  onClose: () => void;
  documentId: number;
  fileName: string;
  onSuccess: (checkedOutBy: string) => void;
}

export const CheckOutModal: React.FC<CheckOutModalProps> = ({ 
  isOpen, 
  onClose, 
  documentId, 
  fileName,
  onSuccess
}) => {
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [data, setData] = useState<any>(null);
  const [error, setError] = useState<string | null>(null);
  const [userName, setUserName] = useState('User');
  const [submitError, setSubmitError] = useState('');
  const submitErrorPanel = useRef<HTMLDivElement>(null);
  const requestId = useRef(0);

  const relatedFiles = data ? uniqueRelatedFiles(documentId, data) : [];
  const normalizedUserName = userName.trim().toLowerCase();
  const blockingLocks = relatedFiles.filter((file: any) => (
    file.checkedOutBy &&
    file.checkedOutBy.trim().toLowerCase() !== normalizedUserName
  ));
  const hasBlockingLocks = blockingLocks.length > 0;

  useEffect(() => {
    if (submitError) {
      submitErrorPanel.current?.focus();
      submitErrorPanel.current?.scrollIntoView({ block: 'nearest' });
    }
  }, [submitError]);

  useEffect(() => {
    if (isOpen) {
      setSubmitError('');
      void loadReferences();
    }
    return () => { requestId.current++; };
  }, [isOpen, documentId]);

  const loadReferences = async () => {
    const currentRequest = ++requestId.current;
    setLoading(true);
    setError(null);
    setData(null);
    try {
      const res = await getCheckoutReferences(documentId);
      if (!res || !['whereUsed', 'references', 'drawings'].every(key => Array.isArray(res[key]))) {
        throw new Error('關聯預覽格式不正確，請確認服務版本後重新檢查。');
      }
      if (requestId.current === currentRequest) setData(res);
    } catch (err: any) {
      if (requestId.current !== currentRequest) return;
      const errorData = err.response?.data;
      setError(typeof errorData === 'string' ? errorData : (errorData?.title || errorData?.detail || '無法載入關聯資料'));
    } finally {
      if (requestId.current === currentRequest) setLoading(false);
    }
  };

  const handleConfirm = async () => {
    if (hasBlockingLocks || loading || error || !data || !userName.trim() || submitting) {
      return;
    }

    setSubmitting(true);
    setSubmitError('');
    try {
      await checkOutDocument(documentId, userName.trim(), true);
      onSuccess(userName.trim());
      onClose();
    } catch (err: any) {
      const errorData = err.response?.data;
      const errorMsg = typeof errorData === 'string' ? errorData : (errorData?.detail || errorData?.title || '出庫失敗');
      
      setSubmitError(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const renderCheckoutStatus = (file: any) => {
    if (!file.checkedOutBy) {
      return (
        <span className="inline-flex items-center gap-1 text-[10px] bg-green-500/10 text-green-400 px-1.5 py-0.5 rounded border border-green-500/20">
          <Unlock size={10} /> 可出庫
        </span>
      );
    }

    const isOwnedByCurrentUser = file.checkedOutBy.trim().toLowerCase() === normalizedUserName;

    return (
      <span className={`inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded border ${
        isOwnedByCurrentUser
          ? 'bg-blue-500/10 text-blue-300 border-blue-500/20'
          : 'bg-red-500/10 text-red-400 border-red-500/30'
      }`}>
        <Lock size={10} />
        {isOwnedByCurrentUser ? '已由您出庫' : `已由 ${file.checkedOutBy} 出庫`}
      </span>
    );
  };

  return (
    <Modal isOpen={isOpen} onClose={() => !submitting && onClose()} title="圖檔出庫 (Check-out)">
      <div className="space-y-4">
        <div className="p-4 bg-blue-900/20 border border-blue-900/30 rounded-lg">
          <p className="text-sm text-blue-200">
            出庫後會鎖定 <span className="break-all font-bold text-white">{fileName}</span> 與下列必要關聯檔，其他人暫時無法修改；這裡不能個別取消關聯檔。
          </p>
          {!loading && !error && data && <p className="mt-2 text-sm font-semibold text-white" role="status">
            本次範圍：本圖 1 份＋關聯檔 {relatedFiles.length} 份，共 {relatedFiles.length + 1} 份。
            {relatedFiles.some(file => file.checkedOutBy?.trim().toLowerCase() === normalizedUserName) && ' 您已持有的鎖會沿用，不重複出庫。'}
          </p>}
        </div>

        <div>
          <label htmlFor="checkout-user" className="block text-sm font-medium text-gray-300 mb-1">出庫人員（必填）</label>
          <input 
            id="checkout-user"
            type="text" 
            maxLength={100}
            disabled={submitting}
            value={userName} 
            onChange={(e) => setUserName(e.target.value)}
            className="w-full bg-gray-800 border border-gray-700 rounded-md px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          {!userName.trim() && <p className="mt-1 text-xs text-amber-300">請填寫出庫人員後再確認。</p>}
        </div>

        {loading ? (
          <div role="status" className="flex items-center justify-center gap-3 py-8 text-sm text-gray-300">
            <Loader2 className="animate-spin text-blue-500" size={32} />
            正在確認出庫範圍，請稍候…
          </div>
        ) : error ? (
          <div role="alert" className="p-3 bg-red-900/20 border border-red-900/30 rounded text-red-200 text-sm">
            <p>出庫範圍尚未確認，請重新檢查關聯；確認完成前不能出庫。</p>
            <details className="mt-2"><summary className="cursor-pointer">查看原因</summary><p className="mt-1 break-all">{error}</p></details>
            <button onClick={loadReferences} className="mt-3 rounded border border-red-500 px-3 py-2">重新檢查關聯</button>
          </div>
        ) : (
          <div className="space-y-3">
            <h4 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
              <Layout size={16} className="text-yellow-500" />
              必要關聯檔（全部納入本次範圍）
            </h4>
            
            <div className="max-h-60 overflow-y-auto border border-gray-800 rounded-md bg-gray-900/50">
              {relatedFiles.length === 0 ? (
                <div className="p-4 text-center text-gray-500 text-sm italic">
                  沒有必要關聯檔；本次只會出庫本圖。
                </div>
              ) : (
                <ul className="divide-y divide-gray-800">
                  {relatedFiles.map((file: any) => (
                    <li key={file.documentId ?? file.versionId} className="flex items-start gap-3 p-3">
                      {file.documentType === 'Drawing' ? <FileText size={18} className="mt-1 shrink-0 text-purple-400" /> : <Package size={18} className="mt-1 shrink-0 text-blue-400" />}
                      <div className="min-w-0 flex-1">
                        <p className="break-all text-sm font-medium text-gray-200">{file.originalFileName}</p>
                        <p className="mt-1 text-xs text-gray-400">{file.documentType} · 必要關聯</p>
                        <div className="mt-2">{renderCheckoutStatus(file)}</div>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {hasBlockingLocks && (
              <div className="flex items-start gap-2 p-3 bg-red-900/20 border border-red-900/30 rounded text-red-300 text-xs">
                <AlertCircle size={14} className="mt-0.5 shrink-0" />
                <p>
                  有 {blockingLocks.length} 個關聯檔案已被其他人出庫，無法取得完整變更鏈。請先協調釋放鎖定後再出庫。
                </p>
              </div>
            )}
          </div>
        )}

        {submitError && <div ref={submitErrorPanel} tabIndex={-1} role="alert" className="rounded border border-red-700 bg-red-950/30 p-3 text-sm text-red-200">
          <p className="font-semibold">出庫未完成。請重新檢查關聯後再試，或取消並重新整理清單。</p>
          <details className="mt-2"><summary className="cursor-pointer">查看失敗原因</summary><p className="mt-1 break-all">{submitError}</p></details>
          <button disabled={loading || submitting} onClick={() => { setSubmitError(''); void loadReferences(); }} className="mt-3 rounded border border-red-500 px-3 py-2">重新檢查關聯</button>
        </div>}
        <div className="flex flex-wrap justify-end gap-3 pt-4 border-t border-gray-800">
          <button 
            onClick={onClose}
            disabled={submitting}
            className="px-4 py-2 text-sm font-medium text-gray-400 hover:text-white transition-colors"
          >
            取消
          </button>
          <button 
            onClick={handleConfirm}
            disabled={submitting || loading || Boolean(error) || !data || hasBlockingLocks || !userName.trim() || Boolean(submitError)}
            className="bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:opacity-50 text-white px-6 py-2 rounded-md text-sm font-medium transition-all flex items-center gap-2"
          >
            {submitting ? <Loader2 size={16} className="animate-spin" /> : <CheckCircle2 size={16} />}
            {submitting ? '正在出庫…' : '確認出庫'}
          </button>
        </div>
      </div>
    </Modal>
  );
};
