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
using FlightMobileServer.Model;

namespace FlightMobileAppServer.Controllers
{
    [Route("")]
    [ApiController]
    public class CommandController : ControllerBase
    {
        private IManager manager;
        private IFlightGearClient flightGearClient;

        public CommandController(IFlightGearClient flightGearClient)
        {
           // this.manager = manager;
            this.flightGearClient = flightGearClient;
        }

        // GET: screenshot
        [Route("screenshot")]
        [HttpGet]
        public async Task<FileContentResult> Get()
       // public async Task<string> Get()
        {
            //return "ok";

           // byte[] returnValue = await manager.SendRequest("http://localhost:8080");
            byte[] returnValue = await flightGearClient.SendRequest("http://localhost:5000");
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
        public async Task<ActionResult> Post([FromBody] Command command)
        {
            Result res = await flightGearClient.Execute(command);
            if (res.Equals(Result.Ok))
            {
                return Ok();
            }
            if (res.Equals(Result.Error))
            {
                return NotFound();
            }
            return BadRequest();
         
            //SetInfo setInfo = manager.SetSimulator(command);
            //if (setInfo.IsErrorHappend)
            //{
            //    return BadRequest();
            //}
            //return Ok();
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
