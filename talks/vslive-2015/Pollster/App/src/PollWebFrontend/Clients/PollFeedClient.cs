using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Pollster.CommonCode;

namespace Pollster.PollWebFrontend.Clients
{

    public interface IPollFeed
    {
        Task<IList<PollDefinition>> GetFeed();
        Task<PollDefinition> GetById(string id);
    }

    public class PollFeedClient : AbstractWebClient, IPollFeed
    {
        public PollFeedClient(string baseAddress)
            : base(baseAddress)
        {

        }

        public async Task<IList<PollDefinition>> GetFeed()
        {
            var data = await base.Get<IList<PollDefinition>>("api/Feed");
            return data;
        }

        public async Task<PollDefinition> GetById(string id)
        {
            var data = await base.Get<IList<PollDefinition>>("api/Feed/" + id);
            return data.FirstOrDefault();
        }
    }
}
