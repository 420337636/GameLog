@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -Command "Get-AuthenticodeSignature '%~dp0..\dist\GameLog.exe' | Format-List Status,StatusMessage,SignerCertificate,TimeStamperCertificate"
echo.
pause
