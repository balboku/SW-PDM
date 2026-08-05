import React, { useState, useEffect } from 'react';
import { Search, Filter, Loader2, Download, PackageOpen, Server, FileText, Archive, History, Link2, AlertCircle, RefreshCw, GitBranch, ArrowRight } from 'lucide-react';
import { api, searchDocuments, downloadAssemblyZip, downloadVersion, getVersionThumbnailUrl, undoCheckOutDocument, checkAssemblyUpdates, getDocumentRelations } from '../lib/api';
import { BomTreeView } from '../components/BomTreeView';
import { CheckOutModal } from '../components/CheckOutModal';
import { CheckInModal } from '../components/CheckInModal';
import { Modal } from '../components/ui';
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

interface RelatedDrawing {
  documentId: number;
  versionId: number;
  originalFileName: string;
  matchMethod: 'DocumentId' | 'FilenameFallback' | string;
}

interface RelatedDrawingsPanelProps {
  drawings: RelatedDrawing[];
  isLoading: boolean;
  error: string;
  onRetry: () => void;
}

const RelatedDrawingsPanel: React.FC<RelatedDrawingsPanelProps> = ({
  drawings,
  isLoading,
  error,
  onRetry
}) => (
  <section className="mt-4 rounded-lg border border-gray-800 bg-[#1a1a1a]">
    <div className="flex items-center justify-between border-b border-gray-800 px-4 py-2">
      <h5 className="flex items-center text-xs font-semibold tracking-wider text-gray-300">
        <Link2 size={14} className="mr-2 text-purple-400" />
        關聯 2D 工程圖
      </h5>
      {!isLoading && !error && (
        <span className="rounded bg-gray-800 px-2 py-0.5 text-[10px] text-gray-400">
          {drawings.length} 份
        </span>
      )}
    </div>

    {isLoading ? (
      <div className="flex items-center justify-center px-4 py-6 text-xs text-gray-400">
        <Loader2 size={14} className="mr-2 animate-spin text-[#D4AF37]" />
        正在反查工程圖...
      </div>
    ) : error ? (
      <div className="px-4 py-4 text-xs text-red-300" role="alert">
        <p className="font-medium">關聯資料尚未載入，請重新嘗試。</p>
        <p className="mt-1 text-red-400">{error}</p>
        <button
          type="button"
          onClick={onRetry}
          className="mt-3 inline-flex items-center rounded border border-red-900/50 bg-red-950/30 px-2.5 py-1.5 text-red-200 hover:bg-red-950/50"
        >
          <RefreshCw size={12} className="mr-1.5" />
          重新載入關聯
        </button>
      </div>
    ) : drawings.length === 0 ? (
      <div className="px-4 py-5 text-xs text-gray-400">
        <p className="font-medium text-gray-300">尚未找到關聯工程圖。</p>
        <p className="mt-1 leading-5">
          下一步：到「CAD 入庫」使用資料夾批次匯入 3D 與 SLDDRW，
          或確認工程圖內部參考仍指向這個模型。
        </p>
      </div>
    ) : (
      <div className="divide-y divide-gray-800/80">
        {drawings.map((drawing) => (
          <div key={drawing.documentId} className="flex items-center gap-2 px-3 py-2.5">
            <FileText size={15} className="shrink-0 text-purple-400" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-xs font-medium text-gray-200" title={drawing.originalFileName}>
                {drawing.originalFileName}
              </p>
              <p className="mt-0.5 text-[10px] text-gray-500">
                {drawing.matchMethod === 'FilenameFallback' ? '依檔名補足關聯' : '依文件關聯'}
              </p>
            </div>
            <button
              type="button"
              onClick={() => downloadVersion(drawing.versionId)}
              className="rounded p-1.5 text-gray-500 hover:bg-gray-800 hover:text-[#D4AF37]"
              title="下載工程圖"
              aria-label={`下載 ${drawing.originalFileName}`}
            >
              <Download size={14} />
            </button>
          </div>
        ))}
      </div>
    )}
  </section>
);

interface IdentityChangePanelProps {
  identityOrigin: any;
  derivedDocuments: any[];
}

const IdentityChangePanel: React.FC<IdentityChangePanelProps> = ({
  identityOrigin,
  derivedDocuments
}) => {
  if (!identityOrigin && derivedDocuments.length === 0) return null;

  return (
    <section className="mt-4 rounded-lg border border-amber-900/30 bg-amber-950/10">
      <div className="flex items-center justify-between border-b border-amber-900/20 px-4 py-2">
        <h5 className="flex items-center text-xs font-semibold tracking-wider text-amber-200">
          <GitBranch size={14} className="mr-2 text-amber-400" />
          品號衍生關聯
        </h5>
        <span className="rounded bg-amber-950/40 px-2 py-0.5 text-[10px] text-amber-300">
          {derivedDocuments.length + (identityOrigin ? 1 : 0)} 筆
        </span>
      </div>

      {identityOrigin && (
        <div className="border-b border-amber-900/20 px-3 py-3">
          <p className="text-[10px] font-medium text-gray-500">本文件來源</p>
          <div className="mt-1.5 flex min-w-0 items-center gap-2 text-xs">
            <span className="truncate font-mono text-gray-400">{identityOrigin.oldPartNumber}</span>
            <ArrowRight size={12} className="shrink-0 text-amber-400" />
            <span className="truncate font-mono font-medium text-amber-200">{identityOrigin.newPartNumber}</span>
            <button
              type="button"
              onClick={() => downloadVersion(identityOrigin.sourceVersionId)}
              className="ml-auto shrink-0 rounded p-1.5 text-gray-500 hover:bg-amber-950/40 hover:text-amber-300"
              title="下載衍生時的來源版本"
              aria-label={`下載來源品號 ${identityOrigin.oldPartNumber} 的版本`}
            >
              <Download size={13} />
            </button>
          </div>
          <p className="mt-1.5 break-words text-[10px] leading-4 text-gray-500">
            {identityOrigin.changeReason} · {identityOrigin.changedBy}
          </p>
        </div>
      )}

      {derivedDocuments.length > 0 && (
        <div className="divide-y divide-amber-900/20">
          {derivedDocuments.map((item) => (
            <div key={item.identityChangeId} className="px-3 py-3">
              <p className="text-[10px] font-medium text-gray-500">衍生新品號</p>
              <div className="mt-1.5 flex min-w-0 items-center gap-2 text-xs">
                <span className="truncate font-mono text-gray-400">{item.oldPartNumber}</span>
                <ArrowRight size={12} className="shrink-0 text-amber-400" />
                <span className="truncate font-mono font-medium text-amber-200">{item.newPartNumber}</span>
                {item.targetVersionId && (
                  <button
                    type="button"
                    onClick={() => downloadVersion(item.targetVersionId)}
                    className="ml-auto shrink-0 rounded p-1.5 text-gray-500 hover:bg-amber-950/40 hover:text-amber-300"
                    title="下載新品號目前版本"
                    aria-label={`下載新品號 ${item.newPartNumber}`}
                  >
                    <Download size={13} />
                  </button>
                )}
              </div>
              <p className="mt-1.5 break-words text-[10px] leading-4 text-gray-500">
                {item.changeReason} · {item.changedBy}
              </p>
            </div>
          ))}
        </div>
      )}
    </section>
  );
};

export default function Documents() {
  const [query, setQuery] = useState('');
  const [documents, setDocuments] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedDoc, setSelectedDoc] = useState<any>(null);
  const [isCheckOutModalOpen, setIsCheckOutModalOpen] = useState(false);
  const [isCheckInModalOpen, setIsCheckInModalOpen] = useState(false);
  const [packAndGoTarget, setPackAndGoTarget] = useState<number | null>(null);
  const [packAndGoUpdates, setPackAndGoUpdates] = useState<any[]>([]);
  const [packAndGoSelections, setPackAndGoSelections] = useState<Record<number, number>>({});
  const [packAndGoRelatedDrawings, setPackAndGoRelatedDrawings] = useState<RelatedDrawing[]>([]);
  const [packAndGoPreviewError, setPackAndGoPreviewError] = useState('');
  const [includeDrawings, setIncludeDrawings] = useState(true);
  const [relationData, setRelationData] = useState<any>(null);
  const [isRelationLoading, setIsRelationLoading] = useState(false);
  const [relationError, setRelationError] = useState('');
  const [detailTab, setDetailTab] = useState<'structure' | 'history'>('structure');
  const [selectedType, setSelectedType] = useState('All');
  const [selectedStatus, setSelectedStatus] = useState('All');

  const fetchDocuments = async (searchQuery: string = query, type: string = selectedType, status: string = selectedStatus) => {
    setIsLoading(true);
    try {
      const data = await searchDocuments(searchQuery, type, status);
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
    fetchDocuments(query, selectedType, selectedStatus);
  };

  const loadDocumentRelations = async (documentId: number) => {
    setIsRelationLoading(true);
    setRelationError('');
    try {
      const data = await getDocumentRelations(documentId);
      setRelationData(data);
    } catch (error: any) {
      console.error('Failed to load document relations', error);
      setRelationData(null);
      setRelationError(error.response?.data?.detail || error.message || '無法載入關聯資料');
    } finally {
      setIsRelationLoading(false);
    }
  };

  const handleSelectDocument = async (doc: any) => {
    setSelectedDoc(doc);
    setDetailTab('structure');
    setRelationData(null);
    setRelationError('');
    void loadDocumentRelations(doc.documentId);

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

  const handlePackAndGoClick = async (versionId: number) => {
    setPackAndGoTarget(versionId);
    setPackAndGoUpdates([]);
    setPackAndGoSelections({});
    setPackAndGoRelatedDrawings([]);
    setPackAndGoPreviewError('');
    setIncludeDrawings(true);

    try {
      const result = await checkAssemblyUpdates(versionId);
      const updates = Array.isArray(result?.updates) ? result.updates : [];
      const drawings = Array.isArray(result?.relatedDrawings) ? result.relatedDrawings : [];
      const selections = updates.reduce((acc: Record<number, number>, item: any) => {
        const defaultVersionId = item.currentVersionId ?? item.versions?.[0]?.versionId ?? item.sourceVersionId;
        acc[item.sourceVersionId] = Number(defaultVersionId);
        return acc;
      }, {});

      setPackAndGoUpdates(updates);
      setPackAndGoSelections(selections);
      setPackAndGoRelatedDrawings(drawings);
    } catch (error: any) {
      console.error('Failed to check package updates', error);
      setIncludeDrawings(false);
      setPackAndGoPreviewError(
        error.response?.data?.detail || error.message || '無法載入 Pack & Go 預覽'
      );
    }
  };

  const closePackAndGoModal = () => {
    setPackAndGoTarget(null);
    setPackAndGoUpdates([]);
    setPackAndGoSelections({});
    setPackAndGoRelatedDrawings([]);
    setPackAndGoPreviewError('');
    setIncludeDrawings(true);
  };

  const handlePackAndGoDownload = (mode: 'original' | 'selected') => {
    if (packAndGoTarget) {
      const overrides = mode === 'selected'
        ? Object.entries(packAndGoSelections).map(([sourceVersionId, selectedVersionId]) => `${sourceVersionId}:${selectedVersionId}`)
        : [];

      downloadAssemblyZip(packAndGoTarget, false, overrides, includeDrawings);
    }

    closePackAndGoModal();
  };

  const canPackAndGo = selectedDoc && ['Assembly', 'Drawing'].includes(selectedDoc.documentType);
  const versionHistory = Array.isArray(selectedDoc?.versions)
    ? [...selectedDoc.versions].sort((a: any, b: any) => (b.versionNo ?? 0) - (a.versionNo ?? 0))
    : [];

  return (
    <div className="flex h-full flex-col p-0 sm:p-4 md:p-6 animate-in fade-in duration-500">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center">
            <Server className="mr-3 text-[#D4AF37]" size={28} />
            圖檔中心 (Vault)
          </h1>
          <p className="text-gray-600 mt-2 text-sm">搜尋、檢視與管理伺服器上的設計圖檔</p>
        </div>
      </div>

      <div className="flex flex-col lg:flex-row gap-6 flex-1 min-h-0">
        {/* 左側列表區 */}
        <div className="flex-1 flex flex-col min-h-0 bg-[#121212] border border-gray-800 rounded-xl overflow-hidden shadow-2xl">
          <div className="p-4 border-b border-gray-800 bg-gray-900/50">
            <form onSubmit={handleSearch} className="flex flex-col gap-2 sm:flex-row">
              <div className="relative min-w-0 flex-1">
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

              <select
                value={selectedType}
                onChange={(e) => {
                  setSelectedType(e.target.value);
                  fetchDocuments(query, e.target.value, selectedStatus);
                }}
                className="w-full bg-gray-800/50 text-gray-200 border border-gray-700 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[#D4AF37] sm:w-auto"
              >
                <option value="All">所有類型</option>
                <option value="Part">零件 (Part)</option>
                <option value="Assembly">組合件 (Asm)</option>
                <option value="Drawing">工程圖 (Drw)</option>
              </select>

              <select
                value={selectedStatus}
                onChange={(e) => {
                  setSelectedStatus(e.target.value);
                  fetchDocuments(query, selectedType, e.target.value);
                }}
                className="w-full bg-gray-800/50 text-gray-200 border border-gray-700 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[#D4AF37] sm:w-auto"
              >
                <option value="All">所有狀態</option>
                <option value="Available">可用 (Available)</option>
                <option value="CheckedOut">已出庫 (Locked)</option>
              </select>

              <button
                type="submit"
                className="inline-flex w-full items-center justify-center px-4 py-2 border border-transparent text-sm font-medium rounded-lg text-white bg-[#D4AF37] hover:bg-[#c2a033] shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-900 focus:ring-[#D4AF37] disabled:opacity-50 sm:w-auto"
                disabled={isLoading}
              >
                {isLoading ? <Loader2 size={16} className="animate-spin mr-2" /> : <Filter size={16} className="mr-2" />}
                篩選
              </button>
            </form>
            <p className="mt-2 text-xs text-gray-500 sm:hidden">表格可左右滑動查看料號、類型、版次與狀態。</p>
          </div>

          <div className="flex-1 overflow-auto">
            <table className="min-w-[760px] divide-y divide-gray-800 text-sm">
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
                  <div className="h-full">
                    <div className="mb-3 flex items-center justify-between gap-3">
                      <h4 className="flex items-center font-medium text-white">
                        <PackageOpen size={16} className="mr-2 text-[#D4AF37]" />
                        關聯結構預覽
                      </h4>
                      {canPackAndGo && selectedDoc.currentVersionId && (
                        <button
                          onClick={() => handlePackAndGoClick(selectedDoc.currentVersionId)}
                          className="flex shrink-0 items-center rounded bg-gray-800 px-2 py-1 text-xs text-gray-400 transition-colors hover:bg-gray-700 hover:text-white"
                        >
                          <Download size={12} className="mr-1" /> Pack & Go
                        </button>
                      )}
                    </div>

                    {canPackAndGo && selectedDoc.currentVersionId ? (
                      <BomTreeView rootVersionId={selectedDoc.currentVersionId} />
                    ) : (
                      <div className="rounded-lg border border-gray-800/70 bg-gray-900/30 p-4">
                        <p className="text-sm font-medium text-gray-300">此零件沒有向下 BOM。</p>
                        <p className="mt-1 text-xs leading-5 text-gray-500">
                          下方會反向列出使用此 3D 模型的 2D 工程圖。
                        </p>
                        {selectedDoc.currentVersionId && (
                          <button
                            onClick={() => downloadVersion(selectedDoc.currentVersionId)}
                            className="mt-3 flex items-center rounded bg-[#D4AF37] px-3 py-2 text-xs font-medium text-gray-950 shadow transition-colors hover:bg-[#c2a033]"
                          >
                            <Download size={14} className="mr-2" /> 下載 3D 檔案
                          </button>
                        )}
                      </div>
                    )}

                    <RelatedDrawingsPanel
                      drawings={Array.isArray(relationData?.drawings) ? relationData.drawings : []}
                      isLoading={isRelationLoading}
                      error={relationError}
                      onRetry={() => loadDocumentRelations(selectedDoc.documentId)}
                    />

                    <IdentityChangePanel
                      identityOrigin={relationData?.identityOrigin ?? null}
                      derivedDocuments={Array.isArray(relationData?.derivedDocuments) ? relationData.derivedDocuments : []}
                    />
                  </div>
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
                                    onClick={() => handlePackAndGoClick(v.versionId)}
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
          void fetchDocuments(query);
          if (selectedDoc?.documentId) {
            void handleSelectDocument({ ...selectedDoc, checkedOutBy: null, checkedOutAt: null });
          }
          setIsCheckInModalOpen(false);
        }}
      />
      <Modal
        isOpen={packAndGoTarget !== null}
        onClose={closePackAndGoModal}
        title="Pack & Go 版本選擇"
      >
        <div className="space-y-4">
          <p className="text-sm text-gray-300">
            下載前確認套件範圍。預設保留原簽入 BOM，並包含目前找到的關聯 2D 工程圖。
          </p>

          {packAndGoPreviewError && (
            <div className="rounded border border-red-900/40 bg-red-950/20 p-3 text-xs text-red-300" role="alert">
              <p className="font-medium">工程圖預覽未完成；目前只能下載既有 BOM。</p>
              <p className="mt-1 text-red-400">{packAndGoPreviewError}</p>
            </div>
          )}

          <label className={`block rounded-lg border p-3 ${
            packAndGoPreviewError
              ? 'cursor-not-allowed border-gray-800 bg-gray-900/40 opacity-60'
              : 'cursor-pointer border-purple-900/40 bg-purple-950/20 hover:border-purple-700/60'
          }`}>
            <div className="flex items-start gap-3">
              <input
                type="checkbox"
                checked={includeDrawings}
                disabled={Boolean(packAndGoPreviewError)}
                onChange={(event) => setIncludeDrawings(event.target.checked)}
                className="mt-0.5 h-4 w-4 accent-purple-500"
              />
              <div className="min-w-0">
                <p className="text-sm font-medium text-white">
                  包含關聯 2D 工程圖（{packAndGoRelatedDrawings.length}）
                </p>
                <p className="mt-1 text-xs leading-5 text-gray-400">
                  {packAndGoRelatedDrawings.length > 0
                    ? '會加入下列工程圖的目前版本；取消勾選可只下載 BOM。'
                    : '目前沒有找到可加入的工程圖；可先完成批次匯入或檢查工程圖參考。'}
                </p>
              </div>
            </div>
          </label>

          {packAndGoRelatedDrawings.length > 0 && (
            <div className="max-h-28 overflow-y-auto rounded-lg border border-gray-800 bg-gray-950/40">
              {packAndGoRelatedDrawings.map((drawing) => (
                <div
                  key={drawing.documentId}
                  className="flex items-center gap-2 border-b border-gray-800/70 px-3 py-2 last:border-b-0"
                >
                  <FileText size={13} className="shrink-0 text-purple-400" />
                  <span className="min-w-0 flex-1 truncate text-xs text-gray-300" title={drawing.originalFileName}>
                    {drawing.originalFileName}
                  </span>
                  <span className="shrink-0 text-[10px] text-gray-500">
                    {drawing.matchMethod === 'FilenameFallback' ? '檔名補足' : '已連結'}
                  </span>
                </div>
              ))}
            </div>
          )}

          <div className="space-y-3">
            <button
              onClick={() => handlePackAndGoDownload('original')}
              className="w-full rounded-lg border border-gray-700 bg-gray-800 px-4 py-3 text-left text-sm text-gray-200 transition-colors hover:border-[#D4AF37]/60 hover:bg-gray-700"
            >
              <span className="block font-medium text-white">下載原簽入版本</span>
              <span className="mt-1 block text-xs text-gray-400">
                維持當初 BOM；工程圖依上方勾選加入
              </span>
            </button>

            {packAndGoUpdates.length === 0 ? (
              <p className="rounded-lg border border-green-900/30 bg-green-950/20 px-3 py-2 text-xs text-green-300">
                BOM 內檔案沒有其他版本需要選擇，可直接下載。
              </p>
            ) : (
              <div className="rounded-lg border border-[#D4AF37]/30 bg-[#D4AF37]/5 p-4">
              <div className="mb-3">
                <p className="text-sm font-medium text-white">改用系統內其他版本</p>
                <p className="mt-1 text-xs text-gray-400">請為下列檔案選擇要放入 Pack & Go 的版本</p>
              </div>

              <div className="max-h-72 space-y-3 overflow-auto pr-1">
                {packAndGoUpdates.map((item: any) => (
                  <div key={item.sourceVersionId} className="rounded-md border border-gray-800 bg-gray-900/70 p-3">
                    <div className="mb-2 min-w-0">
                      <p className="truncate text-sm font-medium text-gray-100">{item.originalFileName}</p>
                      <p className="mt-0.5 text-xs text-gray-500">
                        原簽入：Ver. {item.packageVersionNo} / Rev. {item.packageRevisionLabel || '-'}
                      </p>
                    </div>
                    <select
                      value={packAndGoSelections[item.sourceVersionId] ?? item.sourceVersionId}
                      onChange={(e) => setPackAndGoSelections((current) => ({
                        ...current,
                        [item.sourceVersionId]: Number(e.target.value)
                      }))}
                      className="w-full rounded-md border border-gray-800 bg-gray-800 px-3 py-2 text-sm text-white focus:outline-none focus:ring-1 focus:ring-[#D4AF37]"
                    >
                      {(item.versions || []).map((version: any) => (
                        <option key={version.versionId} value={version.versionId}>
                          Ver. {version.versionNo} / Rev. {version.revisionLabel || '-'}
                          {version.isCurrentVersion ? ' (目前最新版)' : ''}
                          {version.isPackageVersion ? ' (原簽入)' : ''}
                        </option>
                      ))}
                    </select>
                  </div>
                ))}
              </div>

              <button
                onClick={() => handlePackAndGoDownload('selected')}
                className="mt-4 w-full rounded-lg border border-[#D4AF37]/40 bg-[#D4AF37]/10 px-4 py-3 text-left text-sm text-gray-200 transition-colors hover:border-[#D4AF37] hover:bg-[#D4AF37]/20"
              >
                <span className="block font-medium text-white">下載所選版本</span>
                <span className="mt-1 block text-xs text-gray-400">
                  依照版本選擇與工程圖勾選組成 Pack & Go
                </span>
              </button>
            </div>
            )}
          </div>
        </div>
      </Modal>
    </div>
  );
}
