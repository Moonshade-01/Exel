@echo off
setlocal
cd /d "%~dp0"
if exist "app\Exel.exe" (
    start "" "app\Exel.exe"
    exit /b 0
)
if exist "src\Exel\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Exel.exe" (
    start "" "src\Exel\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Exel.exe"
    exit /b 0
)
if exist "src\Exel\bin\Release\net10.0-windows10.0.19041.0\win-x64\Exel.exe" (
    start "" "src\Exel\bin\Release\net10.0-windows10.0.19041.0\win-x64\Exel.exe"
    exit /b 0
)
if exist "src\Exel\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Exel.exe" (
    start "" "src\Exel\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Exel.exe"
    exit /b 0
)
echo No built app found. Run Build-Release.cmd once, or use the portable Windows ZIP.
pause
exit /b 1
