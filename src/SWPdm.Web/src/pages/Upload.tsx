import React, { useState } from 'react';
import { UploadCloud, CheckCircle, AlertCircle, RefreshCw, AlertTriangle } from 'lucide-react';
import { Card, Button } from '../components/ui';
import { uploadTempFile, ingestCad } from '../lib/api';

export default function UploadPage() {
  // Upload States
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'processing' | 'success' | 'error'>('idle');
  const [result, setResult] = useState<any>(null);
  const [errorMessage, setErrorMessage] = useState('');

  const handleFileDrop = (e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      setFile(e.dataTransfer.files[0]);
    }
  };

  const handleSubmit = async () => {
    if (!file) return;
    try {
      setStatus('uploading');
      setErrorMessage('');

      const uploadRes = await uploadTempFile(file);
      const serverLocalPath = uploadRes.localFilePath;

      setStatus('processing');
      const isAssembly = file.name.toLowerCase().endsWith('.sldasm');

      const ingestRes = await ingestCad(serverLocalPath, isAssembly);

      setResult(ingestRes);
      setStatus('success');
    } catch (err: any) {
      console.error(err);
      setStatus('error');
      setErrorMessage(err.response?.data?.detail || err.message || '發生未知錯誤');
    }
  };

  const resetForm = () => {
    setFile(null);
    setStatus('idle');
    setResult(null);
    setErrorMessage('');
  };

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight text-[#171717]">入庫 CAD 檔案</h1>
        <p className="mt-2 text-[#404040]">將您的零件 (.SLDPRT) 或組合件 (.SLDASM) 拖曳上傳入庫。</p>
      </div>

      {/* 重要提示區塊 */}
      <div className="flex items-start gap-3 p-4 bg-amber-50 border border-amber-300 rounded-lg">
        <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
        <div className="text-sm text-amber-800">
          <p className="font-semibold mb-1">上傳前請確認</p>
          <p>
            系統將直接讀取 CAD 檔案內的 <code className="bg-amber-100 px-1 rounded font-mono">PartNumber</code> 屬性作為料號。
            請確保上傳前已在 SolidWorks 的自訂屬性中填寫完成；若屬性為空，系統將阻擋入庫。
          </p>
        </div>
      </div>

      <Card className="p-8">
        {status === 'idle' || status === 'error' ? (
          <>
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={handleFileDrop}
              className={`border-2 border-dashed rounded-xl p-12 text-center transition-colors cursor-pointer hover:bg-gray-50 ${
                file ? 'border-accent bg-accent/5' : 'border-gray-300'
              }`}
            >
              <input
                type="file"
                className="hidden"
                id="file-upload"
                accept=".SLDPRT,.sldprt,.SLDASM,.sldasm"
                onChange={(e) => e.target.files && setFile(e.target.files[0])}
              />
              <label htmlFor="file-upload" className="flex flex-col items-center cursor-pointer">
                <UploadCloud className="w-12 h-12 text-gray-400 mb-4" />
                {file ? (
                  <div className="text-lg font-medium text-primary">已選擇：{file.name}</div>
                ) : (
                  <>
                    <div className="text-lg font-medium text-primary">點擊或拖曳檔案至此</div>
                    <p className="mt-1 text-sm text-gray-500">僅支援 .SLDPRT 與 .SLDASM</p>
                  </>
                )}
              </label>
            </div>

            {status === 'error' && (
              <div className="mt-6 p-4 bg-red-50 text-red-700 rounded-lg flex items-start space-x-3">
                <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
                <div>
                  <h3 className="font-medium">入庫失敗</h3>
                  <p className="text-sm mt-1">{errorMessage}</p>
                </div>
              </div>
            )}

            {file && (
              <div className="mt-6 border-t border-gray-100 pt-6 flex justify-end">
                <Button onClick={handleSubmit} className="w-full sm:w-auto">
                  開始上傳入庫
                </Button>
              </div>
            )}
          </>
        ) : status === 'success' ? (
          <div className="text-center py-12">
            <CheckCircle className="w-16 h-16 text-green-500 mx-auto mb-4" />
            <h2 className="text-2xl font-semibold mb-2">檔案入庫成功！</h2>
            <p className="text-gray-600 mb-6">
              料號：<span className="font-bold">{result?.files?.[0]?.partNumber || result?.Files?.[0]?.PartNumber || 'N/A'}</span>
              <br />
              檔案版本已更新並儲存。
            </p>
            <div className="mt-8">
              <Button onClick={resetForm}>繼續操作</Button>
            </div>
          </div>
        ) : (
          <div className="text-center py-16 flex flex-col items-center">
            <RefreshCw className="w-12 h-12 text-accent animate-spin mb-4" />
            <h2 className="text-xl font-medium">
              {status === 'uploading' ? '正在上傳檔案...' : '正在讀取 PartNumber 並入庫處理...'}
            </h2>
            <p className="text-gray-500 mt-2">請稍候，系統正在解析 CAD 屬性並寫入資料庫。</p>
          </div>
        )}
      </Card>
    </div>
  );
}
