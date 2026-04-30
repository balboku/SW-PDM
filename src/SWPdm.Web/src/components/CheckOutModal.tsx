import React, { useEffect, useState } from 'react';
import { Modal } from './ui';
import { getCheckoutReferences, checkOutDocument } from '../lib/api';
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

  const relatedFiles = [
    ...(data?.whereUsed ?? []),
    ...(data?.references ?? [])
  ];
  const normalizedUserName = userName.trim().toLowerCase();
  const blockingLocks = relatedFiles.filter((file: any) => (
    file.checkedOutBy &&
    file.checkedOutBy.trim().toLowerCase() !== normalizedUserName
  ));
  const hasBlockingLocks = blockingLocks.length > 0;

  useEffect(() => {
    if (isOpen) {
      loadReferences();
    }
  }, [isOpen, documentId]);

  const loadReferences = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getCheckoutReferences(documentId);
      setData(res);
    } catch (err: any) {
      const errorData = err.response?.data;
      setError(typeof errorData === 'string' ? errorData : (errorData?.title || errorData?.detail || '無法載入關聯資料'));
    } finally {
      setLoading(false);
    }
  };

  const handleConfirm = async () => {
    if (hasBlockingLocks) {
      return;
    }

    setSubmitting(true);
    try {
      await checkOutDocument(documentId, userName, true);
      onSuccess(userName);
      onClose();
    } catch (err: any) {
      const errorData = err.response?.data;
      const errorMsg = typeof errorData === 'string' ? errorData : (errorData?.detail || errorData?.title || '出庫失敗');
      
      alert(errorMsg);

      // 如果已經被出庫了，我們還是更新一下前端狀態並關閉視窗，讓使用者看到正確的鎖定狀態
      if (typeof errorMsg === 'string' && errorMsg.includes('already checked out')) {
        onSuccess(userName);
        onClose();
      }
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
    <Modal isOpen={isOpen} onClose={onClose} title="圖檔出庫 (Check-out)">
      <div className="space-y-4">
        <div className="p-4 bg-blue-900/20 border border-blue-900/30 rounded-lg">
          <p className="text-sm text-blue-200">
            您即將出庫 <span className="font-bold text-white">{fileName}</span>。出庫後，系統將自動強制出庫所有關聯的工程圖與子零件，以確保設計變更的一致性。
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-400 mb-1">出庫人員</label>
          <input 
            type="text" 
            value={userName} 
            onChange={(e) => setUserName(e.target.value)}
            className="w-full bg-gray-800 border border-gray-700 rounded-md px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {loading ? (
          <div className="flex justify-center py-8">
            <Loader2 className="animate-spin text-blue-500" size={32} />
          </div>
        ) : error ? (
          <div className="flex items-center gap-2 p-3 bg-red-900/20 border border-red-900/30 rounded text-red-400 text-sm">
            <AlertCircle size={16} />
            {error}
          </div>
        ) : (
          <div className="space-y-3">
            <h4 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
              <Layout size={16} className="text-yellow-500" />
              相關聯的圖面與零件 (建議同步出庫)
            </h4>
            
            <div className="max-h-60 overflow-y-auto border border-gray-800 rounded-md bg-gray-900/50">
              {data?.whereUsed?.length === 0 && data?.references?.length === 0 ? (
                <div className="p-4 text-center text-gray-500 text-sm italic">
                  沒有找到相關聯的檔案
                </div>
              ) : (
                <ul className="divide-y divide-gray-800">
                  {/* Where-Used (Drawings / Parents) */}
                  {data?.whereUsed?.map((file: any) => (
                    <li key={file.versionId} className={`p-3 flex items-center gap-3 transition-colors ${
                      file.checkedOutBy && file.checkedOutBy.trim().toLowerCase() !== normalizedUserName
                        ? 'bg-red-950/20 hover:bg-red-950/30'
                        : 'hover:bg-gray-800/50'
                    }`}>
                      <div className="text-blue-400">
                        {file.documentType === 'Drawing' ? <FileText size={18} /> : <Package size={18} />}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-gray-200 truncate font-medium">{file.originalFileName}</p>
                        <p className="text-[11px] text-gray-500">{file.documentType} • 使用於上層</p>
                      </div>
                      <div className="flex shrink-0 flex-col items-end gap-1">
                        <span className="text-[10px] bg-blue-900/30 text-blue-400 px-1.5 py-0.5 rounded border border-blue-900/50">引用此檔</span>
                        {renderCheckoutStatus(file)}
                      </div>
                    </li>
                  ))}
                  
                  {/* References (Children) */}
                  {data?.references?.map((file: any) => (
                    <li key={file.versionId} className={`p-3 flex items-center gap-3 transition-colors ${
                      file.checkedOutBy && file.checkedOutBy.trim().toLowerCase() !== normalizedUserName
                        ? 'bg-red-950/20 hover:bg-red-950/30'
                        : 'hover:bg-gray-800/50'
                    }`}>
                      <div className="text-yellow-500">
                        <Package size={18} />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-gray-200 truncate font-medium">{file.originalFileName}</p>
                        <p className="text-[11px] text-gray-500">{file.documentType} • 子零件 (階層: {file.depth})</p>
                      </div>
                      <div className="flex shrink-0 flex-col items-end gap-1">
                        <span className="text-[10px] bg-yellow-900/20 text-yellow-500 px-1.5 py-0.5 rounded border border-yellow-900/30">內部參考</span>
                        {renderCheckoutStatus(file)}
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

        <div className="flex justify-end gap-3 pt-4 border-t border-gray-800">
          <button 
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-gray-400 hover:text-white transition-colors"
          >
            取消
          </button>
          <button 
            onClick={handleConfirm}
            disabled={submitting || loading || Boolean(error) || hasBlockingLocks}
            className="bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:opacity-50 text-white px-6 py-2 rounded-md text-sm font-medium transition-all flex items-center gap-2"
          >
            {submitting ? <Loader2 size={16} className="animate-spin" /> : <CheckCircle2 size={16} />}
            確認出庫
          </button>
        </div>
      </div>
    </Modal>
  );
};
