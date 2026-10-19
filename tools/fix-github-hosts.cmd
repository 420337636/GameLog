@echo off
setlocal
cd /d "%~dp0"

rem Run fix-github-hosts.ps1 with administrator rights.
rem Usage:  fix-github-hosts.cmd          (apply)
rem         fix-github-hosts.cmd -Undo    (remove)

net session >nul 2>&1
if errorlevel 1 (
  echo Requesting administrator rights...
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%~1' -Verb RunAs"
  exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fix-github-hosts.ps1" %*
echo.
pause
