param([string]$UpdaterFolder)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$testFolder = Join-Path ([IO.Path]::GetTempPath()) ('HydroLink-download-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testFolder
$zipName = 'VRC-HydroLink-1.0.0-win-x64.zip'
$fixtureZip = Join-Path $testFolder $zipName
$archive = [IO.Compression.ZipFile]::Open($fixtureZip, 'Create')
$writer = New-Object IO.StreamWriter($archive.CreateEntry('VRC HydroLink.exe').Open())
$writer.Write('fixture, never run'); $writer.Dispose(); $archive.Dispose()
$checksum = (Get-FileHash $fixtureZip -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($fixtureZip + '.sha256'), $checksum)
function Invoke-RestMethod {
    param($Uri, $Headers, $TimeoutSec)
    if ($Uri -ne 'https://api.github.com/repos/Hydroman-dot/VRC-HydroLink/releases/latest') { throw 'Wrong API URL' }
    return [pscustomobject]@{ tag_name='v1.0.0'; draft=$false; prerelease=$false; assets=@(
        [pscustomobject]@{ name=$zipName; browser_download_url="https://github.com/Hydroman-dot/VRC-HydroLink/releases/download/v1.0.0/$zipName" },
        [pscustomobject]@{ name="$zipName.sha256"; browser_download_url="https://github.com/Hydroman-dot/VRC-HydroLink/releases/download/v1.0.0/$zipName.sha256" }
    ) }
}
function Invoke-WebRequest {
    param($Uri, $OutFile, [switch]$UseBasicParsing, $TimeoutSec)
    $leaf = [IO.Path]::GetFileName(([uri]$Uri).AbsolutePath)
    Copy-Item -LiteralPath (Join-Path $testFolder $leaf) -Destination $OutFile
}
function Get-Process { param($Name, $ErrorAction) return @() }
$previousData = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $testFolder 'localappdata'
    & (Join-Path $UpdaterFolder 'Update-HydroLink.ps1')
    $stateFile = Join-Path $env:LOCALAPPDATA 'VRC HydroLink Updates/state.json'
    if (Test-Path $stateFile) { throw 'Check-only unexpectedly installed' }
    & (Join-Path $UpdaterFolder 'Update-HydroLink.ps1') -Install
    if ((Get-Content $stateFile -Raw | ConvertFrom-Json).current -ne '1.0.0') { throw 'Install missing' }
    & (Join-Path $UpdaterFolder 'Update-HydroLink.ps1') -Install
    Write-Host 'PASS: simulated GitHub check-only, download/install, and no-op update; no app launched.'
} finally { $env:LOCALAPPDATA = $previousData }
