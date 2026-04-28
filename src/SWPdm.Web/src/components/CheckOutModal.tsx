import React, { useEffect, useState } from 'react';
import { Modal } from './ui';
import { getCheckoutReferences, checkOutDocument } from '../lib/api';
import { AlertCircle, CheckCircle2, FileText, Package, Layout, Loader2 } from 'lucide-react';

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
      setError(err.response?.data?.title || '無法載入關聯資料');
    } finally {
      setLoading(false);
    }
  };

  const handleConfirm = async () => {
    setSubmitting(true);
    try {
      const res = await checkOutDocument(documentId, userName);
      onSuccess(userName);
      onClose();
    } catch (err: any) {
      alert(err.response?.data || '出庫失敗');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="圖檔出庫 (Check-out)">
      <div className="space-y-4">
        <div className="p-4 bg-blue-900/20 border border-blue-900/30 rounded-lg">
          <p className="text-sm text-blue-200">
            您即將出庫 <span className="font-bold text-white">{fileName}</span>。
            出庫後，其他使用者將無法修改此圖檔，直到您入庫為止。
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
                    <li key={file.versionId} className="p-3 flex items-center gap-3 hover:bg-gray-800/50 transition-colors">
                      <div className="text-blue-400">
                        {file.documentType === 'Drawing' ? <FileText size={18} /> : <Package size={18} />}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-gray-200 truncate font-medium">{file.originalFileName}</p>
                        <p className="text-[11px] text-gray-500">{file.documentType} • 使用於上層</p>
                      </div>
                      <span className="text-[10px] bg-blue-900/30 text-blue-400 px-1.5 py-0.5 rounded border border-blue-900/50">引用此檔</span>
                    </li>
                  ))}
                  
                  {/* References (Children) */}
                  {data?.references?.map((file: any) => (
                    <li key={file.versionId} className="p-3 flex items-center gap-3 hover:bg-gray-800/50 transition-colors">
                      <div className="text-yellow-500">
                        <Package size={18} />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-gray-200 truncate font-medium">{file.originalFileName}</p>
                        <p className="text-[11px] text-gray-500">{file.documentType} • 子零件 (階層: {file.depth})</p>
                      </div>
                      <span className="text-[10px] bg-yellow-900/20 text-yellow-500 px-1.5 py-0.5 rounded border border-yellow-900/30">內部參考</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
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
            disabled={submitting}
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
