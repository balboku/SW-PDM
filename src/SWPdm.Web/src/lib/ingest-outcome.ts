export type RootIngestOutcome = 'CreatedVersion' | 'Reused' | 'Unknown';

// Root outcome wins; a reused child must never turn a newly-created root into a no-op.
export const getRootIngestOutcome = (result: any): RootIngestOutcome => {
  const root = result?.files?.find((file: any) => file.documentId === result.rootDocumentId && file.versionId === result.rootVersionId);
  const outcome = result?.rootOutcome ?? root?.outcome;
  if (outcome === undefined) return 'CreatedVersion'; // Published legacy API always creates a version.
  return outcome === 'CreatedVersion' || outcome === 'Reused' ? outcome : 'Unknown';
};
