using Common.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scurry.Executor.Job.Base
{
    public abstract class Job : MarshalByRefObject, IDisposable, IEquatable<Job>
    {
        protected ILog Log { get; set; }
        protected bool disposed = false;

        // MUST be unique across all jobs to be loaded
        public bool IsCancelled { get; private set; }
        public string Name { get; private set; }
        public Queue Queue { get; private set; }

        private Job()
        {
            Log = LogManager.GetCurrentClassLogger();
            IsCancelled = false;
        }

        protected Job(string name)
            : this()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(
                    "name", "Name must not be null or whitespace");
            }

            Name = name;

            // Grab queue settings from config
            string queueHost = ConfigurationManager.AppSettings["queueHost"] ?? "localhost";

            if (!string.IsNullOrWhiteSpace(queueHost))
                Queue = new Queue() { Name = name, Host = queueHost };
            else
            {
                throw new ArgumentNullException(
                    "queue", "Queue, queue name and queue host must not be null or whitespace");
            }

            Log.Info(string.Format("Created job {0} with queue {1} on {2}", name, Queue.Name, Queue.Host));
        }

        protected Job(string name, Queue queue)
            : this()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(
                    "name", "Name must not be null or whitespace");
            }

            if (queue == null ||
                (string.IsNullOrWhiteSpace(queue.Name) ||
                string.IsNullOrWhiteSpace(queue.Host)))
            {
                throw new ArgumentNullException(
                    "queue", "Queue, queue name and queue host must not be null or whitespace");
            }

            Name = name;
            Queue = queue;

            Log.Info(string.Format("Created job {0} with queue {1} on {2}", name, Queue.Name, Queue.Host));
        }

        public abstract void Execute(dynamic context);

        public void Start()
        {
            try
            {
                var factory = new ConnectionFactory() { HostName = Queue.Host };
                    
                using (var connection = factory.CreateConnection())
                {
                    using (var channel = connection.CreateModel())
                    {
                        channel.QueueDeclare(Queue.Name, true, false, false, null);
                        channel.BasicQos(0, 1, false);
                        var consumer = new QueueingBasicConsumer(channel);
                        channel.BasicConsume(Queue.Name, false, consumer);

                        while (!IsCancelled)
                        {
                            BasicDeliverEventArgs ea = null;
                            consumer.Queue.Dequeue(10000, out ea);
                            if (ea == null) continue; // Short circuit null processing
                            var body = ea.Body;
                            var message = Encoding.UTF8.GetString(body);

                            try
                            {
                                if (string.IsNullOrWhiteSpace(message))
                                {
                                    throw new ArgumentException("Message on queue is null or whitespace");
                                }

                                dynamic context;

                                if (message.StartsWith("{"))
                                {
                                    try
                                    {
                                        context = JsonConvert.DeserializeObject<dynamic>(message);
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new ArgumentException("Message was invalid JSON", ex);
                                    }
                                }
                                else
                                {
                                    context = message;
                                }
                                
                                // Only method subclasses MUST implement
                                Execute(context);

                                // Should we be moving this to the global space outside the inner try/catch?
                                channel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (ArgumentException aex)
                            {
                                Log.Error(string.Format("Invalid message for job {0}", Name), aex);
                                // Ack the invalid message, we won't ever process it correctly
                                channel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(string.Format("Error executing job {0}", Name), ex);
                            }
                        }
                    }

                    Log.Info(string.Format("Job {0} has been shutdown", Name));
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Error occurred while listening for job {0}", Name), ex);
            }
        }

        public void Stop()
        {
            IsCancelled = true;
        }

        #region Overloads

        public override string ToString()
        {
            return "Job " + Name;
        }

        public static bool operator ==(Job job1, Job job2)
        {
            if (object.ReferenceEquals(job1, job2)) return true;
            if (object.ReferenceEquals(job1, null)) return false;
            if (object.ReferenceEquals(job2, null)) return false;
            return job1.Equals(job2);
        }

        public static bool operator !=(Job job1, Job job2)
        {
            if (object.ReferenceEquals(job1, job2)) return false;
            if (object.ReferenceEquals(job1, null)) return true;
            if (object.ReferenceEquals(job2, null)) return true;
            return !job1.Equals(job2);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                // noop, nothing to dispose by default
            }
            
            disposed = true;
        }

        #endregion

        #region IEquatable<Job>

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Job job = obj as Job;

            if (job != null)
            {
                return Equals(job);
            }
            else
            {
                return false;
            }
        }

        public bool Equals(Job other)
        {
            return this.Name == other.Name;
        }

        #endregion
    }
}
