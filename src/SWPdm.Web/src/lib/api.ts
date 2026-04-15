import axios from 'axios';

// 建立 Axios 實例，對應 .NET 後端預設連接埠 (若有更改請自行替換)
export const api = axios.create({
  baseURL: 'http://localhost:5000',
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

export const ingestCad = async (
  localFilePath: string, 
  ingestReferencedFiles: boolean = true,
  allocatedPartNumber?: string,
  existingPartNumber?: string
) => {
  const payload: any = {
    localFilePath,
    ingestReferencedFiles,
    additionalSearchPaths: []
  };

  if (allocatedPartNumber) payload.allocatedPartNumber = allocatedPartNumber;
  if (existingPartNumber) payload.existingPartNumber = existingPartNumber;

  const response = await api.post('/api/ingest/cad', payload);
  return response.data;
};

export const allocateNumber = async (documentType: string) => {
  const response = await api.post('/api/documents/allocate-number', { documentType });
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
