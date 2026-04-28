import React, { useState } from 'react';
import { Modal, Button } from './ui';
import { uploadTempFile, ingestCad } from '../lib/api';
import { UploadCloud, CheckCircle, AlertCircle, RefreshCw, FileWarning, Loader2 } from 'lucide-react';

interface CheckInModalProps {
  isOpen: boolean;
  onClose: () => void;
  documentId: number;
  fileName: string;
  onSuccess: () => void;
}

export const CheckInModal: React.FC<CheckInModalProps> = ({ 
  isOpen, 
  onClose, 
  documentId, 
  fileName,
  onSuccess
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'processing' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [userName, setUserName] = useState('User');

  const handleFileSelected = (selectedFile: File | null) => {
    setFile(selectedFile);
    setStatus('idle');
    setErrorMessage('');
  };

  const handleFileDrop = (e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileSelected(e.dataTransfer.files[0]);
    }
  };

  const handleSubmit = async () => {
    if (!file) return;

    try {
      setStatus('uploading');
      setErrorMessage('');

      // 1. 上傳暫存檔
      const uploadRes = await uploadTempFile(file);
      const serverLocalPath = uploadRes.localFilePath;

      // 2. 執行入庫 (Check-in)
      setStatus('processing');
      const isAssembly = file.name.toLowerCase().endsWith('.sldasm');
      await ingestCad(serverLocalPath, isAssembly, userName);

      setStatus('success');
      setTimeout(() => {
        onSuccess();
        handleClose();
      }, 1500);
    } catch (err: any) {
      console.error(err);
      setStatus('error');
      setErrorMessage(err.response?.data?.detail || err.message || '入庫時發生錯誤');
    }
  };

  const handleClose = () => {
    setFile(null);
    setStatus('idle');
    setErrorMessage('');
    onClose();
  };

  // 檢查上傳的檔名是否與目標相符 (不強制，僅提示)
  const isNameMismatch = file && !file.name.toLowerCase().includes(fileName.toLowerCase()) && !fileName.toLowerCase().includes(file.name.toLowerCase().replace(/\.[^/.]+$/, ""));

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="圖檔入庫 (Check-in)">
      <div className="space-y-5">
        <div className="p-3 bg-green-900/10 border border-green-900/20 rounded-lg">
          <p className="text-xs text-green-200">
            針對 <span className="font-bold text-white">{fileName}</span> 進行入庫。
            入庫後系統將自動建立新版本並解鎖圖檔。
          </p>
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-400 mb-1.5">入庫人員</label>
          <input 
            type="text" 
            value={userName} 
            onChange={(e) => setUserName(e.target.value)}
            className="w-full bg-gray-800 border border-gray-700 rounded-md px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {status === 'success' ? (
          <div className="py-6 text-center animate-in zoom-in-95 duration-300">
            <CheckCircle className="mx-auto mb-3 h-12 w-12 text-green-500" />
            <h3 className="text-lg font-semibold text-white">入庫成功</h3>
            <p className="text-sm text-gray-400">正在更新列表...</p>
          </div>
        ) : (
          <div className="space-y-4">
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={handleFileDrop}
              onClick={() => document.getElementById('checkin-file-upload')?.click()}
              className={`cursor-pointer rounded-lg border-2 border-dashed p-8 text-center transition-all hover:bg-gray-800/30 ${
                file ? 'border-blue-500 bg-blue-500/5' : 'border-gray-700 hover:border-gray-500'
              }`}
            >
              <input
                type="file"
                className="hidden"
                id="checkin-file-upload"
                accept=".SLDPRT,.sldprt,.SLDASM,.sldasm,.SLDDRW,.slddrw"
                onChange={(e) => handleFileSelected(e.target.files ? e.target.files[0] : null)}
              />
              <UploadCloud className={`mx-auto mb-3 h-10 w-10 ${file ? 'text-blue-500' : 'text-gray-600'}`} />
              {file ? (
                <div className="text-sm font-medium text-blue-400 break-all">{file.name}</div>
              ) : (
                <>
                  <div className="text-sm font-medium text-gray-300">點擊或拖曳檔案至此</div>
                  <p className="mt-1 text-xs text-gray-500">支援 .SLDPRT, .SLDASM, .SLDDRW</p>
                </>
              )}
            </div>

            {isNameMismatch && (
              <div className="flex items-start gap-2 p-3 bg-yellow-900/20 border border-yellow-900/30 rounded text-yellow-500 text-xs">
                <FileWarning size={14} className="mt-0.5 flex-shrink-0" />
                <p>上傳的檔名與原圖檔似乎不符，請確認是否上傳了正確的檔案。</p>
              </div>
            )}

            {status === 'error' && (
              <div className="flex items-start gap-2 p-3 bg-red-900/20 border border-red-900/30 rounded text-red-400 text-xs">
                <AlertCircle size={14} className="mt-0.5 flex-shrink-0" />
                <p>{errorMessage}</p>
              </div>
            )}

            <div className="flex justify-end gap-3 pt-2 border-t border-gray-800">
              <Button 
                variant="secondary" 
                onClick={handleClose}
                disabled={status === 'uploading' || status === 'processing'}
              >
                取消
              </Button>
              <Button 
                onClick={handleSubmit}
                disabled={!file || status === 'uploading' || status === 'processing'}
                className="min-w-[100px]"
              >
                {status === 'uploading' || status === 'processing' ? (
                  <>
                    <Loader2 size={16} className="animate-spin mr-2" />
                    處理中
                  </>
                ) : '確認入庫'}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
};
