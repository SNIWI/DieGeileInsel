@echo off
title Dynamic Island - EXE bauen
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "Fertige App" -nologo
if errorlevel 1 (
  pause
  exit /b 1
)
echo.
echo Fertig! Die App liegt im Ordner "Fertige App".
explorer "Fertige App"
pause
