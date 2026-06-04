@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-HapticDrive.ps1" %*
exit /b %ERRORLEVEL%
