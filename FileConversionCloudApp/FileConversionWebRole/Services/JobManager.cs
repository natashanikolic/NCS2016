using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace FileConversionWebRole.Services
{
    public class JobManager
    {
        public static readonly JobManager Instance = new JobManager();

        public JobManager()
        {
            // hubContext is used to send messages from outside of the Hub class
            _hubContext = GlobalHost.ConnectionManager.GetHubContext<ProgressHub>();
        }
        
        ConcurrentDictionary<string, Job> _runningJobs = new ConcurrentDictionary<string, Job>();
        private IHubContext _hubContext;

        //1. it creates an object to represent the job and allow progress to be reported
        //2. it stores this Job for future reference (in a ConcurrentDictionary so that updates from multiple threads are handled for us)
        public Job DoJobAsync(Action<Job> action)
        {
            var job = new Job(Guid.NewGuid().ToString());

            // this will (should!) never fail, because job.Id is globally unique
            _runningJobs.TryAdd(job.Id, job);

            // launch the work represented by the action on a background thread
            Task.Factory.StartNew(() =>
            {
                action(job);
                job.ReportComplete();
                _runningJobs.TryRemove(job.Id, out job);
            },
            TaskCreationOptions.LongRunning);

            BroadcastJobStatus(job);

            return job;
        }

        private void BroadcastJobStatus(Job job)
        {
            job.ProgressChanged += HandleJobProgressChanged;
            job.Completed += HandleJobCompleted;
        }

        private void HandleJobCompleted(object sender, EventArgs e)
        {
            var job = (Job)sender;

            _hubContext.Clients.Group(job.Id).jobCompleted(job.Id);

            job.ProgressChanged -= HandleJobProgressChanged;
            job.Completed -= HandleJobCompleted;
        }

        private void HandleJobProgressChanged(object sender, EventArgs e)
        {
            var job = (Job)sender;
            _hubContext.Clients.Group(job.Id).progressChanged(job.Id, job.Progress);
        }

        public Job GetJob(string id)
        {
            Job result;
            return _runningJobs.TryGetValue(id, out result) ? result : null;
        }
    }
}