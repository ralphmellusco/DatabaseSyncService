using System;

namespace DatabaseSyncService
{
    public class SyncResult
    {
        public bool Success { get; set; }
        public int RowsInserted { get; set; }
        public int RowsUpdated { get; set; }
        public int RowsDeleted { get; set; }
        public int RowsSkipped { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        
        public SyncResult()
        {
            Timestamp = DateTime.Now;
        }
    }
}