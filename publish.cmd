@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================
echo   GameLog - publish to GitHub
echo ============================================
echo.

where git >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Git is not installed.
  echo.
  echo   Install Git:      https://git-scm.com/download/win
  echo   Or GitHub Desktop: https://desktop.github.com/
  echo.
  pause
  exit /b 1
)

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
  echo [1/5] git init
  git init || goto :fail
)

echo [2/5] git user
for /f "delims=" %%i in ('git config user.email') do set EXISTING_EMAIL=%%i
if "%EXISTING_EMAIL%"=="" (
  set /p EMAIL=GitHub email [420337636@qq.com]:
  if "!EMAIL!"=="" set EMAIL=420337636@qq.com
  git config user.email "!EMAIL!"
)
for /f "delims=" %%i in ('git config user.name') do set EXISTING_NAME=%%i
if "%EXISTING_NAME%"=="" (
  set /p GITNAME=Display name [GameLog]:
  if "!GITNAME!"=="" set GITNAME=GameLog
  git config user.name "!GITNAME!"
)

echo.
echo Create an EMPTY repository on GitHub first (no README, no license),
echo then paste its URL below.
echo Example: https://github.com/yourname/GameLog.git
echo.
set /p REPO=Repository URL:
if "!REPO!"=="" (
  echo [ERROR] Empty URL.
  goto :fail
)

echo.
echo [3/5] stage files (dist/ and user data are ignored by .gitignore)
git add -A || goto :fail
git status --short

echo.
echo [4/5] commit
git commit -m "GameLog 1.0.0: Windows game playtime tracker" || goto :fail

echo.
echo [5/5] push
git branch -M main
git remote remove origin >nul 2>nul
git remote add origin "!REPO!" || goto :fail
git push -u origin main || goto :fail

echo.
echo Done. Repository: !REPO:.git=!
echo Next: create a Release (tag v1.0.0) and upload dist\GameLog.exe + dist\icon.ico
echo Then apply for a free open-source signing certificate: see docs\open-source.md
echo.
start "" "!REPO:.git=!"
pause
exit /b 0

:fail
echo.
echo [FAILED] Command failed. Read the messages above.
pause
exit /b 1
