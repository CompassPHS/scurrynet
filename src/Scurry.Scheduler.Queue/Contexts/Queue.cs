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
    public class Queue
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Host { get; set; }
    }
}
