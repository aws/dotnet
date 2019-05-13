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
    public class PollController : Controller
    {
        ILogger<PollFeedsController> _logger;
        PollManager _manager;

        public PollController(ILogger<PollFeedsController> logger, PollManager manager)
        {
            _logger = logger;
            _manager = manager;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return BadRequest("Missing id to get poll");

                var poll = await this._manager.GetPollByIdAsync(id);
                return Ok(poll);
            }
            catch(Exception e)
            {
                _logger.LogError($"Unknown error getting feed of polls: {e}");
            }

            return BadRequest("Failed to get current feed of polls");
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PollDefinition poll)
        {
            try
            {
                if(!this.ModelState.IsValid)
                    return BadRequest(this.ModelState);

                if (string.IsNullOrEmpty(poll.Id))
                    poll.Id = Guid.NewGuid().ToString();

                if (poll.StartTime < DateTime.Now)
                    poll.StartTime = DateTime.Now;

                if (poll.EndTime < DateTime.Now)
                    return BadRequest("End time for the poll is in the past");
                if (poll.EndTime <= poll.StartTime)
                    return BadRequest("End time is before start time");

                await this._manager.SavePollAsync(poll);

                return Ok(poll);
            }
            catch (Exception e)
            {
                _logger.LogError($"Unknown error getting feed of polls: {e}");
            }

            return BadRequest("Failed to get current feed of polls");
        }
    }
}
