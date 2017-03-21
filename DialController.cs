using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using APIModels.Avaya;
namespace ServicesAPI.Controllers
{
    public class DialController : ApiController
    {
        [HttpGet]
        [Route("Dial/{extoruser}/{dest}")]
        public IHttpActionResult Dial(string extoruser, string dest)
        {
            
            return Ok(Avaya.MakeCall(extoruser, dest));
        }
    }
}
