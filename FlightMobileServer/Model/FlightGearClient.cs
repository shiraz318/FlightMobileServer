using FlightMobileAppServer.Model;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FlightMobileServer.Model
{
    public class FlightGearClient: IFlightGearClient
    {
        private const string TimeOutMessage = "A connection attempt failed because the connected" +
            " party did not properly respond after a period of time, or" +
            " established connection failed because connected host has failed to respond.";
        private const string ConnectionFaultedErrorMessage = "Connection faulted Error";

        private readonly BlockingCollection<AsyncCommand> queue;
        private readonly TcpClient client;
        private NetworkStream stream;
        private ServerData serverData;

        public string Error { get; set; }
        public string TimeOutError { get; set; }
        private Dictionary<string, string> pathMap = new Dictionary<string, string>();
        private const string Aileron = "aileron";
        private const string Elevator = "elevator";
        private const string Throttle = "throttle";
        private const string Rudder = "rudder";
        private bool isConnected = false;

        public FlightGearClient(IOptions<ServerData> o)
        {
            queue = new BlockingCollection<AsyncCommand>();
            client = new TcpClient();
            serverData = o.Value;
            pathMap.Add(Aileron, "/controls/flight/aileron");
            pathMap.Add(Throttle, "/controls/engines/current-engine/throttle");
            pathMap.Add(Rudder, "/controls/flight/rudder");
            pathMap.Add(Elevator, "/controls/flight/elevator");
            Error = "";
            isConnected = false;
            Start();
        }

        public Task<Result> Execute(Command command)
        {
            var asyncCommand = new AsyncCommand(command);
            if (!isConnected)
            {
                asyncCommand.Completion.SetResult(Result.Error);
                return asyncCommand.Task;
            }
            queue.Add(asyncCommand);
            return asyncCommand.Task;
        }


        public void ProcessCommand()
        {
            try
            {
                //dummy server.
                string ip = serverData.Ip;
                int port = serverData.Port;
                client.Connect(ip, port);
            }catch(Exception)
            {
                isConnected = false;
                return;
            }
            isConnected = true;
            stream = client.GetStream();
            Write("data\n");

            foreach (AsyncCommand command in queue.GetConsumingEnumerable())
            {
                string setMessage = CreateSetMessage(command);
                Result resWrite = Write(setMessage);
                if (resWrite.Equals(Result.Error))
                {
                    command.Completion.SetResult(resWrite);
                    continue;
                }
                string getMessage = CreateGetMessage();
                Write(getMessage);
                 string returnValue = Read();
                if(returnValue.Equals("E") || returnValue.Equals("T"))
                {
                    command.Completion.SetResult(Result.Error);
                    continue;
                }
                Result res = CheckValidation(command.Command, returnValue);
                command.Completion.SetResult(res);
            }
        }


        private string CreateGetMessage()
        {
            string pathRudder = pathMap[Rudder];
            string pathThrottle = pathMap[Throttle];
            string pathElevator = pathMap[Elevator];
            string pathAileron = pathMap[Aileron];

            string Aileronmessage = "get " + pathAileron + "\n";
            string Elevatormessage = "get " + pathElevator + "\n";
            string Ruddermessage = "get " + pathRudder + "\n";
            string Throttlemessage = "get " + pathThrottle + "\n";

            string message = Aileronmessage + Elevatormessage + Ruddermessage + Throttlemessage;
            return message;
        }

        private Command setActualCommand(string[] actualValues)
        {
            Command command = new Command();
            if (Double.TryParse(actualValues[0], out double result1))
            {
                command.Aileron = result1;
            } else
            {
                return null;
            }
            if (Double.TryParse(actualValues[1], out double result2))
            {
                command.Elevator = result2;
            } else
            {
                return null;
            }
            if (Double.TryParse(actualValues[2], out double result3))
            {
                command.Rudder = result3;
            } else
            {
                return null;
            }
            if (Double.TryParse(actualValues[3], out double result4))
            {
                command.Throttle = result4;
            } else
            {
                return null;
            }
            return command;
        }

        private Result CheckValidation(Command command, string returnValue)
        {
            double expectedAileron = command.Aileron;
            double expectedElevator = command.Elevator;
            double expectedRudder = command.Rudder;
            double expectedThrottle = command.Throttle;

            string[] actualValues = returnValue.Split('\n'); 
            if (actualValues.Length != 5)
            {
                return Result.NotOk;
            }
            Command actualCommand = setActualCommand(actualValues);
            if (actualCommand == null)
            {
                return Result.NotOk;
            }
            if (!actualCommand.Aileron.Equals(expectedAileron)
                || !actualCommand.Elevator.Equals(expectedElevator)
                || !actualCommand.Rudder.Equals(expectedRudder)
                || !actualCommand.Throttle.Equals(expectedThrottle))
            {
                return Result.NotOk;
            }
            return Result.Ok;
        }

        private string CreateSetMessage(AsyncCommand command)
        {

            string pathRudder = pathMap[Rudder];
            string pathThrottle = pathMap[Throttle];
            string pathElevator = pathMap[Elevator];
            string pathAileron = pathMap[Aileron];

            string valueRudder = command.Command.Rudder.ToString();
            string valueThrottle = command.Command.Throttle.ToString();
            string valueElevator = command.Command.Elevator.ToString();
            string valueAileron = command.Command.Aileron.ToString();

            string Aileronmessage = "set " + pathAileron + " " + valueAileron + "\n";
            string Elevatormessage = "set " + pathElevator + " " + valueElevator + "\n";
            string Ruddermessage = "set " + pathRudder + " " + valueRudder + "\n";
            string Throttlemessage = "set " + pathThrottle + " " + valueThrottle + "\n";

            string message = Aileronmessage + Elevatormessage + Ruddermessage + Throttlemessage;
            return message;
        }

        public void Start()
        {
            Task.Factory.StartNew(ProcessCommand);
        }

        private Result Write(string message)
        {
            try
            {
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                stream.Write(data, 0, data.Length);
                Thread.Sleep(100);
                return Result.Ok;
            }
            // Connection error.
            catch (Exception e)
            {
                string messageerror = e.Message;
                Error = ConnectionFaultedErrorMessage;
                return Result.Error;
               // stop = true;
            }
        }


        private string Read()
        {
            try
            {
                stream.ReadTimeout = 10000;
                Byte[] data = new Byte[1024];
                String responseData = String.Empty;
                Int32 bytes = stream.Read(data, 0, data.Length);
                TimeOutError = "";
                responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                return responseData;
            }
            catch (Exception e)
            {
                // Timeout error.
                if (e.Message.Contains(TimeOutMessage))
                {
                    TimeOutError = "Server is slow";
                    return "T";
                }
                // Connection error.
                else
                {
                    Error = ConnectionFaultedErrorMessage;
                    return "E";
                }
            }
        }
        public async Task<byte[]> SendRequest()
        {
            try
            {
                string http = serverData.HttpAddress;
                string command = http + "/screenshot";
                using var client = new HttpClient();
                TimeSpan timeout = new TimeSpan(0, 0, 0, 10);
                client.Timeout = timeout;
                HttpResponseMessage responseMessage = await client.GetAsync(command);
                byte[] content = await responseMessage.Content.ReadAsByteArrayAsync();

                return content;
            }
            // Server did not responsed in 50 seconds.
            catch (Exception t)
            {
                string g = t.Message;
                return null;
            }

        }


    }
}
