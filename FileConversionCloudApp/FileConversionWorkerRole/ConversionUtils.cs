using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Json;
using Newtonsoft.Json.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage.Blob;

namespace FileConversionWorkerRole
{
    class ConversionUtils
    {
        const string apiKey = "4470bd458bae4d377dae35234e7332f39795adc7";
        const string endpoint = "https://sandbox.zamzar.com/v1/jobs";
        public static CloudBlockBlob outputBlobz;

        public string startJob(Stream streamInput, string filename, string targetFormat) //still returning /devstoreaccoutn
        {
            //send a request to the jobs endpoint
            JObject json = Upload(apiKey, endpoint, streamInput, filename, targetFormat).Result;
            //json contains the status of the job i.e. initialising

            //check to see whether your job has finished successfully by sending GET request to the jobs/ID endpoint
            string jobId = json.GetValue("id").ToString();

            return jobId;

        }

        public string queryJob(String jobId)
        {
            Boolean statusSuccessful = false;
            JObject jsonJob;
            string endpointJob = "https://sandbox.zamzar.com/v1/jobs/" + jobId;

            do
            {
                jsonJob = Query(apiKey, endpointJob).Result;
                JToken token = JToken.Parse(jsonJob.ToString());
                String status = token.SelectToken("status").ToString();

                if (status == "successful")
                {
                    statusSuccessful = true;
                }

            } while (statusSuccessful == false);
            
            Console.WriteLine(jsonJob);

            //get the target ID from json
            JToken token2 = JToken.Parse(jsonJob.ToString());
            JArray targetArray = (JArray)token2.SelectToken("target_files");
            string fileId = "";

            foreach (JToken t in targetArray)
            {
                fileId = t["id"].ToString();
            }

            return fileId;
        }

        public void downloadJob(CloudBlockBlob outputBlob, String fileId)
        {
            string endpointContent = "https://sandbox.zamzar.com/v1/files/" + fileId + "/content";

            outputBlobz = outputBlob;

            //downloads the converted file
            Download(apiKey, endpointContent, outputBlob.Uri.ToString()).Wait();
        }

        static async Task<JObject> Upload(string key, string url, Stream sourceFile, string filename, string targetFormat)
        {
            using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
            using (HttpClient client = new HttpClient(handler))
            {
                var request = new MultipartFormDataContent();
                request.Add(new StringContent(targetFormat), "target_format");
                request.Add(new StreamContent(sourceFile), "source_file", filename);
                using (HttpResponseMessage response = await client.PostAsync(url, request))
                using (HttpContent content = response.Content)
                {
                    string data = await content.ReadAsStringAsync();
                    JObject jv = JObject.Parse(data);
                    return jv;
                }
            }
        }

        static async Task<JObject> Query(string key, string url)
        {
            using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            {
                string data = await content.ReadAsStringAsync();
                return JObject.Parse(data);
            }
        }

        //static async Task<JObject> Download(string key, string url, Stream outputStream)
        //{
        //    using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
        //    using (HttpClient client = new HttpClient(handler))
        //    using (HttpResponseMessage response = await client.GetAsync(url))
        //    using (HttpContent content = response.Content)
        //    using (Stream stream = await content.ReadAsStreamAsync())
        //    using (outputStream)
        //    {
        //        stream.CopyTo(outputStream);
        //        JObject jv = JObject.Parse(stream.ToString());
        //        return jv;
        //    }

        //    //return null; //added because otherwise it gives me an error
        //}

        static async Task<JObject> Download(string key, string url, string file)
        {
           // Stream output = outputBlobz.OpenWrite();

            using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            using (Stream stream = await content.ReadAsStreamAsync())
            using (Stream output = outputBlobz.OpenWrite())
            {
                stream.CopyTo(output);
            }

            return null; //added because otherwise it gives me an error
        }

    }
}
