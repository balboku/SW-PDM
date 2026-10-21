export const uniqueRelatedFiles = (documentId: number, data: any): any[] => {
  const files = [...data.whereUsed, ...data.references, ...data.drawings];
  return Array.from(new Map(files
    .filter(file => String(file.documentId) !== String(documentId))
    .map(file => [String(file.documentId ?? `version-${file.versionId}`), file])).values());
};

export const downloadFileName = (disposition: string, fallback: string): string => {
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1];
  let name = plain || fallback;
  if (encoded) { try { name = decodeURIComponent(encoded); } catch { /* use plain name */ } }
  return name.replace(/[\\/]/g, '_');
};
