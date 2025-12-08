using System;
using System.ServiceProcess;
using System.Diagnostics;

namespace DatabaseSyncService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the Windows Service.
        /// </summary>
        static void Main()
        {
            try
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new DatabaseSyncService()
                };
                ServiceBase.Run(ServicesToRun);
            }
            catch (Exception ex)
            {
                // Log to Event Viewer if service fails to start
                EventLog.WriteEntry("DatabaseSyncService", $"Failed to start service: {ex.Message}", EventLogEntryType.Error);
            }
        }
    }
}