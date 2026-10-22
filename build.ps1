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
    if ($Publish) {
        dotnet publish src/HydroLink.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o artifacts/v0.12.1-win-x64
        if ($LASTEXITCODE) { throw 'Publish failed' }
        Copy-Item README.md,THIRD-PARTY-NOTICES.md -Destination artifacts/v0.12.1-win-x64
        Copy-Item licenses -Destination artifacts/v0.12.1-win-x64 -Recurse -Force
        Copy-Item docs -Destination artifacts/v0.12.1-win-x64 -Recurse -Force
        Compress-Archive -Path artifacts/v0.12.1-win-x64/* -DestinationPath artifacts/VRC-HydroLink-0.12.1-win-x64.zip -Force
    }
} finally { Pop-Location }
