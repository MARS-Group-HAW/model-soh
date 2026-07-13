@echo off
REM Analyze MARS results for scenarios 01-06 (WSL Python + venv).
wsl -d Ubuntu-24.04 bash -lc "cd /mnt/c/Users/doria/Documents/model-soh/CarletonDrivingBox && source .venv/bin/activate && python3 scripts/analyze_all_scenarios.py"
exit /b %ERRORLEVEL%
