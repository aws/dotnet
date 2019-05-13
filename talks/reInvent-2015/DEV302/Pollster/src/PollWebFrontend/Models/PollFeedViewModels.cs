using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Pollster.CommonCode;

namespace Pollster.PollWebFrontend.Models
{
    public class IndexPollFeedViewModel
    {
        public IList<PollDefinition> Polls { get; set; }
    }

    public class SubmittedVoteViewModel
    {
        public string PollId { get; set; }

        public string VotedOptionId { get; set; }
        public Dictionary<string, int> LatestVotes { get; set; }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

    }
}
