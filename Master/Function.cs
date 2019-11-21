/*
 * MIT License
 *
 * Copyright (c) 2019 LambdaSharp
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.IO;
using System.Threading.Tasks;
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
        private string _chromeFunctionArn;
        private IAmazonLambda _lambdaClient;
        private IAmazonS3 _s3Client;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _chromeFunctionArn = config.ReadText("ChromeFunction");
            _lambdaClient = new AmazonLambdaClient();
            _s3Client = new AmazonS3Client();
        }

        public override async Task<string> ProcessMessageAsync(S3Event s3Event) {
            foreach(var record in s3Event.Records) {
                var scrapeListString = await GetDataFromS3(record.S3.Bucket.Name, record.S3.Object.Key);
                var scrapeList = JsonConvert.DeserializeObject<Models.Payload>(scrapeListString);
                foreach(var site in scrapeList.Sites) {
                    AddPendingTask(_lambdaClient.InvokeAsync(new InvokeRequest {
                        FunctionName = _chromeFunctionArn,
                        InvocationType = "Event",
                        Payload = JsonConvert.SerializeObject(site)
                    }));
                }
            }
            return "Success";
        }

        public async Task<string> GetDataFromS3(string bucket, string key) {
            try {
                using(var response = await _s3Client.GetObjectAsync(new GetObjectRequest {
                    BucketName = bucket,
                    Key = key
                }))
                using(var reader = new StreamReader(response.ResponseStream)) {
                    return await reader.ReadToEndAsync();
                }
            } catch(Exception e) {
                LogError(e, $"Error getting object from bucket {bucket}. Make sure it exists and your bucket is in the same region as this function.");
                throw;
            }
        }
    }
}
