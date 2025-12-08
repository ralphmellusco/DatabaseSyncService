using System;
using System.Diagnostics;

namespace DatabaseSyncService.Utilities
{
    public class EventLogger
    {
        private readonly EventLog _eventLog;
        private readonly string _source;
        private readonly bool _enabled;
        
        public EventLogger(string source, bool enabled = true)
        {
            _source = source;
            _enabled = enabled;
            
            if (!_enabled) return;
            
            _eventLog = new EventLog();
            if (!EventLog.SourceExists(_source))
            {
                try
                {
                    EventLog.CreateEventSource(_source, "Application");
                }
                catch
                {
                    // May fail if not running with sufficient privileges
                }
            }
            
            _eventLog.Source = _source;
            _eventLog.Log = "Application";
        }
        
        public void LogInfo(string message)
        {
            if (!_enabled) return;
            
            try
            {
                _eventLog.WriteEntry(message, EventLogEntryType.Information);
            }
            catch
            {
                // Ignore event log errors
            }
        }
        
        public void LogWarning(string message)
        {
            if (!_enabled) return;
            
            try
            {
                _eventLog.WriteEntry(message, EventLogEntryType.Warning);
            }
            catch
            {
                // Ignore event log errors
            }
        }
        
        public void LogError(string message)
        {
            if (!_enabled) return;
            
            try
            {
                _eventLog.WriteEntry(message, EventLogEntryType.Error);
            }
            catch
            {
                // Ignore event log errors
            }
        }
    }
}