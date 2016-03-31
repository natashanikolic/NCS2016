using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using FileConversionCommon;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure;
using System.IO;
using Microsoft.AspNet.SignalR;

namespace FileConversionWorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private CloudQueue filesQueue;
        private CloudBlobContainer filesBlobContainer;
        private FileContext db;

        //called after onStart
        public override void Run()
        {
            Trace.TraceInformation("FileConversionWorker entry point called");
            CloudQueueMessage msg = null;

            IHubContext hub = GlobalHost.ConnectionManager.GetHubContext<FileConversionWebRole.ProgressHub>();
            // To make the worker role more scalable, implement multi-threaded and 
            // asynchronous code. See:
            // http://msdn.microsoft.com/en-us/library/ck8bc5c6.aspx
            // http://www.asp.net/aspnet/overview/developing-apps-with-windows-azure/building-real-world-cloud-apps-with-windows-azure/web-development-best-practices#async
            while (true)
            {
                try
                {
                    // Retrieve a new message from the queue.
                    // A production app could be more efficient and scalable and conserve
                    // on transaction costs by using the GetMessages method to get
                    // multiple queue messages at a time. See:
                    // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-5-worker-role-b/#addcode
                    msg = this.filesQueue.GetMessage();
                    if (msg != null)
                    {
                        hub.Clients.All.progress(50);
                        ProcessQueueMessage(msg);
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                catch (StorageException e)
                {
                    if (msg != null && msg.DequeueCount > 5)
                    {
                        this.filesQueue.DeleteMessage(msg);
                        Trace.TraceError("Deleting poison queue item: '{0}'", msg.AsString);
                    }
                    Trace.TraceError("Exception in FileConversion: '{0}'", e.Message);
                    System.Threading.Thread.Sleep(5000);
                }
            }          
        }

        private void ProcessQueueMessage(CloudQueueMessage msg)
        {
            try
            {
                ConversionUtils cu = new ConversionUtils();
                Trace.TraceInformation("Processing queue message {0}", msg);
                // Queue message contains AdId.
                var fileId = int.Parse(msg.AsString);
                FileConversionCommon.File f = db.Files.Find(fileId);
                if (f == null)
                {
                    throw new Exception(String.Format("FileId {0} not found, can't create png", fileId.ToString()));
                }

                Uri blobUri = new Uri(f.fileURL);
                string blobName = blobUri.Segments[blobUri.Segments.Length - 1];

                CloudBlockBlob inputBlob = this.filesBlobContainer.GetBlockBlobReference(blobName);
                string convertedFileName = Path.GetFileNameWithoutExtension(inputBlob.Name) + ".png";
                CloudBlockBlob outputBlob = this.filesBlobContainer.GetBlockBlobReference(convertedFileName);

                using (Stream input = inputBlob.OpenRead())
                using (Stream output = outputBlob.OpenWrite())
                {
                    Thread.Sleep(20000);
                    //string jobId = cu.startJob(input, f.filename, "png");
                    //string jobFileId = cu.queryJob(jobId);
                    outputBlob.Properties.ContentType = "image/png";
                    //cu.downloadJob(output, jobFileId);
                }
                Trace.TraceInformation("Generated png in blob {0}", convertedFileName);

                //updating the converted file URL and name in the database
                f.convertedFilelURL = outputBlob.Uri.ToString();
                f.convertedFilename = Path.GetFileNameWithoutExtension(f.filename) + ".png";
                db.SaveChanges();
                Trace.TraceInformation("Updated png URL in database: {0}", f.convertedFilelURL);

                // Remove message from queue.
                this.filesQueue.DeleteMessage(msg);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("I don't understand the path you supplied.");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.ToString());
            }
        }

        // A production app would also include an OnStop override to provide for
        // graceful shut-downs of worker-role VMs.  See
        // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-3-web-role/#restarts
        public override bool OnStart()
        {
            // Read database connection string and open database.
            var dbConnString = CloudConfigurationManager.GetSetting("FileConversionDbConnectionString");
            db = new FileContext(dbConnString);

            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse
                (RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            Trace.TraceInformation("Creating images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            filesBlobContainer = blobClient.GetContainerReference("files");
            if (filesBlobContainer.CreateIfNotExists())
            {
                // Enable public access on the newly created "images" container.
                filesBlobContainer.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }

            Trace.TraceInformation("Creating files queue");
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            filesQueue = queueClient.GetQueueReference("files");
            filesQueue.CreateIfNotExists();

            Trace.TraceInformation("Storage initialized");
            return base.OnStart();

        }

        public override void OnStop()
        {
            Trace.TraceInformation("FileConversionWorkerRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("FileConversionWorkerRole has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
