using FlightMobileAppServer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlightMobileServer.Model
{
    public enum Result {Ok, NotOk, Error}
    public class AsyncCommand
    {
        public Command Command { get; private set; }
        public TaskCompletionSource<Result> Completion { get; private set; }
        public Task<Result> Task { get => Completion.Task; }
        
        public AsyncCommand(Command command)
        {
            this.Command = command;
            Completion = new TaskCompletionSource<Result>
                (TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
