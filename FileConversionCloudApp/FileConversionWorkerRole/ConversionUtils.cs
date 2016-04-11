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

namespace FileConversionWorkerRole
{
    class ConversionUtils
    {
        const string apiKey = "4470bd458bae4d377dae35234e7332f39795adc7";
        //01650bfde2f6f4c950f802526652ce19842f1f27
        const string endpoint = "https://sandbox.zamzar.com/v1/jobs";



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

            return fileId;
        }

        public void downloadJob(Stream outputStream, String fileId)
        {
            string endpointContent = "https://sandbox.zamzar.com/v1/files/" + fileId + "/content";

            //downloads the converted file
            Download(apiKey, endpointContent, outputStream).Wait();
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

        static async Task<JsonValue> Download(string key, string url, Stream outputStream)
        {
            using (HttpClientHandler handler = new HttpClientHandler { Credentials = new NetworkCredential(key, "") })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            using (Stream stream = await content.ReadAsStreamAsync())
            using (outputStream)
            {
                stream.CopyTo(outputStream);
            }

            return null; //added because otherwise it gives me an error
        }

    }
}
