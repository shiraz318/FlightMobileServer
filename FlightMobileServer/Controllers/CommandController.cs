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
       
        private IFlightGearClient flightGearClient;

        public CommandController(IFlightGearClient flightGearClient)
        {
            this.flightGearClient = flightGearClient;
        }

        // GET: screenshot
        [Route("screenshot")]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            byte[] returnValue = await flightGearClient.SendRequest();
            // Error accured
            if (returnValue == null)
            {
                return NotFound();
            }
            return File(returnValue, "image/jpg");
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
        }

    }
}
