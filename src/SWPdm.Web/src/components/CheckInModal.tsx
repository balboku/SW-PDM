import React, { useState } from 'react';
import { Modal, Button } from './ui';
import { getDocumentRelations, ingestCad, uploadTempFile } from '../lib/api';
import {
  AlertCircle,
  ArrowRight,
  CheckCircle,
  FileWarning,
  GitBranch,
  Loader2,
  RefreshCw,
  ShieldCheck,
  UploadCloud
} from 'lucide-react';

interface CheckInModalProps {
  isOpen: boolean;
  onClose: () => void;
  documentId: number;
  fileName: string;
  onSuccess: () => void;
}

interface IdentityMismatch {
  code: string;
  targetDocumentId: number;
  expectedPartNumber?: string;
  expectedDocumentType: string;
  actualPartNumber?: string;
  actualDocumentType: string;
  canCreateNewDocument: boolean;
  partNumberChangeBlockReason?: string;
  detail?: string;
}

interface RelationImpact {
  references: unknown[];
  drawings: unknown[];
  whereUsed: unknown[];
}

type CheckInStatus =
  | 'idle'
  | 'uploading'
  | 'processing'
  | 'identity-mismatch'
  | 'success'
  | 'error';

export const CheckInModal: React.FC<CheckInModalProps> = ({
  isOpen,
  onClose,
  documentId,
  fileName,
  onSuccess
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [stagedPath, setStagedPath] = useState('');
  const [status, setStatus] = useState<CheckInStatus>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [userName, setUserName] = useState('User');
  const [changeReason, setChangeReason] = useState('');
  const [identityMismatch, setIdentityMismatch] = useState<IdentityMismatch | null>(null);
  const [relationImpact, setRelationImpact] = useState<RelationImpact | null>(null);
  const [impactStatus, setImpactStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle');
  const [result, setResult] = useState<any>(null);

  const isBusy = status === 'uploading' || status === 'processing';

  const resetSelectedFile = () => {
    setFile(null);
    setStagedPath('');
    setStatus('idle');
    setErrorMessage('');
    setIdentityMismatch(null);
    setRelationImpact(null);
    setImpactStatus('idle');
    setResult(null);
  };

  const handleFileSelected = (selectedFile: File | null) => {
    resetSelectedFile();
    setFile(selectedFile);
  };

  const handleFileDrop = (event: React.DragEvent) => {
    event.preventDefault();
    if (event.dataTransfer.files && event.dataTransfer.files.length > 0) {
      handleFileSelected(event.dataTransfer.files[0]);
    }
  };

  const loadRelationImpact = async () => {
    setImpactStatus('loading');
    try {
      const data = await getDocumentRelations(documentId);
      setRelationImpact({
        references: Array.isArray(data?.references) ? data.references : [],
        drawings: Array.isArray(data?.drawings) ? data.drawings : [],
        whereUsed: Array.isArray(data?.whereUsed) ? data.whereUsed : []
      });
      setImpactStatus('ready');
    } catch (error: any) {
      console.error('Failed to load part-number change impact', error);
      setRelationImpact(null);
      setImpactStatus('error');
    }
  };

  const handleSubmit = async (createNewDocumentForPartNumberChange = false) => {
    if (!file) return;

    try {
      setErrorMessage('');

      let serverLocalPath = stagedPath;
      if (!serverLocalPath) {
        setStatus('uploading');
        const uploadResponse = await uploadTempFile(file);
        serverLocalPath = uploadResponse.localFilePath;
        setStagedPath(serverLocalPath);
      }

      setStatus('processing');
      const lowerFileName = file.name.toLowerCase();
      const shouldIngestReferences =
        lowerFileName.endsWith('.sldasm') || lowerFileName.endsWith('.slddrw');
      const ingestResult = await ingestCad(
        serverLocalPath,
        shouldIngestReferences,
        userName,
        changeReason,
        documentId,
        createNewDocumentForPartNumberChange
      );

      setResult(ingestResult);
      setStatus('success');

      if (!createNewDocumentForPartNumberChange) {
        window.setTimeout(() => {
          onSuccess();
          handleClose();
        }, 1500);
      }
    } catch (error: any) {
      const problem = error.response?.data;
      const isIdentityMismatch =
        !createNewDocumentForPartNumberChange &&
        error.response?.status === 409 &&
        problem?.code === 'CAD_IDENTITY_MISMATCH';
      const canBranch =
        isIdentityMismatch &&
        problem?.canCreateNewDocument === true;

      if (canBranch) {
        setIdentityMismatch(problem as IdentityMismatch);
        setStatus('identity-mismatch');
        await loadRelationImpact();
        return;
      }

      if (!isIdentityMismatch) {
        console.error(error);
      }

      setStatus('error');
      setErrorMessage(
        problem?.partNumberChangeBlockReason ||
        problem?.detail ||
        error.message ||
        '入庫時發生錯誤'
      );
    }
  };

  const handleClose = () => {
    if (isBusy) return;

    resetSelectedFile();
    setChangeReason('');
    onClose();
  };

  const isNameMismatch =
    file &&
    !file.name.toLowerCase().includes(fileName.toLowerCase()) &&
    !fileName
      .toLowerCase()
      .includes(file.name.toLowerCase().replace(/\.[^/.]+$/, ''));
  const canCreateNewPartNumber =
    Boolean(changeReason.trim()) &&
    Boolean(userName.trim()) &&
    impactStatus === 'ready' &&
    !isBusy;
  const partNumberChange = result?.partNumberChange;

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="圖檔入庫 (Check-in)">
      <div className="space-y-5">
        <div className="rounded-lg border border-green-900/20 bg-green-900/10 p-3">
          <p className="text-xs leading-5 text-green-200">
            針對 <span className="font-bold text-white">{fileName}</span> 進行入庫。
            一般入庫會建立新版本；若 CAD 品號改變，系統會先停止並讓您確認是否建立新品號文件。
          </p>
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-medium text-gray-400" htmlFor="checkin-user-name">
            入庫人員
          </label>
          <input
            id="checkin-user-name"
            type="text"
            value={userName}
            onChange={(event) => setUserName(event.target.value)}
            disabled={isBusy}
            className="w-full rounded-md border border-gray-700 bg-gray-800 px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-60"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-medium text-gray-400" htmlFor="checkin-change-reason">
            變更原因
            {status === 'identity-mismatch' && <span className="ml-1 text-amber-400">（建立新品號必填）</span>}
          </label>
          <textarea
            id="checkin-change-reason"
            value={changeReason}
            onChange={(event) => setChangeReason(event.target.value)}
            disabled={isBusy}
            rows={3}
            className="w-full resize-none rounded-md border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-60"
            placeholder="請輸入本次入庫的變更原因"
          />
        </div>

        {status === 'success' ? (
          <div className="py-5 text-center animate-in zoom-in-95 duration-300">
            <CheckCircle className="mx-auto mb-3 h-12 w-12 text-green-500" />
            <h3 className="text-lg font-semibold text-white">
              {partNumberChange ? '新品號文件已建立' : '入庫成功'}
            </h3>
            {partNumberChange ? (
              <div className="mt-3 space-y-3 text-left">
                <div className="flex items-center justify-center gap-2 rounded-lg border border-green-900/30 bg-green-950/20 p-3 text-sm">
                  <span className="font-mono text-gray-400">{partNumberChange.oldPartNumber}</span>
                  <ArrowRight size={15} className="text-green-400" />
                  <span className="font-mono font-semibold text-green-300">{partNumberChange.newPartNumber}</span>
                </div>
                <p className="text-xs leading-5 text-gray-400">
                  新文件 ID：{partNumberChange.targetDocumentId}，初始狀態為 WIP。
                  原文件歷史與既有 BOM 均未變更，原出庫鎖已解除。
                </p>
                <Button className="w-full" onClick={() => { onSuccess(); handleClose(); }}>
                  完成並返回圖檔中心
                </Button>
              </div>
            ) : (
              <p className="text-sm text-gray-400">正在更新列表...</p>
            )}
          </div>
        ) : identityMismatch && (status === 'identity-mismatch' || status === 'processing') ? (
          <div className="space-y-4" role="status">
            <div className="rounded-lg border border-amber-700/40 bg-amber-950/25 p-4">
              <div className="flex items-start gap-3">
                <GitBranch size={19} className="mt-0.5 shrink-0 text-amber-400" />
                <div className="min-w-0">
                  <p className="font-semibold text-amber-200">
                    此檔案品號已變更，不能覆寫原文件。
                  </p>
                  <p className="mt-1 text-xs leading-5 text-amber-100/70">
                    若這是有效的設計衍生，請確認影響後另存為新料號；若選錯檔案，請重新選擇。
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-lg border border-gray-800 bg-gray-900/60 p-4">
              <div className="flex flex-wrap items-center justify-center gap-2 text-sm">
                <span className="rounded bg-gray-800 px-2 py-1 font-mono text-gray-300">
                  {identityMismatch.expectedPartNumber || '未設定'}
                </span>
                <ArrowRight size={16} className="text-amber-400" />
                <span className="rounded bg-amber-950/50 px-2 py-1 font-mono font-semibold text-amber-300">
                  {identityMismatch.actualPartNumber || '未設定'}
                </span>
              </div>
              <p className="mt-2 text-center text-xs text-gray-500">
                文件類型：{identityMismatch.actualDocumentType}
              </p>
            </div>

            <div className="rounded-lg border border-gray-800 bg-gray-900/40 p-3">
              <div className="flex items-center justify-between gap-3">
                <p className="flex items-center text-xs font-medium text-gray-300">
                  <ShieldCheck size={14} className="mr-2 text-blue-400" />
                  影響範圍預覽
                </p>
                {impactStatus === 'loading' && <Loader2 size={14} className="animate-spin text-blue-400" />}
              </div>

              {impactStatus === 'ready' && relationImpact ? (
                <div className="mt-3 grid grid-cols-3 gap-2 text-center">
                  <div className="rounded bg-gray-800/80 p-2">
                    <p className="text-base font-semibold text-white">{relationImpact.references.length}</p>
                    <p className="text-[10px] text-gray-500">向下參照</p>
                  </div>
                  <div className="rounded bg-gray-800/80 p-2">
                    <p className="text-base font-semibold text-white">{relationImpact.drawings.length}</p>
                    <p className="text-[10px] text-gray-500">關聯工程圖</p>
                  </div>
                  <div className="rounded bg-gray-800/80 p-2">
                    <p className="text-base font-semibold text-white">{relationImpact.whereUsed.length}</p>
                    <p className="text-[10px] text-gray-500">被使用項目</p>
                  </div>
                </div>
              ) : impactStatus === 'error' ? (
                <div className="mt-3 text-xs text-red-300">
                  <p>影響範圍尚未載入，為避免誤操作，目前不能建立新品號。</p>
                  <button
                    type="button"
                    onClick={loadRelationImpact}
                    className="mt-2 inline-flex items-center rounded border border-red-900/50 px-2.5 py-1.5 hover:bg-red-950/30"
                  >
                    <RefreshCw size={12} className="mr-1.5" />
                    重新載入影響範圍
                  </button>
                </div>
              ) : (
                <p className="mt-2 text-xs text-gray-500">正在確認工程圖與 where-used...</p>
              )}

              <p className="mt-3 text-xs leading-5 text-gray-500">
                系統只建立新文件與來源紀錄，不會自動修改以上關聯或歷史 BOM。
              </p>
            </div>

            {!changeReason.trim() && (
              <p className="text-xs text-amber-300">下一步：先填寫變更原因，才能建立新品號文件。</p>
            )}

            <details className="text-xs text-gray-500">
              <summary className="cursor-pointer hover:text-gray-300">查看判定細節</summary>
              <p className="mt-2 break-words leading-5">{identityMismatch.detail}</p>
            </details>

            <div className="flex flex-col-reverse gap-2 border-t border-gray-800 pt-3 sm:flex-row sm:justify-end">
              <Button variant="secondary" onClick={resetSelectedFile} disabled={isBusy}>
                重新選擇檔案
              </Button>
              <Button
                onClick={() => handleSubmit(true)}
                disabled={!canCreateNewPartNumber}
                className="sm:min-w-[150px]"
              >
                {isBusy ? (
                  <><Loader2 size={16} className="mr-2 animate-spin" />處理中</>
                ) : (
                  <><GitBranch size={16} className="mr-2" />另存為新料號</>
                )}
              </Button>
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            <div
              onDragOver={(event) => event.preventDefault()}
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
                aria-label="選擇要入庫的 CAD 檔案"
                accept=".SLDPRT,.sldprt,.SLDASM,.sldasm,.SLDDRW,.slddrw"
                onChange={(event) => {
                  handleFileSelected(event.target.files ? event.target.files[0] : null);
                  event.currentTarget.value = '';
                }}
              />
              <UploadCloud className={`mx-auto mb-3 h-10 w-10 ${file ? 'text-blue-500' : 'text-gray-600'}`} />
              {file ? (
                <div className="break-all text-sm font-medium text-blue-400">{file.name}</div>
              ) : (
                <>
                  <div className="text-sm font-medium text-gray-300">點擊或拖曳檔案至此</div>
                  <p className="mt-1 text-xs text-gray-500">支援 .SLDPRT、.SLDASM、.SLDDRW</p>
                </>
              )}
            </div>

            {isNameMismatch && (
              <div className="flex items-start gap-2 rounded border border-blue-900/40 bg-blue-900/20 p-3 text-xs text-blue-300">
                <FileWarning size={14} className="mt-0.5 shrink-0" />
                <p>
                  檔名與上一版不同，仍可入庫。系統會核對 CAD 內的品號與文件類型；
                  品號改變時會先停止並提供安全分流。
                </p>
              </div>
            )}

            {status === 'error' && (
              <div className="flex items-start gap-2 rounded border border-red-900/30 bg-red-900/20 p-3 text-xs text-red-400" role="alert">
                <AlertCircle size={14} className="mt-0.5 shrink-0" />
                <div>
                  <p className="font-medium">入庫未完成；請依下方原因修正後重試。</p>
                  <p className="mt-1 break-words">{errorMessage}</p>
                </div>
              </div>
            )}

            <div className="flex flex-col-reverse gap-2 border-t border-gray-800 pt-2 sm:flex-row sm:justify-end sm:gap-3">
              <Button variant="secondary" onClick={handleClose} disabled={isBusy}>
                取消
              </Button>
              <Button
                onClick={() => handleSubmit(false)}
                disabled={!file || isBusy}
                className="sm:min-w-[100px]"
              >
                {isBusy ? (
                  <><Loader2 size={16} className="mr-2 animate-spin" />處理中</>
                ) : '確認入庫'}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
};
