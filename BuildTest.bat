@echo off
echo Building Database Synchronization Service...

REM Check if MSBuild is available
where msbuild >nul 2>&1
if %errorLevel% NEQ 0 (
    echo MSBuild not found. Please install Visual Studio or Build Tools.
    pause
    exit /b
)

REM Build the solution
msbuild DatabaseSyncService.sln /p:Configuration=Debug /m

if %errorLevel% EQU 0 (
    echo Build completed successfully.
) else (
    echo Build failed.
)

pause