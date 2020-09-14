using System.Threading.Tasks;
using FlightMobileAppServer.Model;
using Microsoft.AspNetCore.Mvc;
using FlightMobileServer.Model;

namespace FlightMobileAppServer.Controllers
{
    [Route("")]
    [ApiController]
    public class CommandController : ControllerBase
    {
       
        private IFlightGearClient flightGearClient;

        // Constractor.
        public CommandController(IFlightGearClient flightGearClient)
        {
            this.flightGearClient = flightGearClient;
        }

        // GET: screenshot
        [Route("screenshot")]
        [HttpGet]
        // Return an FileContentResult of image or NotFound() if error accured.
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
        // Update the simulator with the given command values.
        public async Task<ActionResult> Post([FromBody] Command command)
        {
            Result res = await flightGearClient.Execute(command);

            if (res.Equals(Result.Ok)) return Ok();
            if (res.Equals(Result.Error)) return NotFound();

            // Result.NotOk
            return BadRequest();
        }

    }
}
