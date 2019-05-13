using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Framework.Configuration;
using Microsoft.Dnx.Runtime;

using Amazon;
using Amazon.Runtime;
using Amazon.ECS;
using Amazon.ECS.Model;

using Task = System.Threading.Tasks.Task;

namespace Pollster.PollsterDeploymentCommands
{
    public class ECSUpdateTaskProcessor : IDeploymentProcessor
    {
        public const string CommandName = "ecs-task-update";

        private IApplicationEnvironment AppEnv { get; set; }
        private IConfiguration Configuration { get; set; }
        private IAmazonECS ECSClient { get; set; }

        private string[] Args;

        public ECSUpdateTaskProcessor(IApplicationEnvironment appEnv, IConfiguration configuration, IAmazonECS ecsClient , string[] args)
        {
            this.AppEnv = appEnv;
            this.Configuration = configuration;
            this.ECSClient = ecsClient;
            this.Args = args;
        }

        public async System.Threading.Tasks.Task ExecuteAsync()
        {
            if (this.Args.Length == 0)
            {
                Console.Error.WriteLine("Missing image tag to update ecs tag to.");
                return;
            }


            var ecsTaskName = this.AppEnv.ApplicationName.ToLower();
            var ecsCluster = this.Configuration["Deployment:ECSCluster"];

            Console.Write("Fetching existing task definition from ECS...");
            var taskDefinition = (await this.ECSClient.DescribeTaskDefinitionAsync(
                new DescribeTaskDefinitionRequest
                    {
                        TaskDefinition = ecsTaskName }
                )).TaskDefinition;

            var containerDefinition = taskDefinition.ContainerDefinitions.FirstOrDefault(
                x => x.Image.Contains(string.Format("/{0}", 
                ecsTaskName)));

            if (containerDefinition == null)
            {
                Console.Error.WriteLine("\nFailed to find existing container definition for {0}", ecsTaskName);
                return;
            }

            containerDefinition.Image = string.Format("{0}/{1}:{2}",
                this.Configuration["Deployment:DockerImageOwner"], ecsTaskName, this.Args[0]);
            Console.WriteLine("Found Container Definition: " + containerDefinition.Image);

            var registerResponse = await this.ECSClient.RegisterTaskDefinitionAsync(
                new RegisterTaskDefinitionRequest
                {
                    ContainerDefinitions = taskDefinition.ContainerDefinitions,
                    Family = taskDefinition.Family,
                    Volumes = taskDefinition.Volumes
                });
            
            var newTaskRevision = string.Format("{0}:{1}", 
                registerResponse.TaskDefinition.Family, 
                registerResponse.TaskDefinition.Revision);
            Console.WriteLine("Registered new task definition revision: {0}", newTaskRevision);

            Console.WriteLine("Starting rolling deployment of new task definition to service"); 
            await PerformRollingServiceDeployment(ecsCluster, ecsTaskName, newTaskRevision);
        }


        private async Task<bool> PerformRollingServiceDeployment(
            string cluster, string serviceName, string taskRevision)
        {
            var describeRequest = new DescribeServicesRequest
            {
                Cluster = cluster,
                Services = new List<string> { serviceName}
            };
            var originalService = (await this.ECSClient.DescribeServicesAsync(describeRequest)).Services[0];

            int totalTasks = originalService.DesiredCount;

            Console.Write("Reducing the number of tasks to {0} for existing definition...", totalTasks / 2);
            await this.ECSClient.UpdateServiceAsync(new UpdateServiceRequest
            {
                Cluster = cluster,
                Service = serviceName,
                DesiredCount = totalTasks / 2
            });
            if (!await WaitTillUpdateServiceComplete(describeRequest))
            {
                Console.Error.WriteLine("\nECS Cluster did not reduce the number of existing tasks.");
                return false;
            }
            Console.WriteLine("Complete");

            Console.Write("Starting {0} task(s) with new definition...", totalTasks / 2);
            await this.ECSClient.UpdateServiceAsync(new UpdateServiceRequest
            {
                Cluster = cluster,
                Service = serviceName,
                TaskDefinition = taskRevision,
                DesiredCount = totalTasks / 2
            });
            if (!await WaitTillUpdateServiceComplete(describeRequest))
            {
                Console.Error.WriteLine("\nECS Cluster did not start tasks with new task definition.");
                return false;
            }
            Console.WriteLine("Complete");

            Console.Write("Starting remaining tasks with new task definition...");
            await this.ECSClient.UpdateServiceAsync(new UpdateServiceRequest
            {
                Cluster = cluster,
                Service = serviceName,
                DesiredCount = totalTasks
            });
            if (!await WaitTillUpdateServiceComplete(describeRequest))
            {
                Console.Error.WriteLine("\nECS Cluster did not start tasks with new task definition.");
                return false;
            }
            Console.WriteLine("Complete with {0} total tasks", totalTasks);

            return true;
        }

        private async Task<bool> WaitTillUpdateServiceComplete(DescribeServicesRequest describeRequest)
        {
            long start = DateTime.Now.Ticks;
            Service service = null;
            do
            {
                Thread.Sleep(1000);
                service = (await this.ECSClient.DescribeServicesAsync(describeRequest)).Services[0];
            } while (   service.Deployments.Count != 1 && 
                        TimeSpan.FromTicks(DateTime.Now.Ticks - start).TotalMinutes < 2);

            if(service.Deployments.Count != 1)
            {
                return false;
            }

            return true;
        }
    }
}
