using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using DatabaseSyncService.Models;

namespace DatabaseSyncService.Core
{
    /// <summary>
    /// Enhanced table synchronizer that implements proper change tracking and bulk operations
    /// </summary>
    public class EnhancedTableSynchronizer
    {
        public SyncResult Synchronize(SyncJob job)
        {
            var result = new SyncResult();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrEmpty(job.SourceConnectionString) || 
                    string.IsNullOrEmpty(job.DestinationConnectionString) ||
                    string.IsNullOrEmpty(job.SourceTable) ||
                    string.IsNullOrEmpty(job.DestinationTable))
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid job configuration: missing connection strings or table names";
                    Trace.TraceError($"[{job.JobName}] {result.ErrorMessage}");
                    return result;
                }
                
                using (var sourceConnection = new SqlConnection(job.SourceConnectionString))
                using (var destinationConnection = new SqlConnection(job.DestinationConnectionString))
                {
                    sourceConnection.Open();
                    destinationConnection.Open();
                    
                    Trace.TraceInformation($"[{job.JobName}] Starting synchronization from {job.SourceTable} to {job.DestinationTable}");
                    
                    // Validate that change tracking is enabled
                    if (!IsChangeTrackingEnabled(sourceConnection, job.SourceTable))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Change tracking is not enabled for table {job.SourceTable}. Please enable change tracking on the source database and table.";
                        Trace.TraceError($"[{job.JobName}] {result.ErrorMessage}");
                        return result;
                    }
                    
                    // Validate schema compatibility
                    var schemaValidationResult = ValidateSchemaCompatibility(sourceConnection, destinationConnection, job.SourceTable, job.DestinationTable);
                    if (!schemaValidationResult.IsValid)
                    {
                        result.Success = false;
                        result.ErrorMessage = schemaValidationResult.ErrorMessage;
                        Trace.TraceError($"[{job.JobName}] Schema validation failed: {result.ErrorMessage}");
                        return result;
                    }
                    
                    // Get primary key information for proper upsert logic
                    var primaryKeyColumns = GetPrimaryKeyColumns(sourceConnection, job.SourceTable);
                    
                    if (primaryKeyColumns.Length == 0)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"No primary key found for table {job.SourceTable}. Synchronization requires a primary key for proper upsert logic.";
                        Trace.TraceError($"[{job.JobName}] {result.ErrorMessage}");
                        return result;
                    }
                    
                    // Get the last sync version for incremental sync
                    var lastSyncVersion = GetLastSyncVersion(destinationConnection, job.JobName);
                    Trace.TraceInformation($"[{job.JobName}] Last sync version: {lastSyncVersion}");
                    
                    // Get changed data from source since last sync
                    var changedData = GetChangedData(sourceConnection, job.SourceTable, lastSyncVersion, primaryKeyColumns);
                    Trace.TraceInformation($"[{job.JobName}] Found {changedData.Rows.Count} changed rows");
                    
                    // Process changed data to destination with bulk upsert logic
                    ProcessChangedDataWithBulkUpsert(destinationConnection, job.DestinationTable, changedData, primaryKeyColumns, result, job.JobName);
                    
                    // Update the last sync version
                    if (result.Success && changedData.Rows.Count > 0)
                    {
                        var currentVersion = GetCurrentChangeTrackingVersion(sourceConnection);
                        UpdateLastSyncVersion(destinationConnection, job.JobName, currentVersion);
                        Trace.TraceInformation($"[{job.JobName}] Updated sync version to {currentVersion}");
                    }
                }
                
                result.Success = true;
                Trace.TraceInformation($"[{job.JobName}] Synchronization completed successfully. Inserted: {result.RowsInserted}, Updated: {result.RowsUpdated}, Deleted: {result.RowsDeleted}, Skipped: {result.RowsSkipped}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Trace.TraceError($"[{job.JobName}] Error during synchronization: {ex}");
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }
            
            return result;
        }
        
        private bool IsChangeTrackingEnabled(SqlConnection connection, string tableName)
        {
            try
            {
                var sql = @"
                    SELECT COUNT(*)
                    FROM sys.change_tracking_tables ct
                    INNER JOIN sys.tables t ON ct.object_id = t.object_id
                    WHERE t.name = @TableName";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@TableName", tableName);
                    var count = (int)command.ExecuteScalar();
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error checking change tracking status: {ex.Message}");
                return false;
            }
        }
        
        private long GetCurrentChangeTrackingVersion(SqlConnection connection)
        {
            try
            {
                using (var command = new SqlCommand("SELECT CHANGE_TRACKING_CURRENT_VERSION()", connection))
                {
                    var result = command.ExecuteScalar();
                    return result != DBNull.Value ? Convert.ToInt64(result) : 0;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error getting current change tracking version: {ex.Message}");
                return 0;
            }
        }
        
        private long GetLastSyncVersion(SqlConnection connection, string jobName)
        {
            try
            {
                var sql = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SyncMetadata' AND xtype='U')
                    BEGIN
                        CREATE TABLE SyncMetadata (
                            JobName NVARCHAR(255) PRIMARY KEY,
                            LastSyncVersion BIGINT NOT NULL DEFAULT 0,
                            LastSyncTime DATETIME NOT NULL DEFAULT GETUTCDATE()
                        )
                    END
                    
                    SELECT ISNULL(LastSyncVersion, 0)
                    FROM SyncMetadata
                    WHERE JobName = @JobName";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@JobName", jobName);
                    var result = command.ExecuteScalar();
                    return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error getting last sync version: {ex.Message}");
                return 0;
            }
        }
        
        private void UpdateLastSyncVersion(SqlConnection connection, string jobName, long version)
        {
            try
            {
                var sql = @"
                    IF EXISTS (SELECT * FROM SyncMetadata WHERE JobName = @JobName)
                        UPDATE SyncMetadata 
                        SET LastSyncVersion = @Version, LastSyncTime = GETUTCDATE()
                        WHERE JobName = @JobName
                    ELSE
                        INSERT INTO SyncMetadata (JobName, LastSyncVersion, LastSyncTime)
                        VALUES (@JobName, @Version, GETUTCDATE())";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@JobName", jobName);
                    command.Parameters.AddWithValue("@Version", version);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error updating last sync version: {ex.Message}");
            }
        }
        
        private string[] GetPrimaryKeyColumns(SqlConnection connection, string tableName)
        {
            var primaryKeyColumns = new string[0];
            
            try
            {
                var sql = @"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE TABLE_NAME = @TableName 
                    AND CONSTRAINT_NAME IN (
                        SELECT NAME FROM SYS.OBJECTS 
                        WHERE TYPE = 'PK' AND OBJECT_ID = (
                            SELECT OBJECT_ID FROM SYS.OBJECTS 
                            WHERE TYPE = 'U' AND NAME = @TableName
                        )
                    )
                    ORDER BY ORDINAL_POSITION";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@TableName", tableName);
                    
                    var dataTable = new DataTable();
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                    
                    primaryKeyColumns = dataTable.AsEnumerable()
                        .Select(row => row["COLUMN_NAME"].ToString())
                        .ToArray();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error getting primary key columns: {ex.Message}");
            }
            
            return primaryKeyColumns;
        }
        
        private SchemaValidationResult ValidateSchemaCompatibility(SqlConnection sourceConnection, SqlConnection destinationConnection, string sourceTable, string destinationTable)
        {
            var result = new SchemaValidationResult { IsValid = true };
            
            try
            {
                // Get schema information for both tables
                var sourceSchema = GetTableSchema(sourceConnection, sourceTable);
                var destinationSchema = GetTableSchema(destinationConnection, destinationTable);
                
                // Check if both tables have the same columns
                foreach (var sourceColumn in sourceSchema)
                {
                    if (!destinationSchema.ContainsKey(sourceColumn.Key))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Column '{sourceColumn.Key}' exists in source table '{sourceTable}' but not in destination table '{destinationTable}'.";
                        return result;
                    }
                    
                    var destColumn = destinationSchema[sourceColumn.Key];
                    
                    // Check data type compatibility
                    if (!AreDataTypesCompatible(sourceColumn.Value.DataType, destColumn.DataType))
                    {
                        result.Warnings.Add($"Data type mismatch for column '{sourceColumn.Key}': source is {sourceColumn.Value.DataType}, destination is {destColumn.DataType}.");
                    }
                    
                    // Check nullability compatibility
                    if (sourceColumn.Value.IsNullable && !destColumn.IsNullable)
                    {
                        result.Warnings.Add($"Nullability mismatch for column '{sourceColumn.Key}': source allows NULL, but destination does not.");
                    }
                }
                
                // Check for columns in destination that don't exist in source (not necessarily an error)
                foreach (var destColumn in destinationSchema)
                {
                    if (!sourceSchema.ContainsKey(destColumn.Key))
                    {
                        result.Warnings.Add($"Column '{destColumn.Key}' exists in destination table '{destinationTable}' but not in source table '{sourceTable}'. This column will not be synchronized.");
                    }
                }
                
                // Log warnings if any
                if (result.Warnings.Count > 0)
                {
                    Trace.TraceWarning($"Schema validation warnings: {string.Join("; ", result.Warnings)}");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error validating schema compatibility: {ex.Message}";
                Trace.TraceError(result.ErrorMessage);
            }
            
            return result;
        }
        
        private Dictionary<string, ColumnInfo> GetTableSchema(SqlConnection connection, string tableName)
        {
            var schema = new Dictionary<string, ColumnInfo>();
            
            try
            {
                var sql = @"
                    SELECT 
                        COLUMN_NAME,
                        DATA_TYPE,
                        IS_NULLABLE,
                        CHARACTER_MAXIMUM_LENGTH,
                        NUMERIC_PRECISION,
                        NUMERIC_SCALE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                    ORDER BY ORDINAL_POSITION";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@TableName", tableName);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var columnInfo = new ColumnInfo
                            {
                                Name = reader["COLUMN_NAME"].ToString(),
                                DataType = reader["DATA_TYPE"].ToString(),
                                IsNullable = reader["IS_NULLABLE"].ToString().Equals("YES", StringComparison.OrdinalIgnoreCase),
                                MaxLength = reader["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value ? Convert.ToInt32(reader["CHARACTER_MAXIMUM_LENGTH"]) : (int?)null,
                                NumericPrecision = reader["NUMERIC_PRECISION"] != DBNull.Value ? Convert.ToInt32(reader["NUMERIC_PRECISION"]) : (int?)null,
                                NumericScale = reader["NUMERIC_SCALE"] != DBNull.Value ? Convert.ToInt32(reader["NUMERIC_SCALE"]) : (int?)null
                            };
                            
                            schema[columnInfo.Name] = columnInfo;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error getting table schema for {tableName}: {ex.Message}");
            }
            
            return schema;
        }
        
        private bool AreDataTypesCompatible(string sourceType, string destType)
        {
            // This is a simplified compatibility check
            // In a production environment, you would want more comprehensive checks
            
            // Exact match is always compatible
            if (string.Equals(sourceType, destType, StringComparison.OrdinalIgnoreCase))
                return true;
            
            // VARCHAR and NVARCHAR are generally compatible
            if ((sourceType.Equals("varchar", StringComparison.OrdinalIgnoreCase) && destType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase)) ||
                (sourceType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) && destType.Equals("varchar", StringComparison.OrdinalIgnoreCase)))
                return true;
            
            // INT and BIGINT are compatible (BIGINT can hold larger values)
            if (sourceType.Equals("int", StringComparison.OrdinalIgnoreCase) && destType.Equals("bigint", StringComparison.OrdinalIgnoreCase))
                return true;
            
            return false;
        }
        
        private DataTable GetChangedData(SqlConnection connection, string tableName, long lastSyncVersion, string[] primaryKeyColumns)
        {
            var dataTable = new DataTable();
            
            try
            {
                // Build the column list including change tracking columns
                var pkColumns = string.Join(", ", primaryKeyColumns);
                var allColumns = "*";
                
                var sql = $@"
                    SELECT ct.SYS_CHANGE_VERSION, ct.SYS_CHANGE_OPERATION, t.*
                    FROM CHANGETABLE(CHANGES {tableName}, @LastSyncVersion) AS ct
                    LEFT JOIN {tableName} AS t ON {string.Join(" AND ", primaryKeyColumns.Select(col => $"ct.{col} = t.{col}"))}
                    ORDER BY ct.SYS_CHANGE_VERSION";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@LastSyncVersion", lastSyncVersion);
                    command.CommandTimeout = 300; // 5 minutes timeout for large datasets
                    
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error getting changed data from {tableName}: {ex.Message}");
            }
            
            return dataTable;
        }
        
        private void ProcessChangedDataWithBulkUpsert(SqlConnection connection, string tableName, DataTable changedData, string[] primaryKeyColumns, SyncResult result, string jobName)
        {
            if (changedData.Rows.Count == 0)
            {
                result.RowsSkipped = 0;
                result.RowsInserted = 0;
                result.RowsUpdated = 0;
                result.RowsDeleted = 0;
                return;
            }
            
            try
            {
                // Create a temporary staging table
                var tempTableName = $"#{tableName}_Staging_{Guid.NewGuid().ToString("N")}";
                CreateStagingTable(connection, tableName, tempTableName, primaryKeyColumns);
                
                // Use SqlBulkCopy to insert changed data into staging table
                using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    bulkCopy.DestinationTableName = tempTableName;
                    bulkCopy.BatchSize = 10000;
                    bulkCopy.BulkCopyTimeout = 300; // 5 minutes
                    
                    // Map columns (excluding change tracking columns)
                    foreach (DataColumn column in changedData.Columns)
                    {
                        if (column.ColumnName != "SYS_CHANGE_VERSION" && column.ColumnName != "SYS_CHANGE_OPERATION")
                        {
                            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                        }
                    }
                    
                    bulkCopy.WriteToServer(changedData);
                }
                
                // Perform merge operation from staging table to destination
                ExecuteMergeOperation(connection, tableName, tempTableName, primaryKeyColumns, changedData, result, jobName);
                
                // Clean up staging table
                using (var command = new SqlCommand($"DROP TABLE {tempTableName}", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error processing changed data: {ex.Message}";
                Trace.TraceError($"[{jobName}] {result.ErrorMessage}");
                
                // Log failed records to dead letter queue
                LogFailedRecordsToDeadLetterQueue(connection, changedData, jobName, ex.Message);
            }
        }
        
        private void CreateStagingTable(SqlConnection connection, string sourceTableName, string tempTableName, string[] primaryKeyColumns)
        {
            try
            {
                // Create staging table with same schema as source table
                var sql = $@"
                    SELECT TOP 0 * 
                    INTO {tempTableName} 
                    FROM {sourceTableName}
                    
                    ALTER TABLE {tempTableName} 
                    ADD SYS_CHANGE_VERSION BIGINT NULL, 
                        SYS_CHANGE_OPERATION NVARCHAR(1) NULL";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error creating staging table {tempTableName}: {ex.Message}");
                throw;
            }
        }
        
        private void ExecuteMergeOperation(SqlConnection connection, string targetTable, string sourceTable, string[] primaryKeyColumns, DataTable changedData, SyncResult result, string jobName)
        {
            try
            {
                // Build column lists
                var allColumns = changedData.Columns.Cast<DataColumn>()
                    .Where(c => c.ColumnName != "SYS_CHANGE_VERSION" && c.ColumnName != "SYS_CHANGE_OPERATION")
                    .Select(c => c.ColumnName)
                    .ToList();
                
                var nonPkColumns = allColumns.Where(c => !primaryKeyColumns.Contains(c)).ToList();
                
                // Build MERGE statement
                var mergeSql = $@"
                    MERGE {targetTable} AS target
                    USING (
                        SELECT * FROM {sourceTable}
                    ) AS source ON {string.Join(" AND ", primaryKeyColumns.Select(col => $"target.{col} = source.{col}"))}
                    
                    WHEN MATCHED AND source.SYS_CHANGE_OPERATION = 'U' THEN
                        UPDATE SET {string.Join(", ", nonPkColumns.Select(col => $"{col} = source.{col}"))}
                        
                    WHEN NOT MATCHED BY TARGET AND source.SYS_CHANGE_OPERATION IN ('I', 'U') THEN
                        INSERT ({string.Join(", ", allColumns)}) 
                        VALUES ({string.Join(", ", allColumns.Select(col => $"source.{col}"))})
                        
                    WHEN NOT MATCHED BY SOURCE AND EXISTS (
                        SELECT 1 FROM {sourceTable} WHERE SYS_CHANGE_OPERATION = 'D'
                    ) THEN
                        DELETE;";
                
                using (var command = new SqlCommand(mergeSql, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes for large operations
                    var affectedRows = command.ExecuteNonQuery();
                    
                    // Count operations by type (simplified counting)
                    result.RowsInserted = changedData.AsEnumerable()
                        .Count(row => row["SYS_CHANGE_OPERATION"].ToString() == "I");
                    result.RowsUpdated = changedData.AsEnumerable()
                        .Count(row => row["SYS_CHANGE_OPERATION"].ToString() == "U");
                    result.RowsDeleted = changedData.AsEnumerable()
                        .Count(row => row["SYS_CHANGE_OPERATION"].ToString() == "D");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error executing merge operation from {sourceTable} to {targetTable}: {ex.Message}");
                throw;
            }
        }
        
        private void LogFailedRecordsToDeadLetterQueue(SqlConnection connection, DataTable failedData, string jobName, string errorMessage)
        {
            try
            {
                // Ensure dead letter queue table exists
                CreateDeadLetterQueueTable(connection);
                
                // Insert failed records into dead letter queue
                foreach (DataRow row in failedData.Rows)
                {
                    var insertSql = @"
                        INSERT INTO DeadLetterQueue (JobName, ErrorTimestamp, ErrorMessage, RecordData)
                        VALUES (@JobName, @ErrorTimestamp, @ErrorMessage, @RecordData)";
                    
                    using (var command = new SqlCommand(insertSql, connection))
                    {
                        command.Parameters.AddWithValue("@JobName", jobName);
                        command.Parameters.AddWithValue("@ErrorTimestamp", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@ErrorMessage", errorMessage);
                        
                        // Serialize the row data as JSON
                        var recordData = SerializeRowData(row);
                        command.Parameters.AddWithValue("@RecordData", recordData);
                        
                        command.ExecuteNonQuery();
                    }
                }
                
                Trace.TraceWarning($"Logged {failedData.Rows.Count} failed records to dead letter queue for job '{jobName}'");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to log records to dead letter queue for job '{jobName}': {ex.Message}");
            }
        }
        
        private void CreateDeadLetterQueueTable(SqlConnection connection)
        {
            try
            {
                var sql = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DeadLetterQueue' AND xtype='U')
                    BEGIN
                        CREATE TABLE DeadLetterQueue (
                            Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                            JobName NVARCHAR(255) NOT NULL,
                            ErrorTimestamp DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            ErrorMessage NVARCHAR(MAX) NOT NULL,
                            RecordData NVARCHAR(MAX) NOT NULL,
                            Processed BIT NOT NULL DEFAULT 0,
                            ProcessedTimestamp DATETIME NULL
                        )
                        
                        CREATE INDEX IX_DeadLetterQueue_JobName_ErrorTimestamp ON DeadLetterQueue (JobName, ErrorTimestamp);
                    END";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error creating dead letter queue table: {ex.Message}");
            }
        }
        
        private string SerializeRowData(DataRow row)
        {
            try
            {
                // Simple serialization of row data
                var dataParts = new List<string>();
                foreach (DataColumn col in row.Table.Columns)
                {
                    var value = row[col] != DBNull.Value ? row[col].ToString() : "NULL";
                    dataParts.Add($"{col.ColumnName}={value}");
                }
                
                return string.Join("|", dataParts);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error serializing row data: {ex.Message}");
                return "Serialization error";
            }
        }
    }
}