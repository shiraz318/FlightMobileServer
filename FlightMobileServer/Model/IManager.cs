using FlightMobileAppServer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlightMobileAppServer.Model
{
    public interface IManager
    {
        SetInfo SetSimulator(Command command);
        Task<byte[]> SendRequest(string url);
    }
}
