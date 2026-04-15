import React, { useState } from 'react';
import { UploadCloud, CheckCircle, AlertCircle, FileBox, RefreshCw } from 'lucide-react';
import { Card, Button } from '../components/ui';
import { uploadTempFile, ingestCad } from '../lib/api';

export default function UploadPage() {
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
      
      // Step 1: Upload to temp location on server
      const uploadRes = await uploadTempFile(file);
      const serverLocalPath = uploadRes.localFilePath;

      // Step 2: Trigger Ingest CAD process
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

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight text-[#171717]">入庫 CAD 檔案</h1>
        <p className="mt-2 text-[#404040]">將您的 SolidWorks 零件 (.SLDPRT) 或組合件 (.SLDASM) 拖曳至下方區塊進行解析與儲存。</p>
      </div>

      <Card className="p-8">
        {status === 'idle' || status === 'error' ? (
          <div 
            onDragOver={(e) => e.preventDefault()}
            onDrop={handleFileDrop}
            className={`border-2 border-dashed rounded-xl p-12 text-center transition-colors cursor-pointer hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-accent ${file ? 'border-accent bg-accent/5' : 'border-gray-300'}`}
          >
            <input 
              type="file" 
              className="hidden" 
              id="file-upload" 
              accept=".SLDPRT,.sldprt,.SLDASM,.sldasm"
              onChange={(e) => e.target.files && setFile(e.target.files[0])}
            />
            <label htmlFor="file-upload" className="cursor-pointer flex flex-col items-center">
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
        ) : status === 'success' ? (
          <div className="text-center py-12">
            <CheckCircle className="w-16 h-16 text-green-500 mx-auto mb-4" />
            <h2 className="text-2xl font-semibold mb-2">解析入庫成功！</h2>
            <p className="text-gray-600 mb-6">已成功讀取結構並寫入資料庫。</p>
            <div className="inline-flex items-center space-x-4 bg-gray-50 p-4 rounded-lg border border-gray-200">
              <FileBox className="text-gray-500" />
              <div className="text-left">
                <div className="font-medium">根文件：{result?.RootDocumentType}</div>
                <div className="text-sm text-gray-500">處理了 {result?.ProcessedFileCount} 個關聯檔案</div>
              </div>
            </div>
            <div className="mt-8">
              <Button onClick={() => { setFile(null); setStatus('idle'); setResult(null); }}>
                上傳另一個檔案
              </Button>
            </div>
          </div>
        ) : (
          <div className="text-center py-16 flex flex-col items-center">
            <RefreshCw className="w-12 h-12 text-accent animate-spin mb-4" />
            <h2 className="text-xl font-medium">
              {status === 'uploading' ? '正在上傳檔案至伺服器...' : 'SolidWorks 正在解析文件結構...'}
            </h2>
            <p className="text-gray-500 mt-2">大型組合件可能會花費數十秒鐘，請稍候。</p>
          </div>
        )}

        {status === 'error' && (
          <div className="mt-6 p-4 bg-red-50 text-red-700 rounded-lg flex items-start space-x-3">
            <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-medium">寫入失敗</h3>
              <p className="text-sm mt-1">{errorMessage}</p>
            </div>
          </div>
        )}

        {(status === 'idle' || status === 'error') && file && (
          <div className="mt-6 border-t border-gray-100 pt-6 flex justify-end">
            <Button onClick={handleSubmit} className="w-full sm:w-auto">
              開始解析與入庫
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}
