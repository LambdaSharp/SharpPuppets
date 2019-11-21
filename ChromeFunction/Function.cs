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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaSharp;
using PuppeteerSharp;
using Sharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SharpPuppets.Module.ChromeFunction {

    public class FunctionResponse { }

    public class Function : ALambdaFunction<Models.Site, FunctionResponse> {

        //--- Constants ---
        private const string SCREENSHOT_PATH = "/tmp/screenshot.png";

        //--- Fields ---
        private IAmazonS3 _s3Client;
        private string _bucketName;
        private Browser _browser;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            _bucketName = AwsConverters.ReadS3BucketName(config, "ScrapeBucket");

            // initialize clients
            _s3Client = new AmazonS3Client();

            // initialize chrome with puppeteer
            var nodeResult = Execute("/bin/bash", "-c \"/opt/node-v10.17.0-linux-x64/bin/node /opt/chromeLayer/startBrowser.js\"");
            LogInfo($"startBrowser.js output: {nodeResult.Output}");
            if(!string.IsNullOrEmpty(nodeResult.Error)) {
                LogInfo($"startBrowser.js errors: {nodeResult.Error}");
            }
            var match = Regex.Match(nodeResult.Output, @"(=browser.wsEndpoint=)(?<ws>([\w.:/-])*)(=)");
            _browser = await Puppeteer.ConnectAsync(new ConnectOptions() {
                BrowserWSEndpoint = match.Groups["ws"].Value
            });
        }

        public override async Task<FunctionResponse> ProcessMessageAsync(Models.Site site) {
            var page = await _browser.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions {
                Width = 1280,
                Height = 1024
            });
            foreach(var step in site.Steps) {
                LogInfo($"STEP: {SerializeJson(step)}");
                switch(step.Action) {
                case "go":
                    await page.GoToAsync(step.Value, new NavigationOptions {
                        WaitUntil = new[] {
                            WaitUntilNavigation.DOMContentLoaded
                        }
                    });
                    break;
                case "title":
                    var title = await page.GetTitleAsync();
                    LogInfo($"Page Title: {title}");
                    break;
                case "screenshot":
                    await page.ScreenshotAsync(SCREENSHOT_PATH);
                    await _s3Client.PutObjectAsync(new PutObjectRequest {
                        FilePath = SCREENSHOT_PATH,
                        BucketName = _bucketName,
                        Key = $"{site.SiteName}/{step.Value ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.png"
                    });
                    break;
                case "click":
                    await page.ClickAsync(step.Selector);
                    break;
                case "fill":
                    await page.TypeAsync(step.Selector, step.Value);
                    break;
                default:
                    LogWarn($"Unrecognized command: {step.Action}");
                    break;
                }
            }
            return new FunctionResponse();
        }

        private (int ExitCode, string Output, string Error) Execute(string application, string arguments) {
            Console.WriteLine($"executing: {application} {arguments}");
            using(var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = application,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            }) {
                process.Start();
                var output = Task.Run(() => process.StandardOutput.ReadToEndAsync());
                var error = Task.Run(() => process.StandardError.ReadToEndAsync());
                process.WaitForExit(5000);
                try {
                    process.Kill();
                    process.WaitForExit();
                } catch(System.ComponentModel.Win32Exception) {

                    // nothing to do
                }
                return (ExitCode: process.ExitCode, Output: output.Result, Error: error.Result);
            }
        }

    }
}
