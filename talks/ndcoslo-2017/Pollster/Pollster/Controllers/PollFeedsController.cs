using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Pollster.Models;
using Microsoft.Extensions.Logging;

namespace Pollster.Controllers
{
    [Route("api/[controller]")]
    public class PollFeedsController : Controller
    {
        ILogger<PollFeedsController> _logger;
        PollManager _manager;

        public PollFeedsController(ILogger<PollFeedsController> logger, PollManager manager)
        {
            _logger = logger;
            _manager = manager;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var polls = await this._manager.GetPollFeedAsync();
                return Ok(polls);
            }
            catch(Exception e)
            {
                _logger.LogError($"Unknown error getting feed of polls: {e}");
            }

            return BadRequest("Failed to get current feed of polls");
        }
    }
}
