@echo off
rem Checks the setup, then starts every Titans server in its own window (see Start-Servers.ps1).
rem   Start-Servers.bat              check, then start everything
rem   Start-Servers.bat -NoLogin     without the Login-Server
rem   Start-Servers.bat -CheckOnly   only check
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Servers.ps1" %*
pause
