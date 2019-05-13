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
    public class PollVoterController : Controller
    {
        ILogger<PollVoterController> _logger;
        PollManager _manager;

        public PollVoterController(ILogger<PollVoterController> logger, PollManager manager)
        {
            _logger = logger;
            _manager = manager;
        }

        // PUT api/Voter/5
        [HttpPut("{id}/{optionId}")]
        public async Task<IActionResult> Put(string id, string optionId)
        {
            try
            {
                _logger.LogInformation("Submitting vote for poll {0} with option {1}", id, optionId);

                var currentVotes = await _manager.SubmitVoteAsync(id, optionId);
                return Ok(currentVotes);
            }
            catch (Exception e)
            {
                _logger.LogError($"Unknown error submitting vote for poll {id} with option {optionId}: {e}");
            }

            return BadRequest();
        }
    }
}
