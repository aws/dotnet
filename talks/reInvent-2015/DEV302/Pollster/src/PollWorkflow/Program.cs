using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNet.Hosting;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;

using Pollster.CommonCode;


namespace Pollster.PollWorkflow
{
    public class Program
    {
        public Program(IApplicationEnvironment appEnv)
        {
            var builder = new ConfigurationBuilder(appEnv.ApplicationBasePath)
                .AddJsonFile("config.json");

            Configuration = builder.Build();

            Pollster.CommonCode.Utilities.ConfigureAWS(this.Configuration);

            Pollster.CommonCode.Logger.Initialize("Workflow").Wait();
            Pollster.CommonCode.Logger.LogMessage("Started poll workflow: {0} process id", System.Diagnostics.Process.GetCurrentProcess().Id);
        }

        public IConfiguration Configuration { get; set; }

        public void Main(string[] args)
        {
            try
            {
                CancellationTokenSource source = new CancellationTokenSource();

                var startTimerActivityTask = new StartTimerExpiredActivity().Start(source.Token);
                var endTimerActivityTask = new EndTimerExpiredActivity().Start(source.Token);
                var deciderTask = new WorkflowDecider().Start(source.Token);

                Logger.LogMessage("All threads start.");

                while(!startTimerActivityTask.IsCanceled && !endTimerActivityTask.IsCanceled && !deciderTask.IsCanceled)
                {
                    Thread.Sleep(5000);
                }
                source.Cancel();
                Logger.LogMessage("Workflow processor exiting.");
            }
            catch(Exception e)
            {
                Logger.LogMessage("Unknown error causing Workflow processor to quit: {0}", Utilities.FormatInnerException(e));
            }
        }
    }
}
