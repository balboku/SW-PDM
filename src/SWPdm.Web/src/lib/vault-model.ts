export const isVaultPage = (value: any): boolean => {
  if (!value || !Array.isArray(value.items)) return false;
  const integers = ['page', 'pageSize', 'totalCount', 'totalPages', 'checkedOutCount', 'missingReferenceDocumentCount', 'newerReferenceDocumentCount'];
  if (!integers.every(key => Number.isSafeInteger(value[key]) && value[key] >= 0)) return false;
  return value.page >= 1 && value.pageSize >= 1 && value.pageSize <= 100 &&
    value.totalPages === Math.max(1, Math.ceil(value.totalCount / value.pageSize)) && value.page <= value.totalPages &&
    value.items.length === Math.min(value.pageSize, Math.max(0, value.totalCount - (value.page - 1) * value.pageSize)) &&
    ['checkedOutCount', 'missingReferenceDocumentCount', 'newerReferenceDocumentCount'].every(key => value[key] <= value.totalCount) &&
    value.items.every((item: any) => Number.isSafeInteger(item.documentId) && item.documentId > 0) &&
    new Set(value.items.map((item: any) => item.documentId)).size === value.items.length;
};

export const isBatchId = (id: unknown): id is string => typeof id === 'string' && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(id);
export const isBatchPreview = (value: any, root: number, batchId: string, actor: string): boolean => {
  if (!value || value.scope !== 'CheckoutBatch' || value.rootDocumentId !== root || value.batchId !== batchId ||
      value.actor !== actor.trim() || !isBatchId(batchId) || !Array.isArray(value.items) || !value.items.length) return false;
  const ids = value.items.map((item: any) => item.documentId);
  return new Set(ids).size === ids.length && ids.includes(root) && value.items.every((item: any) =>
    Number.isSafeInteger(item.documentId) && item.documentId > 0 && Number.isSafeInteger(item.versionId) && item.versionId > 0 &&
    typeof item.lockTime === 'string' && item.lockTime.length > 0 && typeof item.canRestore === 'boolean' &&
    typeof item.reason === 'string' && typeof item.fileName === 'string') &&
    value.restorableCount === value.items.filter((item: any) => item.canRestore).length &&
    value.canRestore === (value.restorableCount > 0);
};

// where-used parents are informational, never checkout/restore targets (ADR-003).
export const checkoutTargets = (root: number, value: any): any[] => Array.from(new Map(
  [...value.references, ...value.drawings].filter((item: any) => item.documentId !== root)
    .map((item: any) => [item.documentId, item])).values());

export const changedOverrides = (selections: Record<string, number>): string[] => Object.entries(selections)
  .filter(([source, selected]) => Number(source) !== selected).map(([source, selected]) => `${source}:${selected}`);

export const referenceLabel = (node: { isVirtual?: boolean; isSuppressed?: boolean; referenceStatus?: string }): string =>
  node.isVirtual === true ? '虛擬件（內嵌）' : node.referenceStatus === 'Virtual' ? '虛擬旗標待確認' :
    node.isSuppressed ? '抑制／待確認' : node.referenceStatus === 'Broken' ? '關聯待確認' : '缺少參照';
