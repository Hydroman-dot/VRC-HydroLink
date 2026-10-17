; Compile with Inno Setup 6 after running build.ps1 -Publish.
[Setup]
SetupIconFile=..\src\HydroLink.App\Assets\HydroLink.ico
AppId={{B7ED767D-E3E8-4A44-89D1-0F9D1B380463}
AppName=VRC HydroLink
AppVersion=0.10.0
DefaultDirName={localappdata}\Programs\VRC HydroLink
DefaultGroupName=VRC HydroLink
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=VRC-HydroLink-0.10.0-Setup-x64
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\VRC HydroLink.exe
[Files]
Source: "..\artifacts\v0.10.0-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\VRC HydroLink"; Filename: "{app}\VRC HydroLink.exe"
[Run]
Filename: "{app}\VRC HydroLink.exe"; Description: "VRC HydroLink starten"; Flags: nowait postinstall skipifsilent
