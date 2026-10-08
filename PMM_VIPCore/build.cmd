@echo off
rem Builds PMM_VIPCore in Release and writes the log next to this file.
cd /d "%~dp0"
dotnet build PMM_VIPCore.csproj -c Release -nologo > build.log 2>&1
echo exit %ERRORLEVEL% >> build.log
