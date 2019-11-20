using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Amazon.Lambda.Core;
using LambdaSharp;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Newtonsoft.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Lambda.S3Events;
using Sharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace My.SharpPuppets.Master {

    public class Function : ALambdaFunction<S3Event, string> {
         //--- Fields ---
        private string _googleArn;
        private string _twitterArn;
        private IAmazonLambda _lambdaClient;
        private IAmazonS3 _s3Client;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            _googleArn = System.Environment.GetEnvironmentVariable("STR_GOOGLEFUNCTION");
            _twitterArn = System.Environment.GetEnvironmentVariable("STR_TWITTERFUNCTION");
            _lambdaClient = new AmazonLambdaClient();
            _s3Client = new AmazonS3Client();
        }

        public override async Task<string> ProcessMessageAsync(S3Event s3Event)
        {
            var record = s3Event.Records.FirstOrDefault();
            var sourceBucket = record.S3.Bucket.Name;
            var sourceKey = record.S3.Object.Key;

            var scrapeListString = await GetDataFromS3(sourceBucket, sourceKey);
            var scrapeList = JsonConvert.DeserializeObject<Models.Payload>(scrapeListString);

            var tasks = new List<Task>();

            foreach(var site in scrapeList.Sites) {
                switch (site.SiteName)
                {
                    case "Google":
                        tasks.Add(InvokeLambda(_googleArn, site));
                        break;
                    case "Twitter":
                        tasks.Add(InvokeLambda(_twitterArn, site));
                        break;
                    default:
                        break;
                }
            }

            await Task.WhenAll(tasks);

            return "Success";
        }

        public async Task InvokeLambda(string lambdaArn, Models.Site site) {
            var invokeRequest = new InvokeRequest {
                FunctionName = lambdaArn,
                InvocationType = "Event",
                Payload = JsonConvert.SerializeObject(site)
            };

            await _lambdaClient.InvokeAsync(invokeRequest);
        }
        
        public async Task<string> GetDataFromS3(string bucket, string key) {
            try
            {
                var request = new GetObjectRequest {
                    BucketName = bucket,
                    Key = key
                };
                using (GetObjectResponse response = await _s3Client.GetObjectAsync(request))
                using (Stream responseStream = response.ResponseStream) 
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error getting object from bucket {bucket}. Make sure it exists and your bucket is in the same region as this function.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                throw;
            }
        }
    }
}
