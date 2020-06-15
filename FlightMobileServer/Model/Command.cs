using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FlightMobileAppServer.Model
{
    public class Command
    {
        [Required]
        [Range(-1.0, 1.0)]
        [JsonPropertyName("rudder")]
        public float Rudder { get; set; }
        [Required]
        [Range(0.0, 1.0)]
        [JsonPropertyName("throttle")]
        public float Throttle { get; set; }
        [Required]
        [Range(-1.0, 1.0)]
        [JsonPropertyName("aileron")]
        public float Aileron { get; set; }
        [Required]
        [Range(-1.0, 1.0)]
        [JsonPropertyName("elevator")]
        public float Elevator { get; set; }
    }
}
