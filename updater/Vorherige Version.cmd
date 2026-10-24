@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-HydroLink.ps1" -Rollback -Launch
if errorlevel 1 pause
