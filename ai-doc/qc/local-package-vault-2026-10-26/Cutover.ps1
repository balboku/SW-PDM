$ErrorActionPreference='Stop'
function Health([int]$port){
    for($attempt=0;$attempt -lt 15;$attempt++){
        try { if((Invoke-WebRequest "http://127.0.0.1:$port/health" -TimeoutSec 2).StatusCode -eq 200){return} } catch {}
        Start-Sleep -Milliseconds 200
    }
    throw "API health failed on $port"
}
if((git -C 'C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM' rev-parse HEAD) -ne '14de0f58717cfa58c274efa9e72e6ca475c4e4da'){throw 'Release source changed'}
if((Get-FileHash (Join-Path $PSScriptRoot 'before.json')).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot 'precutover.json')).Hash){throw 'Pre-cutover snapshot mismatch'}
$oldApi=[IO.Path]::GetFullPath('C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM/src/SWPdm.Api/bin/local-main-74cecca-20261022/api/SWPdm.Api.exe')
$apiOwners=@(Get-NetTCPConnection -State Listen -LocalPort 5000 | Select-Object -ExpandProperty OwningProcess -Unique)
$webOwners=@(Get-NetTCPConnection -State Listen -LocalPort 5174 | Select-Object -ExpandProperty OwningProcess -Unique)
if($apiOwners.Count -ne 1 -or $apiOwners[0] -ne 24072 -or (Get-CimInstance Win32_Process -Filter 'ProcessId=24072').ExecutablePath -ne $oldApi){throw 'Original API owner changed'}
if($webOwners.Count -ne 1 -or $webOwners[0] -ne 27392 -or (Get-CimInstance Win32_Process -Filter 'ProcessId=27392').CommandLine -notlike '*frontend-clarity-20261021*local-main-e40f3e4-20261022*'){throw 'Original Web owner changed'}
if((Get-FileHash ([IO.Path]::ChangeExtension($oldApi,'.dll'))).Hash -ne '5136F5E3A2B526257604CCCB0C1098A5D6B5AAF4CD079A54B567E00D9C0FAE89'){throw 'Original API bytes changed'}
$preview=Invoke-RestMethod 'http://127.0.0.1:5001/api/documents/search?paged=true'
if($preview.totalCount -ne 0 -or $preview.evidenceLevel -ne 'StoredSnapshot'){throw 'Read-only actual preflight mismatch'}
$manifest=Get-Content (Join-Path $PSScriptRoot 'artifact-sha.json') -Raw | ConvertFrom-Json
foreach($item in $manifest.ApiFiles){if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('api/'+$item.Name))).Hash -ne $item.SHA256){throw 'New API bytes changed'}}
$site='C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM/src/SWPdm.Web/dist/local-main-14de0f5-20261026/site'
foreach($item in $manifest.WebFiles){if((Get-FileHash -LiteralPath (Join-Path $site $item.Name)).Hash -ne $item.SHA256){throw 'New Web bytes changed'}}
try {
    $oldProcess=Get-Process -Id 24072; Stop-Process -Id 24072; if(-not $oldProcess.WaitForExit(10000)){throw 'Original API did not stop'}
    $api=& (Join-Path $PSScriptRoot 'Start-Api.ps1') -Port 5000
    Health 5000
    $actual=Invoke-RestMethod 'http://127.0.0.1:5000/api/documents/search?paged=true'
    if($actual.totalCount -ne 0 -or $actual.evidenceLevel -ne 'StoredSnapshot'){throw 'New actual API mismatch'}
    $oldProcess=Get-Process -Id 27392; Stop-Process -Id 27392; if(-not $oldProcess.WaitForExit(10000)){throw 'Original Web did not stop'}
    $web=& (Join-Path $PSScriptRoot 'Start-Web.ps1') -Port 5174
    $webReady=$false
    for($attempt=0;$attempt -lt 15;$attempt++){
        try { $html=(Invoke-WebRequest http://127.0.0.1:5174/ -TimeoutSec 2).Content; if($html -match 'index-tPhSW7r-\.js'){$webReady=$true;break} } catch {}
        Start-Sleep -Milliseconds 200
    }
    if(-not $webReady){throw 'New Web did not serve fixed artifact'}
    $result=[ordered]@{Passed=$true;SourceCommit=$manifest.SourceCommit;TimeUtc=[DateTimeOffset]::UtcNow.ToString('O');PreviousApiPid=24072;PreviousWebPid=27392;Api=$api;Web=$web;ActualDocumentCount=$actual.totalCount;MigrationExecuted=$false;RollbackInvoked=$false}
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'cutover.json'),($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    $result|ConvertTo-Json -Depth 8
} catch {
    Write-Warning 'Cutover failed; restoring only known fixed API/Web artifacts.'
    & (Join-Path $PSScriptRoot 'Rollback.ps1')
    throw
}
