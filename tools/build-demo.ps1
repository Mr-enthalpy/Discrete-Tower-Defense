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
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
$previousBuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:DOTNET_ROOT = Split-Path $resolvedDotNet -Parent
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
function Run-Checked([string]$Program, [string[]]$Arguments) {
    $engineLog = $null
    if ($Program -eq $GodotPath) {
        $logDirectory = Join-Path $repoRoot 'build/validation'
        New-Item -ItemType Directory -Force $logDirectory | Out-Null
        $engineLog = Join-Path $logDirectory (([guid]::NewGuid().ToString()) + '.log')
        $Arguments = @('--log-file', $engineLog) + $Arguments
    }
    # Avoid a live PowerShell output pipeline holding up Godot's export shutdown.
    & $Program @Arguments
    $hadError = $engineLog -and (Test-Path -LiteralPath $engineLog) -and
        (Select-String -LiteralPath $engineLog -Pattern '^ERROR:' -Quiet)
    if ($LASTEXITCODE -ne 0 -or $hadError) { throw "$Program failed (exit $LASTEXITCODE)" }
}
Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Force game/client/generated | Out-Null
    Copy-Item -LiteralPath content/demo-a.json -Destination game/client/generated/demo-a.json
    Run-Checked $DotNetPath @('build','tests/simulation/DiscreteTD.Checks.csproj','--configuration','Release','--nologo','-m:1','/nodeReuse:false','/p:UseSharedCompilation=false')
    Run-Checked $DotNetPath @('tests/simulation/bin/Release/net10.0/DiscreteTD.Checks.dll')
    Run-Checked $DotNetPath @('build','game/client/DiscreteTD.Godot.csproj','--nologo','-m:1','/nodeReuse:false','/p:UseSharedCompilation=false')
    Run-Checked $GodotPath @('--headless','--path','game/client','--editor','--import','--quit')
    Run-Checked $GodotPath @('--headless','--path','game/client','--','--smoke')
    if ($Export) {
        New-Item -ItemType Directory -Force build/demo-a5 | Out-Null
        Run-Checked $GodotPath @('--headless','--path','game/client','--export-release','Windows Desktop','../../build/demo-a5/DiscreteTD.exe','--quit')
        Copy-Item -LiteralPath DEMO-A.md -Destination build/demo-a5/README.md
        Copy-Item -LiteralPath docs/media -Destination build/demo-a5/screenshots -Recurse -Force
        $readme = [IO.File]::ReadAllText((Join-Path $repoRoot 'build/demo-a5/README.md'))
        $readme = $readme.Replace('(docs/decisions/0004-战场空间语言.md)', '(SPACE-RULES.md)').Replace('(docs/media/', '(screenshots/')
        [IO.File]::WriteAllText((Join-Path $repoRoot 'build/demo-a5/README.md'), $readme, [Text.UTF8Encoding]::new($false))
        $spaceRules = [IO.File]::ReadAllText((Join-Path $repoRoot 'docs/decisions/0004-战场空间语言.md')).Replace('(../../DEMO-A.md)', '(README.md)')
        [IO.File]::WriteAllText((Join-Path $repoRoot 'build/demo-a5/SPACE-RULES.md'), $spaceRules, [Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath third-party -Destination build/demo-a5 -Recurse -Force
    }
}
finally {
    Pop-Location
    $env:PATH = $previousPath
    $env:DOTNET_ROOT = $previousDotNetRoot
    $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = $previousBuildServer
}
