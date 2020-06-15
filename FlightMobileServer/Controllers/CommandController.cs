using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlightMobileAppServer.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static System.Net.Mime.MediaTypeNames;
using System.Web;

namespace FlightMobileAppServer.Controllers
{
    [Route("")]
    [ApiController]
    public class CommandController : ControllerBase
    {
        private IManager manager;

        public CommandController(IManager manager)
        {
            this.manager = manager;
        }

        // GET: screenshot
        [Route("screenshot")]
        [HttpGet]
        public async Task<FileContentResult> Get()
       // public async Task<string> Get()
        {
            //return "ok";

           // byte[] returnValue = await manager.SendRequest("http://localhost:8080");
            byte[] returnValue = await manager.SendRequest("http://localhost:5000");
            // Error accured
            if (returnValue == null)
            {
                return null;
            }
            return File(returnValue, "image/jpg");
            //  return Ok("screenshot!!");
        }

        // POST: api/Command
        [Route("api/Command")]
        [HttpPost]
        public ActionResult<string> Post([FromBody] Command command)
        {
            SetInfo setInfo = manager.SetSimulator(command);
            if (setInfo.IsErrorHappend)
            {
                return BadRequest(setInfo.ErrorMessage);
            }
            return Ok();
        }

        // POST: disconnect
        [Route("disconnect")]
        [HttpPost]
        public void Disconnect()
        {
            manager.Disconnect();
        }

    }
}
