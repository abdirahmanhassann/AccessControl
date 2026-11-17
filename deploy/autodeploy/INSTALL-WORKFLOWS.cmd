@echo off
rem Copies the two pipeline workflows into place in both repos.
set R=%~dp0..\..\..
mkdir "%R%\AccessControl\.github\workflows" 2>nul
mkdir "%R%\AccessControlFE\.github\workflows" 2>nul
copy /Y "%~dp0workflows\AccessControl-release.yml" "%R%\AccessControl\.github\workflows\release.yml"
copy /Y "%~dp0workflows\AccessControlFE-release.yml" "%R%\AccessControlFE\.github\workflows\release.yml"
echo.
echo Done. Now commit and push both repos.
pause
