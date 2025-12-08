using System;

namespace DatabaseSyncService
{
    public class SyncJob
    {
        public string JobName { get; set; }
        public string SourceConnectionString { get; set; }
        public string DestinationConnectionString { get; set; }
        public string SourceTable { get; set; }
        public string DestinationTable { get; set; }
        public string ScheduleCron { get; set; }
        public bool Enabled { get; set; }
        public DateTime LastRun { get; set; }
        public SyncStatus Status { get; set; }
    }
    
    public enum SyncStatus
    {
        Pending,
        Running,
        Success,
        Failed,
        Cancelled
    }
}