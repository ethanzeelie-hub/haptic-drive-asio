@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0Prepare-ReleaseArtifact.ps1" %*
