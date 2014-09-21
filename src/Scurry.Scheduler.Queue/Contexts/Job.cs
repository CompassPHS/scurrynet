using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Scurry.Scheduler.Queue.Contexts
{
    [DataContract]
    [Serializable]
    public class Job
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Cron { get; set; }
        [DataMember]
        public Queue Queue { get; set; }
        [DataMember]
        public dynamic Message { get; set; }
    }
}
