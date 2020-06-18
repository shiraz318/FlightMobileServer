using FlightMobileAppServer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlightMobileServer.Model
{
    public interface IFlightGearClient
    {
        Task<Result> Execute(Command command);
        void ProcessCommand();
        void Start();
        Task<byte[]> SendRequest();



    }
}
