using Common.Logging;
using Scurry.Executor.Loader;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tools.XmlConfigMerge;

namespace Scurry.Executor.Manager
{
    public class JobManager : MarshalByRefObject
    {
        public static JobManager Manager { get; private set; }

        static JobManager()
        {
            Manager = new JobManager();
        }

        private ILog Log { get; set; }
        private CancellationTokenSource TokenSource { get; set; }
        private Dictionary<Scurry.Executor.Job.Base.Job, Tuple<Task, AppDomain>> Jobs { get; set; }
        

        private JobManager()
        {
            Log = LogManager.GetCurrentClassLogger();
            TokenSource = new CancellationTokenSource();
            Jobs = new Dictionary<Scurry.Executor.Job.Base.Job, Tuple<Task, AppDomain>>();
        }

        public void LoadJobs()
        {
            try
            {
                // Copy specified job folder recursively to execution folder
                var jobPath = ConfigurationManager.AppSettings["jobPath"];
                var jobExecutionPath = ConfigurationManager.AppSettings["jobExecutionPath"];

                if (jobExecutionPath == null)
                {
                    throw new ApplicationException(string.Format("jobExecutionPath in appSettings does not exist"));
                }

                if (jobPath != null && Directory.Exists(jobPath))
                {
                    foreach (var dir in Directory.GetDirectories(jobPath))
                    {
                        if (new DirectoryInfo(dir).GetFileSystemInfos().Count() > 0) // Not Empty Directory
                        {
                            // Copy dir to execution folder
                            var exeDir = jobExecutionPath + new DirectoryInfo(dir).Name;
                            DirectoryCopy(dir, exeDir, true);
                            // For each sub folder, load the contents into an application domain
                            var appDom = CreateApplicationDomainFrom(exeDir);
                            // Within the app domain, locate and instantiate any classes that implement Job
                            var appDomJobs = FindJobsIn(appDom);

                            foreach (var appDomJob in appDomJobs)
                            {
                                if (Jobs.Keys.Contains(appDomJob))
                                {
                                    throw new ApplicationException(
                                        string.Format("Attempted to load multiple jobs with name {0}", appDomJob.Name));
                                }

                                // Create new Task calling the Start() method of the Job
                                Task task = Task.Factory.StartNew(() =>
                                {
                                    appDomJob.Start();
                                }, TokenSource.Token);

                                // Add job proxy, task and app domain to Jobs tracking object
                                Jobs.Add(appDomJob, Tuple.Create<Task, AppDomain>(task, appDom));
                            }
                        }
                    }
                }
                else
                {
                    throw new ApplicationException(string.Format("Configured jobPath {0} in appSettings does not exist", jobPath));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error occurred while setting up jobs", ex);
                throw;
            }
        }

        public void UnloadJobs()
        {
            // Cancel all of the running jobs, each job will run to completion before cancelling
            try
            {
                foreach (var job in Jobs.Keys) job.Stop();
                Task.WaitAll(Jobs.Values.Select<Tuple<Task, AppDomain>, Task>(tuple =>
                    tuple.Item1).ToArray()); // Add timeout?
            }
            catch (RemotingException re)
            {
                Log.Error("Communication error occurred while stopping jobs", re);
            }

            // Unload all of the application domains
            foreach (var job in Jobs.Keys)
            {
                try
                {
                    var appDomain = Jobs[job].Item2;
                    AppDomain.Unload(appDomain);
                }
                catch (Exception ex)
                {
                    // If more than one job per application domain, what exception is thrown? Need to catch that
                    // specifically and not log error
                    Log.Error(string.Format("Could not unload application domain for job {0}", job.Name), ex);
                }
            }
        }

        public List<Job.Base.Job> GetJobs()
        {
            return Jobs.Keys.ToList();
        }

        public Job.Base.Job GetJob(string name)
        {
            return Jobs.Keys.SingleOrDefault(j => j.Name == name);
        }

        internal AppDomain CreateApplicationDomainFrom(string dir)
        {
            var dirInfo = new DirectoryInfo(dir);

            var setup = new AppDomainSetup()
            {
                ApplicationName = dirInfo.Name,
                ApplicationBase = dir,
                ConfigurationFile = MergeJobConfig(dir)
            };

            Evidence evidence = new Evidence(AppDomain.CurrentDomain.Evidence);
            var appDomain = AppDomain.CreateDomain(dirInfo.Name, evidence, setup);
            Resolve(appDomain);
            
            return appDomain;
        }

        private string MergeJobConfig(string dir)
        {
            ConfigFileManager config = null;
            var appConfig = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            
            if (File.Exists(Path.Combine(dir, "job.config")))
            {
                var jobConfig = Path.Combine(dir, "job.config");
                config = new ConfigFileManager(appConfig, jobConfig);
            }
            else
            {
                config = new ConfigFileManager(appConfig);
            }

            config.Save(Path.Combine(dir, "app.config"));
            return Path.Combine(dir, "app.config");
        }

        private void Resolve(AppDomain appDomain)
        {
            appDomain.CreateInstanceFromAndUnwrap(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scurry.Executor.Loader.dll"),
                "Scurry.Executor.Loader.Resolver",
                false, 0, null,
                new object[] { AppDomain.CurrentDomain.BaseDirectory,
                    new string[] { AppDomain.CurrentDomain.BaseDirectory } },
                null, null);
        }

        private AssemblyLoader GetLoader(AppDomain appDomain)
        {
            return appDomain.CreateInstanceFromAndUnwrap(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scurry.Executor.Loader.dll"),
                "Scurry.Executor.Loader.AssemblyLoader") as AssemblyLoader;
        }

        internal List<Job.Base.Job> FindJobsIn(AppDomain appDom)
        {
            var jobs = new List<Job.Base.Job>();
            var loader = GetLoader(appDom);
            var jobTypes = loader.GetJobTypes();

            foreach (var jobType in jobTypes)
            {
                var job = appDom.CreateInstanceFromAndUnwrap(jobType.Item1, jobType.Item2) as Job.Base.Job;

                if (job != null)
                {
                    jobs.Add(job);
                }
            }

            return jobs;
        }

        // Direct copy from: http://msdn.microsoft.com/en-us/library/bb762914(v=vs.110).aspx
        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (Directory.Exists(destDirName))
            {
                Directory.Delete(destDirName, true);
            }

            // Create the directory after deleting
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}
