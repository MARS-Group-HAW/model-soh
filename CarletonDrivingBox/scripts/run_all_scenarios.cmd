@echo off
REM Run MARS scenarios 01-06 via native Windows dotnet (no PowerShell required).
setlocal
cd /d "%~dp0.."

set "PROJECT=SOHCarletonDrivingBox.csproj"

if /i not "%~1"=="--no-build" (
    echo + dotnet build %PROJECT%
    dotnet build %PROJECT%
    if errorlevel 1 exit /b 1
)

for %%s in (01 02 03 04 05 06) do call :run_scenario %%s
echo.
echo Done. Results under results\scenario_XX\
exit /b 0

:run_scenario
echo.
echo === scenario_%1 ===
echo + dotnet run --project %PROJECT% -- configs\config_scenario_%1.json
dotnet run --project %PROJECT% -- configs\config_scenario_%1.json
if errorlevel 1 exit /b 1
goto :eof
