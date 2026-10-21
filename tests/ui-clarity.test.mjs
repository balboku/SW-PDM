import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';

// Use the existing TypeScript dependency; no new runtime/testing package is needed.
const require = createRequire(new URL('../src/SWPdm.Web/package.json', import.meta.url));
const ts = require('typescript');
const source = readFileSync(new URL('../src/SWPdm.Web/src/lib/ui-model.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 } });
const api = {};
new Function('exports', compiled.outputText)(api);
const { uniqueRelatedFiles, downloadFileName } = api;

test('scope excludes the root and counts a related document once across categories', () => {
  const files = uniqueRelatedFiles(1, { whereUsed: [{ documentId: 1 }, { documentId: 2, versionId: 20 }],
    references: [{ documentId: 2, versionId: 21 }], drawings: [{ documentId: 3, versionId: 30 }] });
  assert.deepEqual(files.map(file => file.documentId), [2, 3]);
});
test('no related documents means only the root is affected', () => {
  assert.equal(uniqueRelatedFiles(1, { whereUsed: [], references: [], drawings: [] }).length + 1, 1);
});
test('version IDs support legacy preview rows without document IDs', () => {
  assert.equal(uniqueRelatedFiles(1, { whereUsed: [], references: [{ versionId: 2 }, { versionId: 2 }], drawings: [] }).length, 1);
});
test('keeps the server-provided CAD extension', () => {
  assert.equal(downloadFileName('attachment; filename="part.sldprt"', 'fallback'), 'part.sldprt');
});
test('uses UTF-8 Chinese file names ahead of ASCII fallback', () => {
  assert.equal(downloadFileName("attachment; filename=part.sldprt; filename*=UTF-8''%E5%9C%96%E9%9D%A2.sldprt", 'fallback'), '圖面.sldprt');
});
test('malformed encoded names fall back safely', () => {
  assert.equal(downloadFileName("attachment; filename=part.sldprt; filename*=UTF-8''%ZZ", 'fallback'), 'part.sldprt');
  assert.equal(downloadFileName('', 'version_57'), 'version_57');
});
test('download names cannot introduce directory separators', () => {
  assert.equal(downloadFileName('attachment; filename="a/b\\c.sldasm"', 'fallback'), 'a_b_c.sldasm');
});
