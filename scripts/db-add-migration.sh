#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT="$ROOT_DIR/.dotnet"
PROJECT_PATH="$ROOT_DIR/src/SWPdm.Sample/SWPdm.Sample.csproj"
STARTUP_PROJECT_PATH="$ROOT_DIR/src/SWPdm.DbTool/SWPdm.DbTool.csproj"
MIGRATION_NAME="${1:-}"

if [[ -z "$MIGRATION_NAME" ]]; then
  echo "Usage: ./scripts/db-add-migration.sh <MigrationName>" >&2
  exit 1
fi

export DOTNET_CLI_HOME="$ROOT_DIR/.dotnet-cli-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES="$ROOT_DIR/.nuget/packages"
export PDM_DB_PROVIDER="${PDM_DB_PROVIDER:-PostgreSql}"
export PDM_DB_CONNECTION_STRING="${PDM_DB_CONNECTION_STRING:-Host=localhost;Port=5432;Database=swpdm;Username=postgres;Password=postgres}"

"$DOTNET_ROOT/dotnet" dotnet-ef migrations add "$MIGRATION_NAME" \
  --project "$PROJECT_PATH" \
  --startup-project "$STARTUP_PROJECT_PATH" \
  --context SWPdm.Sample.Data.PdmDbContext \
  --output-dir Data/Migrations
