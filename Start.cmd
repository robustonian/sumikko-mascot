@echo off
setlocal
if not exist "%~dp0bin\SumikkoCompanion.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo.
    echo Build failed. Please check the messages above.
    pause
    exit /b 1
  )
)
start "" "%~dp0bin\SumikkoCompanion.exe" %*
