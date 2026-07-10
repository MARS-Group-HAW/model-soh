#!/usr/bin/env bash
# Run all six MARS scenarios (DEVS delay parity). Usage: ./scripts/run_scenarios.sh [01 03 ...]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

DOTNET="${DOTNET:-/mnt/c/Program Files/dotnet/dotnet.exe}"
PROJECT="SOHCarletonDrivingBox.csproj"

scenarios=("$@")
if [ ${#scenarios[@]} -eq 0 ]; then
  scenarios=(01 02 03 04 05 06)
fi

"$DOTNET" build "$PROJECT"

for id in "${scenarios[@]}"; do
  cfg="configs/config_scenario_${id}.json"
  echo "=== scenario_${id} ==="
  "$DOTNET" run --project "$PROJECT" -- "$cfg"
done

echo "Done. Results under results/scenario_XX/"
