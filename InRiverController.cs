using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using APIModels.InRiver;
using System.Web.Http.Cors;

namespace ServicesAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "get,post")]
    public class InRiverController : BaseController
    {
        [HttpGet]
        [Route("InRiver/Queue/{CompanyID}/{Item}")]
        public async Task<IHttpActionResult> PushToQueue(string CompanyID, string Item)
        {
            long ID=await InRiverProcessor.PushItemForUpdate(CompanyID, Item);
            return Ok(ID);
        }
    }
}
