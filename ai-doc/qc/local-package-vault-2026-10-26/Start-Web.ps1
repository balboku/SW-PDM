param([ValidateSet(5174,5175)][int]$Port=5174)
$ErrorActionPreference='Stop'
$web='C:/Users/user/.codex/worktrees/frontend-clarity-20261021/SW-PDM/src/SWPdm.Web'
$site=Join-Path $web 'dist/local-main-14de0f5-20261026/site'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'artifact-sha.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.WebFiles){if((Get-FileHash -LiteralPath (Join-Path $site $file.Name)).Hash -ne $file.SHA256){throw "Web artifact mismatch: $($file.Name)"}}
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Web port occupied; inspect owner first'}
$bind=if($Port -eq 5174){'0.0.0.0'}else{'127.0.0.1'}
$arguments=@(('"'+(Join-Path $web 'node_modules/vite/bin/vite.js')+'"'),'preview','--host',$bind,'--port',"$Port",'--strictPort','--outDir',('"'+$site+'"'))
$process=Start-Process -FilePath 'C:/Program Files/nodejs/node.exe' -ArgumentList $arguments -WorkingDirectory $web -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $PSScriptRoot "web-$Port.stdout.log") -RedirectStandardError (Join-Path $PSScriptRoot "web-$Port.stderr.log")
[pscustomobject]@{ProcessId=$process.Id;Port=$Port;SourceCommit=$manifest.SourceCommit}
