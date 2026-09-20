# Links a mod's package\ folder into the Thunderstore Mod Manager profile so the game loads it straight from this repo.
# Uses a junction (no admin / Developer Mode needed). Re-running is safe.
#
#   .\Link-DevMod.ps1 -Mod SpecialOrders                          # profile "Default"
#   .\Link-DevMod.ps1 -Mod SpecialOrders -ProfileName MyProfile
param(
    [Parameter(Mandatory = $true)][string]$Mod,
    [string]$ProfileName = "Default",
    [string]$Namespace = "vibez"
)

$ErrorActionPreference = "Stop"
$source = Join-Path $PSScriptRoot "$Mod\package"
$plugins = Join-Path $env:APPDATA "Thunderstore Mod Manager\DataFolder\REPO\profiles\$ProfileName\BepInEx\plugins"
$link = Join-Path $plugins "$Namespace-$Mod"

if (-not (Test-Path $source)) { throw "Nothing to link: $source does not exist." }
if (-not (Test-Path $plugins)) { throw "Profile plugins folder not found: $plugins" }

if (Test-Path $link) {
    $item = Get-Item $link -Force
    if ($item.LinkType -eq "Junction") {
        Write-Host "Already linked: $link -> $($item.Target)"
        return
    }
    throw "$link exists and is a real folder, not a junction. Move it away first."
}

New-Item -ItemType Junction -Path $link -Target $source | Out-Null
Write-Host "Linked: $link -> $source"
