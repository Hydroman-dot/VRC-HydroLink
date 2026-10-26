@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-HydroLink.ps1" -Install -Launch -DesktopShortcut
if errorlevel 1 pause
