# Windows PowerShell 5.1 or PowerShell 7; public GitHub releases, no embedded credentials.
param([switch]$Install, [switch]$Launch, [switch]$Rollback, [switch]$StartOnly, [switch]$DesktopShortcut, [int]$WaitForPid = 0, [string]$ExpectedVersion = "")
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HydroLink.Update.psm1') -Force
$repo = 'Hydroman-dot/VRC-HydroLink'
$root = Join-Path $env:LOCALAPPDATA 'VRC HydroLink Updates'
$lock = $null
try {
    if ($WaitForPid -gt 0) {
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while (Get-Process -Id $WaitForPid -ErrorAction SilentlyContinue) {
            if ([DateTime]::UtcNow -ge $deadline) { throw 'HydroLink hat sich nicht rechtzeitig beendet. Update abgebrochen.' }
            Start-Sleep -Milliseconds 250
        }
    }
    Assert-HydroPath $root
    $null = New-Item -ItemType Directory -Path $root -Force
    $lockPath = Join-Path $root 'update.lock'
    Assert-HydroPath $lockPath
    $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
    if (($Install -or $Rollback -or $Launch -or $StartOnly) -and (Get-Process -Name 'VRC HydroLink' -ErrorAction SilentlyContinue)) { throw 'Bitte HydroLink zuerst schliessen. Die App wird nicht zwangsweise beendet.' }
    $state = Read-HydroState $root
    $folder = $null
    if ($Rollback) { $folder = Undo-HydroUpdate $root; Write-Host 'Vorherige Version ausgewaehlt.' }
    elseif ($StartOnly) {
        if (!$state) { throw 'Noch keine Version mit diesem Updater installiert.' }
        $folder = Get-HydroVersionFolder $root $state.current
    } else {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        try { $release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'HydroLink-Updater'; Accept = 'application/vnd.github+json' } -TimeoutSec 30 }
        catch { throw 'Kein oeffentliches GitHub-Release erreichbar. Solange das Repository privat ist oder kein Release veroeffentlicht wurde, kann dieser Updater nichts herunterladen. Netzwerk und GitHub-Limit ebenfalls pruefen.' }
        if ($release.draft -or $release.prerelease) { throw 'Entwuerfe und Vorab-Releases werden nicht installiert.' }
        $version = $release.tag_name -replace '^v', ''
        $null = Get-HydroVersionFolder $root $version
        if ($ExpectedVersion -and $version -cne $ExpectedVersion) { throw 'Das angebotene Release hat sich geaendert. Bitte HydroLink erneut starten und das Update bestaetigen.' }
        Write-Host ('GitHub-Version: ' + $version)
        if ($state -and [version]$version -le [version]$state.current) {
            Write-Host ('Installiert: ' + $state.current + ' - kein neueres Release.')
            $folder = Get-HydroVersionFolder $root $state.current
        } elseif ($Install) {
            $name = "VRC-HydroLink-$version-win-x64.zip"
            $asset = @($release.assets | Where-Object name -eq $name)
            $checksum = @($release.assets | Where-Object name -eq "$name.sha256")
            if ($asset.Count -ne 1 -or $checksum.Count -ne 1) { throw 'Release ist unvollstaendig: ZIP oder SHA256-Datei fehlt.' }
            $download = Join-Path $root ('.download-' + [guid]::NewGuid().ToString('N'))
            $null = New-Item -ItemType Directory -Path $download
            foreach ($item in @($asset[0], $checksum[0])) {
                $uri = [uri]$item.browser_download_url
                if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or !$uri.AbsolutePath.StartsWith("/$repo/releases/download/", [StringComparison]::OrdinalIgnoreCase)) { throw 'Unerwartete Download-Adresse.' }
                Invoke-WebRequest $uri.AbsoluteUri -OutFile (Join-Path $download $item.name) -UseBasicParsing -TimeoutSec 300
            }
            $hash = ((Get-Content -LiteralPath (Join-Path $download "$name.sha256") -Raw).Trim() -split '\s+')[0]
            $folder = Install-HydroPackage $root $version (Join-Path $download $name) $hash
            Write-Host ('Installiert: ' + $version + '. Vorherige Version bleibt erhalten.')
        } else { Write-Host 'Zum Installieren bitte Update starten.cmd verwenden.' }
    }
    if ($DesktopShortcut -and $folder) {
        $desktop = [Environment]::GetFolderPath('DesktopDirectory')
        if (!$desktop) { throw 'Desktop-Ordner nicht gefunden.' }
        $shortcutPath = Join-Path $desktop 'HydroLink.lnk'
        # Windows may redirect the known Desktop folder through OneDrive.
        # Reject a redirected shortcut file, but allow the OS-selected Desktop.
        if ((Test-Path -LiteralPath $shortcutPath) -and ((Get-Item -LiteralPath $shortcutPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Die Desktop-Verknuepfung selbst darf kein umgeleiteter Dateipfad sein.' }
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = Join-Path $folder 'VRC HydroLink.exe'
        $shortcut.WorkingDirectory = $folder
        $shortcut.IconLocation = $shortcut.TargetPath + ',0'
        $shortcut.Description = 'HydroLink starten'
        $shortcut.Save()
    }
    if (($Launch -or $StartOnly) -and $folder) {
        Assert-HydroPath $folder
        $exe = Join-Path $folder 'VRC HydroLink.exe'
        Assert-HydroPath $exe
        if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Installierte Anwendung fehlt.' }
        Start-Process -FilePath $exe -WorkingDirectory $folder -WindowStyle Hidden
    }
} catch {
    $message = 'Update fehlgeschlagen: ' + $_.Exception.Message
    Write-Host $message -ForegroundColor Red
    if ($WaitForPid -gt 0) { Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show($message + "`nDie bisherige Version bleibt im Installationsordner erhalten.", 'HydroLink Update') }
    exit 1
}
finally { if ($lock) { $lock.Dispose() } }
