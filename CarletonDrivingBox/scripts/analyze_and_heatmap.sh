#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
source .venv/bin/activate

CSV="${1:-results/CarletonCarDriver.csv}"
if [[ ! -f "$CSV" && -f results/CarDriver.csv ]]; then
  CSV="results/CarDriver.csv"
fi
python3 scripts/analyze_run.py "$@"
python3 scripts/build_heatmap_matrix.py "$CSV"
python3 scripts/plot_heatmap.py "$(dirname "$CSV")/heatmap_matrix.csv"
