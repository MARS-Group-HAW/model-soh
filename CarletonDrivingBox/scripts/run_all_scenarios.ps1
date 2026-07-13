# Run MARS campus evacuation scenarios 01-06 via native Windows dotnet.
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Scenarios = @('01', '02', '03', '04', '05', '06'),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$Project = 'SOHCarletonDrivingBox.csproj'

if (-not $NoBuild) {
    Write-Host "+ dotnet build $Project"
    dotnet build $Project
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

foreach ($sid in $Scenarios) {
    $sid = $sid.PadLeft(2, '0')
    $cfg = "configs\config_scenario_$sid.json"
    if (-not (Test-Path -LiteralPath $cfg)) {
        Write-Error "Missing config: $cfg"
        exit 1
    }

    Write-Host ""
    Write-Host "=== scenario_$sid ==="
    Write-Host "+ dotnet run --project $Project -- $cfg"
    dotnet run --project $Project -- $cfg
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ""
Write-Host "Done. Results under results/scenario_XX/"
