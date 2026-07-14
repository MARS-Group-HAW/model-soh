@echo off
REM Plot agent route PNGs for scenario 01-07 (WSL Python + venv).
REM Usage: plot_agent_routes.cmd 07
REM        plot_agent_routes.cmd 07 --lot P3 --limit 20
setlocal
if "%~1"=="" (
    echo Usage: %~nx0 SCENARIO [extra args...]
    echo Example: %~nx0 07
    echo          %~nx0 07 --lot P3 --limit 20
    exit /b 1
)
wsl -d Ubuntu-24.04 bash -lc "cd /mnt/c/Users/doria/Documents/model-soh/CarletonDrivingBox && source .venv/bin/activate && python3 scripts/plot_agent_routes.py %*"
exit /b %ERRORLEVEL%
