param(
    [string]$GodotPath = 'godot',
    [string]$DotNetPath = 'dotnet',
    [switch]$Export
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$resolvedDotNet = (Get-Command $DotNetPath -ErrorAction Stop).Source
$previousPath = $env:PATH
$previousDotNetRoot = $env:DOTNET_ROOT
$env:DOTNET_ROOT = Split-Path $resolvedDotNet -Parent
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
function Run-Checked([string]$Program, [string[]]$Arguments) {
    $hadError = $false
    & $Program @Arguments 2>&1 | ForEach-Object {
        Write-Output $_
        if ("$_" -match '^ERROR:') { $hadError = $true }
    }
    if ($LASTEXITCODE -ne 0 -or $hadError) { throw "$Program failed (exit $LASTEXITCODE)" }
}
Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Force game/client/generated | Out-Null
    Copy-Item -LiteralPath content/demo-a.json -Destination game/client/generated/demo-a.json
    Run-Checked $DotNetPath @('run','--project','tests/simulation/DiscreteTD.Checks.csproj','--configuration','Release')
    Run-Checked $DotNetPath @('build','game/client/DiscreteTD.Godot.csproj','--nologo')
    Run-Checked $GodotPath @('--headless','--path','game/client','--editor','--import','--quit')
    Run-Checked $GodotPath @('--headless','--path','game/client','--','--smoke')
    if ($Export) {
        New-Item -ItemType Directory -Force build/demo-a | Out-Null
        Run-Checked $GodotPath @('--headless','--path','game/client','--export-release','Windows Desktop','../../build/demo-a/DiscreteTD.exe')
        Copy-Item -LiteralPath DEMO-A.md -Destination build/demo-a/README.md
        Copy-Item -LiteralPath third-party -Destination build/demo-a -Recurse -Force
    }
}
finally {
    Pop-Location
    $env:PATH = $previousPath
    $env:DOTNET_ROOT = $previousDotNetRoot
}
