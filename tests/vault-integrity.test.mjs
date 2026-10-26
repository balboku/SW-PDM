import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
const require = createRequire(new URL('../src/SWPdm.Web/package.json', import.meta.url));
const ts = require('typescript');
const source = readFileSync(new URL('../src/SWPdm.Web/src/lib/vault-model.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 } });
const model = {}; new Function('exports', compiled.outputText)(model);
const { isVaultPage, isBatchPreview, changedOverrides, checkoutTargets } = model;
test('embedded virtual reference is not a missing file', () => assert.equal(model.referenceLabel({isVirtual:true,referenceStatus:'Virtual'}), '虛擬件（內嵌）'));
test('suppressed unresolved reference remains visible for confirmation', () => assert.equal(model.referenceLabel({isSuppressed:true,referenceStatus:'Missing'}), '抑制／待確認'));
test('contradictory virtual flag is not silently treated as embedded', () => assert.equal(model.referenceLabel({isVirtual:false,referenceStatus:'Virtual'}), '虛擬旗標待確認'));
const page = () => ({ page: 2, pageSize: 2, totalCount: 3, totalPages: 2, checkedOutCount: 2,
  missingReferenceDocumentCount: 1, newerReferenceDocumentCount: 0, items: [{ documentId: 3 }] });
test('valid paged total is not confused with the page length', () => assert.equal(isVaultPage(page()), true));
for (const [key, value] of [['page', 0], ['page', 3], ['pageSize', 101], ['totalCount', -1], ['totalPages', 99], ['checkedOutCount', 4], ['missingReferenceDocumentCount', undefined]]) {
  test('rejects invalid paged contract ' + key + '=' + value, () => assert.equal(isVaultPage({ ...page(), [key]: value }), false));
}
test('rejects incomplete or duplicate page items', () => {
  assert.equal(isVaultPage({ ...page(), items: [] }), false);
  assert.equal(isVaultPage({ ...page(), page: 1, items: [{ documentId: 1 }, { documentId: 1 }] }), false);
});
test('empty is an explicit page 1/1, not a missing response', () => {
  assert.equal(isVaultPage({ ...page(), page: 1, totalCount: 0, totalPages: 1, checkedOutCount: 0, missingReferenceDocumentCount: 0, items: [] }), true);
  assert.equal(isVaultPage(null), false);
});
const batchId = '11111111-1111-4111-8111-111111111111';
const preview = () => ({ scope: 'CheckoutBatch', rootDocumentId: 60, batchId, actor: 'User', canRestore: true, restorableCount: 1,
  items: [{ documentId: 60, versionId: 60, lockTime: '2026-10-26T00:00:00.123456Z', canRestore: true, reason: '可解除', fileName: 'root.sldasm' },
    { documentId: 58, versionId: 58, lockTime: '2026-10-26T00:00:00.123456Z', canRestore: false, reason: '後續新鎖保留', fileName: 'part.sldprt' }] });
test('batch preview preserves exclusions and microsecond timestamps', () => {
  const value = preview(); assert.equal(isBatchPreview(value, 60, batchId, ' User '), true);
  assert.equal(value.items[0].lockTime, '2026-10-26T00:00:00.123456Z');
});
for (const [key, value] of [['scope', 'SingleDocument'], ['rootDocumentId', 59], ['actor', 'Other'], ['batchId', 'wrong'], ['restorableCount', 2], ['canRestore', false]]) {
  test('batch confirmation blocks stale or inconsistent ' + key, () => assert.equal(isBatchPreview({ ...preview(), [key]: value }, 60, batchId, 'User'), false));
}
test('a batch needs a unique item list including the root', () => {
  assert.equal(isBatchPreview({ ...preview(), items: [preview().items[1]] }, 60, batchId, 'User'), false);
  assert.equal(isBatchPreview({ ...preview(), items: [preview().items[0], preview().items[0]] }, 60, batchId, 'User'), false);
});
test('checkout excludes where-used parents and deduplicates actual targets', () => {
  assert.deepEqual(checkoutTargets(1, { whereUsed: [{ documentId: 99, checkedOutBy: 'Other' }],
    references: [{ documentId: 1 }, { documentId: 2 }], drawings: [{ documentId: 2 }, { documentId: 3 }] }).map(d => d.documentId), [2, 3]);
});
test('unchanged descendant choices do not become stale overrides after parent selection', () => {
  assert.deepEqual(changedOverrides({ 1: 1, 2: 5, 3: 3 }), ['2:5']);
});
