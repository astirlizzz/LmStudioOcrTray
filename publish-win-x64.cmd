@echo off
setlocal
cd /d "%~dp0"
dotnet publish ".\src\LmStudioOcrTray\LmStudioOcrTray.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ".\artifacts\publish\win-x64"
if errorlevel 1 exit /b %errorlevel%
echo.
echo Published to: %CD%\artifacts\publish\win-x64
