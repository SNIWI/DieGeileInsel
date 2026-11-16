@echo off
title Dynamic Island
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo.
  echo Das .NET SDK wurde nicht gefunden.
  echo Bitte installieren: https://dotnet.microsoft.com/download  ^(.NET 8 SDK oder neuer^)
  echo.
  pause
  exit /b 1
)
echo.
echo  Dynamic Island wird gebaut ... beim ersten Mal ca. 1 Minute.
echo.
dotnet build -c Release -nologo -v q
if errorlevel 1 (
  echo.
  echo Beim Bauen ist ein Fehler aufgetreten.
  echo Steht oben "No .NET SDKs were found", installiere das .NET 8 SDK:
  echo https://dotnet.microsoft.com/download
  echo Sonst: Fehlermeldung kopieren und Claude schicken.
  echo.
  pause
  exit /b 1
)
start "" "bin\Release\net8.0-windows10.0.19041.0\DynamicIsland.exe"
