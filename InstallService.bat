@echo off
echo Installing Database Synchronization Service...

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo This script requires administrator privileges.
    echo Please run as Administrator.
    pause
    exit /b
)

REM Install using InstallUtil (requires .NET Framework SDK)
echo Installing service using InstallUtil...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe" "%~dp0\bin\Debug\DatabaseSyncService.exe"

REM Alternative installation using SC command
REM echo Installing service using SC command...
REM sc create "DatabaseSyncService" binPath= "%~dp0\bin\Debug\DatabaseSyncService.exe" DisplayName= "Database Table Sync Service"
REM sc description "DatabaseSyncService" "Periodically synchronizes data between SQL Server tables"
REM sc config "DatabaseSyncService" start= auto

echo Service installation completed.
echo.
echo To start the service, run:
echo   net start DatabaseSyncService
echo.
echo To uninstall the service, run UninstallService.bat
pause