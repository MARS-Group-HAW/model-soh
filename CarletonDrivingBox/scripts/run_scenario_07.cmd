@echo off
REM Run scenario 07 only (Bronson/Raven emergency exit).
setlocal
cd /d "%~dp0.."

set "PROJECT=SOHCarletonDrivingBox.csproj"

if /i not "%~1"=="--no-build" (
    echo + dotnet build %PROJECT%
    dotnet build %PROJECT%
    if errorlevel 1 exit /b 1
)

echo.
echo === scenario_07 (Bronson/Raven emergency exit) ===
echo + dotnet run --project %PROJECT% -- configs\config_scenario_07.json
dotnet run --project %PROJECT% -- configs\config_scenario_07.json
exit /b %ERRORLEVEL%
