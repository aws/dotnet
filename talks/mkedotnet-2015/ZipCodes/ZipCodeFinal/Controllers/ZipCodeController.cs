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
    public class ZipCodeController : ApiController
    {
        

        #region Original Code
        // GET: api/ZipCode/98052
        public ZipCodeEntity Get(string id)
        {
            try
            {
                long start = DateTime.Now.Ticks;

                var context = new ZipCodeManager();
                ZipCode.DAL.ZipCodeEntity code = context.GetZipCode(id);

                var ts = new TimeSpan(DateTime.Now.Ticks - start);
                Trace.WriteLine(string.Format(
                    "Zip code {0} details received in {1} ms from database", id, ts.TotalMilliseconds));

                return code;
            }
            finally
            {
                Trace.Flush();
            }
        }
        #endregion
    }
}
