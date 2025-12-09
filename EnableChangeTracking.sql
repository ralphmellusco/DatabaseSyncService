/*
Script to enable Change Tracking on a database and table
This is required for the EnhancedTableSynchronizer to work properly
*/

-- Enable change tracking on the database
-- Replace 'YourDatabaseName' with your actual database name
ALTER DATABASE YourDatabaseName 
SET CHANGE_TRACKING = ON  
(CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);

-- Enable change tracking on a specific table
-- Replace 'YourTableName' with your actual table name
ALTER TABLE YourTableName
ENABLE CHANGE_TRACKING  
WITH (TRACK_COLUMNS_UPDATED = ON);

-- Verify change tracking is enabled
SELECT 
    DB_NAME(database_id) AS DatabaseName,
    CASE 
        WHEN is_auto_cleanup_on = 1 THEN 'ON' 
        ELSE 'OFF' 
    END AS AutoCleanup,
    retention_period AS RetentionPeriod,
    retention_period_units_desc AS RetentionPeriodUnits
FROM sys.change_tracking_databases;

-- Verify change tracking is enabled for specific tables
SELECT 
    t.name AS TableName,
    ct.is_track_columns_updated_on AS TrackColumnsUpdated
FROM sys.change_tracking_tables ct
INNER JOIN sys.tables t ON ct.object_id = t.object_id;