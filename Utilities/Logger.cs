using System;
using System.IO;

namespace DatabaseSyncService.Utilities
{
    public class Logger
    {
        private static readonly object _lockObject = new object();
        private readonly string _logFilePath;
        private readonly int _maxLogFiles;
        
        public Logger(string logFilePath, int maxLogFiles)
        {
            _logFilePath = logFilePath;
            _maxLogFiles = maxLogFiles;
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        
        public void LogInfo(string message)
        {
            LogMessage("INFO", message);
        }
        
        public void LogWarning(string message)
        {
            LogMessage("WARN", message);
        }
        
        public void LogError(string message)
        {
            LogMessage("ERROR", message);
        }
        
        private void LogMessage(string level, string message)
        {
            try
            {
                lock (_lockObject)
                {
                    var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }
        
        public void RotateLogs()
        {
            try
            {
                lock (_lockObject)
                {
                    // Implement log rotation based on _maxLogFiles
                    // This is a simplified implementation
                }
            }
            catch
            {
                // Ignore rotation errors
            }
        }
    }
}