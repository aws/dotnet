using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pollster.PollsterDeploymentCommands
{
    public interface IDeploymentProcessor
    {
        Task ExecuteAsync();
    }
}
