using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pollster.PollWebFrontend.Clients
{
    public interface IPollVoter
    {
        Task<Dictionary<string, int>> SubmitVote(string pollId, string optionId);
    }

    public class PollVoterClient : AbstractWebClient, IPollVoter
    {
        public PollVoterClient(string baseAddress)
            : base(baseAddress)
        {

        }

        public async Task<Dictionary<string, int>> SubmitVote(string pollId, string optionId)
        {
            return await base.Put<Dictionary<string, int>>(string.Format("api/Voter/{0}/{1}", pollId, optionId), null);
        }
    }
}
