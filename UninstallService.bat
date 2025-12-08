@echo off
echo Uninstalling Database Synchronization Service...

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo This script requires administrator privileges.
    echo Please run as Administrator.
    pause
    exit /b
)

REM Stop the service if it's running
echo Stopping service if running...
net stop DatabaseSyncService

REM Uninstall using InstallUtil (requires .NET Framework SDK)
echo Uninstalling service using InstallUtil...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe" /u "%~dp0\bin\Debug\DatabaseSyncService.exe"

REM Alternative uninstallation using SC command
REM echo Uninstalling service using SC command...
REM sc delete "DatabaseSyncService"

echo Service uninstallation completed.
pause