@echo off
setlocal
cd /d "%~dp0"
dotnet test tests\Exel.Tests\Exel.Tests.csproj -c Release
set testResult=%errorlevel%
pause
exit /b %testResult%
