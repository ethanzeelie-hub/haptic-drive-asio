@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Prepare-ReleaseArtifact.ps1" %*
exit /b %ERRORLEVEL%
