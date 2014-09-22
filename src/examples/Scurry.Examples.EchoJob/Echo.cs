using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scurry.Examples.EchoJob
{
    // Job must inherit from Job.Base.Job in order to be picked up
    public class Echo : Scurry.Executor.Job.Base.Job
    {
        // Overloaded base constructor, determines name of job and name of queue
        // host of queue will be loaded from job runner's config queueHost
        // Must use default, no arguments constructor to be picked up
        public Echo()
            : base("echo")
        {

        }

        // Implement the execute, context will either be a string value or a JSON object
        // Execute will be called anytime a message is dropped on the queue.
        protected override void Execute(dynamic context)
        {
            Log.Debug(string.Format(
                "{0}Echoing context {1}",
                ConfigurationManager.AppSettings["MessagePrefix"], context));
        }
    }
}
