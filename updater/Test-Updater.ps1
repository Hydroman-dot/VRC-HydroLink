$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HydroLink.Update.psm1') -Force
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('HydroLink-updater-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot
$installRoot = Join-Path $testRoot 'install'
$null = New-Item -ItemType Directory -Path $installRoot
$count = 0
function Check([bool]$Condition, [string]$Name) { if (!$Condition) { throw "FAIL: $Name" }; $script:count++; Write-Host "PASS: $Name" }
function Package([string]$Name, [string]$Entry, [string]$Text) {
    $file = Join-Path $testRoot $Name
    $zip = [IO.Compression.ZipFile]::Open($file, 'Create')
    try {
        $entryObject = $zip.CreateEntry($Entry)
        $writer = New-Object IO.StreamWriter($entryObject.Open())
        try { $writer.Write($Text) } finally { $writer.Dispose() }
    } finally { $zip.Dispose() }
    return $file
}
$one = Package 'one.zip' 'VRC HydroLink.exe' 'test executable - never launched'
$hash = (Get-FileHash $one -Algorithm SHA256).Hash
$null = Install-HydroPackage $installRoot '1.0.0' $one $hash
Check ((Read-HydroState $installRoot).current -eq '1.0.0') 'first install commits selected version'
$two = Package 'two.zip' 'VRC HydroLink.exe' 'new test executable - never launched'
$hashTwo = (Get-FileHash $two -Algorithm SHA256).Hash
$null = Install-HydroPackage $installRoot '1.0.1' $two $hashTwo
Check ((Read-HydroState $installRoot).previous -eq '1.0.0' -and (Test-Path (Join-Path $installRoot '1.0.0/VRC HydroLink.exe'))) 'update preserves previous application'
$null = Undo-HydroUpdate $installRoot
Check ((Read-HydroState $installRoot).current -eq '1.0.0') 'rollback selects previous application'
$badHash = $false
try { $null = Install-HydroPackage $installRoot '1.0.2' $two ('0' * 64) } catch { $badHash = $true }
Check ($badHash -and (Read-HydroState $installRoot).current -eq '1.0.0') 'corrupt download cannot change selected version'
$badPath = Package 'traversal.zip' '../escaped.txt' 'do not extract'
$blocked = $false
try { $null = Install-HydroPackage $installRoot '1.0.2' $badPath (Get-FileHash $badPath -Algorithm SHA256).Hash } catch { $blocked = $true }
Check ($blocked -and !(Test-Path (Join-Path $installRoot 'escaped.txt'))) 'ZIP traversal rejected before extraction'
$missing = Package 'missing.zip' 'readme.txt' 'no application'
$blocked = $false
try { $null = Install-HydroPackage $installRoot '1.0.2' $missing (Get-FileHash $missing -Algorithm SHA256).Hash } catch { $blocked = $true }
Check ($blocked -and (Read-HydroState $installRoot).current -eq '1.0.0') 'missing application keeps previous state'
$blocked = $false
try { $null = Install-HydroPackage $installRoot '../outside' $one $hash } catch { $blocked = $true }
Check $blocked 'invalid version cannot escape install root'
$blocked = $false
try { $null = Install-HydroPackage $installRoot '0.9.0' $one $hash } catch { $blocked = $true }
Check $blocked 'release downgrade rejected'
$blocked = $false
try { $null = Install-HydroPackage $installRoot '1.0.1' $two $hashTwo } catch { $blocked = $true }
Check $blocked 'existing version never overwritten'
Check ((Get-Content (Join-Path $installRoot '1.0.0/VRC HydroLink.exe') -Raw) -eq 'test executable - never launched') 'old installation bytes preserved'
Write-Host "FINAL: $count updater checks passed. Fixtures: $testRoot"
