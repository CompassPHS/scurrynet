using Quartz;
using Quartz.Impl;
using Quartz.Collection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Scurry.Scheduler.Queue;
using Scurry.Scheduler.Queue.Contexts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Impl.Matchers;

namespace Scurry.Scheduler.Quartz
{
    public class SchedulerWrapper
    {
        public static SchedulerWrapper Wrapper { get; private set; }

        static SchedulerWrapper()
        {
            Wrapper = new SchedulerWrapper();
        }

        private IScheduler _scheduler;

        public void Load()
        {
            ISchedulerFactory sf = new StdSchedulerFactory();
            _scheduler = sf.GetScheduler();
            _scheduler.Start();
        }

        public void Unload()
        {
            _scheduler.Shutdown();
        }

        public void ScheduleJob(Job jobContext)
        {
            var jobData = SetJobDataMapFromContext(jobContext);
            IJobDetail job = JobBuilder.Create<QueueJob>()
                .WithIdentity(jobContext.Name)
                .SetJobData(jobData)
                .Build();

            var triggerBuilder = TriggerBuilder.Create()
                .WithIdentity(jobContext.Name)
                .ForJob(jobContext.Name);

            if (!String.IsNullOrWhiteSpace(jobContext.Cron))
                triggerBuilder.WithCronSchedule(jobContext.Cron);
            else
                triggerBuilder.StartNow();

            var trigger = triggerBuilder.Build();

            if (_scheduler.CheckExists(new JobKey(jobContext.Name)))
                _scheduler.DeleteJob(new JobKey(jobContext.Name));

            _scheduler.ScheduleJob(job, trigger);
        }

        public IEnumerable<Job> GetJobs()
        {
            try
            {
                var jobs = new List<Job>();
                
                IList<string> jobGroups = _scheduler.GetJobGroupNames();

                foreach (string group in jobGroups)
                {
                    var groupMatcher = GroupMatcher<JobKey>.GroupContains(group);
                    var jobKeys = _scheduler.GetJobKeys(groupMatcher);
                    
                    foreach (var jobKey in jobKeys)
                    {
                        var jobDetail = _scheduler.GetJobDetail(jobKey);
                        var dataMap = jobDetail.JobDataMap;
                        var jobContext = GetContextFromJobDataMap(jobKey.Name, dataMap);
                        jobs.Add(jobContext);
                    }
                }

                return jobs;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("There was an error retrieving the job detail"), ex);
            }
        }

        public Job GetJob(string name)
        {
            try
            {
                var jobDetail = _scheduler.GetJobDetail(new JobKey(name));
                var dataMap = jobDetail.JobDataMap;
                var jobContext = GetContextFromJobDataMap(name, dataMap);
                return jobContext;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("There is no scheduled job with name {0}", name), ex);
            }
        }

        private static JobDataMap SetJobDataMapFromContext(Job context)
        {
            if (context.Message != null &&
                !string.IsNullOrWhiteSpace(context.Message.ToString()) &&
                context.Message.ToString().Trim().StartsWith("{"))
            {
                context.Message = JsonConvert.SerializeObject(context.Message);
            }

            var jobData = new JobDataMap();
            jobData.Put("JobContext", context);
            return jobData;
        }

        public Job GetContextFromJobDataMap(string name, JobDataMap dataMap)
        {
            var job = dataMap["JobContext"] as Job;
            
            if (job == null)
                throw new ApplicationException(string.Format("JobContext from schedule {0} is null", name));
            
            if (job.Message.GetType() == typeof(String) &&
                !string.IsNullOrWhiteSpace(job.Message) &&
                job.Message.Trim().StartsWith("{"))
            {
                job.Message = JsonConvert.DeserializeObject<dynamic>(job.Message);
            }

            return job;
        }

        public void DeleteJob(string SchedulerName)
        {
            _scheduler.DeleteJob(new JobKey(SchedulerName));
        }
    }
}
