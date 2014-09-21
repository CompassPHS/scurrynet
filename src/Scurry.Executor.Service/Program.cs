using Fclp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace Scurry.Executor.Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            bool console = false;
            var p = new FluentCommandLineParser();
            p.Setup<bool>('c', "console").Callback(c => console = c);
            p.Parse(args);

            if (console)
            {
                var service = new Service();
                service.Start(args);
                Console.ReadLine();
                service.Stop();
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] 
			    { 
				    new Service() 
			    };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
