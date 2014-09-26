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
    public class Resolver : MarshalByRefObject
    {
        public string[] LoadPaths { get; set; }

        public Resolver(string workingPath, string[] loadPaths)
        {
            LoadPaths = loadPaths;
            Directory.SetCurrentDirectory(workingPath);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            string fileName = string.Format("{0}.dll", assemblyName.Name);
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)))
                return Assembly.LoadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName));
            else
            {
                foreach (string path in LoadPaths)
                {
                    if (File.Exists(Path.Combine(path, fileName)))
                        return Assembly.LoadFile(Path.Combine(path, fileName));
                }
            }

            return null;
        }
    }
}
