using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

using Pollster.CommonCode;

namespace Pollster.PollVoter.Controllers
{
    [Route("api/[controller]")]
    public class VoterController : Controller
    {
        // PUT api/Voter/5
        [HttpPut("{id}/{optionId}")]
        public async Task<Dictionary<string, int>> Put(string id, string optionId)
        {
            try
            {
                Logger.LogMessage("Submitting vote for poll {0} with option {1}", id, optionId);
                return await VoterProcessor.Instance.SubmitVoteAsync(id, optionId); ;
            }
            catch (Exception e)
            {
                Logger.LogMessage("Unknown error submitting vote for poll {0} with option {1}", id, optionId, Utilities.FormatInnerException(e));
                throw;
            }
        }
    }
}
