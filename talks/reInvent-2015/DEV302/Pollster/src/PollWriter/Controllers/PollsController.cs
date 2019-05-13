using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

using Pollster.CommonCode;

using Newtonsoft.Json;

namespace Pollster.PollWriter.Controllers
{
    [Route("api/[controller]")]
    public class PollsController : Controller
    {
        // GET: api/polls
        [HttpGet("{id?}/{author?}")]
        public async Task<IEnumerable<PollDefinition>> Get(string id = "", string author = "")
        {
            if(string.IsNullOrEmpty(id) && string.IsNullOrEmpty(author))
            {
                var message = "Either id or author should be set";
                Logger.LogMessage(message);
                throw new Exception(message);
            }
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(author))
            {
                var message = "Either id or author should be set but not both";
                Logger.LogMessage(message);
                throw new Exception(message);
            }

            try
            {                
                if (!string.IsNullOrEmpty(id))
                {
                    Logger.LogMessage("Getting poll {0}", id);
                    return new List<PollDefinition> { await PollWriterManager.Instance.GetPollByIdAsync(id) };
                }

                Logger.LogMessage("Getting polls authored by {0}", author);
                return await PollWriterManager.Instance.GetPollsByAuthorAsync(author);
            }
            catch (Exception e)
            {
                Logger.LogMessage("Unknown error getting poll id = {0}, author = {1}: {2}", id, author, Utilities.FormatInnerException(e));
                throw;
            }
        }

        // POST api/polls
        [HttpPost]
        public async Task<string> Post()
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(this.Request.Body))
                    requestBody = reader.ReadToEnd();

                var poll = JsonConvert.DeserializeObject<PollDefinition>(requestBody);
                var id = await PollWriterManager.Instance.SavePollAsync(poll);
                Logger.LogMessage("New poll saved: {0}", id);
                return id;
            }
            catch (Exception e)
            {
                Logger.LogMessage("Unknown saving new poll: {0}", Utilities.FormatInnerException(e));
                throw;
            }
        }

        // PUT api/polls/5
        [HttpPut("{id}")]
        public async Task Put(string id, [FromBody]PollDefinition value)
        {
            try
            {
                value.Id = id;
                await PollWriterManager.Instance.SavePollAsync(value);
                Logger.LogMessage("Saved existing poll saved: {0}", id);
            }
            catch (Exception e)
            {
                Logger.LogMessage("Unknown saving existing poll: {0}", Utilities.FormatInnerException(e));
                throw;
            }
        }

        // DELETE api/polls/5
        [HttpDelete("{id}")]
        public async Task Delete(string id)
        {
            try
            {
                await PollWriterManager.Instance.DeletePollAsync(id);
                Logger.LogMessage("Deleted poll: {0}", id);
            }
            catch (Exception e)
            {
                Logger.LogMessage("Unknown deleting poll {0}: {1}", id, Utilities.FormatInnerException(e));
                throw;
            }
        }
    }
}
