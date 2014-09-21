using Common.Logging;
using Microsoft.Owin.Hosting;
using Scurry.Scheduler.Quartz;
using Scurry.Scheduler.Service.Api.Setup;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace Scurry.Scheduler.Service
{
    public partial class Service : ServiceBase
    {
        ILog Log { get; set; }
        
        public Service()
        {
            InitializeComponent();
            Log = LogManager.GetCurrentClassLogger();
        }

        public void Start(string[] args)
        {
            Log.Info("Starting service");
            Log.Info("Starting scheduler");
            SchedulerWrapper.Wrapper.Load();
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
            SchedulerWrapper.Wrapper.Unload();
            Log.Info("Stopped api");
            Log.Info("Stopped scheduler");
            Log.Info("Stopped service");
            
        }

        protected override void OnStop()
        {
            Stop();
        }
    }
}
