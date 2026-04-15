#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT="$ROOT_DIR/.dotnet"
SAMPLE_PROJECT_PATH="$ROOT_DIR/src/SWPdm.Sample/SWPdm.Sample.csproj"
API_PROJECT_PATH="$ROOT_DIR/src/SWPdm.Api/SWPdm.Api.csproj"
DB_TOOL_PROJECT_PATH="$ROOT_DIR/src/SWPdm.DbTool/SWPdm.DbTool.csproj"

if [[ ! -x "$DOTNET_ROOT/dotnet" ]]; then
  echo "Local .NET SDK not found at: $DOTNET_ROOT/dotnet" >&2
  exit 1
fi

export DOTNET_CLI_HOME="$ROOT_DIR/.dotnet-cli-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES="$ROOT_DIR/.nuget/packages"

"$DOTNET_ROOT/dotnet" restore "$SAMPLE_PROJECT_PATH"
"$DOTNET_ROOT/dotnet" restore "$API_PROJECT_PATH"
"$DOTNET_ROOT/dotnet" restore "$DB_TOOL_PROJECT_PATH"
"$DOTNET_ROOT/dotnet" build "$SAMPLE_PROJECT_PATH" -c Debug --no-restore
"$DOTNET_ROOT/dotnet" build "$API_PROJECT_PATH" -c Debug --no-restore
"$DOTNET_ROOT/dotnet" build "$DB_TOOL_PROJECT_PATH" -c Debug --no-restore
