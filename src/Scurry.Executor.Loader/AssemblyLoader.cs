using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scurry.Executor.Loader
{
    [Serializable]
    public class AssemblyLoader : MarshalByRefObject
    {
        public void LoadAssemblies()
        {
            foreach (var fileInfo in new DirectoryInfo(
                AppDomain.CurrentDomain.BaseDirectory).EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly))
            {
                // Called from within the appDomain's space
                Assembly.LoadFrom(fileInfo.FullName);
            }
        }

        public List<Tuple<string, string>> GetJobTypes()
        {
            LoadAssemblies();

            var jobRefs = new List<Tuple<string, string>>();

            foreach (var refAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in refAssembly.GetTypes())
                {
                    if (!type.IsAbstract && typeof(Scurry.Executor.Job.Base.Job).IsAssignableFrom(type))
                    {
                        jobRefs.Add(
                            Tuple.Create<string, string>(
                            refAssembly.Location, type.FullName));
                    }
                }
            }

            return jobRefs;
        }
    }
}
