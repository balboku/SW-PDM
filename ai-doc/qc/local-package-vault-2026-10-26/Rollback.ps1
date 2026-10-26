$ErrorActionPreference='Stop'
$oldApi='C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM/src/SWPdm.Api/bin/local-main-74cecca-20261022'
$oldWeb='C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM/src/SWPdm.Web/dist/local-main-e40f3e4-20261022/site'
if((Get-FileHash -LiteralPath (Join-Path $oldApi 'api/SWPdm.Api.dll')).Hash -ne '5136F5E3A2B526257604CCCB0C1098A5D6B5AAF4CD079A54B567E00D9C0FAE89'){throw 'Old API mismatch'}
if((Get-FileHash -LiteralPath (Join-Path $oldWeb 'assets/index-DKZLPEHx.js')).Hash -ne '9BC4D54E4483C4D2185DC38FDE9915B18CB28F0D470CF8A0022CCB3091D0D661'){throw 'Old Web mismatch'}
$apiOwners=@(Get-NetTCPConnection -State Listen -LocalPort 5000 -ErrorAction SilentlyContinue|Select-Object -ExpandProperty OwningProcess -Unique)
foreach($owner in $apiOwners){
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$owner"
    if($process.ExecutablePath -eq [IO.Path]::GetFullPath((Join-Path $oldApi 'api/SWPdm.Api.exe'))){continue}
    if($process.ExecutablePath -ne [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'api/SWPdm.Api.exe'))){throw 'Unknown API owner; refusing stop'}
    $running=Get-Process -Id $owner; Stop-Process -Id $owner; if(-not $running.WaitForExit(10000)){throw 'New API did not stop'}
}
if(-not (Get-NetTCPConnection -State Listen -LocalPort 5000 -ErrorAction SilentlyContinue)){& (Join-Path $oldApi 'Start-Api.ps1') -Port 5000}
$webOwners=@(Get-NetTCPConnection -State Listen -LocalPort 5174 -ErrorAction SilentlyContinue|Select-Object -ExpandProperty OwningProcess -Unique)
foreach($owner in $webOwners){
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$owner"
    if($process.CommandLine -like '*local-main-e40f3e4-20261022*'){continue}
    if($process.CommandLine.Replace('\','/') -notlike '*frontend-clarity-20261021*local-main-14de0f5-20261026/site*'){throw 'Unknown Web owner; refusing stop'}
    $running=Get-Process -Id $owner; Stop-Process -Id $owner; if(-not $running.WaitForExit(10000)){throw 'New Web did not stop'}
}
if(-not (Get-NetTCPConnection -State Listen -LocalPort 5174 -ErrorAction SilentlyContinue)){
    $webRoot='C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM/src/SWPdm.Web'
    Start-Process -FilePath 'C:/Program Files/nodejs/node.exe' -ArgumentList @(('"'+(Join-Path $webRoot 'node_modules/vite/bin/vite.js')+'"'),'preview','--host','0.0.0.0','--port','5174','--strictPort','--outDir',('"'+$oldWeb+'"')) -WorkingDirectory $webRoot -WindowStyle Hidden -PassThru
}
# Does not build, migrate, repair data, or alter Vault/configuration. Only restores known fixed artifacts.
