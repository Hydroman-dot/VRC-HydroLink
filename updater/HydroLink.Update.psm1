Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-HydroVersionFolder([string]$Root, [string]$Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Ungueltige Versionsnummer.' }
    $null = [version]$Version
    return Join-Path ([IO.Path]::GetFullPath($Root)) $Version
}
function Assert-HydroPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Verknuepfte Installationspfade werden nicht verwendet.' }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}
function Read-HydroState([string]$Root) {
    $file = Join-Path $Root 'state.json'
    Assert-HydroPath $file
    if (!(Test-Path -LiteralPath $file)) { return $null }
    $state = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $null = Get-HydroVersionFolder $Root $state.current
    if ($state.previous) { $null = Get-HydroVersionFolder $Root $state.previous }
    return $state
}
function Write-HydroState([string]$Root, [string]$Current, $Previous) {
    $null = Get-HydroVersionFolder $Root $Current
    if ($Previous) { $null = Get-HydroVersionFolder $Root $Previous }
    Assert-HydroPath $Root
    $file = Join-Path $Root 'state.json'
    Assert-HydroPath $file
    $temp = Join-Path $Root ([guid]::NewGuid().ToString('N') + '.state.tmp')
    [IO.File]::WriteAllText($temp, (@{ current = $Current; previous = $Previous } | ConvertTo-Json))
    if (Test-Path -LiteralPath $file) { [IO.File]::Replace($temp, $file, (Join-Path $Root 'state.previous.json')) }
    else { [IO.File]::Move($temp, $file) }
}
function Install-HydroPackage([string]$Root, [string]$Version, [string]$Zip, [string]$Hash) {
    $destination = Get-HydroVersionFolder $Root $Version
    Assert-HydroPath $destination
    if ($Hash -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash -ne $Hash) { throw 'SHA256-Pruefung fehlgeschlagen. Alte Installation bleibt erhalten.' }
    if (Test-Path -LiteralPath $destination) { throw 'Versionsordner existiert bereits. Keine vorhandene Version wird ueberschrieben.' }
    $state = Read-HydroState $Root
    if ($state -and [version]$Version -le [version]$state.current) { throw 'Downgrade nur ueber die Rueckkehr-Funktion erlaubt.' }
    $stage = Join-Path $Root ('.extract-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $stage -Force
    $archive = [IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        $total = 0L
        $targets = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        if ($archive.Entries.Count -gt 20000) { throw 'Zu viele Dateien im Paket.' }
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('/', '\')
            if ([IO.Path]::IsPathRooted($name) -or $name.Contains(':') -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Ungueltiger ZIP-Pfad.' }
            $target = [IO.Path]::GetFullPath((Join-Path $stage $name))
            if (!$target.StartsWith($stage + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'ZIP-Pfad verlaesst den Installationsordner.' }
            if (!$targets.Add($target)) { throw 'Doppelter ZIP-Pfad.' }
            $total += $entry.Length
            if ($total -gt 2GB) { throw 'Entpacktes Paket ist zu gross.' }
        }
    } finally { $archive.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($Zip, $stage)
    if (!(Test-Path -LiteralPath (Join-Path $stage 'VRC HydroLink.exe') -PathType Leaf)) { throw 'HydroLink-Anwendung fehlt im Paket.' }
    [IO.Directory]::Move($stage, $destination)
    Write-HydroState $Root $Version $(if ($state) { $state.current } else { $null })
    return $destination
}
function Undo-HydroUpdate([string]$Root) {
    $state = Read-HydroState $Root
    if (!$state -or !$state.previous) { throw 'Keine vorherige Version vorhanden.' }
    $folder = Get-HydroVersionFolder $Root $state.previous
    Assert-HydroPath $folder
    if (!(Test-Path -LiteralPath (Join-Path $folder 'VRC HydroLink.exe') -PathType Leaf)) { throw 'Vorherige Installation fehlt.' }
    Write-HydroState $Root $state.previous $state.current
    return $folder
}
Export-ModuleMember -Function Get-HydroVersionFolder, Assert-HydroPath, Read-HydroState, Install-HydroPackage, Undo-HydroUpdate
