import axios from 'axios';
import { downloadFileName } from './ui-model';

// 動態取得 API 位址，支援區域網路分享
const API_HOST = typeof window !== 'undefined' ? window.location.hostname : 'localhost';
export const API_BASE = (import.meta as any).env?.VITE_PDM_API_URL || `http://${API_HOST}:5000`;
export const api = axios.create({
  baseURL: API_BASE,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const uploadTempFile = async (file: File) => {
  const formData = new FormData();
  formData.append('file', file);
  
  // Use a completely clean axios instance or omit the Content-Type header 
  // so the browser automatically handles the multipart boundary.
  const response = await axios.post(`${API_BASE}/api/web/upload-temp`, formData);
  return response.data;
};

export const ingestCadBatch = async (
  files: File[],
  uploadedBy: string = 'User',
  changeReason: string = ''
) => {
  const formData = new FormData();
  files.forEach((file) => {
    formData.append('files', file, file.name);
    formData.append('relativePaths', file.webkitRelativePath || file.name);
  });
  formData.append('uploadedBy', uploadedBy);
  formData.append('changeReason', changeReason);

  const response = await axios.post(
    `${API_BASE}/api/ingest/cad-batch`,
    formData
  );
  return response.data;
};

/**
 * 發起 CAD 檔案入庫。
 * 系統將直接讀取 CAD 檔案內部的 PartNumber 或 品號 自訂屬性作為料號；
 * 若 CAD 檔案未填寫 PartNumber / 品號，後端將拒絕入庫並回傳錯誤。
 */
export const ingestCad = async (
  localFilePath: string,
  ingestReferencedFiles: boolean = true,
  uploadedBy: string = 'User',
  changeReason: string = '',
  targetDocumentId?: number,
  createNewDocumentForPartNumberChange: boolean = false,
  additionalSearchPaths: string[] = []
) => {
  const payload = {
    localFilePath,
    ingestReferencedFiles,
    additionalSearchPaths,
    uploadedBy,
    changeReason,
    targetDocumentId,
    createNewDocumentForPartNumberChange
  };

  const response = await api.post('/api/ingest/cad', payload);
  return response.data;
};

export const parseSolidWorksFile = async (
  filePath: string,
  additionalSearchPaths: string[] = []
) => {
  const response = await api.post('/api/solidworks/parse', {
    filePath,
    additionalSearchPaths
  });
  return response.data;
};



export const getSystemStatus = async () => {
  const response = await api.get('/api/config/status');
  return response.data;
};

export const getDatabaseStatus = async () => (await api.get('/api/database/status')).data;

export const checkAssemblyUpdates = async (rootVersionId: number) => {
  const response = await api.get(`/api/assemblies/${rootVersionId}/check-updates`);
  return response.data;
};

export const downloadAssemblyZip = async (
  rootVersionId: number,
  useLatest: boolean = false,
  versionOverrides: string[] = [],
  includeDrawings: boolean = false
) => {
  const params = new URLSearchParams({
    useLatest: String(useLatest),
    includeDrawings: String(includeDrawings)
  });
  versionOverrides.forEach((override) => params.append('versionOverrides', override));
  const response = await api.get(`/api/assemblies/${rootVersionId}/download-zip?${params.toString()}`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `assembly_${rootVersionId}.zip`;
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
};

export const downloadVersion = async (versionId: number, fallbackFileName: string) => {
  const response = await api.get(`/api/versions/${versionId}/download`, { responseType: 'blob' });
  const disposition = String(response.headers['content-disposition'] || '');
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = downloadFileName(disposition, fallbackFileName);
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
};

export const getVersionThumbnailUrl = (versionId: number) => {
  return `${API_BASE}/api/versions/${versionId}/thumbnail`;
};

export const searchDocuments = async (query: string = '', documentType: string = '', status: string = '', page = 1) => {
  const response = await api.get('/api/documents/search', {
    params: { query, documentType, status, paged: true, page, pageSize: 50 }
  });
  return response.data;
};

export const getVersionChildren = async (versionId: number) => {
  const response = await api.get(`/api/versions/${versionId}/children`);
  return response.data;
};

export const getCheckoutReferences = async (documentId: number) => {
  const response = await api.get(`/api/documents/${documentId}/checkout-references`);
  return response.data;
};

export const getDocumentRelations = async (documentId: number) => {
  const response = await api.get(`/api/documents/${documentId}/relations`);
  return response.data;
};

export const getDocumentReferenceUpdates = async (documentId: number) => {
  const response = await api.get(`/api/documents/${documentId}/reference-updates`);
  return response.data;
};

export const checkOutDocument = async (
  documentId: number,
  checkOutBy: string,
  forceIncludeRelations: boolean = false
) => {
  const response = await api.post(
    `/api/documents/${documentId}/checkout?forceIncludeRelations=${forceIncludeRelations}`,
    { checkOutBy }
  );
  return response.data;
};

export interface CancelCheckoutRequest {
  actor: string;
  expectedLockTime: string;
  expectedVersionId: number;
  includeBatch: false;
}

export const getCancelCheckoutPreview = async (documentId: number, actor: string) => {
  const response = await api.get(`/api/documents/${documentId}/undo-checkout-preview`, { params: { actor } });
  return response.data;
};

export const checkInDocument = async (documentId: number, request: CancelCheckoutRequest) => {
  const response = await api.post(`/api/documents/${documentId}/checkin`, request);
  return response.data;
};

export const undoCheckOutDocument = async (documentId: number, request: CancelCheckoutRequest) => {
  const response = await api.post(`/api/documents/${documentId}/undo-checkout`, request);
  return response.data;
};
