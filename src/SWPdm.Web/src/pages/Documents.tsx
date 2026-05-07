import React, { useState, useEffect } from 'react';
import { Search, Filter, Loader2, Download, PackageOpen, Server, FileText, Archive, History } from 'lucide-react';
import { api, searchDocuments, downloadAssemblyZip, downloadVersion, getVersionThumbnailUrl, undoCheckOutDocument } from '../lib/api';
import { BomTreeView } from '../components/BomTreeView';
import { CheckOutModal } from '../components/CheckOutModal';
import { CheckInModal } from '../components/CheckInModal';
import { Lock, Unlock, LogOut, LogIn } from 'lucide-react';

interface VersionThumbnailProps {
  versionId?: number | null;
  className: string;
  iconSize?: number;
}

const VersionThumbnail: React.FC<VersionThumbnailProps> = ({ versionId, className, iconSize = 18 }) => {
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    setHasError(false);
  }, [versionId]);

  return (
    <div className={`${className} shrink-0 overflow-hidden rounded-md border border-gray-800 bg-gray-900/70 flex items-center justify-center`}>
      {versionId && !hasError ? (
        <img
          src={getVersionThumbnailUrl(versionId)}
          alt=""
          className="h-full w-full object-contain"
          loading="lazy"
          onError={() => setHasError(true)}
        />
      ) : (
        <FileText size={iconSize} className="text-gray-600" />
      )}
    </div>
  );
};

export default function Documents() {
  const [query, setQuery] = useState('');
  const [documents, setDocuments] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedDoc, setSelectedDoc] = useState<any>(null);
  const [isCheckOutModalOpen, setIsCheckOutModalOpen] = useState(false);
  const [isCheckInModalOpen, setIsCheckInModalOpen] = useState(false);
  const [detailTab, setDetailTab] = useState<'structure' | 'history'>('structure');

  const fetchDocuments = async (searchQuery: string = '') => {
    setIsLoading(true);
    try {
      const data = await searchDocuments(searchQuery);
      setDocuments(data);
    } catch (error) {
      console.error('Failed to search documents', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchDocuments();
  }, []);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchDocuments(query);
  };

  const handleSelectDocument = async (doc: any) => {
    setSelectedDoc(doc);
    setDetailTab('structure');

    try {
      const response = await api.get(`/api/documents/${doc.documentId}`);
      setSelectedDoc((current: any) => (
        current?.documentId === doc.documentId
          ? { ...doc, ...response.data }
          : current
      ));
    } catch (error) {
      console.error('Failed to load document details', error);
    }
  };

  const canPackAndGo = selectedDoc && ['Assembly', 'Drawing'].includes(selectedDoc.documentType);
  const versionHistory = Array.isArray(selectedDoc?.versions)
    ? [...selectedDoc.versions].sort((a: any, b: any) => (b.versionNo ?? 0) - (a.versionNo ?? 0))
    : [];

  return (
    <div className="flex h-full flex-col p-6 animate-in fade-in duration-500">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-3xl font-bold text-white tracking-tight flex items-center">
            <Server className="mr-3 text-[#D4AF37]" size={28} />
            圖檔中心 (Vault)
          </h1>
          <p className="text-gray-400 mt-2 text-sm">搜尋、檢視與管理伺服器上的設計圖檔</p>
        </div>
      </div>

      <div className="flex flex-col lg:flex-row gap-6 flex-1 min-h-0">
        {/* 左側列表區 */}
        <div className="flex-1 flex flex-col min-h-0 bg-[#121212] border border-gray-800 rounded-xl overflow-hidden shadow-2xl">
          <div className="p-4 border-b border-gray-800 bg-gray-900/50">
            <form onSubmit={handleSearch} className="flex gap-2">
              <div className="relative flex-1">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-500">
                  <Search size={18} />
                </div>
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="搜尋檔名、料號..."
                  className="block w-full pl-10 pr-3 py-2 border border-gray-700 rounded-lg leading-5 bg-gray-800/50 text-gray-100 placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-[#D4AF37] focus:border-[#D4AF37] sm:text-sm transition-all"
                />
              </div>
              <button
                type="submit"
                className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-lg text-white bg-[#D4AF37] hover:bg-[#c2a033] shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-900 focus:ring-[#D4AF37] disabled:opacity-50"
                disabled={isLoading}
              >
                {isLoading ? <Loader2 size={16} className="animate-spin mr-2" /> : <Filter size={16} className="mr-2" />}
                篩選
              </button>
            </form>
          </div>

          <div className="flex-1 overflow-auto">
            <table className="min-w-full divide-y divide-gray-800 text-sm">
              <thead className="bg-[#1a1a1a] sticky top-0 z-10 w-full">
                <tr>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">檔名</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">料號</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">類型</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">版次</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">狀態</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">更新時間</th>
                </tr>
              </thead>
              <tbody className="bg-[#121212] divide-y divide-gray-800/50">
                {documents.length === 0 && !isLoading && (
                  <tr>
                    <td colSpan={6} className="px-6 py-10 text-center text-gray-500">
                      查無圖檔資料
                    </td>
                  </tr>
                )}
                
                {documents.map((doc) => {
                  const isSelected = selectedDoc?.documentId === doc.documentId;
                  const isCheckedOutWip = Boolean(
                    doc.checkedOutBy &&
                    String(doc.currentLifecycleState || '').toLowerCase() === 'wip'
                  );

                  return (
                  <tr 
                    key={doc.documentId}
                    onClick={() => handleSelectDocument(doc)}
                    className={`cursor-pointer transition-colors ${
                      isSelected
                        ? 'bg-gray-800/80 border-l-2 border-[#D4AF37]'
                        : isCheckedOutWip
                          ? 'bg-orange-950/20 hover:bg-orange-950/30 border-l-2 border-orange-500/80'
                          : 'hover:bg-gray-800/40 border-l-2 border-transparent'
                    }`}
                  >
                    <td className="px-6 py-4 whitespace-nowrap text-gray-200 font-medium flex items-center">
                      <VersionThumbnail versionId={doc.currentVersionId} className="mr-3 h-10 w-10" />
                      <span className="min-w-0 truncate">{doc.fileName}</span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-gray-400">{doc.partNumber || '-'}</td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className={`px-2 py-1 inline-flex text-xs leading-5 font-semibold rounded-md ${
                        doc.documentType === 'Assembly' ? 'bg-yellow-900/40 text-yellow-500' : 
                        doc.documentType === 'Drawing' ? 'bg-purple-900/40 text-purple-400' : 
                        'bg-blue-900/40 text-blue-400'
                      }`}>
                        {doc.documentType}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-gray-400 text-center">{doc.revisionLabel || '-'}</td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {doc.checkedOutBy ? (
                        <span className="flex items-center gap-1 text-[10px] text-orange-400 bg-orange-400/10 px-1.5 py-0.5 rounded border border-orange-400/20">
                          <Lock size={10} /> {doc.checkedOutBy}
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-[10px] text-green-400 bg-green-400/10 px-1.5 py-0.5 rounded border border-green-400/20">
                          <Unlock size={10} /> 可用
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-gray-500 text-xs">
                      {new Date(doc.updatedAt).toLocaleString('zh-TW', {
                         year: 'numeric', month: '2-digit', day: '2-digit',
                         hour: '2-digit', minute: '2-digit'
                      })}
                    </td>
                  </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* 右側預覽區 (Bom Tree / Info) */}
        {selectedDoc && (
          <div className="w-full lg:w-96 flex flex-col min-h-0 bg-[#121212] border border-gray-800 rounded-xl shadow-2xl animate-in slide-in-from-right-4 duration-300">
            <div className="p-4 border-b border-gray-800 bg-[#1a1a1a]">
              <VersionThumbnail
                versionId={selectedDoc.currentVersionId}
                className="mb-4 h-40 w-full rounded-lg"
                iconSize={48}
              />
              <div className="flex justify-between items-start">
                <h3 className="text-sm border border-gray-700 bg-gray-800 px-2 py-0.5 rounded text-gray-400 font-mono mb-2 inline-block">
                  {selectedDoc.partNumber || 'No Part Number'}
                </h3>
              </div>
              <h2 className="text-xl font-bold text-white break-all mb-1">{selectedDoc.fileName}</h2>
              <div className="flex items-center text-xs text-gray-400 mt-2 space-x-4">
                <span>Rev: <span className="text-gray-200 font-medium">{selectedDoc.revisionLabel}</span></span>
                <span>Type: <span className="text-gray-200">{selectedDoc.documentType}</span></span>
              </div>

              {/* Checkout Controls */}
              <div className="mt-4 flex gap-2">
                {!selectedDoc.checkedOutBy ? (
                  <button 
                    onClick={() => setIsCheckOutModalOpen(true)}
                    className="flex-1 bg-blue-600 hover:bg-blue-700 text-white px-3 py-2 rounded text-xs font-medium flex items-center justify-center gap-2 transition-colors"
                  >
                    <LogOut size={14} /> 出庫 (Check-out)
                  </button>
                ) : (
                  <>
                    <button 
                      onClick={() => setIsCheckInModalOpen(true)}
                      className="flex-1 bg-green-600 hover:bg-green-700 text-white px-3 py-2 rounded text-xs font-medium flex items-center justify-center gap-2 transition-colors"
                    >
                      <LogIn size={14} /> 入庫 (Check-in)
                    </button>
                    <button 
                      onClick={async () => {
                        if (confirm('確定要復原出庫嗎？您的修改將不會被儲存。')) {
                          await undoCheckOutDocument(selectedDoc.documentId);
                          fetchDocuments(query);
                        }
                      }}
                      className="bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-white border border-gray-700 px-3 py-2 rounded text-xs transition-colors"
                      title="Undo Checkout"
                    >
                      復原
                    </button>
                  </>
                )}
              </div>
            </div>

            <div className="p-4 flex-1 min-h-0 flex flex-col">
              <div className="grid grid-cols-2 gap-2 mb-4 rounded-lg bg-gray-900/70 border border-gray-800 p-1">
                <button
                  onClick={() => setDetailTab('structure')}
                  className={`flex items-center justify-center gap-2 rounded-md px-3 py-2 text-xs font-medium transition-colors ${
                    detailTab === 'structure'
                      ? 'bg-gray-800 text-white shadow'
                      : 'text-gray-400 hover:text-white hover:bg-gray-800/60'
                  }`}
                >
                  <PackageOpen size={14} className={detailTab === 'structure' ? 'text-[#D4AF37]' : ''} />
                  結構預覽
                </button>
                <button
                  onClick={() => setDetailTab('history')}
                  className={`flex items-center justify-center gap-2 rounded-md px-3 py-2 text-xs font-medium transition-colors ${
                    detailTab === 'history'
                      ? 'bg-gray-800 text-white shadow'
                      : 'text-gray-400 hover:text-white hover:bg-gray-800/60'
                  }`}
                >
                  <History size={14} className={detailTab === 'history' ? 'text-[#D4AF37]' : ''} />
                  版本歷史
                </button>
              </div>

              <div className="flex-1 min-h-0 overflow-auto">
                {detailTab === 'structure' ? (
                  canPackAndGo && selectedDoc.currentVersionId ? (
                    <div className="h-full flex flex-col">
                      <div className="flex justify-between items-center mb-3">
                        <h4 className="text-white font-medium flex items-center">
                          <PackageOpen size={16} className="mr-2 text-[#D4AF37]" />
                          關聯結構預覽
                        </h4>
                        <button
                          onClick={() => downloadAssemblyZip(selectedDoc.currentVersionId)}
                          className="text-xs flex items-center text-gray-400 hover:text-white bg-gray-800 hover:bg-gray-700 px-2 py-1 rounded transition-colors"
                        >
                          <Download size={12} className="mr-1" /> Pack & Go
                        </button>
                      </div>

                      <BomTreeView rootVersionId={selectedDoc.currentVersionId} />
                    </div>
                  ) : (
                    <div className="h-full flex flex-col items-center justify-center text-gray-500 bg-gray-900/30 rounded-lg border border-gray-800/50 p-6">
                      <FileText size={48} className="mb-4 text-gray-700" />
                      <p className="text-center text-sm">此為零件檔案，無 BOM 或關聯結構可顯示。</p>
                      <p className="text-center text-xs mt-2 text-gray-600">檢視僅支援組合件 (Assembly) 與工程圖 (Drawing)</p>
                      {selectedDoc.currentVersionId && (
                        <button
                          onClick={() => downloadVersion(selectedDoc.currentVersionId)}
                          className="mt-4 text-sm flex items-center text-gray-200 hover:text-white bg-[#D4AF37] hover:bg-[#c2a033] px-4 py-2 rounded shadow transition-colors"
                        >
                          <Download size={16} className="mr-2" /> 下載檔案
                        </button>
                      )}
                    </div>
                  )
                ) : (
                  <div className="space-y-3">
                    <div className="flex items-center justify-between">
                      <h4 className="text-white font-medium flex items-center">
                        <History size={16} className="mr-2 text-[#D4AF37]" />
                        版本歷史
                      </h4>
                      <span className="text-xs text-gray-500">{versionHistory.length} versions</span>
                    </div>

                    {versionHistory.length === 0 ? (
                      <div className="rounded-lg border border-gray-800 bg-gray-900/30 p-6 text-center">
                        <History size={36} className="mx-auto mb-3 text-gray-700" />
                        <p className="text-sm text-gray-400">尚無版本歷史資料。</p>
                      </div>
                    ) : (
                      <div className="space-y-2">
                        {versionHistory.map((v: any) => (
                          <div
                            key={v.versionId}
                            className="rounded-lg border border-gray-800 bg-gray-900/40 p-3 hover:bg-gray-800/50 transition-colors"
                          >
                            <div className="flex items-start justify-between gap-3">
                              <div className="min-w-0">
                                <div className="flex flex-wrap items-center gap-2">
                                  <span className="text-sm font-semibold text-white">Ver. {v.versionNo}</span>
                                  <span className="rounded border border-[#D4AF37]/30 bg-[#D4AF37]/10 px-2 py-0.5 text-[11px] font-medium text-[#D4AF37]">
                                    Rev. {v.revisionLabel || '-'}
                                  </span>
                                </div>
                                <p className="mt-1 text-xs text-gray-400">
                                  {v.createdAt ? new Date(v.createdAt).toLocaleString() : '-'}
                                </p>
                                {v.changeReason && (
                                  <div className="mt-2 rounded-md border border-gray-800 bg-gray-950/40 px-3 py-2">
                                    <p className="text-[11px] font-medium text-gray-500">變更原因</p>
                                    <p className="mt-1 whitespace-pre-wrap break-words text-xs text-gray-300">
                                      {v.changeReason}
                                    </p>
                                  </div>
                                )}
                              </div>

                              <div className="flex shrink-0 items-center gap-1.5">
                                <button
                                  onClick={() => downloadVersion(v.versionId)}
                                  className="inline-flex h-8 w-8 items-center justify-center rounded border border-gray-700 bg-gray-800 text-gray-400 hover:border-[#D4AF37]/60 hover:text-white transition-colors"
                                  title="單檔下載"
                                  aria-label={`下載 Ver. ${v.versionNo} 單檔`}
                                >
                                  <Download size={14} />
                                </button>
                                {canPackAndGo && (
                                  <button
                                    onClick={() => downloadAssemblyZip(v.versionId)}
                                    className="inline-flex h-8 w-8 items-center justify-center rounded border border-gray-700 bg-gray-800 text-gray-400 hover:border-[#D4AF37]/60 hover:text-[#D4AF37] transition-colors"
                                    title="Pack & Go"
                                    aria-label={`下載 Ver. ${v.versionNo} Pack & Go`}
                                  >
                                    <Archive size={14} />
                                  </button>
                                )}
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      <CheckOutModal 
        isOpen={isCheckOutModalOpen}
        onClose={() => setIsCheckOutModalOpen(false)}
        documentId={selectedDoc?.documentId}
        fileName={selectedDoc?.fileName}
        onSuccess={() => {
          fetchDocuments(query);
          setIsCheckOutModalOpen(false);
        }}
      />
      <CheckInModal 
        isOpen={isCheckInModalOpen}
        onClose={() => setIsCheckInModalOpen(false)}
        documentId={selectedDoc?.documentId}
        fileName={selectedDoc?.fileName}
        onSuccess={() => {
          fetchDocuments(query);
          setIsCheckInModalOpen(false);
        }}
      />
    </div>
  );
}
