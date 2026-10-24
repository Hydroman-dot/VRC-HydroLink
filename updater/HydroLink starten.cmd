@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-HydroLink.ps1" -StartOnly
if errorlevel 1 pause
