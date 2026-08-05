import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Files,
  FolderOpen,
  Loader2,
  RotateCcw,
  UploadCloud,
  XCircle
} from 'lucide-react';
import { ingestCadBatch } from '../lib/api';
import { Button, Card } from './ui';

const SUPPORTED_EXTENSIONS = ['.sldprt', '.sldasm', '.slddrw'];
const MAX_FILE_COUNT = 200;
const MAX_TOTAL_BYTES = 1024 * 1024 * 1024;

interface BatchIngestFileResult {
  relativePath: string;
  succeeded: boolean;
  documentId?: number | null;
  versionId?: number | null;
  documentType?: string | null;
  partNumber?: string | null;
  versionNo?: number | null;
  issues?: string[];
  errorMessage?: string | null;
}

interface BatchIngestResult {
  totalFileCount: number;
  succeededFileCount: number;
  failedFileCount: number;
  files: BatchIngestFileResult[];
}

interface BatchUploadPanelProps {
  defaultUserName?: string;
}

const getExtension = (fileName: string) => {
  const lastDot = fileName.lastIndexOf('.');
  return lastDot >= 0 ? fileName.slice(lastDot).toLowerCase() : '';
};

const getDisplayPath = (file: File) => file.webkitRelativePath || file.name;

export const BatchUploadPanel: React.FC<BatchUploadPanelProps> = ({
  defaultUserName = 'User'
}) => {
  const folderInputRef = useRef<HTMLInputElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [ignoredFileCount, setIgnoredFileCount] = useState(0);
  const [userName, setUserName] = useState(defaultUserName);
  const [changeReason, setChangeReason] = useState('');
  const [status, setStatus] = useState<'idle' | 'processing' | 'complete' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [result, setResult] = useState<BatchIngestResult | null>(null);

  useEffect(() => {
    if (folderInputRef.current) {
      folderInputRef.current.setAttribute('webkitdirectory', '');
      folderInputRef.current.setAttribute('directory', '');
    }
  }, []);

  const summary = useMemo(() => {
    const counts = { part: 0, assembly: 0, drawing: 0 };
    let totalBytes = 0;

    files.forEach((file) => {
      totalBytes += file.size;
      const extension = getExtension(file.name);
      if (extension === '.sldprt') counts.part += 1;
      if (extension === '.sldasm') counts.assembly += 1;
      if (extension === '.slddrw') counts.drawing += 1;
    });

    return { ...counts, totalBytes };
  }, [files]);

  const validationMessage = useMemo(() => {
    if (files.length > MAX_FILE_COUNT) {
      return `單次最多可匯入 ${MAX_FILE_COUNT} 個 CAD 檔案。`;
    }

    if (summary.totalBytes > MAX_TOTAL_BYTES) {
      return '單次批次檔案總量不可超過 1 GiB。';
    }

    if (!userName.trim()) {
      return '請填寫上傳／入庫人員。';
    }

    return '';
  }, [files.length, summary.totalBytes, userName]);

  const handleSelectedFiles = (selectedFiles: File[]) => {
    const cadFiles = selectedFiles.filter((file) =>
      SUPPORTED_EXTENSIONS.includes(getExtension(file.name))
    );
    const uniqueFiles = Array.from(
      new Map(
        cadFiles.map((file) => [
          `${getDisplayPath(file).toLowerCase()}-${file.size}-${file.lastModified}`,
          file
        ])
      ).values()
    );

    setFiles(uniqueFiles);
    setIgnoredFileCount(selectedFiles.length - cadFiles.length);
    setStatus('idle');
    setErrorMessage('');
    setResult(null);
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    handleSelectedFiles(Array.from(event.dataTransfer.files));
  };

  const reset = () => {
    setFiles([]);
    setIgnoredFileCount(0);
    setStatus('idle');
    setErrorMessage('');
    setResult(null);
    setChangeReason('');
    if (folderInputRef.current) folderInputRef.current.value = '';
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleSubmit = async () => {
    if (files.length === 0 || validationMessage) return;

    setStatus('processing');
    setErrorMessage('');
    setResult(null);

    try {
      const response = await ingestCadBatch(files, userName.trim(), changeReason);
      setResult(response);
      setStatus('complete');
    } catch (error: any) {
      console.error('Batch ingest failed', error);
      setStatus('error');
      setErrorMessage(
        error.response?.data?.detail ||
        error.response?.data?.errors?.files?.[0] ||
        error.message ||
        '批次匯入失敗'
      );
    }
  };

  return (
    <Card className="overflow-hidden">
      <div className="border-b border-gray-200 px-6 py-5 sm:px-8">
        <div className="flex items-start gap-3">
          <FolderOpen className="mt-0.5 h-6 w-6 shrink-0 text-[#D4AF37]" />
          <div>
            <h2 className="text-xl font-semibold text-[#171717]">資料夾／多檔批次匯入</h2>
            <p className="mt-1 text-sm leading-6 text-gray-600">
              選取同一設計組的零件、組合件與工程圖。系統會保留相對路徑，
              逐檔驗證並在完成後列出成功與失敗。
            </p>
          </div>
        </div>
      </div>

      <div className="space-y-6 p-6 sm:p-8">
        {status === 'processing' ? (
          <div className="flex flex-col items-center py-12 text-center">
            <Loader2 className="mb-4 h-12 w-12 animate-spin text-[#D4AF37]" />
            <h3 className="text-lg font-medium text-[#171717]">
              正在逐檔解析與建立關聯...
            </h3>
            <p className="mt-2 max-w-lg text-sm leading-6 text-gray-500">
              已送出 {files.length} 個 CAD。單一檔案失敗不會取消其他成功檔案，
              請等待結果摘要完成。
            </p>
          </div>
        ) : result ? (
          <div className="space-y-5">
            <div className={`rounded-lg border p-4 ${
              result.failedFileCount === 0
                ? 'border-green-200 bg-green-50 text-green-800'
                : 'border-amber-300 bg-amber-50 text-amber-900'
            }`}>
              <div className="flex items-start gap-3">
                {result.failedFileCount === 0 ? (
                  <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
                ) : (
                  <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
                )}
                <div>
                  <p className="font-medium">
                    {result.failedFileCount === 0
                      ? '批次匯入完成，不需再處理。'
                      : '批次已完成；請修正失敗檔案後再單獨重試。'}
                  </p>
                  <p className="mt-1 text-sm">
                    成功 {result.succeededFileCount}、失敗 {result.failedFileCount}，
                    共 {result.totalFileCount} 個 CAD。
                  </p>
                </div>
              </div>
            </div>

            <div className="max-h-96 overflow-y-auto rounded-lg border border-gray-200">
              {result.files.map((item) => (
                <div
                  key={item.relativePath}
                  className="flex items-start gap-3 border-b border-gray-100 px-4 py-3 last:border-b-0"
                >
                  {item.succeeded ? (
                    <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-green-600" />
                  ) : (
                    <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />
                  )}
                  <div className="min-w-0 flex-1">
                    <p className="break-all text-sm font-medium text-gray-800">
                      {item.relativePath}
                    </p>
                    {item.succeeded ? (
                      <p className="mt-1 text-xs text-gray-500">
                        {item.documentType} · 品號 {item.partNumber || '-'} · Ver. {item.versionNo}
                      </p>
                    ) : (
                      <p className="mt-1 text-xs leading-5 text-red-600">
                        {item.errorMessage || '未提供失敗原因'}
                      </p>
                    )}
                    {item.issues && item.issues.length > 0 && (
                      <p className="mt-1 text-xs leading-5 text-amber-700">
                        {item.issues.join('；')}
                      </p>
                    )}
                  </div>
                </div>
              ))}
            </div>

            <div className="flex justify-end">
              <Button onClick={reset} variant="secondary">
                <RotateCcw className="mr-2 h-4 w-4" />
                匯入另一批檔案
              </Button>
            </div>
          </div>
        ) : (
          <>
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  上傳／入庫人員
                </label>
                <input
                  type="text"
                  value={userName}
                  onChange={(event) => setUserName(event.target.value)}
                  className="w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[#D4AF37]"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  共用變更原因（選填）
                </label>
                <input
                  type="text"
                  value={changeReason}
                  onChange={(event) => setChangeReason(event.target.value)}
                  placeholder="例如：試作設計調整"
                  className="w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[#D4AF37]"
                />
              </div>
            </div>

            <input
              ref={folderInputRef}
              type="file"
              multiple
              accept=".SLDPRT,.sldprt,.SLDASM,.sldasm,.SLDDRW,.slddrw"
              className="hidden"
              onChange={(event) =>
                handleSelectedFiles(Array.from(event.target.files || []))
              }
            />
            <input
              ref={fileInputRef}
              type="file"
              multiple
              accept=".SLDPRT,.sldprt,.SLDASM,.sldasm,.SLDDRW,.slddrw"
              className="hidden"
              onChange={(event) =>
                handleSelectedFiles(Array.from(event.target.files || []))
              }
            />

            <div
              onDragOver={(event) => event.preventDefault()}
              onDrop={handleDrop}
              className={`rounded-xl border-2 border-dashed p-8 text-center transition-colors ${
                files.length > 0
                  ? 'border-[#D4AF37] bg-amber-50/50'
                  : 'border-gray-300 bg-gray-50'
              }`}
            >
              <UploadCloud className="mx-auto mb-3 h-10 w-10 text-gray-400" />
              <p className="text-sm font-medium text-gray-800">
                拖曳多個 CAD 到這裡，或使用下方選擇器
              </p>
              <p className="mt-1 text-xs text-gray-500">
                僅處理 SLDPRT、SLDASM、SLDDRW；上限 200 檔／1 GiB
              </p>
              <div className="mt-4 flex flex-col justify-center gap-2 sm:flex-row">
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => folderInputRef.current?.click()}
                  className="w-full sm:w-auto"
                >
                  <FolderOpen className="mr-2 h-4 w-4" />
                  選擇資料夾
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => fileInputRef.current?.click()}
                  className="w-full sm:w-auto"
                >
                  <Files className="mr-2 h-4 w-4" />
                  選擇多個檔案
                </Button>
              </div>
            </div>

            {ignoredFileCount > 0 && (
              <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                已忽略 {ignoredFileCount} 個非 CAD 檔案；它們不會上傳。
              </div>
            )}

            {files.length > 0 && (
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  {[
                    ['CAD 總數', files.length],
                    ['零件', summary.part],
                    ['組合件', summary.assembly],
                    ['工程圖', summary.drawing]
                  ].map(([label, value]) => (
                    <div key={label} className="rounded-lg border border-gray-200 bg-gray-50 p-3">
                      <p className="text-xs text-gray-500">{label}</p>
                      <p className="mt-1 text-xl font-semibold text-[#171717]">{value}</p>
                    </div>
                  ))}
                </div>

                <div className="max-h-48 overflow-y-auto rounded-lg border border-gray-200">
                  {files.map((file) => (
                    <div
                      key={`${getDisplayPath(file)}-${file.size}`}
                      className="flex items-center gap-2 border-b border-gray-100 px-3 py-2 text-xs last:border-b-0"
                    >
                      <Files className="h-3.5 w-3.5 shrink-0 text-gray-400" />
                      <span className="min-w-0 flex-1 break-all text-gray-700">
                        {getDisplayPath(file)}
                      </span>
                    </div>
                  ))}
                </div>

                <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-xs leading-5 text-blue-800">
                  送出後會逐檔建立版本。既有文件仍須由同一人先出庫；
                  失敗檔案不會解除鎖定，也不會取消其他成功檔案。
                </div>

                {validationMessage && (
                  <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700">
                    <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                    {validationMessage}
                  </div>
                )}

                {status === 'error' && (
                  <div className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700" role="alert">
                    <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                    <div>
                      <p className="font-medium">批次未開始，請修正後重試。</p>
                      <p className="mt-1">{errorMessage}</p>
                    </div>
                  </div>
                )}

                <div className="flex flex-col-reverse gap-3 border-t border-gray-100 pt-5 sm:flex-row sm:justify-end">
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={reset}
                    className="w-full sm:w-auto"
                  >
                    清除
                  </Button>
                  <Button
                    type="button"
                    onClick={handleSubmit}
                    disabled={Boolean(validationMessage)}
                    className="w-full sm:w-auto"
                  >
                    匯入 {files.length} 個 CAD
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </Card>
  );
};
