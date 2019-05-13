using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

using Pollster.PollWebFrontend.Models;
using Pollster.PollWebFrontend.Clients;

namespace Pollster.PollWebFrontend.Controllers
{
    public class PollFeedController : Controller
    {
        IPollFeed _pollFeed;
        IPollVoter _pollVoter;

        public PollFeedController(IPollFeed pollFeed, IPollVoter pollVoter)
        {
            this._pollFeed = pollFeed;
            this._pollVoter = pollVoter;
        }

        public async Task<IActionResult> Index(IndexPollFeedViewModel model)
        {
            model.Polls = await this._pollFeed.GetFeed();
            return View(model);
        }

        [HttpPost]
        public async Task<JsonResult> SubmittedVote(string pollId, string optionId)
        {
            var model = new SubmittedVoteViewModel
            {
                PollId = pollId,
                VotedOptionId = optionId
            };
            try
            {
                model.LatestVotes = await this._pollVoter.SubmitVote(pollId, optionId);
                model.Success = true;
            }
            catch(Exception e)
            {
                model.Success = false;
                model.ErrorMessage = Pollster.CommonCode.Utilities.FormatInnerException(e);
            }

            var result = new JsonResult(model);
            return result;
        }
    }
}
