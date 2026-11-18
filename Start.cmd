@echo off
setlocal
rem Builds bin\SumikkoCompanion.exe on the first run and again after the source changes.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -IfOutdated
if errorlevel 1 (
  echo.
  echo Build failed. Please check the messages above.
  pause
  exit /b 1
)
start "" "%~dp0bin\SumikkoCompanion.exe" %*
