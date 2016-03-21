using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Json;
using Newtonsoft.Json.Linq;
using System.Web;

namespace FileConversionWorkerRole
{
    class ConversionUtils
    {
        public void startJob()
        {
            const string apiKey = "4470bd458bae4d377dae35234e7332f39795adc7";
            const string endpoint = "https://sandbox.zamzar.com/v1/jobs";
            const string sourceFile = @"/tmp/music.gif";
            const string targetFormat = "png";

            //send a request to the jobs endpoint
            JObject json = Upload(apiKey, endpoint, sourceFile, targetFormat).Result;
            //json contains the status of the job i.e. initialising
            Console.WriteLine(json);

            //check to see whether your job has finished successfully by sending GET request to the jobs/ID endpoint
            string jobId = json.GetValue("id").ToString();
            string endpointJob = "https://sandbox.zamzar.com/v1/jobs/" + jobId;
            JObject jsonJob = Query(apiKey, endpointJob).Result;
            Console.WriteLine(jsonJob);

            //get the target ID from json
            JToken token = JToken.Parse(jsonJob.ToString());
            JArray targetArray = (JArray)token.SelectToken("target_files");
            string fileId = "";

            foreach (JToken t in targetArray)
            {
                fileId = t["id"].ToString();
            }

            //string fileId = men.First().ToString();
            string endpointContent = "https://sandbox.zamzar.com/v1/files/" + fileId + "/content";
            const string localFilename = @"/tmp/music.png";

            //downloads the converted file
            Download(apiKey, endpointContent, localFilename).Wait();
        }

        static async Task<JObject> Upload(string key, string url, string sourceFile, string targetFormat)
        {
            using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
            using (HttpClient client = new HttpClient(handler))
            {
                var request = new MultipartFormDataContent();
                request.Add(new StringContent(targetFormat), "target_format");
                request.Add(new StreamContent(File.OpenRead(sourceFile)), "source_file", new FileInfo(sourceFile).Name);
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

        static async Task<JsonValue> Download(string key, string url, string file)
        {
            using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            using (Stream stream = await content.ReadAsStreamAsync())
            using (FileStream writer = File.Create(file))
            {        
                stream.CopyTo(writer);
            }
            
            return null; //added because otherwise it gives me an error
        }

    }
}
