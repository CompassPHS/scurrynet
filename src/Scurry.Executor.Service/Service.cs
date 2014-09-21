using Common.Logging;
using Microsoft.Owin.Hosting;
using Scurry.Executor.Service.Api.Setup;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Scurry.Executor.Job.Base;
using Scurry.Executor.Manager;

namespace Scurry.Executor.Service
{
    public partial class Service : ServiceBase
    {
        ILog Log { get; set; }

        public Service()
        {
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            InitializeComponent();
            Log = LogManager.GetCurrentClassLogger();
        }

        public void Start(string[] args)
        {
            Log.Info("Starting service");
            Log.Info("Starting job listeners");
            JobManager.Manager.LoadJobs();
            Log.Info("Starting api");
            var address = ConfigurationManager.AppSettings["apiAddress"];
            WebApp.Start<Config>(url: address);
        }

        protected override void OnStart(string[] args)
        {
            Start(args);
        }

        public new void Stop()
        {
            Log.Info("Stopped api");
            JobManager.Manager.UnloadJobs();
            Log.Info("Stopped job listeners");
            Log.Info("Stopped service");
        }

        protected override void OnStop()
        {
            Stop();
        }
    }
}
