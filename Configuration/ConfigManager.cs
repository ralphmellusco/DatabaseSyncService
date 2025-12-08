using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Collections.Specialized;

namespace DatabaseSyncService.Configuration
{
    public class ConfigManager
    {
        public static List<SyncJob> LoadSyncJobs()
        {
            var jobs = new List<SyncJob>();
            
            try
            {
                // Load jobs from the databaseSync section
                var databaseSyncSection = ConfigurationManager.GetSection("databaseSync") as NameValueCollection;
                if (databaseSyncSection != null)
                {
                    // Group settings by job (Job1.*, Job2.*, etc.)
                    var jobGroups = databaseSyncSection.AllKeys
                        .Where(key => key.Contains("."))
                        .GroupBy(key => key.Substring(0, key.IndexOf('.')))
                        .ToDictionary(g => g.Key, g => g.ToDictionary(k => k.Substring(k.IndexOf('.') + 1), k => databaseSyncSection[k]));
                    
                    // Create SyncJob objects for each group
                    foreach (var jobGroup in jobGroups)
                    {
                        var job = new SyncJob
                        {
                            JobName = jobGroup.Value.ContainsKey("Name") ? jobGroup.Value["Name"] : jobGroup.Key,
                            SourceConnectionString = jobGroup.Value.ContainsKey("SourceConnection") ? jobGroup.Value["SourceConnection"] : "",
                            DestinationConnectionString = jobGroup.Value.ContainsKey("DestinationConnection") ? jobGroup.Value["DestinationConnection"] : "",
                            SourceTable = jobGroup.Value.ContainsKey("SourceTable") ? jobGroup.Value["SourceTable"] : "",
                            DestinationTable = jobGroup.Value.ContainsKey("DestinationTable") ? jobGroup.Value["DestinationTable"] : "",
                            ScheduleCron = jobGroup.Value.ContainsKey("Schedule") ? jobGroup.Value["Schedule"] : "",
                            Enabled = jobGroup.Value.ContainsKey("Enabled") ? bool.Parse(jobGroup.Value["Enabled"]) : true,
                            LastRun = DateTime.MinValue,
                            Status = SyncStatus.Pending
                        };
                        
                        jobs.Add(job);
                    }
                }
                
                // If no jobs were loaded from databaseSync section, create a default job from appSettings
                if (jobs.Count == 0)
                {
                    var defaultJob = new SyncJob
                    {
                        JobName = ConfigurationManager.AppSettings["JobName"] ?? "DefaultJob",
                        SourceConnectionString = ConfigurationManager.AppSettings["SourceConnection"] ?? "",
                        DestinationConnectionString = ConfigurationManager.AppSettings["DestinationConnection"] ?? "",
                        SourceTable = ConfigurationManager.AppSettings["SourceTable"] ?? "",
                        DestinationTable = ConfigurationManager.AppSettings["DestinationTable"] ?? "",
                        ScheduleCron = ConfigurationManager.AppSettings["Schedule"] ?? "",
                        Enabled = bool.Parse(ConfigurationManager.AppSettings["Enabled"] ?? "true"),
                        LastRun = DateTime.MinValue,
                        Status = SyncStatus.Pending
                    };
                    
                    jobs.Add(defaultJob);
                }
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Trace.TraceError($"Error loading sync jobs: {ex.Message}");
            }
            
            return jobs;
        }
        
        public static int GetSyncIntervalMinutes()
        {
            try
            {
                return int.Parse(ConfigurationManager.AppSettings["SyncIntervalMinutes"] ?? "15");
            }
            catch
            {
                return 15; // Default to 15 minutes
            }
        }
        
        public static bool IsEventLoggingEnabled()
        {
            try
            {
                return bool.Parse(ConfigurationManager.AppSettings["EnableEventLogging"] ?? "true");
            }
            catch
            {
                return true; // Default to enabled
            }
        }
        
        public static string GetEventLogSource()
        {
            return ConfigurationManager.AppSettings["EventLogSource"] ?? "DatabaseSyncService";
        }
        
        public static string GetLogFilePath()
        {
            return ConfigurationManager.AppSettings["LogFilePath"] ?? @"C:\Logs\DatabaseSync\Service.log";
        }
        
        public static int GetMaxLogFiles()
        {
            try
            {
                return int.Parse(ConfigurationManager.AppSettings["MaxLogFiles"] ?? "10");
            }
            catch
            {
                return 10; // Default to 10 log files
            }
        }
        
        public static bool IsEmailAlertsEnabled()
        {
            try
            {
                return bool.Parse(ConfigurationManager.AppSettings["EnableEmailAlerts"] ?? "false");
            }
            catch
            {
                return false; // Default to disabled
            }
        }
        
        public static string GetSmtpServer()
        {
            return ConfigurationManager.AppSettings["SmtpServer"] ?? "";
        }
    }
}