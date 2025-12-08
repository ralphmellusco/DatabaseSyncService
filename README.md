# Database Synchronization Service

A Windows Service application that periodically synchronizes data between SQL Server tables.

## Overview

This service provides enterprise-grade database synchronization capabilities with full Windows Service integration, configurable scheduling, multiple job support, and comprehensive logging. It's designed to run continuously in the background, automatically synchronizing data between SQL Server databases according to configured schedules.

## Features

- Runs as a Windows Service with full lifecycle management (Start, Stop, Pause, Continue)
- Configurable synchronization intervals
- Multiple job configurations
- Windows Event Log integration
- File-based logging with rotation
- PowerShell management module

## Prerequisites

- Windows OS
- .NET Framework 4.7.2 or higher
- SQL Server instances for source and destination databases

## Installation

### Method 1: Using InstallUtil (Recommended)

1. Open Command Prompt as Administrator
2. Navigate to the service directory
3. Run `InstallService.bat`

### Method 2: Using SC Command

1. Open Command Prompt as Administrator
2. Run the following commands:
   ```
   sc create "DatabaseSyncService" binPath= "C:\Path\To\DatabaseSyncService.exe" DisplayName= "Database Table Sync Service"
   sc description "DatabaseSyncService" "Periodically synchronizes data between SQL Server tables"
   sc config "DatabaseSyncService" start= auto
   ```

## Configuration

Edit the `App.config` file to configure:

- Sync interval (`SyncIntervalMinutes`)
- Connection strings for source and destination databases
- Table mappings
- Logging settings

### Example Configuration

```xml
<appSettings>
  <!-- Service Configuration -->
  <add key="SyncIntervalMinutes" value="15"/>
  <add key="EnableEventLogging" value="true"/>
  <add key="EventLogSource" value="DatabaseSyncService"/>
  <add key="LogFilePath" value="C:\Logs\DatabaseSync\Service.log"/>
  <add key="MaxLogFiles" value="10"/>
</appSettings>

<databaseSync>
  <!-- Multiple Synchronization Jobs -->
  <add key="Job1.Name" value="CustomerDataSync"/>
  <add key="Job1.SourceConnection" value="Server=SRV1;Database=SourceDB;Integrated Security=true;"/>
  <add key="Job1.DestinationConnection" value="Server=SRV2;Database=DestDB;Integrated Security=true;"/>
  <add key="Job1.SourceTable" value="Customers"/>
  <add key="Job1.DestinationTable" value="CustomerArchive"/>
  <add key="Job1.Schedule" value="0 */2 * * *"/>
  <add key="Job1.Enabled" value="true"/>
</databaseSync>
```

## Usage

### Starting the Service

```cmd
net start DatabaseSyncService
```

### Stopping the Service

```cmd
net stop DatabaseSyncService
```

### PowerShell Management

Import the PowerShell module:

```powershell
Import-Module .\DatabaseSyncManagement.psm1
```

Available commands:

- `Get-DatabaseSyncStatus` - Get service status
- `Start-DatabaseSyncJob` - Start the service
- `Stop-DatabaseSyncJob` - Stop the service
- `Get-SyncHistory` - Get synchronization history
- `Set-SyncSchedule` - Set synchronization schedule (placeholder)

## Uninstallation

1. Open Command Prompt as Administrator
2. Navigate to the service directory
3. Run `UninstallService.bat`

Or manually delete the service:

```
sc delete "DatabaseSyncService"
```

## Troubleshooting

Check the Windows Event Log for errors:
- Open Event Viewer
- Navigate to Windows Logs > Application
- Filter by event source "DatabaseSyncService"

## Service Details

- **Service Name**: DatabaseSyncService
- **Display Name**: Database Table Sync Service
- **Description**: Periodically synchronizes data between SQL Server tables
- **Startup Type**: Automatic (Delayed Start)
- **Log On As**: NT AUTHORITY\NETWORK SERVICE

## Expected Event Log Entries

- Information: Service started successfully
- Information: Job 'CustomerDataSync' started
- Information: Job 'CustomerDataSync' completed: 150 rows inserted, 45 updated
- Warning: Job 'ProductSync' delayed - database unavailable
- Error: Job 'OrderSync' failed - schema mismatch detected