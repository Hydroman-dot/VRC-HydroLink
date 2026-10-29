param([switch]$Publish, [switch]$NetworkTests)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet restore VRC-HydroLink.sln
    if ($LASTEXITCODE) { throw 'Restore failed' }
    dotnet build VRC-HydroLink.sln -c Release --no-restore
    if ($LASTEXITCODE) { throw 'Build failed' }
    $testArguments = @('run', '--project', 'tests/HydroLink.Tests', '-c', 'Release', '--no-build')
    $testArguments += @('--', '--plugin', 'examples/ExamplePlugin/bin/Release/net8.0')
    if ($NetworkTests) { $testArguments += '--network' }
    & dotnet @testArguments
    if ($LASTEXITCODE) { throw 'Tests failed' }
    dotnet run --project tests/HydroLink.Tests -c Release --no-build -- --mqtt-only
    if ($LASTEXITCODE) { throw 'MQTT tests failed' }
    & (Join-Path $PSScriptRoot 'updater/Test-Updater.ps1')
    & (Join-Path $PSScriptRoot 'updater/Test-Download.ps1') -UpdaterFolder (Join-Path $PSScriptRoot 'updater')
    if ($Publish) {
        [xml]$project = Get-Content src/HydroLink.App/HydroLink.App.csproj
        $version = [string]$project.Project.PropertyGroup.Version
        $publishFolder = "artifacts/v$version-win-x64"
        $archivePath = "artifacts/VRC-HydroLink-$version-win-x64.zip"
        dotnet publish src/HydroLink.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $publishFolder
        if ($LASTEXITCODE) { throw 'Publish failed' }
        Copy-Item README.md,THIRD-PARTY-NOTICES.md -Destination $publishFolder
        Copy-Item licenses -Destination $publishFolder -Recurse -Force
        Copy-Item docs -Destination $publishFolder -Recurse -Force
        Copy-Item updater -Destination $publishFolder -Recurse -Force
        Compress-Archive -Path "$publishFolder/*" -DestinationPath $archivePath -Force
        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText((Join-Path $PSScriptRoot "$archivePath.sha256"), "$hash  $([IO.Path]::GetFileName($archivePath))")
        Compress-Archive -Path updater/* -DestinationPath artifacts/HydroLink-Updater.zip -Force
    }
} finally { Pop-Location }
