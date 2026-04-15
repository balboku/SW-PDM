# SW PDM Backend Blueprint

This workspace contains a reference implementation for the following stack:

- CAD source: SolidWorks (`.sldprt`, `.sldasm`, `.slddrw`)
- Backend: C# ASP.NET Core
- CAD parsing: SolidWorks Document Manager API
- Database: PostgreSQL or SQL Server
- Binary storage: Google Drive API v3

## Included files

- `docs/pdm-postgresql-schema.sql`
- `docs/pdm-postgresql-ef-idempotent.sql`
- `src/SWPdm.Api/`
- `src/SWPdm.DbTool/`
- `src/SWPdm.Sample/Data/`
- `src/SWPdm.Sample/Data/Migrations/`
- `src/SWPdm.Sample/Services/GoogleDriveStorageService.cs`
- `src/SWPdm.Sample/Services/SolidWorksDocumentManagerService.cs`
- `src/SWPdm.Sample/SWPdm.Sample.csproj`
- `scripts/build-all.sh`
- `scripts/db-add-migration.sh`
- `scripts/db-update.sh`
- `scripts/db-script.sh`
- `scripts/run-api.sh`
- `docs/assembly-download-package-flow.md`

## NuGet and references

### Google Drive service

Install:

```bash
dotnet restore src/SWPdm.Sample/SWPdm.Sample.csproj
dotnet build src/SWPdm.Sample/SWPdm.Sample.csproj
```

If you use the workspace-local SDK installed in this repository, run:

```bash
./scripts/build-sample.sh
./scripts/build-all.sh
./scripts/run-api.sh
```

## Current API surface

The API host currently exposes:

- `GET /health`
- `GET /api/config/status`
- `GET /api/database/status`
- `POST /api/database/migrate`
- `GET /api/documents/{documentId}`
- `GET /api/versions/{versionId}`
- `GET /api/versions/{versionId}/children`
- `GET /api/assemblies/{rootVersionId}/package-closure`
- `POST /api/ingest/cad`
- `POST /api/drive/upload`
- `POST /api/drive/download`
- `POST /api/solidworks/parse`

## Database layer

The current implementation includes:

- `PdmDbContext` for `pdm_documents`, `pdm_document_versions`, `pdm_custom_properties`, `pdm_bom_occurrences`
- `PdmRepository` for document/version reads and BOM traversal
- `InitialCreate` EF Core migration for PostgreSQL
- provider selection for PostgreSQL or SQL Server
- a PostgreSQL and SQL Server compatible recursive package-closure query path

Database scripts:

```bash
./scripts/db-add-migration.sh <MigrationName>
./scripts/db-update.sh
./scripts/db-script.sh
```

Default environment variables used by those scripts:

```bash
export PDM_DB_PROVIDER=PostgreSql
export PDM_DB_CONNECTION_STRING='Host=localhost;Port=5432;Database=swpdm;Username=postgres;Password=postgres'
```

The migration tooling uses `src/SWPdm.DbTool/` as the startup project to avoid design-time issues with the Web API host.

## Ingest workflow

`POST /api/ingest/cad` now performs:

1. parse SolidWorks metadata through Document Manager
2. optionally recurse referenced assembly children
3. upload each physical file to Google Drive
4. create/update `pdm_documents`
5. insert `pdm_document_versions`
6. persist `pdm_custom_properties`
7. persist assembly `pdm_bom_occurrences`

Example request body:

```json
{
  "localFilePath": "/absolute/path/to/TopLevel.sldasm",
  "driveFolderId": "your-google-drive-folder-id",
  "ingestReferencedFiles": true,
  "additionalSearchPaths": [
    "/absolute/path/to/library",
    "/absolute/path/to/standard-parts"
  ]
}
```

### SolidWorks Document Manager service

There is usually no official NuGet package for Document Manager in enterprise setups.
Instead, add a direct assembly reference to:

```text
<SOLIDWORKS install directory>\api\redist\SolidWorks.Interop.swdocumentmgr.dll
```

For this sample project, copy that DLL to:

```text
lib/SolidWorks.Interop.swdocumentmgr.dll
```

You also need:

- a valid Document Manager license key
- the Document Manager COM components registered on the machine

## BOM design rationale

`pdm_bom_occurrences` is the most important table in this design.

It uses:

- `parent_version_id`
- `child_version_id`

instead of:

- `parent_document_id`
- `child_document_id`

because BOMs are revision-sensitive.

Example:

- Assembly `A-100`, version 3 may reference Part `P-010`, version 7
- Assembly `A-100`, version 4 may reference Part `P-010`, version 8

If the BOM were stored only at the document level, you would lose historical accuracy.

The table also stores:

- `source_reference_path`
- `package_relative_path`
- `occurrence_path`

Those fields make download packaging and reference rewriting deterministic.
