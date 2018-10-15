using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.Web;
using IPOService;
using ICOService;

namespace AutomationAPI.Controllers
{
    [RoutePrefix("api/Automation")]
    public class AutomationController : ApiController
    {
        // GET api/Automation
        public bool Get()
        {
            string dbfile = HttpContext.Current.Server.MapPath("~") + "Data.db3";
            string icologfile = HttpContext.Current.Server.MapPath("~") + "ICOLog.txt";
            string ipologfile = HttpContext.Current.Server.MapPath("~") + "IPOLog.txt";
            ICO icoservice = new ICO();
            icoservice.CheckICO(dbfile, icologfile);
            IPO iposervice = new IPO();
            iposervice.CheckIPO(dbfile, ipologfile);
            TimeZoneInfo INDIAN_ZONE = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            using (StreamWriter writer = new StreamWriter(HttpContext.Current.Server.MapPath("~") + "Log.txt", true))
            {
                DateTime indianTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, INDIAN_ZONE);
                writer.WriteLine("Automation ran on " + indianTime.ToString());
                writer.WriteLine();
            }
            return true;
        }

        // GET api/Automation/{data}
        [Route("Save/{data}")]
        [HttpGet]
        public bool Save(string data)
        {
            TimeZoneInfo INDIAN_ZONE = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            using (StreamWriter writer = new StreamWriter(HttpContext.Current.Server.MapPath("~") + "Data.txt", true))
            {
                DateTime indianTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, INDIAN_ZONE);
                writer.WriteLine("Data received at "+ indianTime.ToString() +": " + data);
                writer.WriteLine();
            }
            return true;
        }

        // GET api/Automation/TestMail}
        [Route("TestMail")]
        [HttpGet]
        public bool TestMail()
        {
            NotificationHelper.NotificationHelper mailhelper = new NotificationHelper.NotificationHelper();
            mailhelper.TestMail();
            return true;
        }
    }
}