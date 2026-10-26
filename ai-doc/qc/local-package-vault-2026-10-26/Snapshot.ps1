param([Parameter(Mandatory)][ValidateSet('before','precutover','after')][string]$Name)
$ErrorActionPreference='Stop'
$configRoot='C:/Users/user/Documents/GitHub/SW-PDM/src/SWPdm.Api'
$config=Get-Content -LiteralPath (Join-Path $configRoot 'appsettings.Development.json') -Raw | ConvertFrom-Json
$connection=[System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($config.Database.ConnectionString)
if($connection['Database'] -ne 'swpdm' -or $connection['Host'] -notin @('localhost','127.0.0.1') -or [int]$connection['Port'] -ne 5432){throw 'Unexpected actual DB target'}
$savedPassword=$env:PGPASSWORD; $savedOptions=$env:PGOPTIONS
try {
    $env:PGPASSWORD=$connection['Password']
    $env:PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=30000 -c lock_timeout=5000'
    $queries=[System.Collections.Generic.List[string]]::new()
    $queries.Add("SELECT 'target|'||current_database()||'|'||current_setting('port')||'|'||current_setting('transaction_read_only')")
    foreach($table in @('pdm_documents','pdm_document_versions','pdm_custom_properties','pdm_bom_occurrences','pdm_document_identity_changes','pdm_numbering_rules','pdm_number_sequences','pdm_checkout_batches','pdm_checkout_batch_items')){
        $queries.Add("SELECT '$table|'||count(*)||'|'||md5(COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text,'[]')) FROM public.$table t")
    }
    $queries.Add(@'
SELECT 'history|'||count(*)||'|'||md5(COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text,'[]')) FROM public."__EFMigrationsHistory" t
'@)
    $queries.Add(@'
SELECT 'columns|'||count(*)||'|'||md5(jsonb_agg(to_jsonb(t) ORDER BY table_name,ordinal_position)::text) FROM information_schema.columns t WHERE table_schema='public'
'@)
    $queries.Add(@'
SELECT 'constraints|'||count(*)||'|'||md5(jsonb_agg(to_jsonb(t) ORDER BY table_name,conname)::text) FROM (SELECT c.relname table_name, con.conname, con.contype, pg_get_constraintdef(con.oid) definition FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public') t
'@)
    $queries.Add(@'
SELECT 'indexes|'||count(*)||'|'||md5(jsonb_agg(to_jsonb(t) ORDER BY tablename,indexname)::text) FROM pg_indexes t WHERE schemaname='public'
'@)
    $queries.Add(@'
SELECT 'sequences|'||count(*)||'|'||md5(jsonb_agg(to_jsonb(t) ORDER BY sequencename)::text) FROM pg_sequences t WHERE schemaname='public'
'@)
    $queries.Add("SELECT 'tables|'||string_agg(tablename,',' ORDER BY tablename) FROM pg_tables WHERE schemaname='public'")
    $sql="BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;`n"+($queries -join ";`n")+";`nROLLBACK;"
    $rows=@(& 'C:/Program Files/PostgreSQL/16/bin/psql.exe' -h 127.0.0.1 -p 5432 -U $connection['Username'] -d swpdm -w -X -q -v ON_ERROR_STOP=1 -A -t -c $sql)
    if($LASTEXITCODE -ne 0){throw 'Actual read-only snapshot failed'}
    if($rows[0] -ne 'target|swpdm|5432|on'){throw 'Read-only transaction not confirmed'}
    $vault='C:/Users/user/Documents/GitHub/SW-PDM/vault_storage'
    $files=@(Get-ChildItem -LiteralPath $vault -Recurse -File | Sort-Object FullName)
    $vaultEntries=@($files | ForEach-Object {
        if($_.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unexpected Vault reparse file'}
        [IO.Path]::GetRelativePath($vault,$_.FullName)+'|'+$_.Length+'|'+(Get-FileHash -LiteralPath $_.FullName).Hash
    })
    $vaultDigest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($vaultEntries -join "`n"))))
    $configHashes=@('appsettings.json','appsettings.Development.json' | ForEach-Object { [ordered]@{Name=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $configRoot $_)).Hash} })
    $snapshot=[ordered]@{DatabaseRows=$rows;Vault=[ordered]@{FileCount=$files.Count;PathSizeContentSHA256=$vaultDigest};ConfigHashes=$configHashes}
    $path=Join-Path $PSScriptRoot ($Name+'.json')
    [IO.File]::WriteAllText($path,($snapshot|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    $rows
    Write-Output "VAULT_FILE_COUNT=$($files.Count)"
    Write-Output "SNAPSHOT_SHA256=$((Get-FileHash -LiteralPath $path).Hash)"
} finally { $env:PGPASSWORD=$savedPassword; $env:PGOPTIONS=$savedOptions }
