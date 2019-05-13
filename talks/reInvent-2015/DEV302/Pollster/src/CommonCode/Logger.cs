using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon;

using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;

namespace Pollster.CommonCode
{
    public static class Logger
    {
        const string LOG_GROUP_BASENAME = "Pollster.";

        static readonly object SEQUENCE_LOCK = new object();
        static readonly object BUFFER_LOCK = new object();

        static IAmazonCloudWatchLogs _cwlClient;
        static string _logGroup;
        static string _logStream;
        static string _sequenceToken;
        static Task _poller;
        static readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);

        static List<string> _cwlBuffer = new List<string>();

        public static string GetASPNETEnvironment()
        {
            var environment = System.Environment.GetEnvironmentVariable("ASPNET_ENV");
            if (string.IsNullOrEmpty(environment))
                return "Development";
            return environment;
        }

        public async static Task Initialize(string service)
        {
            _logGroup += string.Format("{0}{1}.{2}", LOG_GROUP_BASENAME, GetASPNETEnvironment(), service);
            Console.WriteLine("Ensuring log group {0} exists", _logGroup);

            _cwlClient = new AmazonCloudWatchLogsClient();

            var response = await _cwlClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest { LogGroupNamePrefix = LOG_GROUP_BASENAME });
            if (response.LogGroups.FirstOrDefault(x => string.Equals(x.LogGroupName, _logGroup)) == null)
            {
                await _cwlClient.CreateLogGroupAsync(new CreateLogGroupRequest
                {
                    LogGroupName = _logGroup
                });
            }

            _logStream = DateTime.Now.ToString("s").Replace(':', '-') + "." + new Random().Next(9999).ToString("0000");

            _cwlClient.CreateLogStreamAsync(new CreateLogStreamRequest
            {
                LogGroupName = _logGroup,
                LogStreamName = _logStream
            }).Wait();

            if (_poller == null)
            {
                _poller = new Task(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(_flushInterval);
                        FlushBufferToCloudWatchLogs();
                    }
                });
                _poller.Start();
            }
        }



        public static void LogMessage(string message, params object[] parameters)
        {
            string formattedMessage = DateTime.Now.ToString() + ": " + string.Format(message, parameters);
            Console.WriteLine(formattedMessage);
            lock(BUFFER_LOCK)
            {
                _cwlBuffer.Add(formattedMessage);
            }
        }

        private static void FlushBufferToCloudWatchLogs()
        {
            var request = new PutLogEventsRequest
            {
                LogGroupName = _logGroup,
                LogStreamName = _logStream,
                SequenceToken = _sequenceToken
            };

            lock (BUFFER_LOCK)
            {
                if (_cwlBuffer.Count == 0)
                    return;

                foreach (var message in _cwlBuffer)
                {
                    request.LogEvents.Add(
                        new InputLogEvent
                        {
                            Message = message,
                            Timestamp = DateTime.Now
                        });
                }
                _cwlBuffer.Clear();
            }

            lock(SEQUENCE_LOCK)
            {
                var response = _cwlClient.PutLogEventsAsync(request).Result;
                _sequenceToken = response.NextSequenceToken;
            }
        }
    }
}
