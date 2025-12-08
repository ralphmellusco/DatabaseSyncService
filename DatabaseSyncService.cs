using System;
using System.ServiceProcess;
using System.Timers;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;
using System.Configuration;
using DatabaseSyncService.Configuration;
using DatabaseSyncService.Utilities;

namespace DatabaseSyncService
{
    public partial class DatabaseSyncService : ServiceBase
    {
        private Timer _syncTimer;
        private EventLogger _eventLogger;
        private Logger _fileLogger;
        private readonly List<SyncJob> _syncJobs;
        private readonly int _syncIntervalMinutes;
        
        public DatabaseSyncService()
        {
            InitializeComponent();
            
            // Initialize service properties
            ServiceName = "DatabaseSyncService";
            CanPauseAndContinue = true;
            CanShutdown = true;
            CanStop = true;
            
            // Initialize loggers
            var enableEventLogging = ConfigManager.IsEventLoggingEnabled();
            _eventLogger = new EventLogger(ConfigManager.GetEventLogSource(), enableEventLogging);
            _fileLogger = new Logger(ConfigManager.GetLogFilePath(), ConfigManager.GetMaxLogFiles());
            
            // Load configuration
            _syncIntervalMinutes = ConfigManager.GetSyncIntervalMinutes();
            _syncJobs = ConfigManager.LoadSyncJobs();
        }
        
        protected override void OnStart(string[] args)
        {
            try
            {
                _eventLogger.LogInfo("Database Synchronization Service starting...");
                _fileLogger.LogInfo("Database Synchronization Service starting...");
                
                // Initialize and start the timer
                _syncTimer = new Timer(_syncIntervalMinutes * 60 * 1000); // Convert minutes to milliseconds
                _syncTimer.Elapsed += OnTimerElapsed;
                _syncTimer.AutoReset = true;
                _syncTimer.Start();
                
                _eventLogger.LogInfo($"Database Synchronization Service started. Sync interval: {_syncIntervalMinutes} minutes.");
                _fileLogger.LogInfo($"Database Synchronization Service started. Sync interval: {_syncIntervalMinutes} minutes.");
            }
            catch (Exception ex)
            {
                _eventLogger.LogError($"Error starting Database Synchronization Service: {ex.Message}");
                _fileLogger.LogError($"Error starting Database Synchronization Service: {ex.Message}");
                throw;
            }
        }
        
        protected override void OnStop()
        {
            try
            {
                _eventLogger.LogInfo("Database Synchronization Service stopping...");
                _fileLogger.LogInfo("Database Synchronization Service stopping...");
                
                // Stop the timer
                if (_syncTimer != null)
                {
                    _syncTimer.Stop();
                    _syncTimer.Dispose();
                }
                
                _eventLogger.LogInfo("Database Synchronization Service stopped.");
                _fileLogger.LogInfo("Database Synchronization Service stopped.");
            }
            catch (Exception ex)
            {
                _eventLogger.LogError($"Error stopping Database Synchronization Service: {ex.Message}");
                _fileLogger.LogError($"Error stopping Database Synchronization Service: {ex.Message}");
            }
        }
        
        protected override void OnPause()
        {
            try
            {
                _eventLogger.LogInfo("Database Synchronization Service pausing...");
                _fileLogger.LogInfo("Database Synchronization Service pausing...");
                
                if (_syncTimer != null)
                {
                    _syncTimer.Stop();
                }
                
                _eventLogger.LogInfo("Database Synchronization Service paused.");
                _fileLogger.LogInfo("Database Synchronization Service paused.");
            }
            catch (Exception ex)
            {
                _eventLogger.LogError($"Error pausing Database Synchronization Service: {ex.Message}");
                _fileLogger.LogError($"Error pausing Database Synchronization Service: {ex.Message}");
            }
        }
        
        protected override void OnContinue()
        {
            try
            {
                _eventLogger.LogInfo("Database Synchronization Service continuing...");
                _fileLogger.LogInfo("Database Synchronization Service continuing...");
                
                if (_syncTimer != null)
                {
                    _syncTimer.Start();
                }
                
                _eventLogger.LogInfo("Database Synchronization Service continued.");
                _fileLogger.LogInfo("Database Synchronization Service continued.");
            }
            catch (Exception ex)
            {
                _eventLogger.LogError($"Error continuing Database Synchronization Service: {ex.Message}");
                _fileLogger.LogError($"Error continuing Database Synchronization Service: {ex.Message}");
            }
        }
        
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _eventLogger.LogInfo("Starting scheduled synchronization...");
                _fileLogger.LogInfo("Starting scheduled synchronization...");
                
                // Run sync jobs
                foreach (var job in _syncJobs)
                {
                    if (job.Enabled)
                    {
                        RunSyncJob(job);
                    }
                }
            }
            catch (Exception ex)
            {
                _eventLogger.LogError($"Error during scheduled synchronization: {ex.Message}");
                _fileLogger.LogError($"Error during scheduled synchronization: {ex.Message}");
            }
        }
        
        private void RunSyncJob(SyncJob job)
        {
            try
            {
                _eventLogger.LogInfo($"Starting sync job: {job.JobName}");
                _fileLogger.LogInfo($"Starting sync job: {job.JobName}");
                
                var synchronizer = new TableSynchronizer();
                var result = synchronizer.Synchronize(job);
                
                job.LastRun = DateTime.Now;
                job.Status = result.Success ? SyncStatus.Success : SyncStatus.Failed;
                
                if (result.Success)
                {
                    _eventLogger.LogInfo($"Sync job '{job.JobName}' completed successfully. Inserted: {result.RowsInserted}, Updated: {result.RowsUpdated}, Skipped: {result.RowsSkipped}");
                    _fileLogger.LogInfo($"Sync job '{job.JobName}' completed successfully. Inserted: {result.RowsInserted}, Updated: {result.RowsUpdated}, Skipped: {result.RowsSkipped}");
                }
                else
                {
                    _eventLogger.LogError($"Sync job '{job.JobName}' failed: {result.ErrorMessage}");
                    _fileLogger.LogError($"Sync job '{job.JobName}' failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                job.Status = SyncStatus.Failed;
                _eventLogger.LogError($"Sync job '{job.JobName}' failed with exception: {ex.Message}");
                _fileLogger.LogError($"Sync job '{job.JobName}' failed with exception: {ex.Message}");
            }
        }
    }
}