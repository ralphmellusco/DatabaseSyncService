using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;

namespace DatabaseSyncService
{
    /// <summary>
    /// DEPRECATED: This class is deprecated and should not be used for production scenarios.
    /// Use EnhancedTableSynchronizer instead which implements proper change tracking and bulk operations.
    /// </summary>
    public class TableSynchronizer
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
                    return result;
                }
                
                using (var sourceConnection = new SqlConnection(job.SourceConnectionString))
                using (var destinationConnection = new SqlConnection(job.DestinationConnectionString))
                {
                    sourceConnection.Open();
                    destinationConnection.Open();
                    
                    // Get primary key information for proper upsert logic
                    var primaryKeyColumns = GetPrimaryKeyColumns(sourceConnection, job.SourceTable);
                    
                    if (primaryKeyColumns.Length == 0)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"No primary key found for table {job.SourceTable}. Synchronization requires a primary key for proper upsert logic.";
                        return result;
                    }
                    
                    // Get data from source
                    var sourceData = GetData(sourceConnection, job.SourceTable);
                    
                    // Process data to destination with upsert logic
                    ProcessDataWithUpsert(destinationConnection, job.DestinationTable, sourceData, primaryKeyColumns, result);
                }
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }
            
            return result;
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
                    AND CONSTRAINT_NAME LIKE 'PK_%'
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
        
        private DataTable GetData(SqlConnection connection, string tableName)
        {
            var dataTable = new DataTable();
            
            try
            {
                using (var command = new SqlCommand($"SELECT * FROM {tableName}", connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    adapter.Fill(dataTable);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error getting data from {tableName}: {ex.Message}");
            }
            
            return dataTable;
        }
        
        private void ProcessDataWithUpsert(SqlConnection connection, string tableName, DataTable data, string[] primaryKeyColumns, SyncResult result)
        {
            if (data.Rows.Count == 0)
            {
                result.RowsSkipped = 0;
                result.RowsInserted = 0;
                result.RowsUpdated = 0;
                return;
            }
            
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (DataRow row in data.Rows)
                    {
                        try
                        {
                            // Check if record exists
                            if (RecordExists(connection, transaction, tableName, row, primaryKeyColumns))
                            {
                                // Update existing record
                                UpdateRow(connection, transaction, tableName, row, primaryKeyColumns);
                                result.RowsUpdated++;
                            }
                            else
                            {
                                // Insert new record
                                InsertRow(connection, transaction, tableName, row);
                                result.RowsInserted++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.RowsSkipped++;
                            Trace.TraceError($"Error processing row: {ex.Message}");
                        }
                    }
                    
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Transaction failed: {ex.Message}", ex);
                }
            }
        }
        
        private bool RecordExists(SqlConnection connection, SqlTransaction transaction, string tableName, DataRow row, string[] primaryKeyColumns)
        {
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{col} = @{col}"));
            var sql = $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause}";
            
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                foreach (var column in primaryKeyColumns)
                {
                    command.Parameters.AddWithValue($"@{column}", row[column] ?? DBNull.Value);
                }
                
                var count = (int)command.ExecuteScalar();
                return count > 0;
            }
        }
        
        private void InsertRow(SqlConnection connection, SqlTransaction transaction, string tableName, DataRow row)
        {
            // Build column list and parameter list
            var columns = string.Join(", ", row.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            var parameters = string.Join(", ", row.Table.Columns.Cast<DataColumn>().Select(c => "@" + c.ColumnName));
            
            var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
            
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                foreach (DataColumn column in row.Table.Columns)
                {
                    command.Parameters.AddWithValue("@" + column.ColumnName, row[column] ?? DBNull.Value);
                }
                
                command.ExecuteNonQuery();
            }
        }
        
        private void UpdateRow(SqlConnection connection, SqlTransaction transaction, string tableName, DataRow row, string[] primaryKeyColumns)
        {
            // Build SET clause (all columns except primary keys)
            var setColumns = row.Table.Columns.Cast<DataColumn>()
                .Where(c => !primaryKeyColumns.Contains(c.ColumnName))
                .Select(c => $"{c.ColumnName} = @{c.ColumnName}");
            
            var setClause = string.Join(", ", setColumns);
            
            // Build WHERE clause (primary key columns)
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{col} = @{col}_WHERE"));
            
            var sql = $"UPDATE {tableName} SET {setClause} WHERE {whereClause}";
            
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                // Add parameters for SET clause
                foreach (DataColumn column in row.Table.Columns)
                {
                    if (!primaryKeyColumns.Contains(column.ColumnName))
                    {
                        command.Parameters.AddWithValue("@" + column.ColumnName, row[column] ?? DBNull.Value);
                    }
                }
                
                // Add parameters for WHERE clause
                foreach (var column in primaryKeyColumns)
                {
                    command.Parameters.AddWithValue("@" + column + "_WHERE", row[column] ?? DBNull.Value);
                }
                
                command.ExecuteNonQuery();
            }
        }
    }
}