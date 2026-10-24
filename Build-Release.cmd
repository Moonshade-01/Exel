@echo off
setlocal
cd /d "%~dp0"
dotnet publish src\Exel\Exel.csproj -c Release -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifierOverride=win-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --self-contained true -p:PublishTrimmed=false -o app
if errorlevel 1 (
    echo Build failed. Check the .NET 10 SDK and MAUI Windows workload.
    pause
    exit /b 1
)
echo Ready. Next time, double-click Start.cmd. No rebuild is needed until source files change.
pause
