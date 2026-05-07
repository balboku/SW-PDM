import axios from 'axios';

// 動態取得 API 位址，支援區域網路分享
const API_HOST = typeof window !== 'undefined' ? window.location.hostname : 'localhost';
export const api = axios.create({
  baseURL: `http://${API_HOST}:5000`,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const uploadTempFile = async (file: File) => {
  const formData = new FormData();
  formData.append('file', file);
  
  // Use a completely clean axios instance or omit the Content-Type header 
  // so the browser automatically handles the multipart boundary.
  const response = await axios.post(`http://${API_HOST}:5000/api/web/upload-temp`, formData);
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
  changeReason: string = ''
) => {
  const payload = {
    localFilePath,
    ingestReferencedFiles,
    additionalSearchPaths: [],
    uploadedBy,
    changeReason
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

export const checkAssemblyUpdates = async (rootVersionId: number) => {
  const response = await api.get(`/api/assemblies/${rootVersionId}/check-updates`);
  return response.data;
};

export const downloadAssemblyZip = (
  rootVersionId: number,
  useLatest: boolean = false,
  versionOverrides: string[] = []
) => {
  const params = new URLSearchParams({ useLatest: String(useLatest) });
  versionOverrides.forEach((override) => params.append('versionOverrides', override));
  window.open(`http://${API_HOST}:5000/api/assemblies/${rootVersionId}/download-zip?${params.toString()}`, '_blank');
};

export const downloadVersion = (versionId: number) => {
  window.open(`http://${API_HOST}:5000/api/versions/${versionId}/download`, '_blank');
};

export const getVersionThumbnailUrl = (versionId: number) => {
  return `http://${API_HOST}:5000/api/versions/${versionId}/thumbnail`;
};

export const searchDocuments = async (query: string = '') => {
  const response = await api.get('/api/documents/search', {
    params: { query }
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

export const checkInDocument = async (documentId: number) => {
  const response = await api.post(`/api/documents/${documentId}/checkin`);
  return response.data;
};

export const undoCheckOutDocument = async (documentId: number) => {
  const response = await api.post(`/api/documents/${documentId}/undo-checkout`);
  return response.data;
};
