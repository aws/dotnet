using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Pollster.CommonCode;

namespace Pollster.PollWebFrontend.Clients
{
    public interface IPollWriter
    {
        Task<PollDefinition> GetById(string id);
        Task<IEnumerable<PollDefinition>> GetByAuthor(string author);
        Task Save(PollDefinition poll);
        Task DeletePoll(string id);
    }

    public class PollWriterClient : AbstractWebClient, IPollWriter
    {
        public PollWriterClient(string baseAddress)
            : base(baseAddress)
        {

        }

        public async Task<PollDefinition> GetById(string id)
        {
            var data = await base.Get<IEnumerable<PollDefinition>>("api/Polls/?id=" + id);
            return data.FirstOrDefault();
        }

        public async Task<IEnumerable<PollDefinition>> GetByAuthor(string author)
        {
            var data = await base.Get<IEnumerable<PollDefinition>>("api/Polls/?author=" + author);
            return data;
        }

        public async Task Save(PollDefinition poll)
        {
            if(string.IsNullOrEmpty(poll.Id))
            {
                var response = await base.Post<string>("api/Polls", poll);
                Logger.LogMessage("Response from saving poll: {0}", response);
            }
            else
            {
                var response = await base.Put<string>("api/Polls/" + poll.Id, poll);
                Logger.LogMessage("Response from saving poll: {0}", response);
            }
        }

        public async Task DeletePoll(string id)
        {
            await base.Delete<string>("api/Polls/" + id);
        }
    }
}
