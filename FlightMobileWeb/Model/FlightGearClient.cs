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
    public enum Values { AileronE = 0, ElevatorE = 1, RudderE = 2, ThrorrleE = 3 }

    public class FlightGearClient: IFlightGearClient
    {
        // Consts.
        private const string Aileron = "aileron";
        private const string Elevator = "elevator";
        private const string Throttle = "throttle";
        private const string Rudder = "rudder";
        private const string TimeOutMessage = "A connection attempt failed because the connected" +
            " party did not properly respond after a period of time, or" +
            " established connection failed because connected host has failed to respond.";
        private const string ConnectionFaultedErrorMessage = "Connection faulted Error";
        private const int AileronE = (int)Values.AileronE;
        private const int ElevatorE = (int)Values.ElevatorE;
        private const int RudderE = (int)Values.RudderE;
        private const int ThrottleE = (int)Values.ThrorrleE;

        private readonly BlockingCollection<AsyncCommand> queue;
        private readonly TcpClient client;
        private NetworkStream stream;
        private ServerData serverData;
        private bool isConnected = false;
        private Dictionary<string, string> pathMap;

        // Properties.
        public string Error { get; set; }
        public string TimeOutError { get; set; }

        // Constractor.
        public FlightGearClient(IOptions<ServerData> o)
        {
            pathMap = new Dictionary<string, string>();
            queue = new BlockingCollection<AsyncCommand>();
            client = new TcpClient();
            serverData = o.Value;
            // Initialize the pathMap.
            pathMap.Add(Aileron, "/controls/flight/aileron");
            pathMap.Add(Throttle, "/controls/engines/current-engine/throttle");
            pathMap.Add(Rudder, "/controls/flight/rudder");
            pathMap.Add(Elevator, "/controls/flight/elevator");
            Error = "";
            isConnected = false;
            Start();
        }

        // Enter a given command to the queue if there is a connection to the simulator.
        public Task<Result> Execute(Command command)
        {
            var asyncCommand = new AsyncCommand(command);
            // No connection with the simulator.
            if (!isConnected)
            {
                asyncCommand.Completion.SetResult(Result.Error);
                return asyncCommand.Task;
            }
            queue.Add(asyncCommand);
            return asyncCommand.Task;
        }

        // Connect to the simulator.
        private void ConnectToSimulator()
        {
            try
            {
                //dummy server.
                string ip = serverData.Ip;
                int port = serverData.Port;
                client.Connect(ip, port);
                isConnected = true;
                stream = client.GetStream();
            }
            // Connection failed.
            catch (Exception)
            {
                isConnected = false;
                return;
            }
        }

        // Excecute commands from the queue.
        public void ProcessCommand()
        {
            ConnectToSimulator();
            if (!isConnected) return;

            // To get the information in numbers.
            Write("data\n");
            // Go through the commands in the queue
            foreach (AsyncCommand command in queue.GetConsumingEnumerable())
            {
                string setMessage = CreateSetMessage(command);
                Result resWrite = Write(setMessage);
                // Error accured while writing to the simulator.
                if (resWrite.Equals(Result.Error))
                {
                    command.Completion.SetResult(resWrite);
                    continue;
                }
                string getMessage = CreateGetMessage();
                Write(getMessage);
                string returnValue = Read();
                // Error or Timeout while reading from the simulator.
                if(returnValue.Equals("E") || returnValue.Equals("T"))
                {
                    command.Completion.SetResult(Result.Error);
                    continue;
                }
                // Check if the values of the simualtor are simialer to the values we set.
                Result res = CheckValidation(command.Command, returnValue);
                command.Completion.SetResult(res);
            }
        }

        // Create a get message to get the values of the commands properties.
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

        // Generate a command object from the given string array.
        private Command setActualCommand(string[] actualValues)
        {
            Command command = new Command();
            if (Double.TryParse(actualValues[AileronE], out double actualAileron))
            {
                command.Aileron = actualAileron;
            } else return null;

            if (Double.TryParse(actualValues[ElevatorE], out double actualElevator))
            {
                command.Elevator = actualElevator;
            } else return null;

            if (Double.TryParse(actualValues[RudderE], out double actualRudder))
            {
                command.Rudder = actualRudder;
            } else return null;

            if (Double.TryParse(actualValues[ThrottleE], out double actualThrottle))
            {
                command.Throttle = actualThrottle;
            } else return null;

            return command;
        }

        // Check if the given return value is similar to the values of the given command.
        private Result CheckValidation(Command command, string returnValue)
        {
            double expectedAileron = command.Aileron;
            double expectedElevator = command.Elevator;
            double expectedRudder = command.Rudder;
            double expectedThrottle = command.Throttle;
            // Separate returnValue by '\n'
            string[] actualValues = returnValue.Split('\n'); 
            if (actualValues.Length != 5) return Result.NotOk;

            Command actualCommand = setActualCommand(actualValues);
            if (actualCommand == null) return Result.NotOk;

            // If at least one value is different from the expected value - this is not ok.
            if (!actualCommand.Aileron.Equals(expectedAileron)
                || !actualCommand.Elevator.Equals(expectedElevator)
                || !actualCommand.Rudder.Equals(expectedRudder)
                || !actualCommand.Throttle.Equals(expectedThrottle))
            {
                return Result.NotOk;
            }
            return Result.Ok;
        }

        // Create a set message to set the values of the simualtor by the given command values.
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

        // Start communication with the simulator.
        public void Start()
        {
            Task.Factory.StartNew(ProcessCommand);
        }

        // White the given message to the simulator.
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
            catch (Exception)
            {
                Error = ConnectionFaultedErrorMessage;
                return Result.Error;
            }
        }

        // Read data from the simulator.
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

        // Send http request to get the screenshot from the simulator.
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
            // Server did not responsed in 10 seconds.
            catch (Exception)
            {
                return null;
            }

        }

    }
}
