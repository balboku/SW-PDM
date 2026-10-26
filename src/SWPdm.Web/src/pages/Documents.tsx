import React, { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { Search, Filter, Loader2, Download, PackageOpen, Server, FileText, Archive, History, Link2, AlertCircle, AlertTriangle, RefreshCw, GitBranch, ArrowRight } from 'lucide-react';
import { api, searchDocuments, downloadAssemblyZip, downloadVersion, getVersionThumbnailUrl, checkAssemblyUpdates, getDocumentRelations, getDocumentReferenceUpdates, getDocumentIntegrity } from '../lib/api';
import { isVaultPage, changedOverrides } from '../lib/vault-model';
import { DocumentIntegrityPanel } from '../components/DocumentIntegrityPanel';
import { BomTreeView } from '../components/BomTreeView';
import { CheckOutModal } from '../components/CheckOutModal';
import { CheckInModal } from '../components/CheckInModal';
import { CancelCheckoutModal } from '../components/CancelCheckoutModal';
import { Modal, Drawer } from '../components/ui';
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
  isDrawing?: boolean;
  drawings: RelatedDrawing[];
  isLoading: boolean;
  error: string;
  onRetry: () => void;
  onDownload: (versionId: number, fileName?: string) => void;
}

const RelatedDrawingsPanel: React.FC<RelatedDrawingsPanelProps> = ({
  isDrawing,
  drawings,
  isLoading,
  error,
  onRetry,
  onDownload
}) => (
  <section className="mt-4 rounded-lg border border-gray-800 bg-[#1a1a1a]">
    <div className="flex items-center justify-between border-b border-gray-800 px-4 py-2">
      <h5 className="flex items-center text-xs font-semibold tracking-wider text-gray-300">
        <Link2 size={14} className="mr-2 text-purple-400" />
        {isDrawing ? '參照的 3D 模型' : '關聯 2D 工程圖'}
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
        正在查詢關聯...
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
        <p className="font-medium text-gray-300">{isDrawing ? '尚未找到已確認的 3D 模型參照。' : '尚未找到關聯工程圖。'}</p>
        <p className="mt-1 leading-5">
          下一步：到「首次匯入」使用資料夾批次匯入 3D 與 SLDDRW，
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
              onClick={() => onDownload(drawing.versionId, drawing.originalFileName)}
              className="rounded p-1.5 text-gray-500 hover:bg-gray-800 hover:text-[#D4AF37]"
              title={isDrawing ? '下載模型' : '下載工程圖'}
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
  onDownload: (versionId: number, fileName?: string) => void;
}

const IdentityChangePanel: React.FC<IdentityChangePanelProps> = ({
  identityOrigin,
  derivedDocuments,
  onDownload
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
              onClick={() => onDownload(identityOrigin.sourceVersionId)}
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
                    onClick={() => onDownload(item.targetVersionId)}
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

interface ReferenceUpdateItem {
  childDocumentId: number;
  childDocumentType: string;
  childPartNumber?: string | null;
  referencedVersionId: number;
  referencedVersionNo: number;
  referencedRevisionLabel?: string | null;
  referencedFileName: string;
  currentVersionId: number;
  currentVersionNo: number;
  currentRevisionLabel?: string | null;
  currentFileName?: string | null;
  affectedOccurrenceCount: number;
}

interface ReferenceUpdateStatus {
  documentId: number;
  documentType: string;
  currentVersionId?: number | null;
  checkedOccurrenceCount: number;
  affectedOccurrenceCount: number;
  updateCount: number;
  hasUpdates: boolean;
  updates: ReferenceUpdateItem[];
}

interface ReferenceUpdateWarningProps {
  data: ReferenceUpdateStatus | null;
  isLoading: boolean;
  error: string;
  isCheckedOut: boolean;
  onRetry: () => void;
}

const ReferenceUpdateWarning: React.FC<ReferenceUpdateWarningProps> = ({
  data,
  isLoading,
  error,
  isCheckedOut,
  onRetry
}) => {
  if (isLoading) {
    return (
      <div className="mt-4 flex items-center rounded-lg border border-gray-700 bg-gray-900/60 px-3 py-2 text-xs text-gray-400">
        <Loader2 size={14} className="mr-2 animate-spin text-amber-400" />
        正在檢查子文件版本…
      </div>
    );
  }

  if (error) {
    return (
      <div className="mt-4 rounded-lg border border-amber-800/60 bg-amber-950/30 p-3 text-xs text-amber-100" role="alert">
        <div className="flex items-start gap-2">
          <AlertTriangle size={16} className="mt-0.5 shrink-0 text-amber-400" />
          <div className="min-w-0 flex-1">
            <p className="font-semibold">暫時無法確認子文件版本</p>
            <p className="mt-1 leading-5 text-amber-200/80">{error}</p>
            <button
              type="button"
              onClick={onRetry}
              className="mt-2 inline-flex items-center rounded border border-amber-700/70 bg-amber-900/40 px-2.5 py-1.5 font-medium text-amber-100 hover:bg-amber-900/60"
            >
              <RefreshCw size={12} className="mr-1.5" />
              重新檢查
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (!data?.hasUpdates || data.updates.length === 0) return null;

  return (
    <section
      className="mt-4 overflow-hidden rounded-lg border border-amber-500/70 bg-amber-950/40 shadow-[0_0_0_1px_rgba(245,158,11,0.08),0_10px_28px_rgba(0,0,0,0.28)]"
      aria-live="polite"
    >
      <div className="flex items-start gap-3 border-b border-amber-800/50 px-3 py-3">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-400 text-amber-950 shadow">
          <AlertTriangle size={18} aria-hidden="true" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-bold text-amber-100">
              {data.updateCount} 個子文件有新版可用
            </h3>
            <span className="rounded-full border border-amber-700/70 bg-amber-900/50 px-2 py-0.5 text-[10px] font-semibold text-amber-200">
              {data.affectedOccurrenceCount} 個參照位置
            </span>
          </div>
          <p className="mt-1 text-xs leading-5 text-amber-100/80">
            目前保留原簽入版本。若符合設計基準可維持不變；需要更新時再出庫處理。
          </p>
        </div>
      </div>

      <div className="max-h-48 divide-y divide-amber-900/50 overflow-y-auto">
        {data.updates.map((item) => (
          <div key={`${item.childDocumentId}-${item.referencedVersionId}`} className="px-3 py-2.5">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-semibold text-white" title={item.currentFileName || item.referencedFileName}>
                  {item.currentFileName || item.referencedFileName}
                </p>
                <p className="mt-0.5 truncate text-[10px] text-amber-200/60">
                  {item.childPartNumber || '無料號'} · {item.childDocumentType}
                </p>
              </div>
              {item.affectedOccurrenceCount > 1 && (
                <span className="shrink-0 rounded bg-amber-900/60 px-1.5 py-0.5 text-[10px] text-amber-200">
                  {item.affectedOccurrenceCount} 處
                </span>
              )}
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-1.5 text-[11px]">
              <span className="rounded border border-red-900/60 bg-red-950/40 px-2 py-1 text-red-200">
                目前引用 Ver. {item.referencedVersionNo} / Rev. {item.referencedRevisionLabel || '-'}
              </span>
              <ArrowRight size={12} className="text-amber-400" aria-hidden="true" />
              <span className="rounded border border-emerald-800/60 bg-emerald-950/40 px-2 py-1 text-emerald-200">
                子文件目前 Ver. {item.currentVersionNo} / Rev. {item.currentRevisionLabel || '-'}
              </span>
            </div>
          </div>
        ))}
      </div>

      <div className="border-t border-amber-800/50 bg-amber-950/50 px-3 py-3 text-xs leading-5 text-amber-50">
        <p className="font-semibold">
          {isCheckedOut
            ? '下一步：在 SolidWorks 更新參照並儲存父文件，再使用下方「入庫」。'
            : '下一步：先使用下方「出庫」，到 SolidWorks 更新參照後再重新入庫。'}
        </p>
        <p className="mt-1 text-amber-200/70">歷史版本保留原參照，不會被自動改寫。</p>
      </div>
    </section>
  );
};

export default function Documents() {
  const [query, setQuery] = useState('');
  const [documents, setDocuments] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [documentLoadError, setDocumentLoadError] = useState('');
  const [selectedDoc, setSelectedDoc] = useState<any>(null);
  const [detailError, setDetailError] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [downloadState, setDownloadState] = useState<{ versionId: number; fileName: string; status: 'loading' | 'success' | 'error' } | null>(null);
  const downloadFeedback = useRef<HTMLDivElement>(null);
  const downloadRequestId = useRef(0);
  const [actionMessage, setActionMessage] = useState('');
  const [appliedFilters, setAppliedFilters] = useState({ query: '', type: 'All', status: 'All', integrity: 'All' });
  const [selectedIntegrity, setSelectedIntegrity] = useState('All');
  const [integrityData, setIntegrityData] = useState<any>(null);
  const detailRequestId = useRef(0);
  const [isCheckOutModalOpen, setIsCheckOutModalOpen] = useState(false);
  const [isCheckInModalOpen, setIsCheckInModalOpen] = useState(false);
  const [isUndoModalOpen, setIsUndoModalOpen] = useState(false);
  const [cancelActor, setCancelActor] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [pageInfo, setPageInfo] = useState<any>({ totalCount: 0, totalPages: 1, newerReferenceDocumentCount: 0, missingReferenceDocumentCount: 0, checkedOutCount: 0 });
  const searchRequestId = useRef(0);
  const [isPackageBusy, setIsPackageBusy] = useState(false);
  const [legacyApi, setLegacyApi] = useState(false);
  const [packAndGoTarget, setPackAndGoTarget] = useState<number | null>(null);
  const [isPackageOptionsLoading, setIsPackageOptionsLoading] = useState(false);
  const packageTargetRef = useRef<number | null>(null);
  const packageErrorRef = useRef<HTMLDivElement>(null);
  const [packAndGoUpdates, setPackAndGoUpdates] = useState<any[]>([]);
  const [packAndGoSelections, setPackAndGoSelections] = useState<Record<number, number>>({});
  const [packAndGoRelatedDrawings, setPackAndGoRelatedDrawings] = useState<RelatedDrawing[]>([]);
  const [packAndGoPreviewError, setPackAndGoPreviewError] = useState('');
  const [packageProblem, setPackageProblem] = useState<any>(null);
  const [includeDrawings, setIncludeDrawings] = useState(true);
  const [relationData, setRelationData] = useState<any>(null);
  const [isRelationLoading, setIsRelationLoading] = useState(false);
  const [relationError, setRelationError] = useState('');
  const [referenceUpdateData, setReferenceUpdateData] = useState<ReferenceUpdateStatus | null>(null);
  const [isReferenceUpdateLoading, setIsReferenceUpdateLoading] = useState(false);
  const [referenceUpdateError, setReferenceUpdateError] = useState('');
  const [detailTab, setDetailTab] = useState<'structure' | 'history'>('structure');
  const [selectedType, setSelectedType] = useState('All');
  const [selectedStatus, setSelectedStatus] = useState('All');
  const selectedDocumentIdRef = useRef<number | null>(null);

  const fetchDocuments = async (searchQuery = appliedFilters.query, type = appliedFilters.type, status = appliedFilters.status, targetPage = page, integrity = appliedFilters.integrity) => {
    const requestId = ++searchRequestId.current;
    // Retry the last submitted filters, even if that request failed. Draft text is not applied by paging.
    setAppliedFilters({ query: searchQuery, type, status, integrity });
    setPage(targetPage);
    setIsLoading(true);
    setDocumentLoadError('');
    try {
      const data = await searchDocuments(searchQuery, type, status, targetPage, integrity);
      if (requestId !== searchRequestId.current) return;
      if (Array.isArray(data)) {
        setLegacyApi(true); setDocuments(data); setPage(1);
        setPageInfo({ totalCount: data.length, totalPages: 1, newerReferenceDocumentCount: data.filter(d => d.referenceUpdateCount > 0).length,
          missingReferenceDocumentCount: 0, checkedOutCount: data.filter(d => d.checkedOutBy).length });
        return;
      }
      if (!isVaultPage(data)) throw new Error('分頁或統計資料格式不正確');
      setLegacyApi(false);
      setDocuments(data.items); setPageInfo(data); setPage(data.page);
    } catch (error) {
      console.error('Failed to search documents', error);
      if (requestId === searchRequestId.current) setDocumentLoadError('請按「重新載入清單」再試。若持續失敗，請確認 PDM 服務與網路連線；不需要重新匯入圖檔。');
    } finally {
      if (requestId === searchRequestId.current) setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchDocuments();
  }, []);

  useEffect(() => {
    if (packAndGoPreviewError) packageErrorRef.current?.scrollIntoView({ block: 'nearest' });
  }, [packAndGoPreviewError]);
  useEffect(() => { downloadFeedback.current?.scrollIntoView({ block: 'nearest' }); }, [downloadState]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchDocuments(query, selectedType, selectedStatus, 1, selectedIntegrity);
  };

  const clearFilters = () => {
    setQuery(''); setSelectedType('All'); setSelectedStatus('All'); setSelectedIntegrity('All');
    void fetchDocuments('', 'All', 'All', 1, 'All');
  };
  const hasActiveFilters = Boolean(appliedFilters.query.trim() || appliedFilters.type !== 'All' || appliedFilters.status !== 'All' || appliedFilters.integrity !== 'All');
  const closeDetails = () => { selectedDocumentIdRef.current = null; detailRequestId.current++; downloadRequestId.current++; setSelectedDoc(null); };

  const handleDownload = async (versionId: number, fileName?: string) => {
    if (downloadState?.status === 'loading') return;
    const requestId = ++downloadRequestId.current;
    const extension = ({ Part: '.sldprt', Assembly: '.sldasm', Drawing: '.slddrw' } as Record<string, string>)[selectedDoc?.documentType] || '';
    const originalName = fileName || selectedDoc?.fileName || `version_${versionId}`;
    const fallbackFileName = fileName || /\.sld(prt|asm|drw)$/i.test(originalName) ? originalName : originalName + extension;
    setDownloadState({ versionId, fileName: fallbackFileName, status: 'loading' });
    try {
      await downloadVersion(versionId, fallbackFileName);
      if (downloadRequestId.current === requestId) setDownloadState({ versionId, fileName: fallbackFileName, status: 'success' });
    } catch {
      if (downloadRequestId.current === requestId) setDownloadState({ versionId, fileName: fallbackFileName, status: 'error' });
    }
  };

  const loadDocumentRelations = async (documentId: number) => {
    setIsRelationLoading(true);
    setRelationError('');
    try {
      const data = await getDocumentRelations(documentId);
      if (selectedDocumentIdRef.current === documentId) setRelationData(data);
    } catch (error: any) {
      console.error('Failed to load document relations', error);
      if (selectedDocumentIdRef.current === documentId) {
        setRelationData(null); setRelationError(error.response?.data?.detail || error.message || '無法載入關聯資料');
      }
    } finally {
      if (selectedDocumentIdRef.current === documentId) setIsRelationLoading(false);
    }
  };

  const loadDocumentReferenceUpdates = async (documentId: number) => {
    setIsReferenceUpdateLoading(true);
    setReferenceUpdateError('');
    try {
      const data = await getDocumentReferenceUpdates(documentId);
      if (selectedDocumentIdRef.current === documentId) {
        setReferenceUpdateData(data);
      }
    } catch (error) {
      console.error('Failed to check document reference updates', error);
      if (selectedDocumentIdRef.current === documentId) {
        setReferenceUpdateData(null);
        setReferenceUpdateError('更新狀態尚未載入，請確認服務連線後重試。');
      }
    } finally {
      if (selectedDocumentIdRef.current === documentId) {
        setIsReferenceUpdateLoading(false);
      }
    }
  };

  const handleSelectDocument = async (doc: any) => {
    const request = ++detailRequestId.current;
    setIntegrityData(null);
    selectedDocumentIdRef.current = doc.documentId;
    setSelectedDoc(doc);
    downloadRequestId.current++;
    setActionMessage('');
    setDetailError(false);
    setDetailLoading(true);
    setDownloadState(null);
    setDetailTab('structure');
    setRelationData(null);
    setRelationError('');
    setReferenceUpdateData(null);
    setReferenceUpdateError('');
    void loadDocumentRelations(doc.documentId);
    void loadDocumentReferenceUpdates(doc.documentId);

    try {
      const [response, evidence] = await Promise.all([api.get(`/api/documents/${doc.documentId}`), getDocumentIntegrity(doc.documentId)]);
      if (request !== detailRequestId.current || selectedDocumentIdRef.current !== doc.documentId) return;
      if (evidence.documentId !== doc.documentId || evidence.evidenceLevel !== 'StoredSnapshot' || evidence.nativeFileChecked !== false ||
          !Array.isArray(evidence.references) || !Array.isArray(evidence.properties)) throw new Error('無法確認資料版本');
      setIntegrityData(evidence);
      setSelectedDoc((current: any) => (
        current?.documentId === doc.documentId
          ? { ...doc, ...response.data, checkedOutBy: evidence.checkedOutBy, checkedOutAt: evidence.checkedOutAt,
              partName: evidence.propertySources?.partName?.value, propertySources: evidence.propertySources,
              missingReferenceCount: evidence.unresolvedCount + (evidence.currentVersionMissing ? 1 : 0) }
          : current
      ));
    } catch (error) {
      console.error('Failed to load document details', error);
      if (selectedDocumentIdRef.current === doc.documentId && request === detailRequestId.current) setDetailError(true);
    } finally {
      if (selectedDocumentIdRef.current === doc.documentId && request === detailRequestId.current) setDetailLoading(false);
    }
  };

  const handlePackAndGoClick = async (versionId: number) => {
    packageTargetRef.current = versionId;
    setIsPackageOptionsLoading(true);
    setPackAndGoTarget(versionId);
    setPackAndGoUpdates([]);
    setPackAndGoSelections({});
    setPackAndGoRelatedDrawings([]);
    setPackAndGoPreviewError('');
    setPackageProblem(null);
    setIncludeDrawings(true);

    try {
      const result = await checkAssemblyUpdates(versionId);
      if (packageTargetRef.current !== versionId) return;
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
      if (packageTargetRef.current !== versionId) return;
      console.error('Failed to check package updates', error);
      setIncludeDrawings(false);
      setPackAndGoPreviewError(
        error.response?.data?.detail || error.message || '無法載入 Pack & Go 預覽'
      );
    } finally { if (packageTargetRef.current === versionId) setIsPackageOptionsLoading(false); }
  };

  const closePackAndGoModal = () => {
    packageTargetRef.current = null;
    setIsPackageOptionsLoading(false);
    setPackAndGoTarget(null);
    setPackAndGoUpdates([]);
    setPackAndGoSelections({});
    setPackAndGoRelatedDrawings([]);
    setPackAndGoPreviewError('');
    setIncludeDrawings(true);
  };

  const handlePackAndGoDownload = async (mode: 'original' | 'selected') => {
    if (!packAndGoTarget || isPackageBusy || isPackageOptionsLoading) return;
    setIsPackageBusy(true);
    setPackAndGoPreviewError('');
    const overrides = mode === 'selected'
      ? changedOverrides(packAndGoSelections)
      : [];
    try {
      await downloadAssemblyZip(packAndGoTarget, false, overrides, includeDrawings);
      closePackAndGoModal();
    } catch (error: any) {
      let problem = error.response?.data;
      if (error.response?.data instanceof Blob) {
        try { problem = JSON.parse(await error.response.data.text()); } catch { problem = null; }
      }
      setPackageProblem(problem);
      setPackAndGoPreviewError(problem?.detail || '套件尚未下載，請確認參照或重新選版後再試。');
    } finally { setIsPackageBusy(false); }
  };

  const canPackAndGo = selectedDoc && ['Assembly', 'Drawing'].includes(selectedDoc.documentType);
  const versionHistory = Array.isArray(selectedDoc?.versions)
    ? [...selectedDoc.versions].sort((a: any, b: any) => (b.versionNo ?? 0) - (a.versionNo ?? 0))
    : [];
  const referenceAlertDocuments = documents.filter((doc) => Number(doc.referenceUpdateCount) > 0);
  const referenceAlertOccurrenceCount = referenceAlertDocuments.reduce(
    (total, doc) => total + Number(doc.affectedReferenceOccurrenceCount || 0),
    0
  );
  const displayedDocuments = [...documents].sort((left, right) => (
    Number(Number(right.missingReferenceCount) > 0) - Number(Number(left.missingReferenceCount) > 0) ||
    Number(Number(right.referenceUpdateCount) > 0) - Number(Number(left.referenceUpdateCount) > 0)
  ));
  const checkedOutDocumentCount = pageInfo.checkedOutCount;
  const summaryValue = (value: number) => documentLoadError ? '—' : value;
  const vaultSummaryMetrics = [
    { label: legacyApi ? '已載入（非全部）' : '全部結果', value: summaryValue(pageInfo.totalCount), icon: FileText, tone: 'text-slate-600', valueTone: 'text-slate-900' },
    { label: '缺失／待確認', value: legacyApi ? '未檢測' : summaryValue(pageInfo.missingReferenceDocumentCount), icon: AlertTriangle, tone: 'text-amber-600', valueTone: 'text-amber-700' },
    { label: '已出庫', value: summaryValue(checkedOutDocumentCount), icon: Lock, tone: 'text-orange-600', valueTone: 'text-orange-700' },
    { label: '有新版參照', value: summaryValue(pageInfo.newerReferenceDocumentCount), icon: History, tone: 'text-amber-600', valueTone: 'text-amber-700' }
  ];

  return (
    <div className="flex h-full flex-col animate-in fade-in duration-500">
      <div className="mb-5 flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 tracking-tight flex items-center sm:text-3xl">
            <Server className="mr-3 text-[#D4AF37]" size={28} />
            圖檔中心 (Vault)
          </h1>
          {legacyApi && <details className="mt-2 max-w-xl text-sm text-amber-800"><summary className="cursor-pointer">清單最多顯示 50 筆，請使用搜尋縮小範圍</summary><p className="mt-2">目前 API 回傳有限筆數，並非全部圖面。未檢測不代表沒有缺失；批次復原需有本人批次紀錄與成功預覽，不會自動推算舊出庫。</p></details>}
          <p className="text-gray-600 mt-2 text-sm">選取圖檔查看明細、下載或辦理出入庫。</p>
          <Link to="/ingest" className="mt-3 inline-flex rounded-md bg-gray-900 px-3 py-2 text-sm font-medium text-white focus-visible:ring-2 focus-visible:ring-blue-500">首次匯入 CAD</Link>
        </div>
        <div
          className="grid w-full grid-cols-2 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm lg:grid-cols-4 xl:w-auto xl:min-w-[34rem]"
          aria-label="目前 Vault 搜尋結果摘要"
        >
          {vaultSummaryMetrics.map((metric, index) => {
            const MetricIcon = metric.icon;
            return (
              <div
                key={metric.label}
                className={`flex min-w-0 items-center gap-2.5 border-gray-100 px-3 py-2.5 ${
                  index < 2 ? 'border-b' : ''
                } ${index % 2 === 0 ? 'border-r' : ''} lg:border-b-0 ${index < 3 ? 'lg:border-r' : ''}`}
              >
                <MetricIcon size={17} className={`shrink-0 ${metric.tone}`} aria-hidden="true" />
                <div className="min-w-0">
                  <p className="text-xs font-medium tracking-wide text-gray-600">{metric.label}</p>
                  <p className={`text-lg font-bold leading-5 ${documentLoadError ? 'text-gray-400' : metric.valueTone}`}>
                    {isLoading ? '…' : metric.value}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="flex flex-col gap-6 flex-1 min-h-0">
        {/* 左側列表區 */}
        <div className="flex w-full min-h-0 min-w-0 flex-1 self-start flex-col overflow-hidden rounded-xl border border-gray-800 bg-[#121212] shadow-2xl">
          <div className="p-4 border-b border-gray-800 bg-gray-900/50">
            <form onSubmit={handleSearch} className="grid grid-cols-2 gap-2 xl:flex">
              <div className="relative col-span-2 min-w-0 xl:flex-1">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-500">
                  <Search size={18} />
                </div>
                <input
                  aria-label="搜尋檔名、品名、圖號或料號"
                  maxLength={200}
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="搜尋檔名、品名、圖號、料號..."
                  className="block w-full pl-10 pr-3 py-2 border border-gray-700 rounded-lg leading-5 bg-gray-800/50 text-gray-100 placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-[#D4AF37] focus:border-[#D4AF37] sm:text-sm transition-all"
                />
              </div>

              <select
                aria-label="圖檔類型"
                value={selectedType}
                onChange={(e) => {
                  setSelectedType(e.target.value);
                  fetchDocuments(query, e.target.value, selectedStatus, 1, selectedIntegrity);
                }}
                className="w-full bg-gray-800/50 text-gray-200 border border-gray-700 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[#D4AF37] sm:w-auto"
              >
                <option value="All">所有類型</option>
                <option value="Part">零件 (Part)</option>
                <option value="Assembly">組合件 (Asm)</option>
                <option value="Drawing">工程圖 (Drw)</option>
              </select>

              <select
                aria-label="出庫狀態"
                value={selectedStatus}
                onChange={(e) => {
                  setSelectedStatus(e.target.value);
                  fetchDocuments(query, selectedType, e.target.value, 1, selectedIntegrity);
                }}
                className="w-full bg-gray-800/50 text-gray-200 border border-gray-700 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[#D4AF37] sm:w-auto"
              >
                <option value="All">所有狀態</option>
                <option value="Available">可用 (Available)</option>
                <option value="CheckedOut">已出庫 (Locked)</option>
              </select>

              <select aria-label="參照異常篩選" value={selectedIntegrity} disabled={legacyApi || isLoading}
                onChange={event => { setSelectedIntegrity(event.target.value); void fetchDocuments(query, selectedType, selectedStatus, 1, event.target.value); }}
                className="w-full rounded-lg border border-gray-700 bg-gray-800/50 px-3 py-2 text-sm text-gray-200 sm:w-auto">
                <option value="All">所有參照狀態</option><option value="Missing">缺失／待確認</option><option value="Newer">有新版參照</option>
              </select>

              <button
                type="submit"
                className="col-span-2 inline-flex w-full items-center justify-center px-4 py-2 border border-transparent text-sm font-semibold rounded-lg text-gray-950 bg-[#D4AF37] hover:bg-[#c2a033] shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-900 focus:ring-[#D4AF37] disabled:opacity-50 xl:w-auto"
                disabled={isLoading}
              >
                {isLoading ? <Loader2 size={16} className="animate-spin mr-2" /> : <Filter size={16} className="mr-2" />}
                篩選
              </button>
            </form>
            {documentLoadError && !isLoading && (
              <div
                className="mt-3 flex flex-col gap-3 rounded-lg border border-rose-500/60 bg-rose-950/40 px-3 py-3 text-rose-100 shadow-[0_0_0_1px_rgba(244,63,94,0.06)] sm:flex-row sm:items-center sm:justify-between"
                role="alert"
              >
                <div className="flex min-w-0 items-start gap-2.5">
                  <AlertCircle size={19} className="mt-0.5 shrink-0 text-rose-400" aria-hidden="true" />
                  <div className="min-w-0">
                    <p className="text-sm font-bold">目前無法載入圖面清單</p>
                    <p className="mt-0.5 text-xs leading-5 text-rose-200/80">{documentLoadError}</p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => void fetchDocuments()}
                  className="inline-flex shrink-0 items-center justify-center rounded-lg border border-rose-400/50 bg-rose-500/15 px-3 py-2 text-xs font-semibold text-rose-100 transition-colors hover:bg-rose-500/25 focus:outline-none focus:ring-2 focus:ring-rose-400 disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={isLoading}
                >
                  <RefreshCw size={14} className="mr-1.5" aria-hidden="true" />
                  重新載入清單
                </button>
              </div>
            )}
            {referenceAlertDocuments.length > 0 && !isLoading && !documentLoadError && (
              <div
                className="mt-3 flex flex-col gap-2 rounded-lg border border-amber-500/60 bg-amber-950/40 px-3 py-2.5 text-amber-100 shadow-[0_0_0_1px_rgba(245,158,11,0.05)] sm:flex-row sm:items-center sm:justify-between"
                role="status"
              >
                <div className="flex min-w-0 items-start gap-2">
                  <AlertTriangle size={18} className="mt-0.5 shrink-0 text-amber-400" aria-hidden="true" />
                  <div className="min-w-0">
                    <p className="text-sm font-bold">
                      本頁有 {referenceAlertDocuments.length} 份文件的參照有新版可用
                    </p>
                    <p className="mt-0.5 text-xs leading-5 text-amber-200/75">
                      共 {referenceAlertOccurrenceCount} 個位置；保留舊版不一定是錯誤，請依設計基準確認是否更新。
                    </p>
                  </div>
                </div>
                <span className="shrink-0 self-start rounded-full border border-amber-700/70 bg-amber-900/50 px-2.5 py-1 text-xs font-semibold text-amber-200 sm:self-auto">
                  待確認 {referenceAlertDocuments.length}
                </span>
              </div>
            )}
            {hasActiveFilters && documents.length > 0 && <button type="button" onClick={clearFilters} disabled={isLoading} className="mt-3 rounded px-2 py-1 text-sm text-gray-200 underline">清除搜尋條件</button>}
          </div>

          {isLoading && <p role="status" className="px-4 py-3 text-sm text-gray-300">正在載入圖檔清單，請稍候…</p>}
          {documents.length === 0 && !isLoading && !documentLoadError && <section className="px-5 py-10 text-center" aria-label="圖檔清單空狀態">
            <p className="text-lg font-semibold text-gray-100">{hasActiveFilters ? '找不到符合條件的圖檔' : '圖庫尚無圖檔'}</p>
            <p className="mt-2 text-sm text-gray-300">{hasActiveFilters ? '請清除搜尋條件或換個關鍵字，不需要重新匯入。' : '請先匯入 SolidWorks 圖檔，再回這裡搜尋與查看關聯。'}</p>
            {hasActiveFilters ? <button type="button" onClick={clearFilters} className="mt-5 rounded-md bg-[#D4AF37] px-4 py-3 text-sm font-semibold text-gray-950">清除搜尋條件</button>
              : <Link to="/ingest" className="mt-5 inline-block rounded-md bg-[#D4AF37] px-4 py-3 text-sm font-semibold text-gray-950">首次匯入 CAD</Link>}
          </section>}
          <div key={page} className="max-h-[60vh] flex-1 overflow-auto overscroll-contain" aria-busy={isLoading}>
            <div className="divide-y divide-gray-700 lg:hidden">
              {displayedDocuments.map(doc => <button key={doc.documentId} type="button" data-document-row
                onClick={() => handleSelectDocument(doc)} aria-label={`查看 ${doc.fileName} 明細`}
                className={`block w-full p-4 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-blue-400 ${selectedDoc?.documentId === doc.documentId ? 'bg-gray-800' : 'hover:bg-gray-900'}`}>
                <p className="break-all text-sm font-semibold text-gray-100">{doc.fileName}</p>
                <p className="mt-1 break-words text-xs text-gray-300">品名：{doc.partName || '未填寫／待確認'}</p>
                <dl className="mt-3 grid grid-cols-2 gap-3 text-xs">
                  <div className="min-w-0"><dt className="text-gray-400">圖號</dt><dd className="mt-1 break-all font-mono text-gray-100">{doc.drawingNumber || '未填寫'}</dd></div>
                  <div className="min-w-0"><dt className="text-gray-400">料號</dt><dd className="mt-1 break-all font-mono text-gray-100">{doc.partNumber || '未填寫'}</dd></div>
                </dl>
                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                  <span className={doc.checkedOutBy ? 'text-orange-300' : 'text-green-300'}>{doc.checkedOutBy ? `已出庫：${doc.checkedOutBy}` : '可用'}</span>
                  <span className="text-gray-300">{doc.documentType} · Ver. {doc.currentVersionNo ?? '-'} / Rev. {doc.revisionLabel || '-'}</span>
                  {Number(doc.missingReferenceCount) > 0 && <span className="text-red-300">缺失／待確認 {doc.missingReferenceCount} 處</span>}
                  {Number(doc.referenceUpdateCount) > 0 && <span className="text-amber-300">有新版參照 {doc.referenceUpdateCount} 項</span>}
                </div>
                <span className="mt-3 block text-xs font-medium text-blue-300">查看明細 →</span>
              </button>)}
            </div>
            <table className="hidden w-full min-w-[580px] table-fixed divide-y divide-gray-800 text-sm lg:table">
              <thead className="bg-[#1a1a1a] sticky top-0 z-10 w-full whitespace-nowrap">
                <tr>
                  <th scope="col" className="w-[32%] px-3 py-3 text-left text-xs font-medium text-gray-300">檔名／參照狀況</th>
                  <th scope="col" className="px-3 py-3 text-left text-xs font-medium text-gray-300">圖號</th>
                  <th scope="col" className="px-3 py-3 text-left text-xs font-medium text-gray-300">料號</th>
                  <th scope="col" className="hidden px-3 py-3 text-left text-xs font-medium text-gray-300 xl:table-cell">類型</th>
                  <th scope="col" className="px-3 py-3 text-center text-xs font-medium text-gray-300">版本／版次</th>
                  <th scope="col" className="px-3 py-3 text-left text-xs font-medium text-gray-300">狀態</th>
                  <th scope="col" className="hidden px-3 py-3 text-left text-xs font-medium text-gray-300 2xl:table-cell">更新時間</th>
                </tr>
              </thead>
              <tbody className="bg-[#121212] divide-y divide-gray-800/50">
                {displayedDocuments.map((doc) => {
                  const isSelected = selectedDoc?.documentId === doc.documentId;
                  const isCheckedOutWip = Boolean(
                    doc.checkedOutBy &&
                    String(doc.currentLifecycleState || '').toLowerCase() === 'wip'
                  );
                  const hasReferenceUpdates = Number(doc.referenceUpdateCount) > 0;

                  return (
                  <tr 
                    data-document-row
                    key={doc.documentId}
                    onClick={() => handleSelectDocument(doc)}
                    className={`cursor-pointer transition-colors ${
                      isSelected
                        ? 'bg-gray-800/80 border-l-2 border-[#D4AF37]'
                        : hasReferenceUpdates
                          ? 'bg-amber-950/20 hover:bg-amber-950/30 border-l-2 border-amber-500/90'
                        : isCheckedOutWip
                          ? 'bg-orange-950/20 hover:bg-orange-950/30 border-l-2 border-orange-500/80'
                          : 'hover:bg-gray-800/40 border-l-2 border-transparent'
                    }`}
                  >
                    <td className="px-3 py-4 text-gray-200 font-medium">
                      <div className="flex min-w-0 items-center">
                      <VersionThumbnail versionId={doc.currentVersionId} className="mr-3 h-10 w-10" />
                      <div className="min-w-0">
                        <button type="button" aria-label={`查看 ${doc.fileName} 明細`} title={doc.fileName}
                          onClick={event => { event.stopPropagation(); void handleSelectDocument(doc); }}
                          className="block max-w-full truncate text-left hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-400">{doc.fileName}</button>
                        <p className="mt-1 truncate text-xs font-normal text-gray-400">品名：{doc.partName || '未填寫／待確認'}</p>
                        {Number(doc.missingReferenceCount) > 0 && <span className="mt-1 mr-2 inline-flex rounded border border-red-600/60 bg-red-950/40 px-1.5 py-0.5 text-[10px] text-red-200">
                          缺失／待確認 {doc.missingReferenceCount} 處
                        </span>}
                        {hasReferenceUpdates && (
                          <span
                            className="mt-1 inline-flex items-center gap-1 rounded border border-amber-600/60 bg-amber-900/40 px-1.5 py-0.5 text-[10px] font-semibold leading-4 text-amber-200"
                            title={`目前父文件有 ${doc.referenceUpdateCount} 個舊版子文件參照項目，共 ${doc.affectedReferenceOccurrenceCount} 個參照位置；選取此列查看處理步驟。`}
                          >
                            <AlertTriangle size={11} aria-hidden="true" />
                            有新版 {doc.referenceUpdateCount} 項 · {doc.affectedReferenceOccurrenceCount} 處
                          </span>
                        )}
                      </div>
                      </div>
                    </td>
                    <td title={doc.drawingNumber || ''} className="truncate px-3 py-4 font-mono text-xs text-gray-200">
                      {doc.drawingNumber || '-'}
                    </td>
                    <td title={doc.partNumber || ''} className="truncate px-3 py-4 text-xs text-gray-300">{doc.partNumber || '-'}</td>
                    <td className="hidden px-3 py-4 xl:table-cell">
                      <span className={`px-2 py-1 inline-flex text-xs leading-5 font-semibold rounded-md ${
                        doc.documentType === 'Assembly' ? 'bg-yellow-900/40 text-yellow-500' : 
                        doc.documentType === 'Drawing' ? 'bg-purple-900/40 text-purple-400' : 
                        'bg-blue-900/40 text-blue-400'
                      }`}>
                        {doc.documentType}
                      </span>
                    </td>
                    <td className="px-3 py-4 whitespace-nowrap text-center">
                      <p className="text-xs font-semibold text-gray-200">Ver. {doc.currentVersionNo ?? '-'}</p>
                      <p className="mt-0.5 text-[10px] text-gray-500">Rev. {doc.revisionLabel || '-'}</p>
                    </td>
                    <td className="px-3 py-4">
                      {doc.checkedOutBy ? (
                        <span className="flex items-center gap-1 text-[10px] text-orange-400 bg-orange-400/10 px-1.5 py-0.5 rounded border border-orange-400/20">
                          <Lock size={10} className="shrink-0" /> <span className="break-all">已出庫：{doc.checkedOutBy}</span>
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-[10px] text-green-400 bg-green-400/10 px-1.5 py-0.5 rounded border border-green-400/20">
                          <Unlock size={10} /> 可用
                        </span>
                      )}
                    </td>
                    <td className="hidden px-3 py-4 text-gray-400 text-xs 2xl:table-cell">
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
            <nav aria-label="圖檔清單分頁" className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-800 p-3 text-xs text-gray-300">
                <span>{documentLoadError ? '清單尚未載入，筆數未確認' : legacyApi ? `已載入 ${documents.length} 份（非全部）；請搜尋縮小範圍` : `共 ${pageInfo.totalCount} 份 · 第 ${page} / ${pageInfo.totalPages} 頁 · 本頁 ${documents.length} 份`}</span>
              <div className="flex gap-2">
                <button disabled={isLoading || Boolean(documentLoadError) || page <= 1} onClick={() => fetchDocuments(appliedFilters.query, appliedFilters.type, appliedFilters.status, page - 1)} className="rounded border border-gray-600 px-3 py-2 disabled:opacity-40">上一頁</button>
                <button disabled={isLoading || Boolean(documentLoadError) || page >= pageInfo.totalPages} onClick={() => fetchDocuments(appliedFilters.query, appliedFilters.type, appliedFilters.status, page + 1)} className="rounded border border-gray-600 px-3 py-2 disabled:opacity-40">下一頁</button>
              </div>
            </nav>
        </div>

        {/* 右側預覽區 (Bom Tree / Info) */}
        {selectedDoc && (
          <Drawer isOpen onClose={closeDetails} title="圖檔明細" contentKey={selectedDoc.documentId}>
          <div className="flex min-w-0 flex-col bg-[#121212]">
            <div className="p-4 border-b border-gray-800 bg-[#1a1a1a]">
              {detailLoading && <p role="status" className="mb-3 text-sm text-gray-300">正在載入明細，請稍候再操作…</p>}
              {detailError && <div role="alert" className="mb-3 rounded border border-red-700 bg-red-950/30 p-3 text-sm text-red-200">
                <p>明細尚未完整載入，請重試後再辦理出入庫。</p>
                <button onClick={() => handleSelectDocument(selectedDoc)} className="mt-2 rounded border border-red-400 px-3 py-2">重新載入明細</button>
              </div>}
              {actionMessage && <p role="status" className="mb-3 rounded border border-green-700 bg-green-950/30 p-3 text-sm text-green-200">{actionMessage}</p>}
              {downloadState && <div ref={downloadFeedback} role={downloadState.status === 'error' ? 'alert' : 'status'} className={`mb-3 rounded border p-3 text-sm ${downloadState.status === 'error' ? 'border-red-700 bg-red-950/30 text-red-200' : 'border-gray-600 text-gray-200'}`}>
                {downloadState.status === 'loading' ? '正在準備下載，請稍候…' : downloadState.status === 'success' ? '檔案已交給瀏覽器下載，請查看瀏覽器的下載紀錄。' : '檔案尚未下載，請重試；持續失敗請確認 Vault 原檔是否存在。'}
                {downloadState.status === 'error' && <button onClick={() => handleDownload(downloadState.versionId, downloadState.fileName)} className="mt-2 block rounded border border-red-400 px-3 py-2">重試下載</button>}
              </div>}
              <VersionThumbnail
                versionId={selectedDoc.currentVersionId}
                className="mb-4 h-20 w-full rounded-lg sm:h-28"
                iconSize={48}
              />
              <div className="mb-3 grid grid-cols-2 gap-2">
                <div className="min-w-0 rounded border border-gray-700 bg-gray-800 px-2 py-1.5">
                  <p className="text-[10px] text-gray-500">圖號</p>
                  <p className="break-all font-mono text-xs text-gray-200">
                    {selectedDoc.drawingNumber || '-'}
                  </p>
                </div>
                <div className="min-w-0 rounded border border-gray-700 bg-gray-800 px-2 py-1.5">
                  <p className="text-[10px] text-gray-500">料號</p>
                  <p className="break-all font-mono text-xs text-gray-200">
                    {selectedDoc.partNumber || '-'}
                  </p>
                </div>
              </div>
              <h2 className="text-xl font-bold text-white break-all mb-1">{selectedDoc.fileName}</h2>
              <p className="text-sm text-gray-300">品名：{selectedDoc.partName || '未填寫／待確認'}</p>
              {selectedDoc.propertySources && <details className="mt-2 text-xs text-gray-400">
                <summary className="cursor-pointer">屬性來源與衝突</summary>
                {Object.entries(selectedDoc.propertySources).map(([key, value]: [string, any]) => <p key={key} className="mt-2 break-words">
                  {({ partNumber: '品號', partName: '品名', revision: '版次', drawingNumber: '圖號' } as Record<string, string>)[key]}：
                  {value.conflicts?.length ? `有衝突 ${value.conflicts.join(' / ')}，請統一 CAD 屬性` : value.source || '未設定，請在 CAD 填寫'}
                </p>)}
              </details>}
              {Number(selectedDoc.missingReferenceCount) > 0 && <div className="mt-3 rounded border border-red-800 bg-red-950/30 p-3 text-xs text-red-200">
                <p>有 {selectedDoc.missingReferenceCount} 處參照缺失或待確認，完整套件下載會暫停。</p>
                <p className="mt-1">請查看下方結構樹，補齊原始檔案、在 SolidWorks 確認參照並重新儲存，再由出庫人入庫。</p>
              </div>}
              <div className="flex items-center text-xs text-gray-400 mt-2 space-x-4">
                <span>Rev: <span className="text-gray-200 font-medium">{selectedDoc.revisionLabel}</span></span>
                <span>Type: <span className="text-gray-200">{selectedDoc.documentType}</span></span>
              </div>

              <ReferenceUpdateWarning
                data={referenceUpdateData}
                isLoading={isReferenceUpdateLoading}
                error={referenceUpdateError}
                isCheckedOut={Boolean(selectedDoc.checkedOutBy)}
                onRetry={() => loadDocumentReferenceUpdates(selectedDoc.documentId)}
              />
              <DocumentIntegrityPanel data={integrityData} />

              {/* Checkout Controls */}
              <div className="mt-4 flex flex-wrap gap-2">
                {!selectedDoc.checkedOutBy ? (
                  <button 
                    onClick={() => setIsCheckOutModalOpen(true)}
                    disabled={detailLoading || detailError}
                    className="flex-1 bg-blue-600 hover:bg-blue-700 text-white px-3 py-2 rounded text-xs font-medium flex items-center justify-center gap-2 transition-colors"
                  >
                    <LogOut size={14} /> 出庫 (Check-out)
                  </button>
                ) : (
                  <>
                    <button 
                      onClick={() => setIsCheckInModalOpen(true)}
                      disabled={detailLoading || detailError}
                      className="flex-1 bg-green-600 hover:bg-green-700 text-white px-3 py-2 rounded text-xs font-medium flex items-center justify-center gap-2 transition-colors"
                    >
                      <LogIn size={14} /> 修改後入庫
                    </button>
                    <button 
                      onClick={() => setIsUndoModalOpen(true)}
                      disabled={detailLoading || detailError}
                      className="bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-white border border-gray-700 px-3 py-2 rounded text-xs transition-colors"
                      title="Undo Checkout"
                    >
                      取消出庫
                    </button>
                  </>
                )}
              </div>
              {!selectedDoc.checkedOutBy && <button onClick={() => setIsUndoModalOpen(true)}
                disabled={detailLoading || detailError} className="mt-2 text-xs text-gray-300 underline">檢查出庫狀態</button>}
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

              <div className="min-w-0">
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
                      <BomTreeView key={selectedDoc.currentVersionId} rootVersionId={selectedDoc.currentVersionId} onDownload={handleDownload} />
                    ) : (
                      <div className="rounded-lg border border-gray-800/70 bg-gray-900/30 p-4">
                        <p className="text-sm font-medium text-gray-300">此零件沒有向下 BOM。</p>
                        <p className="mt-1 text-xs leading-5 text-gray-500">
                          下方會反向列出使用此 3D 模型的 2D 工程圖。
                        </p>
                        {selectedDoc.currentVersionId && (
                          <button
                            onClick={() => handleDownload(selectedDoc.currentVersionId)}
                            className="mt-3 flex items-center rounded bg-[#D4AF37] px-3 py-2 text-xs font-medium text-gray-950 shadow transition-colors hover:bg-[#c2a033]"
                          >
                            <Download size={14} className="mr-2" /> 下載 3D 檔案
                          </button>
                        )}
                      </div>
                    )}

                    <RelatedDrawingsPanel
                      isDrawing={selectedDoc.documentType === 'Drawing'}
                      drawings={selectedDoc.documentType === 'Drawing'
                        ? (relationData?.references ?? []).filter((r: any) => r.depth === 1)
                        : (relationData?.drawings ?? [])}
                      isLoading={isRelationLoading}
                      error={relationError}
                      onRetry={() => loadDocumentRelations(selectedDoc.documentId)}
                      onDownload={handleDownload}
                    />

                    <IdentityChangePanel
                      identityOrigin={relationData?.identityOrigin ?? null}
                      derivedDocuments={Array.isArray(relationData?.derivedDocuments) ? relationData.derivedDocuments : []}
                      onDownload={handleDownload}
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
                                  onClick={() => handleDownload(v.versionId, v.originalFileName)}
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
          </Drawer>
        )}
      </div>

      <CheckOutModal 
        isOpen={isCheckOutModalOpen}
        onClose={() => setIsCheckOutModalOpen(false)}
        documentId={selectedDoc?.documentId}
        fileName={selectedDoc?.fileName}
        onSuccess={(checkedOutBy, result) => {
          setActionMessage(`出庫完成，由 ${checkedOutBy} 鎖定。${result?.batchId ? `本次新取得 ${result.acquiredCount} 份鎖、沿用 ${result.reusedLockCount} 份；可在「取消出庫」選擇本人批次復原。` : ''}下一步：下載圖檔，在 SolidWorks 修改並儲存後，再使用「修改後入庫」。`);
          setCancelActor(checkedOutBy);
          setSelectedDoc((current: any) => current
            ? { ...current, checkedOutBy, checkedOutAt: new Date().toISOString() }
            : current);
          void fetchDocuments(query);
          setIsCheckOutModalOpen(false);
        }}
      />
      <CheckInModal 
        isOpen={isCheckInModalOpen}
        onClose={() => setIsCheckInModalOpen(false)}
        onRestore={(actor) => { setCancelActor(actor); setIsUndoModalOpen(true); }}
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
      <CancelCheckoutModal isOpen={isUndoModalOpen} documentId={selectedDoc?.documentId} fileName={selectedDoc?.fileName}
        initialActor={cancelActor} onClose={() => { setIsUndoModalOpen(false); setCancelActor(undefined); }} onSuccess={() => {
          void fetchDocuments();
          if (selectedDoc) void handleSelectDocument(selectedDoc);
        }} />
      <Modal
        isOpen={packAndGoTarget !== null}
        onClose={() => !isPackageBusy && closePackAndGoModal()}
        title="Pack & Go 版本選擇"
      >
        <div className="space-y-4">
          <p className="text-sm text-gray-300">
            下載前確認套件範圍。預設保留原簽入 BOM，並包含目前找到的關聯 2D 工程圖。
          </p>
          {isPackageOptionsLoading && <p role="status" className="text-sm text-gray-300">正在載入版本及工程圖，請稍候…</p>}

          {packAndGoPreviewError && (
            <div ref={packageErrorRef} className="break-all rounded border border-red-900/40 bg-red-950/20 p-3 text-xs text-red-300" role="alert">
              <p className="font-medium">套件尚未建立；請修正參照或調整版本／工程圖範圍後重試。</p>
              <p className="mt-1 text-red-400">{packAndGoPreviewError}</p>
              {packageProblem?.code && <details className="mt-2"><summary className="cursor-pointer">查看受影響檔案與錯誤碼</summary>
                <p className="mt-2">{packageProblem.code}</p>
                {Array.isArray(packageProblem.issues) && packageProblem.issues.map((issue: any, index: number) => <p key={index} className="mt-1 break-words">{issue.fileName}：{issue.message}</p>)}
              </details>}
            </div>
          )}

          <label className={`block rounded-lg border p-3 ${
            isPackageBusy
              ? 'cursor-not-allowed border-gray-800 bg-gray-900/40 opacity-60'
              : 'cursor-pointer border-purple-900/40 bg-purple-950/20 hover:border-purple-700/60'
          }`}>
            <div className="flex items-start gap-3">
              <input
                type="checkbox"
                checked={includeDrawings}
                disabled={isPackageBusy || isPackageOptionsLoading}
                onChange={(event) => { setIncludeDrawings(event.target.checked); }}
                className="mt-0.5 h-4 w-4 accent-purple-500"
              />
              <div className="min-w-0">
                <p className="text-sm font-medium text-white">
                  包含關聯 2D 工程圖（{isPackageOptionsLoading ? '載入中' : packAndGoRelatedDrawings.length}）
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
              disabled={isPackageBusy || isPackageOptionsLoading}
              className="w-full rounded-lg border border-gray-700 bg-gray-800 px-4 py-3 text-left text-sm text-gray-200 transition-colors hover:border-[#D4AF37]/60 hover:bg-gray-700"
            >
              <span className="block font-medium text-white">{isPackageBusy ? '正在檢查與建立套件…' : '下載原簽入版本'}</span>
              <span className="mt-1 block text-xs text-gray-400">
                維持當初 BOM；工程圖依上方勾選加入
              </span>
            </button>

            {packAndGoUpdates.length === 0 ? (
              <p className="rounded-lg border border-green-900/30 bg-green-950/20 px-3 py-2 text-xs text-green-300">
                尚無其他版本選項；可依目前範圍下載。
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
                      disabled={isPackageBusy}
                      aria-label={`${item.originalFileName} 的套件版本`}
                      onChange={(e) => { setPackAndGoSelections((current) => ({
                        ...current,
                        [item.sourceVersionId]: Number(e.target.value)
                      })); }}
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
                disabled={isPackageBusy || isPackageOptionsLoading}
                className="mt-4 w-full rounded-lg border border-[#D4AF37]/40 bg-[#D4AF37]/10 px-4 py-3 text-left text-sm text-gray-200 transition-colors hover:border-[#D4AF37] hover:bg-[#D4AF37]/20"
              >
                <span className="block font-medium text-white">下載所選版本</span>
                <span className="mt-1 block text-xs text-gray-400">
                  依照版本選擇與工程圖勾選組成 Pack & Go
                </span>
              </button>
              <button disabled={isPackageBusy} className="mt-2 text-xs text-gray-300 underline"
                onClick={() => { setPackAndGoSelections({}); }}>重設版本選擇</button>
            </div>
            )}

          </div>
        </div>
      </Modal>
    </div>
  );
}
