using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Pollster.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Pollster.Controllers
{
    [Route("api/[controller]")]
    public class SampleDataController : Controller
    {
        ILogger<PollFeedsController> _logger;
        PollManager _manager;

        public SampleDataController(ILogger<PollFeedsController> logger, PollManager manager)
        {
            _logger = logger;
            _manager = manager;
        }


        [HttpPost]
        public async Task<IActionResult> AddSampleData()
        {
            var sb = new StringBuilder();
            try
            {
                using (var reader = new StreamReader(typeof(SampleDataController).GetTypeInfo().Assembly.GetManifestResourceStream("Pollster.sample-polls.json")))
                {
                    var polls = JsonConvert.DeserializeObject<PollDefinition[]>(reader.ReadToEnd());

                    foreach(var poll in polls)
                    {
                        TimeSpan ts = poll.EndTime - poll.StartTime;
                        poll.StartTime = DateTime.Now;
                        poll.EndTime = poll.StartTime.Add(ts);

                        this._logger.LogInformation($"Adding poll {poll.Title}");
                        sb.AppendLine($"Adding poll {poll.Title}");

                        await this._manager.SavePollAsync(poll);
                    }

                    sb.AppendLine($"Complete adding sample data");
                    return Ok(sb.ToString());
                }
            }
            catch (Exception e)
            {
                this._logger.LogError($"Unknown error getting feed of polls: {e}");
            }

            return BadRequest("Failed to get current feed of polls");
        }
    }
}
