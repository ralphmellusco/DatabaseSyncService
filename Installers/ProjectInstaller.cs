using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace DatabaseSyncService.Installers
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller _processInstaller;
        private ServiceInstaller _serviceInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
    
    // This partial class is required for the designer
    public partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._processInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this._serviceInstaller = new System.ServiceProcess.ServiceInstaller();
            
            // 
            // _processInstaller
            // 
            this._processInstaller.Account = System.ServiceProcess.ServiceAccount.NetworkService;
            this._processInstaller.Password = null;
            this._processInstaller.Username = null;
            
            // 
            // _serviceInstaller
            // 
            this._serviceInstaller.ServiceName = "DatabaseSyncService";
            this._serviceInstaller.DisplayName = "Database Table Sync Service";
            this._serviceInstaller.Description = "Periodically synchronizes data between SQL Server tables";
            this._serviceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
                this._processInstaller,
                this._serviceInstaller});
            
            this.components = new System.ComponentModel.Container();
        }

        #endregion
    }
}