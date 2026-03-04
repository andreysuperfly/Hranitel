@echo off
chcp 65001 >nul
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo Установите .NET 8 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Публикация...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "bin\publish"

set ISCC="%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist %ISCC% set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %ISCC% set ISCC="C:\Program Files\Inno Setup 6\ISCC.exe"
if not exist %ISCC% (
    echo Inno Setup не найден. Скачайте: https://jrsoftware.org/isdl.php
    pause
    exit /b 1
)

echo Сборка установщика...
%ISCC% installer.iss

if exist "bin\Hranitel_Setup.exe" (
    echo.
    echo Готово! bin\Hranitel_Setup.exe
)
pause
