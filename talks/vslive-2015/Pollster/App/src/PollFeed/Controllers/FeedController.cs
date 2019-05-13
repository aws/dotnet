using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

using Pollster.CommonCode;

namespace Pollster.PollFeed.Controllers
{
    [Route("api/[controller]")]
    public class FeedController : Controller
    {
        // GET: api/feed
        [HttpGet]
        public async Task<IEnumerable<PollDefinition>> Get()
        {
            try
            {
                Logger.LogMessage("Getting current feed of polls");
                return await PollFetcher.Instance.GetPollFeedAsync();
            }
            catch(Exception e)
            {
                Logger.LogMessage("Unknown error getting current feed of polls: {0}", Utilities.FormatInnerException(e));
                throw;
            }
        }

        // GET api/feed/5
        [HttpGet("{id}")]
        public async Task<PollDefinition> Get(string id)
        {
            try
            {
                Logger.LogMessage("Getting poll {0}", id);
                return await PollFetcher.Instance.GetPollByIdAsync(id);
            }
            catch (Exception e)
            {
                Logger.LogMessage("Unknown error getting poll {0}: {1}", id, Utilities.FormatInnerException(e));
                throw;
            }
        }
    }
}
