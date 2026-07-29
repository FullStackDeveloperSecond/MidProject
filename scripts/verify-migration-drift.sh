#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "${script_directory}/.." && pwd)"
project_path="${repository_root}/MidProject/MidProject.csproj"

cd "${repository_root}"

dotnet build "${project_path}" --no-restore

dotnet "${repository_root}/MidProject/bin/Debug/net8.0/MidProject.dll" \
    --verify-migration-drift
