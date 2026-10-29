# Build and release

Windows 10/11 x64, .NET SDK 8 or newer with the .NET 8 Windows Desktop targeting packs. Development here used SDK 9.0.318 targeting .NET 8. NuGet access is needed for initial restore. No Visual Studio installation is required if the SDK/targeting packs are available.

```powershell
./build.ps1
./build.ps1 -NetworkTests -Publish
```

The first command restores, builds the solution in Release and runs protocol/lifecycle checks. The second additionally runs local multicast/OSCQuery integration checks and publishes a self-contained Windows x64 executable to `artifacts/v0.14.5-win-x64/VRC HydroLink.exe`. It creates `artifacts/VRC-HydroLink-0.14.5-win-x64.zip`. Network tests require local UDP/mDNS access and may need a firewall exception. They advertise a temporary mock service named VRChat-HydroLink-Test; run when the real VRChat client is closed.

Equivalent commands:

```powershell
dotnet restore VRC-HydroLink.sln
dotnet build VRC-HydroLink.sln -c Release --no-restore
dotnet run --project tests/HydroLink.Tests -c Release --no-build -- --network
dotnet publish src/HydroLink.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o artifacts/v0.14.5-win-x64
```

Trimming is disabled because WPF and third-party plugins use runtime reflection. Native runtime components may be extracted by the .NET single-file host. Keep notices and licenses with redistributed binaries.

## Installer

After publishing, compile `installer/HydroLink.iss` with Inno Setup 6 (`ISCC.exe installer/HydroLink.iss`). Installation is per-user and does not require elevation. Settings are retained on uninstall. The installer script has not yet been built or tested on a clean Windows VM. Code signing and release publication are separate release steps.

## GUI smoke check

```powershell
& './artifacts/v0.14.5-win-x64/VRC HydroLink.exe' --render-preview 'C:\path\preview.png'
```

This starts the actual WPF window, renders its initial view, and exits without starting providers. It checks startup and layout rendering, not hardware functionality.

## Release checklist

Run build and tests, verify live VRChat auto-discovery and avatar switching, reconnect all supported providers, check physical fan stop/failsafe and Intiface compatibility, exercise plugins with dependencies and UI, test on a clean Windows x64 machine, build/sign installer, retain source licenses, then publish. Do not describe the preview as feature-complete.
