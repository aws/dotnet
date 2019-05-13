using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using ZipCode.DAL;

namespace ZipCodeFinal.Controllers
{
    public class StateController : ApiController
    {

        // GET: api/State/WA
        public IEnumerable<ZipCodeEntity> Get(string id)
        {
            try
            {
                Trace.WriteLine("Getting zip codes for state " + id);
                long start = DateTime.Now.Ticks;

                var context = new ZipCodeManager();
                var zipCodes = context.GetZipCodesForState(id);

                var ts = new TimeSpan(DateTime.Now.Ticks - start);
                Trace.WriteLine("Zip codes retrieved in " + ts.TotalMilliseconds + " ms");
                return zipCodes.OrderBy(x => x.PostalCode);
            }
            finally
            {
                Trace.Flush();
            }
        }
    }
}
