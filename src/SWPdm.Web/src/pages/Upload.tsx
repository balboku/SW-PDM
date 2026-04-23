import React, { useState } from 'react';
import { UploadCloud, CheckCircle, AlertCircle, RefreshCw, AlertTriangle, SearchCheck } from 'lucide-react';
import { Card, Button } from '../components/ui';
import { uploadTempFile, ingestCad, parseSolidWorksFile } from '../lib/api';

type CustomProperty = {
  value?: string;
  propertyType?: string;
  Value?: string;
  PropertyType?: string;
};

type ParseResult = {
  filePath?: string;
  documentType?: string;
  referencedFilePaths?: string[];
  documentProperties?: Record<string, CustomProperty>;
  configurationProperties?: Record<string, Record<string, CustomProperty>>;
  FilePath?: string;
  DocumentType?: string;
  ReferencedFilePaths?: string[];
  DocumentProperties?: Record<string, CustomProperty>;
  ConfigurationProperties?: Record<string, Record<string, CustomProperty>>;
};

type PropertyRow = {
  scope: string;
  configuration: string;
  name: string;
  value: string;
  type: string;
};

const PART_NUMBER_ALIASES = ['PartNumber', 'Number', 'Part No', 'PartNo', '品號'];

export default function UploadPage() {
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'parsing' | 'processing' | 'success' | 'error'>('idle');
  const [result, setResult] = useState<any>(null);
  const [parseResult, setParseResult] = useState<ParseResult | null>(null);
  const [errorMessage, setErrorMessage] = useState('');
  const [cachedLocalPath, setCachedLocalPath] = useState<string | null>(null);

  const documentProperties = parseResult?.documentProperties || parseResult?.DocumentProperties || {};
  const configurationProperties = parseResult?.configurationProperties || parseResult?.ConfigurationProperties || {};
  const referencedFilePaths = parseResult?.referencedFilePaths || parseResult?.ReferencedFilePaths || [];

  const resolvePropertyValue = (property?: CustomProperty) => property?.value || property?.Value || '';
  const resolvePropertyType = (property?: CustomProperty) => property?.propertyType || property?.PropertyType || '';

  const flattenProperties = (): PropertyRow[] => {
    const documentRows = Object.entries(documentProperties).map(([name, property]) => ({
      scope: '文件',
      configuration: '-',
      name,
      value: resolvePropertyValue(property),
      type: resolvePropertyType(property)
    }));

    const configurationRows = Object.entries(configurationProperties).flatMap(([configurationName, properties]) =>
      Object.entries(properties).map(([name, property]) => ({
        scope: '組態',
        configuration: configurationName,
        name,
        value: resolvePropertyValue(property),
        type: resolvePropertyType(property)
      }))
    );

    return [...documentRows, ...configurationRows].sort((a, b) => {
      if (a.scope !== b.scope) {
        return a.scope.localeCompare(b.scope, 'zh-Hant');
      }

      if (a.configuration !== b.configuration) {
        return a.configuration.localeCompare(b.configuration, 'zh-Hant');
      }

      return a.name.localeCompare(b.name, 'zh-Hant');
    });
  };

  const getPartNumberValue = () => {
    for (const alias of PART_NUMBER_ALIASES) {
      const documentValue = resolvePropertyValue(documentProperties[alias]).trim();
      if (documentValue) {
        return documentValue;
      }
    }

    for (const properties of Object.values(configurationProperties)) {
      for (const alias of PART_NUMBER_ALIASES) {
        const configurationValue = resolvePropertyValue(properties[alias]).trim();
        if (configurationValue) {
          return configurationValue;
        }
      }
    }

    return '';
  };

  const handleFileSelected = (selectedFile: File | null) => {
    setFile(selectedFile);
    setStatus('idle');
    setResult(null);
    setParseResult(null);
    setErrorMessage('');
    setCachedLocalPath(null);
  };

  const handleFileDrop = (e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileSelected(e.dataTransfer.files[0]);
    }
  };

  const ensureUploadedFile = async () => {
    if (cachedLocalPath) {
      return cachedLocalPath;
    }

    if (!file) {
      throw new Error('請先選擇 CAD 檔案');
    }

    const uploadRes = await uploadTempFile(file);
    const serverLocalPath = uploadRes.localFilePath;
    setCachedLocalPath(serverLocalPath);
    return serverLocalPath;
  };

  const handleParse = async () => {
    if (!file) return;

    try {
      setStatus('uploading');
      setErrorMessage('');
      setResult(null);

      const serverLocalPath = await ensureUploadedFile();

      setStatus('parsing');
      const response = await parseSolidWorksFile(serverLocalPath);
      setParseResult(response);
      setStatus('idle');
    } catch (err: any) {
      console.error(err);
      setStatus('error');
      setErrorMessage(err.response?.data?.detail || err.message || '解析 CAD 屬性時發生錯誤');
    }
  };

  const handleSubmit = async () => {
    if (!file) return;

    try {
      setStatus('uploading');
      setErrorMessage('');

      const serverLocalPath = await ensureUploadedFile();

      setStatus('processing');
      const isAssembly = file.name.toLowerCase().endsWith('.sldasm');
      const ingestRes = await ingestCad(serverLocalPath, isAssembly);

      setResult(ingestRes);
      setStatus('success');
    } catch (err: any) {
      console.error(err);
      setStatus('error');
      setErrorMessage(err.response?.data?.detail || err.message || '上傳或入庫時發生錯誤');
    }
  };

  const resetForm = () => {
    setFile(null);
    setStatus('idle');
    setResult(null);
    setParseResult(null);
    setErrorMessage('');
    setCachedLocalPath(null);
  };

  const parsedRows = flattenProperties();
  const detectedPartNumber = getPartNumberValue();
  const isMissingPartNumber = !detectedPartNumber;

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight text-[#171717]">CAD 入庫</h1>
        <p className="mt-2 text-[#404040]">上傳 SolidWorks 零件或組合件前，可以先解析檔內屬性，確認必要欄位是否完整。</p>
      </div>

      <div className="flex items-start gap-3 rounded-lg border border-amber-300 bg-amber-50 p-4">
        <AlertTriangle className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-600" />
        <div className="text-sm text-amber-800">
          <p className="mb-1 font-semibold">上傳前檢查</p>
          <p>
            系統會從 CAD 自訂屬性中讀取 <code className="rounded bg-amber-100 px-1 font-mono">PartNumber</code> 或
            <code className="ml-1 rounded bg-amber-100 px-1 font-mono">品號</code> 作為料號。建議先按下「先解析屬性」，確認檔案內真的有讀到值，再進行入庫。
          </p>
        </div>
      </div>

      <Card className="p-8">
        {status === 'success' ? (
          <div className="py-12 text-center">
            <CheckCircle className="mx-auto mb-4 h-16 w-16 text-green-500" />
            <h2 className="mb-2 text-2xl font-semibold">入庫完成</h2>
            <p className="mb-6 text-gray-600">
              料號：<span className="font-bold">{result?.files?.[0]?.partNumber || result?.Files?.[0]?.PartNumber || 'N/A'}</span>
              <br />
              檔案已完成解析並寫入資料庫。
            </p>
            <Button onClick={resetForm}>重新上傳</Button>
          </div>
        ) : status === 'uploading' || status === 'parsing' || status === 'processing' ? (
          <div className="flex flex-col items-center py-16 text-center">
            <RefreshCw className="mb-4 h-12 w-12 animate-spin text-accent" />
            <h2 className="text-xl font-medium">
              {status === 'uploading'
                ? '正在上傳檔案...'
                : status === 'parsing'
                  ? '正在解析 CAD 屬性...'
                  : '正在讀取 PartNumber / 品號 並入庫處理...'}
            </h2>
            <p className="mt-2 text-gray-500">
              {status === 'parsing'
                ? '系統正在讀取文件層級、組態層級屬性與參考檔資訊。'
                : '請稍候，系統正在處理檔案。'}
            </p>
          </div>
        ) : (
          <div className="space-y-6">
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={handleFileDrop}
              className={`cursor-pointer rounded-xl border-2 border-dashed p-12 text-center transition-colors hover:bg-gray-50 ${
                file ? 'border-accent bg-accent/5' : 'border-gray-300'
              }`}
            >
              <input
                type="file"
                className="hidden"
                id="file-upload"
                accept=".SLDPRT,.sldprt,.SLDASM,.sldasm"
                onChange={(e) => handleFileSelected(e.target.files ? e.target.files[0] : null)}
              />
              <label htmlFor="file-upload" className="flex cursor-pointer flex-col items-center">
                <UploadCloud className="mb-4 h-12 w-12 text-gray-400" />
                {file ? (
                  <div className="text-lg font-medium text-primary">{file.name}</div>
                ) : (
                  <>
                    <div className="text-lg font-medium text-primary">拖曳 CAD 檔案到這裡，或點擊選擇</div>
                    <p className="mt-1 text-sm text-gray-500">僅支援 .SLDPRT 與 .SLDASM</p>
                  </>
                )}
              </label>
            </div>

            {status === 'error' && (
              <div className="flex items-start space-x-3 rounded-lg bg-red-50 p-4 text-red-700">
                <AlertCircle className="mt-0.5 h-5 w-5 flex-shrink-0" />
                <div>
                  <h3 className="font-medium">處理失敗</h3>
                  <p className="mt-1 text-sm">{errorMessage}</p>
                </div>
              </div>
            )}

            {file && (
              <div className="flex flex-col gap-3 border-t border-gray-100 pt-6 sm:flex-row sm:justify-end">
                <Button onClick={handleParse} variant="secondary" className="w-full sm:w-auto">
                  <SearchCheck className="mr-2 h-4 w-4" />
                  先解析屬性
                </Button>
                <Button onClick={handleSubmit} className="w-full sm:w-auto">
                  直接入庫
                </Button>
              </div>
            )}

            {parseResult && (
              <div className="space-y-4 border-t border-gray-100 pt-6">
                <div className={`rounded-lg border p-4 ${isMissingPartNumber ? 'border-red-200 bg-red-50' : 'border-green-200 bg-green-50'}`}>
                  <h3 className={`font-medium ${isMissingPartNumber ? 'text-red-700' : 'text-green-700'}`}>
                    {isMissingPartNumber ? '必要欄位不足' : '必要欄位檢查通過'}
                  </h3>
                  <p className={`mt-1 text-sm ${isMissingPartNumber ? 'text-red-700' : 'text-green-700'}`}>
                    {isMissingPartNumber
                      ? '目前沒有讀到 PartNumber / 品號。請先回到 SolidWorks 補齊自訂屬性。'
                      : `已讀到料號：${detectedPartNumber}`}
                  </p>
                </div>

                <div className="grid gap-4 sm:grid-cols-3">
                  <Card className="p-4">
                    <div className="text-sm text-gray-500">文件類型</div>
                    <div className="mt-1 text-lg font-semibold text-[#171717]">
                      {parseResult.documentType || parseResult.DocumentType || '-'}
                    </div>
                  </Card>
                  <Card className="p-4">
                    <div className="text-sm text-gray-500">屬性筆數</div>
                    <div className="mt-1 text-lg font-semibold text-[#171717]">{parsedRows.length}</div>
                  </Card>
                  <Card className="p-4">
                    <div className="text-sm text-gray-500">參考檔數</div>
                    <div className="mt-1 text-lg font-semibold text-[#171717]">{referencedFilePaths.length}</div>
                  </Card>
                </div>

                <Card className="overflow-hidden">
                  <div className="border-b border-gray-100 px-4 py-3">
                    <h3 className="font-medium text-[#171717]">CAD 屬性預覽</h3>
                    <p className="mt-1 text-sm text-gray-500">這裡會列出檔案內實際解析到的文件屬性與組態屬性。</p>
                  </div>
                  {parsedRows.length === 0 ? (
                    <div className="px-4 py-6 text-sm text-gray-500">沒有讀到任何自訂屬性。</div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="min-w-full text-sm">
                        <thead className="bg-gray-50 text-left text-gray-500">
                          <tr>
                            <th className="px-4 py-3 font-medium">層級</th>
                            <th className="px-4 py-3 font-medium">組態</th>
                            <th className="px-4 py-3 font-medium">屬性名稱</th>
                            <th className="px-4 py-3 font-medium">值</th>
                            <th className="px-4 py-3 font-medium">型別</th>
                          </tr>
                        </thead>
                        <tbody>
                          {parsedRows.map((row) => {
                            const isPartNumberAlias = PART_NUMBER_ALIASES.includes(row.name);
                            const isEmptyRequired = isPartNumberAlias && !row.value.trim();

                            return (
                              <tr key={`${row.scope}-${row.configuration}-${row.name}`} className="border-t border-gray-100 align-top">
                                <td className="px-4 py-3 text-gray-600">{row.scope}</td>
                                <td className="px-4 py-3 text-gray-600">{row.configuration}</td>
                                <td className={`px-4 py-3 font-medium ${isPartNumberAlias ? 'text-[#171717]' : 'text-gray-700'}`}>{row.name}</td>
                                <td className={`px-4 py-3 ${isEmptyRequired ? 'text-red-600' : 'text-gray-700'}`}>
                                  {row.value || <span className="text-gray-400">(空白)</span>}
                                </td>
                                <td className="px-4 py-3 text-gray-500">{row.type || '-'}</td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </Card>

                {referencedFilePaths.length > 0 && (
                  <Card className="p-4">
                    <h3 className="font-medium text-[#171717]">參考檔清單</h3>
                    <div className="mt-3 max-h-48 space-y-2 overflow-y-auto text-sm text-gray-600">
                      {referencedFilePaths.map((path) => (
                        <div key={path} className="rounded-md bg-gray-50 px-3 py-2 font-mono text-xs">
                          {path}
                        </div>
                      ))}
                    </div>
                  </Card>
                )}
              </div>
            )}
          </div>
        )}
      </Card>
    </div>
  );
}
