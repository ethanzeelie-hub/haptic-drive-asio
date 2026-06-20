@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0Test-ReleaseArtifact.ps1" %*
