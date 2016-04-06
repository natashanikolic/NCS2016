using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using FileConversionCommon;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using Microsoft.AspNet.SignalR;
using System.Collections.Concurrent;
using System.Web.Script.Serialization;
using System.Text;
using System.Threading;

namespace FileConversionWebRole.Controllers
{
    public class FilesController : Controller
    {
        private FileContext db = new FileContext();
        private CloudQueue filesQueue;
        private static CloudBlobContainer filesBlobContainer;

        private static BlockingCollection<string> _data = new BlockingCollection<string>();
        private CloudQueueMessage queueMessage;

        public FilesController()
        {
            InitializeStorage();
        }

        private void InitializeStorage()
        {
            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            // Get context object for working with blobs, and 
            // set a default retry policy appropriate for a web user interface.
            var blobClient = storageAccount.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the blob container.
            filesBlobContainer = blobClient.GetContainerReference("files");

            // Get context object for working with queues, and 
            // set a default retry policy appropriate for a web user interface.
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the queue.
            filesQueue = queueClient.GetQueueReference("files");
        }



        // GET: Files
        public async Task<ActionResult> Index()
        {
            IHubContext hub = GlobalHost.ConnectionManager.GetHubContext<FileConversionWebRole.ProgressHub>();
            int estimatedTime = 4000;

            // Fetch the queue attributes.
            filesQueue.FetchAttributes();

            // Retrieve the cached approximate message count.
            int? cachedMessageCount = filesQueue.ApproximateMessageCount;

            // Calculate estimated time
            if (cachedMessageCount != null)
            {
                estimatedTime = (int) cachedMessageCount * 4000;   

            }

            hub.Clients.All.progress(25);

            Trace.TraceInformation("Estiamted time = " + estimatedTime);
   
            return View(await db.Files.ToListAsync());
        }

        // GET: Files/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FileConversionCommon.File file = await db.Files.FindAsync(id);
            if (file == null)
            {
                return HttpNotFound();
            }
            return View(file);
        }

        // GET: Files/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Files/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "fileId,destinationEmail")] FileConversionCommon.File file, HttpPostedFileBase uploadFile)
        {
            CloudBlockBlob imageBlob = null;
            // A production app would implement more robust input validation.
            // For example, validate that the image file size is not too large.
            //TODO: Validate zamzar sizeof < 1mb 
            if (ModelState.IsValid)
            {
                if (uploadFile != null && uploadFile.ContentLength != 0 && uploadFile.ContentLength < 1000000)
                {
                    imageBlob = await UploadAndSaveBlobAsync(uploadFile);
                    file.fileURL = imageBlob.Uri.ToString();
                }
                file.postedDate = DateTime.Now;

                _data.Add("Your file is being saved to the database for conversion");
                
                

                file.filename = Path.GetFileName(uploadFile.FileName);
                db.Files.Add(file);
                await db.SaveChangesAsync();
                Trace.TraceInformation("Created AdId {0} in database", file.fileId);

                Email(file.fileURL, file.postedDate, file.destinationEmail); //send email when conversion is ready. 

                if (imageBlob != null)
                {
                    var queueMessage = new CloudQueueMessage(file.fileId.ToString());
                    _data.Add("Your file is being added to the queue");
                    await filesQueue.AddMessageAsync(queueMessage);
                    _data.Add("Your file has been added to the queue for processing...");
                    //Thread.Sleep(5000);
                    // Fetch the queue attributes.
                    filesQueue.FetchAttributes();
                    // Retrieve the cached approximate message count.
                    int? cachedMessageCount = filesQueue.ApproximateMessageCount;
                    _data.Add("Approximate message count " + cachedMessageCount);
                    Trace.TraceInformation("Created queue message for AdId {0}", file.fileId);
                }
                return RedirectToAction("Index");
            }

            return View(file);
        }

        // GET: Files/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FileConversionCommon.File file = await db.Files.FindAsync(id);
            if (file == null)
            {
                return HttpNotFound();
            }
            return View(file);
        }

        // POST: Files/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "fileId,fileURL,convertedFilelURL,postedDate,destinationEmail")] FileConversionCommon.File file)
        {
            if (ModelState.IsValid)
            {
                db.Entry(file).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(file);
        }

        // GET: Files/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FileConversionCommon.File file = await db.Files.FindAsync(id);
            if (file == null)
            {
                return HttpNotFound();
            }
            return View(file);
        }

        // POST: Files/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            FileConversionCommon.File file = await db.Files.FindAsync(id);
            db.Files.Remove(file);
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private async Task<CloudBlockBlob> UploadAndSaveBlobAsync(HttpPostedFileBase uploadFile)
        {
            Trace.TraceInformation("Uploading image file {0}", uploadFile.FileName);

            //genereates a unique id for the file that will be converted
            string blobName = Guid.NewGuid().ToString() + Path.GetExtension(uploadFile.FileName);
            // Retrieve reference to a blob. 
            CloudBlockBlob filetoBeConvertedBlob = filesBlobContainer.GetBlockBlobReference(blobName);
            // Create the blob by uploading a local file.
            using (var fileStream = uploadFile.InputStream)
            {
                await filetoBeConvertedBlob.UploadFromStreamAsync(fileStream);
            }

            Trace.TraceInformation("Uploaded image file to {0}", filetoBeConvertedBlob.Uri.ToString());

            return filetoBeConvertedBlob;
        }


        private void Email(string convertedFile, DateTime date, string email) //need to add link here - NO - link comes up automatically... will this work for converted file? 
        {
            //Send E-mail with ticket
            var userEmail = email;
            var d = date;
            var cf = convertedFile;

            if (ModelState.IsValid)
            {
                var body = "<p><h3><u>Conversion Details</u></h3><p><b>Date of Conversion:</b> {0} <br><br><b>Converted File:</b> {1} <br><br>We hope you found our service useful!<br><br><b>ConvertIO</b>";
                var message = new MailMessage();
                //to add attachment to email: message.Attachments.Add(new Attachment(PathToAttachment));      http://stackoverflow.com/questions/5034503/adding-an-attachment-to-email-using-c-sharp
                // https://msdn.microsoft.com/en-us/library/system.net.mail.mailmessage(v=vs.110).aspx
                message.To.Add(new MailAddress(userEmail));
                message.Subject = "ConvertIO Conversion Ready";
                message.Body = string.Format(body, d, cf);
                message.IsBodyHtml = true;

                using (var smtp = new SmtpClient())
                {
                    smtp.Send(message);
                }
            }
        }

        public void Message()
        {
            System.Web.HttpContext.Current.Response.ContentType = "text/event-stream";
            System.Diagnostics.Debug.WriteLine("------- MESSAGE METHOD CALLED ---------------");
            var result = string.Empty;
            var sb = new StringBuilder();
            //tries to remove an item from the BlockingCollection in the specified time period
            if (_data.TryTake(out result, TimeSpan.FromMilliseconds(1000)))
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                var serializedObject = ser.Serialize(new { item = result, message = "" });
                //sb.AppendFormat("data: {0}\n\n", serializedObject);
                System.Web.HttpContext.Current.Response.Write(sb.AppendFormat("data: {0}\n\n", serializedObject));
                System.Web.HttpContext.Current.Response.Flush();
            }
            //System.Diagnostics.Debug.WriteLine("====== " + sb.ToString() + " =======");
            //return Content(sb.ToString(), "text/event-stream");
        }

        public void Download(int id)
        {
            FileConversionCommon.File file = db.Files.Find(id);
            string filename = file.convertedFilelURL;
            string convertedFileName = file.convertedFilename;
            //TO DISUCSS: we can opt to save the GUID in model to eliminate the remove
            string file1 = filename.Remove(0, 46);

            CloudBlockBlob blockBlob = filesBlobContainer.GetBlockBlobReference(file1);

            Response.AddHeader("Content-Disposition", "attachment; filename=" + convertedFileName);
            blockBlob.DownloadToStream(Response.OutputStream);
        }


    }
}