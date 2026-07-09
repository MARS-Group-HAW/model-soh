$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
.\.venv\Scripts\Activate.ps1
python scripts/analyze_run.py @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python scripts/build_heatmap_matrix.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python scripts/plot_heatmap.py
