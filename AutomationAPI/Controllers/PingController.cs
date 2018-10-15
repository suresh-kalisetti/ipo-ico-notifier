using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace AutomationAPI.Controllers
{
    public class PingController : ApiController
    {
        // GET api/Ping
        public string Get()
        {
            return "Running";
        }
        
    }
}