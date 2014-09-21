using Common.Logging;
using Quartz;
using Scurry.Scheduler.Queue;
using Scurry.Scheduler.Queue.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scurry.Scheduler.Quartz
{
    public class QueueJob : IJob
    {
        ILog Log { get; set; }

        public QueueJob()
        {
            Log = LogManager.GetCurrentClassLogger();
        }

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                var key = context.JobDetail.Key;
                var dataMap = context.JobDetail.JobDataMap;
                var jobContext = SchedulerWrapper.Wrapper.GetContextFromJobDataMap(key.Name, dataMap);
                QueueManager.Push(jobContext);
            }
            catch (Exception ex)
            {
                Log.Error("Error executing queue job", ex);
                throw new ApplicationException("Error executing queue job", ex);
            }
        }
    } 
}
