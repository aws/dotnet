using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Framework.Configuration;
using Microsoft.Dnx.Runtime;

using Amazon.S3;
using Amazon.S3.Model;

using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;

namespace Pollster.PollsterDeploymentCommands
{
    public class CodeDeployProcessor : IDeploymentProcessor
    {
        public const string CommandName = "codedeploy";

        private IApplicationEnvironment AppEnv { get; set; }
        private IConfiguration Configuration { get; set; }

        private IAmazonS3 S3Client { get; set; }
        private IAmazonCodeDeploy CodeDeployClient { get; set; }
        private UtilityService Utilities { get; set; }

        public CodeDeployProcessor(IApplicationEnvironment appEnv, IConfiguration configuration,  UtilityService utilties,
            IAmazonS3 s3Client, IAmazonCodeDeploy codeDeployClient)
        {
            this.AppEnv = appEnv;
            this.Configuration = configuration;
            this.Utilities = utilties;
            this.S3Client = s3Client;
            this.CodeDeployClient = codeDeployClient;
        }

        public async Task ExecuteAsync()
        {
            var publishDirectory = this.Utilities.GetOutputFolder();
            var applicationBundleFile = string.Format("{0}-{1}.zip", 
                this.AppEnv.ApplicationName, DateTime.Now.Ticks);
            var applicationBundlePath = Path.Combine(publishDirectory, "..", applicationBundleFile);
            Console.WriteLine("Publishing {0} to {1}", this.AppEnv.ApplicationName, publishDirectory);

            // Package up application for publishing
            if(!this.Utilities.ExecutePublish(true))
            {
                Console.Error.WriteLine("Error packaging up project");
                return;
            }


            UtilityService.CopyFile(
                Path.Combine(this.AppEnv.ApplicationBasePath, "CodeDeployScripts/appspec.yml"),
                Path.Combine(publishDirectory, "appspec.yml"));

            if (File.Exists(applicationBundlePath))
                File.Delete(applicationBundlePath);

            Console.WriteLine("Creating application bundle: " + applicationBundlePath);
            ZipFile.CreateFromDirectory(publishDirectory, applicationBundlePath);


            string eTag = null;
            var bucketName = this.Configuration["Deployment:S3Bucket"];

            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                FilePath = applicationBundlePath
            };

            int percentToUpdateOn = 25;
            putRequest.StreamTransferProgress = ((s, e) =>
            {
                if (e.PercentDone == percentToUpdateOn || e.PercentDone > percentToUpdateOn)
                {
                    int increment = e.PercentDone % 25;
                    if (increment == 0)
                        increment = 25;
                    percentToUpdateOn = e.PercentDone + increment;
                    Console.WriteLine("Uploading to S3 {0}%", e.PercentDone);
                }
            });

            Console.WriteLine("Uploading application bunble to S3: {0}/{1}", 
                bucketName, applicationBundleFile);
            eTag = (await this.S3Client.PutObjectAsync(putRequest)).ETag;

            var request = new CreateDeploymentRequest
            {
                ApplicationName = "Pollster-" + this.AppEnv.ApplicationName,
                DeploymentGroupName = "Pollster-" + this.AppEnv.ApplicationName + "-Fleet",
                Revision = new RevisionLocation
                {
                    RevisionType = RevisionLocationType.S3,
                    S3Location = new S3Location
                    {
                        Bucket = bucketName,
                        Key = applicationBundleFile,
                        BundleType = BundleType.Zip,
                        ETag = eTag
                    }
                }
            };
            var response = await this.CodeDeployClient.CreateDeploymentAsync(request);
            Console.WriteLine("Deployment initiated with deployment id {0}", response.DeploymentId);

            return;
        }
    }
}
