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
  
  const response = await api.post('/api/web/upload-temp', formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });
  return response.data;
};

/**
 * 發起 CAD 檔案入庫。
 * 系統將直接讀取 CAD 檔案內部的 PartNumber 自訂屬性作為料號；
 * 若 CAD 檔案未填寫 PartNumber，後端將拒絕入庫並回傳錯誤。
 */
export const ingestCad = async (
  localFilePath: string,
  ingestReferencedFiles: boolean = true
) => {
  const payload = {
    localFilePath,
    ingestReferencedFiles,
    additionalSearchPaths: []
  };

  const response = await api.post('/api/ingest/cad', payload);
  return response.data;
};

export const getNumberingRules = async () => {
  const response = await api.get('/api/settings/numbering-rules');
  return response.data;
};

export const updateNumberingRule = async (documentType: string, pattern: string) => {
  const response = await api.post('/api/settings/numbering-rules', { documentType, pattern });
  return response.data;
};

export const getSystemStatus = async () => {
  const response = await api.get('/api/config/status');
  return response.data;
};

export const downloadAssemblyZip = (rootVersionId: number) => {
  window.open(`http://localhost:5000/api/assemblies/${rootVersionId}/download-zip`, '_blank');
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
