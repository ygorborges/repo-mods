# Builds every mod in this repo with the portable SDK in .tools\dotnet (nothing is installed system-wide).
# The compiled DLL lands in <Mod>\package\, which is what the Thunderstore Mod Manager link points at.
#
#   .\build.ps1                      # Release build of all mods
#   .\build.ps1 -Configuration Debug
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$tools = Join-Path $root ".tools"
$dotnet = Join-Path $tools "dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    throw "Portable SDK not found at $dotnet. Install it with: .\.tools\dotnet-install.ps1 -Channel 8.0 -InstallDir .\.tools\dotnet -NoPath"
}

$env:DOTNET_ROOT = Join-Path $tools "dotnet"
$env:DOTNET_CLI_HOME = Join-Path $tools "home"
$env:NUGET_PACKAGES = Join-Path $tools "nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

Get-ChildItem -Path $root -Directory | ForEach-Object {
    Get-ChildItem -Path $_.FullName -Filter *.csproj -File | ForEach-Object {
        Write-Host "Building $($_.Name) ($Configuration)..."
        & $dotnet build $_.FullName -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $($_.Name)" }
    }
}
