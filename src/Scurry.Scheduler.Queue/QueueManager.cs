using Common.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Scurry.Scheduler.Queue.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scurry.Scheduler.Queue
{
    static public class QueueManager
    {
        static ILog Log { get; set; }

        static QueueManager()
        {
            Log = LogManager.GetCurrentClassLogger();
        }

        static public void Push(Job context)
        {
            var factory = new ConnectionFactory() { HostName = context.Queue.Host };

            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(context.Queue.Name, true, false, false, null);
                    var body = Encoding.UTF8.GetBytes(context.Message.ToString());
                    var properties = channel.CreateBasicProperties();
                    properties.SetPersistent(true);
                    channel.BasicPublish("", context.Queue.Name, properties, body);
                    Log.Debug(string.Format("Pushed message {0} to queue {1}", context.Message.ToString(), context.Queue.Name));
                    Log.Info(string.Format("Pushed message to queue {0}", context.Queue.Name));
                }
            }
        }
    }
}
