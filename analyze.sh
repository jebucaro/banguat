#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck source=.env
  source "$ENV_FILE"
  set +a
else
  echo "Error: .env file not found at $ENV_FILE"
  echo "Copy .env.example to .env and fill in your SONAR_TOKEN."
  exit 1
fi

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "Error: SONAR_TOKEN is not set in .env"
  exit 1
fi

cd "$SCRIPT_DIR"

# Skip the Husky hook-install MSBuild target during restore/build; this is an analysis run, not a commit workflow.
export HUSKY=0

dotnet tool restore

find tests -type d -name TestResults -prune -exec rm -rf {} +

dotnet tool run dotnet-sonarscanner begin \
  /k:"banguat-exchangerates" \
  /d:sonar.host.url="${SONAR_HOST_URL:-http://localhost:9000}" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="tests/**/coverage.opencover.xml" \
  /d:sonar.exclusions="**/obj/**,**/bin/**"

dotnet build --no-incremental --disable-build-servers

# coverage.runsettings replaces each project's own runsettings, so it repeats the filter that keeps the live
# Banguat integration test out of the run.
dotnet test --no-build --collect:"XPlat Code Coverage" --settings coverage.runsettings

dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"
