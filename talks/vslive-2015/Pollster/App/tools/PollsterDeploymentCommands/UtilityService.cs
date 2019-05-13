using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;


namespace Pollster.PollsterDeploymentCommands
{
    public class UtilityService
    {
        public UtilityService(IApplicationEnvironment appEnv, IConfiguration configuration)
        {
            this.AppEnv = appEnv;
            this.Configuration = configuration;
        }

        private IApplicationEnvironment AppEnv { get; set; }
        private IConfiguration Configuration { get; set; }


        public bool ExecutePackage(bool includeActiveRuntime)
        {
            var outputFolder = GetOutputFolder();
            if(Directory.Exists(outputFolder))
            {
                foreach (var file in Directory.GetFiles(outputFolder))
                    File.Delete(file);
                foreach (var directory in Directory.GetDirectories(outputFolder))
                    Directory.Delete(directory, true);
            }

            string arguments = string.Format(@"publish --out {0} --configuration Release ", outputFolder);

            if (Directory.Exists(Path.Combine(this.AppEnv.ApplicationBasePath, "wwwroot")))
                arguments += " --wwwroot-out wwwroot";

            if (includeActiveRuntime)
                arguments += " --runtime active";

            ProcessStartInfo start = new ProcessStartInfo
            {
                WorkingDirectory = this.AppEnv.ApplicationBasePath,
                CreateNoWindow = false,
                UseShellExecute = false,
                RedirectStandardOutput = true,            
                RedirectStandardError = true,
                FileName = GetDNUPath(),
                Arguments = arguments   
            };

            using (Process process = Process.Start(start))
            {
                using (var reader = process.StandardOutput)
                using(var errorReader = process.StandardError)
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Console.WriteLine(line);
                    }
                }

                Console.WriteLine("dnu publish exit code: " + process.ExitCode);
                return process.ExitCode == 0;
            }            
        }

        public string GetOutputFolder()
        {
            string directory = Path.Combine(this.Configuration["Deployment:PackagingDirectory"], this.AppEnv.ApplicationName);
            return directory;
        }

        public string GetDNUPath()
        {
            var dnxPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var binPath = Path.GetDirectoryName(dnxPath);

            var dnuPath = Path.Combine(binPath, "dnu.cmd");
            if (!File.Exists(dnuPath))
                dnuPath = Path.Combine(binPath, "dnu");
            
            if (!File.Exists(dnuPath))
                throw new Exception("Failed to find dnu in runtime path " + binPath);


            return dnuPath;
        }

        public static void CopyFile(string source, string destination)
        {
            if (File.Exists(destination))
                File.Delete(destination);

            File.Copy(source, destination);
        }
    }
}
