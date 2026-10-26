param([ValidateSet(5000,5001,5002)][int]$Port=5000)
$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'api'
$configRoot='C:/Users/user/Documents/GitHub/SW-PDM/src/SWPdm.Api'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'artifact-sha.json') -Raw | ConvertFrom-Json
if($manifest.SourceCommit -ne '14de0f58717cfa58c274efa9e72e6ca475c4e4da'){throw 'Wrong release manifest'}
foreach($entry in $manifest.ApiFiles){if((Get-FileHash -LiteralPath (Join-Path $output $entry.Name)).Hash -ne $entry.SHA256){throw "Artifact mismatch: $($entry.Name)"}}
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Port occupied; inspect owner first'}
$saved=@{}
foreach($name in @('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','LocalStorage__VaultPath','Database__ConnectionString','Database__Provider')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try {
    $env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development';$env:Database__Provider='PostgreSql'
    $db=[System.Data.Common.DbConnectionStringBuilder]::new()
    if($Port -eq 5002){
        $db.set_ConnectionString('Host=127.0.0.1;Port=55433;Database=release_http_20261026;Username=contract;Pooling=false')
        $vault=Join-Path $PSScriptRoot 'isolated-vault'
        New-Item -ItemType Directory -Path $vault -Force | Out-Null
    } else {
        $config=Get-Content -LiteralPath (Join-Path $configRoot 'appsettings.Development.json') -Raw | ConvertFrom-Json
        $db.set_ConnectionString($config.Database.ConnectionString)
        if($db['Database'] -ne 'swpdm' -or $db['Host'] -notin @('localhost','127.0.0.1') -or [int]$db['Port'] -ne 5432){throw 'Unexpected actual DB target'}
        $vault='C:/Users/user/Documents/GitHub/SW-PDM/vault_storage'
    }
    if($Port -eq 5001){$db['Options']='-c default_transaction_read_only=on'}
    $env:Database__ConnectionString=$db.ConnectionString;$env:LocalStorage__VaultPath=$vault
    $bind=if($Port -eq 5000){'0.0.0.0'}else{'127.0.0.1'}
    $process=Start-Process -FilePath (Join-Path $output 'SWPdm.Api.exe') -ArgumentList @('--urls',"http://${bind}:$Port",'--contentRoot',('"'+$configRoot+'"')) -WorkingDirectory $configRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $PSScriptRoot "api-$Port.stdout.log") -RedirectStandardError (Join-Path $PSScriptRoot "api-$Port.stderr.log")
    [pscustomobject]@{ProcessId=$process.Id;Port=$Port;Commit=$manifest.SourceCommit;ReadOnlyActualDatabase=($Port -eq 5001);Isolated=($Port -eq 5002)}
} finally { foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')} }
# Fixed artifacts only. Never builds or calls migration.
