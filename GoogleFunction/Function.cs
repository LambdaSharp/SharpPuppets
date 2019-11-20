using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaSharp;
using My.MyMod.Master;
using PuppeteerSharp;
using Sharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SharpPuppets.Module.GoogleFunction {

    public class FunctionResponse {

        //--- Properties ---
        public string ResponseMessage;
    }

    public class Function : ALambdaFunction<Models.Site, FunctionResponse> {

        //--- Fields ---
        private string _wsEndpoint;
        private string _screenshotPath;
        private IAmazonS3 _s3Client;
        private string _bucketName;
        private Browser _browser;
        
        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            _screenshotPath = "/tmp/screenshot.png";
            _s3Client = new AmazonS3Client();
            _bucketName = AwsConverters.ReadS3BucketName(config, "ScrapeBucket");

            var nodeResult = Helpers.Exec("/bin/bash", "-c \"/opt/node-v10.17.0-linux-x64/bin/node /opt/chromeLayer/startBrowser.js\"");
            LogInfo($"output {nodeResult.Output}");
            LogInfo($"errors {nodeResult.Error}");
            
            var endpointPattern = @"(=browser.wsEndpoint=)([\w.:/-])*(=)";
            Match m = Regex.Match(nodeResult.Output, endpointPattern);
            _wsEndpoint = m.Value.Split("=")[2];

            var connectOptions = new ConnectOptions() {
                BrowserWSEndpoint = _wsEndpoint
            };
            _browser = await Puppeteer.ConnectAsync(connectOptions);
        }

        public override async Task<FunctionResponse> ProcessMessageAsync(Models.Site site) {
            var page = await _browser.NewPageAsync();
            foreach (var step in site.Steps)
            {
                switch(step.Action) {
                    case "go":
                        await page.GoToAsync(step.Value);
                        break;
                    case "title":
                        var title = await page.GetTitleAsync();
                        LogInfo($"Page Title: {title}");
                        break;
                    default:
                        LogInfo("Unrecognized command.");
                        break;
                }
            }

            return new FunctionResponse {
                ResponseMessage = "Success"
            };
        }
    }
}
