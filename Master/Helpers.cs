using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace My.MyMod.Master
{
    public static class Helpers
    {
        public static (int ExitCode, string Output, string Error) Exec(string application, string arguments) {
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
                } catch(System.ComponentModel.Win32Exception e) {

                }
                return (ExitCode: process.ExitCode, Output: output.Result, Error: error.Result);
            }
        }
    }
}