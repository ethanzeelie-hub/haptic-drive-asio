@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-ReleaseArtifact.ps1" %*
exit /b %ERRORLEVEL%
