@echo off
setlocal enabledelayedexpansion
rem GameLog build script - uses the .NET Framework compiler that ships with Windows.
rem Usage: build.cmd  (output: dist\GameLog.exe)

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "GAC=%WINDIR%\Microsoft.NET\assembly"

if not exist "%CSC%" (
  echo [ERROR] csc.exe not found: %CSC%
  exit /b 1
)
if not exist dist mkdir dist
if not exist dist\icon.ico copy /y assets\icon.ico dist\icon.ico >nul

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:3 ^
  /out:dist\GameLog.exe ^
  /win32icon:assets\icon.ico ^
  /win32manifest:assets\app.manifest ^
  /r:System.dll ^
  /r:System.Core.dll ^
  /r:System.Xml.dll ^
  /r:System.Web.Extensions.dll ^
  /r:System.Net.Http.dll ^
  /r:System.Windows.Forms.dll ^
  /r:System.Drawing.dll ^
  /r:"%GAC%\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll" ^
  /r:"%GAC%\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll" ^
  /r:"%GAC%\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" ^
  /r:"%GAC%\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll" ^
  src\*.cs

if errorlevel 1 (
  echo.
  echo [BUILD FAILED]
  exit /b 1
)
echo.
echo [OK] dist\GameLog.exe
for %%F in (dist\GameLog.exe) do echo size: %%~zF bytes

rem Optional: sign with the local self-signed certificate.
rem set SIGN_LOCAL=1 before running build.cmd to enable.
if /i "%SIGN_LOCAL%"=="1" (
  echo.
  echo [sign] signing with local certificate...
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\sign-local.ps1"
)
