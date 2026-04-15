#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT="$ROOT_DIR/.dotnet"
API_PROJECT_PATH="$ROOT_DIR/src/SWPdm.Api/SWPdm.Api.csproj"

if [[ ! -x "$DOTNET_ROOT/dotnet" ]]; then
  echo "Local .NET SDK not found at: $DOTNET_ROOT/dotnet" >&2
  exit 1
fi

export DOTNET_CLI_HOME="$ROOT_DIR/.dotnet-cli-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES="$ROOT_DIR/.nuget/packages"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

"$DOTNET_ROOT/dotnet" run --project "$API_PROJECT_PATH" --no-launch-profile --no-restore
