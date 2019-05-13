using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;

using Amazon;
using Amazon.Runtime;
using Amazon.CodeDeploy;
using Amazon.S3;
using Amazon.ECS;

namespace Pollster.PollsterDeploymentCommands
{
    public class Program
    {
        private readonly IApplicationEnvironment _appEnv;

        private IServiceProvider _serviceProvider;
        public Program(IApplicationEnvironment appEnv)
        {
            _appEnv = appEnv;
        }

        public IConfiguration Configuration { get; set; }

        private void BuildConfiguration(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(_appEnv.ApplicationBasePath, "../.."))
                .AddJsonFile("config.deployment.json");

            Configuration = builder.Build();

            Pollster.CommonCode.Utilities.ConfigureAWS(this.Configuration);
        }

        public IServiceCollection ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddInstance<IAmazonS3>(new AmazonS3Client());
            serviceCollection.AddInstance<IAmazonCodeDeploy>(new AmazonCodeDeployClient());
            serviceCollection.AddInstance<IAmazonECS>(new AmazonECSClient());

            serviceCollection.AddInstance<IApplicationEnvironment>(this._appEnv);
            serviceCollection.AddInstance<IConfiguration>(this.Configuration);
            serviceCollection.AddSingleton<UtilityService>();

            return serviceCollection;
        }

        public void Main(string[] args)
        {
            BuildConfiguration(args);
            var serviceCollection = ConfigureServices();
            this._serviceProvider = serviceCollection.BuildServiceProvider();

            if (args.Length == 0)
            {
                Console.Error.WriteLine("You must supply at least one argument for the command to execute");
                return;
            }

            var commandArguments = new string[args.Length - 1];
            Array.Copy(args, 1, commandArguments, 0, commandArguments.Length);

            try
            {
                IDeploymentProcessor processor;
                switch (args[0].ToLower())
                {
                    case CodeDeployProcessor.CommandName:
                        processor = ActivatorUtilities.GetServiceOrCreateInstance<CodeDeployProcessor>(this._serviceProvider);
                        break;
                    case PublishOnlyProcessor.CommandName:
                        processor = ActivatorUtilities.GetServiceOrCreateInstance<PublishOnlyProcessor>(this._serviceProvider);
                        break;
                    case ECSUpdateTaskProcessor.CommandName:
                        processor = ActivatorUtilities.CreateInstance<ECSUpdateTaskProcessor>(this._serviceProvider, new object[] { commandArguments });
                        break;

                    default:
                        throw new Exception("Unknown deployment service " + args[0]);
                }

                processor.ExecuteAsync().Wait();
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("Error during deployment: {0}", CommonCode.Utilities.FormatInnerException(e));
            }
        }
    }
}
