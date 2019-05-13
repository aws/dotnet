using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Framework.Configuration;
using Microsoft.Dnx.Runtime;


namespace Pollster.PollsterDeploymentCommands
{
    public class PublishOnlyProcessor : IDeploymentProcessor
    {
        public const string CommandName = "publish-only";

        private IApplicationEnvironment AppEnv { get; set; }
        private IConfiguration Configuration { get; set; }
        private UtilityService Utilities { get; set; }
        public PublishOnlyProcessor(UtilityService utilties)
        {
            this.Utilities = utilties;
        }

        public Task ExecuteAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                this.Utilities.ExecutePackage(false);
            });
        }
    }
}
