$ErrorActionPreference='Stop'
$expectedExe=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'api/SWPdm.Api.exe'))
$listener=@(Get-NetTCPConnection -State Listen -LocalPort 5002 | Select-Object -ExpandProperty OwningProcess -Unique)
if($listener.Count -ne 1 -or (Get-CimInstance Win32_Process -Filter "ProcessId=$($listener[0])").ExecutablePath -ne $expectedExe){throw 'Unexpected isolated API owner'}
function History {
    $sql=@'
BEGIN READ ONLY;
SELECT 'target|'||current_database()||'|'||current_setting('port');
SELECT 'versions|'||count(*)||'|'||md5(jsonb_agg(to_jsonb(t) ORDER BY version_id)::text) FROM pdm_document_versions t;
SELECT 'bom|'||count(*)||'|'||md5(jsonb_agg(to_jsonb(t) ORDER BY bom_occurrence_id)::text) FROM pdm_bom_occurrences t;
SELECT 'reused|'||checked_out_by||'|'||checked_out_at FROM pdm_documents WHERE document_id=2;
ROLLBACK;
'@
    $rows=@(& 'C:/Program Files/PostgreSQL/16/bin/psql.exe' -h 127.0.0.1 -p 55433 -U contract -d release_http_20261026 -X -q -A -t -v ON_ERROR_STOP=1 -c $sql)
    if($LASTEXITCODE -ne 0 -or $rows[0] -ne 'target|release_http_20261026|55433'){throw 'Wrong private DB history target'}
    $rows -join "`n"
}
function Post([string]$path,[object]$body){Invoke-RestMethod -Uri ('http://127.0.0.1:5002'+$path) -Method Post -ContentType 'application/json' -Body ($body|ConvertTo-Json -Compress)}
$before=History
$page=Invoke-RestMethod 'http://127.0.0.1:5002/api/documents/search?paged=true&pageSize=2&page=2'
if($page.totalCount -ne 4 -or $page.items.Count -ne 2 -or $page.checkedOutCount -ne 1){throw 'Private paged/global counts mismatch'}
$integrity=Invoke-RestMethod http://127.0.0.1:5002/api/documents/1/integrity
if($integrity.evidenceLevel -ne 'StoredSnapshot' -or $integrity.nativeFileChecked -ne $false -or $integrity.references.Count -ne 2){throw 'Private integrity mismatch'}
$relations=Invoke-RestMethod http://127.0.0.1:5002/api/documents/1/checkout-references
if(@($relations.references|Where-Object {$_.documentId -eq 2 -and $_.checkedOutBy -eq 'User'}).Count -ne 1 -or $relations.drawings[0].documentId -ne 4){throw 'Checkout preview identity/lock mismatch'}
$checkout=Post '/api/documents/1/checkout?forceIncludeRelations=true' @{checkOutBy='User'}
if($checkout.acquiredCount -ne 3 -or $checkout.reusedLockCount -ne 1){throw 'Private checkout scope mismatch'}
$batch=$checkout.batchId
$preview=Invoke-RestMethod "http://127.0.0.1:5002/api/documents/1/undo-checkout-preview?actor=User&includeBatch=true&batchId=$batch"
if($preview.restorableCount -ne 3 -or (($preview.items.documentId|Sort-Object)-join ',') -ne '1,3,4'){throw 'Private restore preview mismatch'}
$wrong=Invoke-WebRequest http://127.0.0.1:5002/api/documents/1/undo-checkout -Method Post -ContentType 'application/json' -Body (@{actor='Other';includeBatch=$true;batchId=$batch}|ConvertTo-Json) -SkipHttpErrorCheck
if([int]$wrong.StatusCode -ne 403){throw 'Wrong actor was not rejected'}
$undo=Post '/api/documents/1/undo-checkout' @{actor='User';includeBatch=$true;batchId=$batch}
$repeat=Post '/api/documents/1/undo-checkout' @{actor='User';includeBatch=$true;batchId=$batch}
if($undo.restoredCount -ne 3 -or $repeat.restoredCount -ne 0){throw 'Private undo/replay mismatch'}
$next=Post '/api/documents/1/checkout?forceIncludeRelations=false' @{checkOutBy='User'}
$oldReplay=Post '/api/documents/1/undo-checkout' @{actor='User';includeBatch=$true;batchId=$batch}
if($next.acquiredCount -ne 1 -or $oldReplay.restoredCount -ne 0){throw 'Old batch affected new lock'}
$singleRaw=(Invoke-WebRequest 'http://127.0.0.1:5002/api/documents/1/undo-checkout-preview?actor=User').Content
$single=$singleRaw|ConvertFrom-Json
$utcToken=[regex]::Match($singleRaw,'"checkedOutAt":"([^"]+)"').Groups[1].Value
if(-not $single.canRestore -or -not $utcToken){throw 'Single preview unavailable'}
$singleUndo=Post '/api/documents/1/undo-checkout' @{actor='User';includeBatch=$false;expectedLockTime=$utcToken;expectedVersionId=$single.currentVersionId}
if($singleUndo.restoredCount -ne 1){throw 'Private single undo failed'}
$package=Invoke-WebRequest 'http://127.0.0.1:5002/api/assemblies/1/download-zip?includeDrawings=true' -SkipHttpErrorCheck
$packagePayload=if($package.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($package.Content)}else{[string]$package.Content}
$problem=$packagePayload|ConvertFrom-Json
if([int]$package.StatusCode -ne 409 -or $problem.code -ne 'PACKAGE_CHECKSUM_MISSING' -or $package.Headers['Content-Type'] -like '*zip*'){throw 'Missing synthetic CAD did not block ZIP'}
$after=History
if($before -cne $after){throw 'Private version/BOM/reused lock changed'}
if(@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'isolated-vault') -Recurse -File).Count -ne 0){throw 'Private Vault changed'}
$result=[ordered]@{Passed=$true;SourceCommit='14de0f58717cfa58c274efa9e72e6ca475c4e4da';Target='127.0.0.1:5002 / 55433/release_http_20261026';PagedTotal=$page.totalCount;PageSize=$page.items.Count;EvidenceLevel=$integrity.evidenceLevel;Checkout=$checkout;Preview=$preview;WrongActorStatus=[int]$wrong.StatusCode;Undo=$undo;Repeat=$repeat;NextCheckout=$next;OldReplay=$oldReplay;SingleUndo=$singleUndo;PackageBlockedStatus=[int]$package.StatusCode;PackageBlockedCode=$problem.code;Before=$before;After=$after;VaultFileCount=0}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'isolated-http.json'),($result|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
Write-Output "ISOLATED_HTTP_PASS acquired=$($checkout.acquiredCount);reused=$($checkout.reusedLockCount);restored=$($undo.restoredCount);replay=$($repeat.restoredCount);package=$($problem.code)"
