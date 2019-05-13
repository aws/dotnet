using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Pollster.CommonCode;

namespace Pollster.PollWebFrontend.Clients
{
    public class AbstractWebClient
    {
        HttpClient _httpClient;

        public AbstractWebClient(string baseAddress)
        {
            this._httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
        }

        protected async Task<T> Get<T>(string url) where T : class
        {
            return await this.Send<T>(HttpMethod.Get, url, null);
        }

        protected async Task<T> Put<T>(string url, object data) where T : class
        {
            return await this.Send<T>(HttpMethod.Put, url, data);
        }

        protected async Task<T> Delete<T>(string url) where T : class
        {
            return await this.Send<T>(HttpMethod.Delete, url, null);
        }

        protected async Task<T> Post<T>(string url, object data) where T : class
        {
            return await this.Send<T>(HttpMethod.Post, url, data);
        }

        private async Task<T> Send<T>(HttpMethod method, string url, object data) where T : class
        {
            string serializedData = null;
            try
            {
                HttpContent content = null;
                if (data != null)
                {
                    serializedData = JsonConvert.SerializeObject(data);
                    content = new StringContent(serializedData);
                    content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                }

                HttpRequestMessage message = new HttpRequestMessage
                {
                    Method = method,
                    RequestUri = new Uri(this._httpClient.BaseAddress, url),
                    Content = content,
                };
                Logger.LogMessage("Making a {0} request to service {1}", message.Method, message.RequestUri);

                var response = await this._httpClient.SendAsync(message);
                response.EnsureSuccessStatusCode();

                var stringContent = await response.Content.ReadAsStringAsync();
                if (typeof(T) == typeof(string))
                    return stringContent as T;

                var marshalledData = JsonConvert.DeserializeObject<T>(stringContent);
                return marshalledData;
            }
            catch(Exception e)
            {
                if(serializedData == null)
                    Logger.LogMessage("Error making request {0}: {1}", url, Utilities.FormatInnerException(e));
                else
                    Logger.LogMessage("Error making request {0}: url - {1}, data - \n{2}", url, Utilities.FormatInnerException(e), serializedData);
                throw;
            }
        }
    }
}
