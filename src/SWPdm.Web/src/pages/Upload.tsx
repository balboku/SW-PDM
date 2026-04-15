import React, { useState } from 'react';
import { UploadCloud, CheckCircle, AlertCircle, FileBox, RefreshCw, Hash } from 'lucide-react';
import { Card, Button } from '../components/ui';
import { uploadTempFile, ingestCad, allocateNumber } from '../lib/api';

export default function UploadPage() {
  const [mode, setMode] = useState<'new' | 'update'>('new');
  
  // States for Allocation
  const [documentType, setDocumentType] = useState('Part');
  const [allocatedNumber, setAllocatedNumber] = useState('');
  const [allocating, setAllocating] = useState(false);

  // States for Updating
  const [existingNumber, setExistingNumber] = useState('');

  // Universal Upload States
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'processing' | 'success' | 'error'>('idle');
  const [result, setResult] = useState<any>(null);
  const [errorMessage, setErrorMessage] = useState('');

  const handleAllocate = async () => {
    setAllocating(true);
    setErrorMessage('');
    try {
      const res = await allocateNumber(documentType);
      setAllocatedNumber(res.allocatedNumber);
    } catch (err: any) {
      setErrorMessage(err.response?.data?.detail || err.message || '派號失敗');
    } finally {
      setAllocating(false);
    }
  };

  const handleFileDrop = (e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      setFile(e.dataTransfer.files[0]);
    }
  };

  const canUpload = () => {
    if (mode === 'new') return allocatedNumber !== '';
    return existingNumber !== '';
  };

  const handleSubmit = async () => {
    if (!file) return;
    try {
      setStatus('uploading');
      
      const uploadRes = await uploadTempFile(file);
      const serverLocalPath = uploadRes.localFilePath;

      setStatus('processing');
      const isAssembly = file.name.toLowerCase().endsWith('.sldasm');
      
      const ingestRes = await ingestCad(
        serverLocalPath, 
        isAssembly,
        mode === 'new' ? allocatedNumber : undefined,
        mode === 'update' ? existingNumber : undefined
      );
      
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
    setAllocatedNumber('');
    setExistingNumber('');
  };

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight text-[#171717]">入庫 CAD 檔案</h1>
        <p className="mt-2 text-[#404040]">將您的零件 (.SLDPRT) 或組合件 (.SLDASM) 拖曳上傳，並防呆圖號派發與升版。</p>
      </div>

      <div className="flex border-b border-gray-200">
        <button
          className={`flex-1 py-3 font-medium text-sm text-center ${mode === 'new' ? 'border-b-2 border-accent text-accent' : 'text-gray-500 hover:text-gray-700'}`}
          onClick={() => { setMode('new'); setStatus('idle'); }}
        >
          建立新圖檔 (領取圖號)
        </button>
        <button
          className={`flex-1 py-3 font-medium text-sm text-center ${mode === 'update' ? 'border-b-2 border-accent text-accent' : 'text-gray-500 hover:text-gray-700'}`}
          onClick={() => { setMode('update'); setStatus('idle'); }}
        >
          升級現有圖檔 (更新)
        </button>
      </div>

      <Card className="p-8">
        {(status === 'idle' || status === 'error') && (
          <div className="mb-8 space-y-6">
            {mode === 'new' ? (
              <div className="space-y-4 bg-gray-50 p-6 rounded-lg border border-gray-200">
                <div className="flex items-center space-x-4">
                  <div className="flex-1">
                    <label className="block text-sm font-medium text-gray-700 mb-1">選擇檔案屬性</label>
                    <select 
                      value={documentType} 
                      onChange={(e) => setDocumentType(e.target.value)}
                      disabled={allocatedNumber !== ''}
                      className="w-full p-2 border border-gray-300 rounded-md focus:ring-accent"
                    >
                      <option value="Part">Part (零件)</option>
                      <option value="Assembly">Assembly (組合件)</option>
                    </select>
                  </div>
                  <div className="flex-1 flex items-end">
                    <Button onClick={handleAllocate} disabled={allocatedNumber !== '' || allocating} className="w-full">
                      {allocating ? '派號中...' : allocatedNumber ? '已領取' : '向系統領取圖號'}
                    </Button>
                  </div>
                </div>
                {allocatedNumber && (
                  <div className="p-4 bg-green-50 border border-green-200 rounded-md flex items-center">
                    <Hash className="text-green-600 mr-3" />
                    <div>
                      <div className="text-sm text-green-800 font-medium">系統已分配圖號：</div>
                      <div className="text-xl font-bold text-green-900">{allocatedNumber}</div>
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <div className="space-y-4 bg-gray-50 p-6 rounded-lg border border-gray-200">
                <label className="block text-sm font-medium text-gray-700 mb-1">請輸入要升級的舊圖號</label>
                <input 
                  type="text" 
                  value={existingNumber}
                  onChange={(e) => setExistingNumber(e.target.value)}
                  placeholder="例如：PRT-2604-0001"
                  className="w-full p-2 border border-gray-300 rounded-md focus:ring-accent focus:border-accent"
                />
                <p className="text-xs text-gray-500">系統會在入庫時查驗此圖號是否存在。</p>
              </div>
            )}
          </div>
        )}

        {status === 'idle' || status === 'error' ? (
          <div 
            onDragOver={(e) => e.preventDefault()}
            onDrop={handleFileDrop}
            className={`border-2 border-dashed rounded-xl p-12 text-center transition-colors ${!canUpload() ? 'opacity-50 cursor-not-allowed bg-gray-50' : 'cursor-pointer hover:bg-gray-50 focus:outline-none'} ${file ? 'border-accent bg-accent/5' : 'border-gray-300'}`}
          >
            <input 
              type="file" 
              className="hidden" 
              id="file-upload" 
              accept=".SLDPRT,.sldprt,.SLDASM,.sldasm"
              disabled={!canUpload()}
              onChange={(e) => e.target.files && setFile(e.target.files[0])}
            />
            <label htmlFor="file-upload" className={`flex flex-col items-center ${canUpload() ? 'cursor-pointer' : 'cursor-not-allowed'}`}>
              <UploadCloud className="w-12 h-12 text-gray-400 mb-4" />
              {file ? (
                <div className="text-lg font-medium text-primary">已選擇：{file.name}</div>
              ) : (
                <>
                  <div className="text-lg font-medium text-primary">點擊或拖曳檔案至此</div>
                  <p className="mt-1 text-sm text-gray-500">
                    {!canUpload() ? '請先填寫圖號資訊後再上傳！' : '僅支援 .SLDPRT 與 .SLDASM'}
                  </p>
                </>
              )}
            </label>
          </div>
        ) : status === 'success' ? (
          <div className="text-center py-12">
            <CheckCircle className="w-16 h-16 text-green-500 mx-auto mb-4" />
            <h2 className="text-2xl font-semibold mb-2">檔案入庫成功！</h2>
            <p className="text-gray-600 mb-6">圖號：<span className="font-bold">{result?.Files?.[0]?.PartNumber || 'N/A'}</span> <br/>
            檔案版本已更新並儲存。</p>
            <div className="mt-8">
              <Button onClick={resetForm}>
                繼續操作
              </Button>
            </div>
          </div>
        ) : (
          <div className="text-center py-16 flex flex-col items-center">
            <RefreshCw className="w-12 h-12 text-accent animate-spin mb-4" />
            <h2 className="text-xl font-medium">
              {status === 'uploading' ? '正在上傳檔案...' : '正在入庫處理與屬性檢查...'}
            </h2>
            <p className="text-gray-500 mt-2">請稍候，防呆機制可能需要數秒時間執行。</p>
          </div>
        )}

        {status === 'error' && (
          <div className="mt-6 p-4 bg-red-50 text-red-700 rounded-lg flex items-start space-x-3">
            <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-medium">系統錯誤/防呆失敗</h3>
              <p className="text-sm mt-1">{errorMessage}</p>
            </div>
          </div>
        )}

        {(status === 'idle' || status === 'error') && file && canUpload() && (
          <div className="mt-6 border-t border-gray-100 pt-6 flex justify-end">
            <Button onClick={handleSubmit} className="w-full sm:w-auto">
              開始上傳入庫
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}
