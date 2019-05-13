using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Framework.Configuration;

using Amazon;

namespace Pollster.CommonCode
{
    public static class Utilities
    {
        public static string FormatInnerException(Exception exception)
        {
            var writer = new System.IO.StringWriter();
            var lastStacktrace = exception.StackTrace;
            var tabCount = 0;
            var currentException = exception;

            while(currentException != null)
            {
                writer.WriteLine("{0}{1}", new string('\t', tabCount), currentException.Message);
                lastStacktrace = currentException.StackTrace;

                tabCount++;
                currentException = currentException.InnerException;
            }

            writer.WriteLine(lastStacktrace);

            return writer.ToString();
        }

        public static void ConfigureAWS(IConfiguration configuration)
        {
            if (!string.IsNullOrEmpty(configuration["AWS:Region"]))
                AWSConfigs.AWSRegion = configuration["AWS:Region"];
            if (!string.IsNullOrEmpty(configuration["AWS:Profile"]))
                AWSConfigs.AWSProfileName = configuration["AWS:Profile"];
        }
    }
}
