using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FlightMobileAppServer.Model;


namespace FlightMobileAppServer.Model

{
    public struct SetInfo
    {
        public SetInfo(bool isErrorHappend, string errorMessage)
        {
            IsErrorHappend = isErrorHappend;
            ErrorMessage = errorMessage;
        }
        public bool IsErrorHappend { get; set; }
        public string ErrorMessage { get; set; }

    }

    public struct SetMessage
    {
        public SetMessage(float value, string var, string message)
        {
            Value = value;
            Var = var;
            Message = message;
        }
        public float Value { get; set; }
        public string Var { get; set; }
        public string Message { get; set; }

    }


    public class Manager : IManager
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

        // Variables.
        private TcpClient tcpClient;
        private NetworkStream strm;
        private bool stop;
        private Queue<SetMessage> setMessages = new Queue<SetMessage> { };
        private Mutex mutex;
        private Dictionary<string, string> pathMap = new Dictionary<string, string>();
        
        // Properties.
        public string Error { get; set; }
        public string TimeOutError { get; set; }


        public Manager()
        {
            pathMap.Add(Aileron, "/controls/flight/aileron");
            pathMap.Add(Throttle, "/controls/engines/current-engine/throttle");
            pathMap.Add(Rudder, "/controls/flight/rudder");
            pathMap.Add(Elevator, "/controls/flight/elevator");
            Error = "";
            // dummy server.
            Connect("127.0.0.1", 5403);
            // flight gear.
            //Connect("127.0.0.1", 5402);
        }

        private void ConnectFunction(string ip, int port)
        {
            try
            {
                stop = false;
                Error = "";
                setMessages = new Queue<SetMessage> { };
                tcpClient = new TcpClient();
                mutex = new Mutex();

                // Connect.
                tcpClient.Connect(ip, port);
                strm = tcpClient.GetStream();
                Start();
            }
            catch (Exception e)
            {
                string message = e.Message;
                Error = ConnectionFaultedErrorMessage;
            }
        }

        public void Connect(string ip, int port)
        {
            Thread connectThread = new Thread(delegate ()
            {
                ConnectFunction(ip, port);
            });
            connectThread.IsBackground = true;
            connectThread.Start();
        }

        public void Disconnect()
        {
            tcpClient.Close();
            tcpClient.Dispose();
            mutex.Dispose();
            stop = true;
        }
        public void Start()
        {
            Write("data\n");
            Thread setThread = new Thread(delegate ()
            {
                while (!stop)
                {
                    InnerLoopStart();
                }

            });
            setThread.IsBackground = true;
            setThread.Start();
        }

        private float FromReadReturnToFloat(string value)
        {
            // Without the \n.
            string valueAfterSplit = value.Split('\n')[0];
            
            return float.Parse(valueAfterSplit);
        }

        private void ValidateSet(SetMessage correctMessage)
        {

            // Write get message for a specific propery.
            string var = correctMessage.Var;
            float value = correctMessage.Value;
            string path = pathMap[var];
            string message = "get " + path + "\n";
            Write(message);
            // Read from simulator.
            string returnValue = Read();
            float actualReturnValue = FromReadReturnToFloat(returnValue);
            // Check if the return value is the same as the value in the message and update the Error propery accordingly.
            // This is not the value we set.
            if (actualReturnValue != value)
            {
                Error = var + " value is not set as required.";
            }
        }

        private void DealWithMessage()
        {
            mutex.WaitOne();
            SetMessage message = setMessages.Dequeue();
            if (Error.Equals(""))
             {
            // Set the value.
            Write(message.Message);

            }
             if (Error.Equals(""))
             {
             ValidateSet(message);
             }
            mutex.ReleaseMutex();
        }

        private void InnerLoopStart()
        {
            // If there are messages in the queue.
            if (setMessages.Count != 0)
            {
                try
                {
                    DealWithMessage();
                }
                catch (Exception e)
                {
                    string message = e.Message;
                    Error = ConnectionFaultedErrorMessage;
                }
            }
        }


        private string Read()
        {
            try
            {
                strm.ReadTimeout = 10000;
                Byte[] data = new Byte[1024];
                String responseData = String.Empty;
                Int32 bytes = strm.Read(data, 0, data.Length);
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
                }
                // Connection error.
                else
                {
                    Error = ConnectionFaultedErrorMessage;
                    stop = true;
                }
                return "";
            }
        }

        private void Write(string message)
        {
            try
            {
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                strm.Write(data, 0, data.Length);
                Thread.Sleep(20);
            }
            // Connection error.
            catch (Exception e)
            {
                string messageerror = e.Message;
                Error = ConnectionFaultedErrorMessage;
                stop = true;
            }
        }


        private void InsetMessageToQueue(float value, string var)
        {
            string path = pathMap[var];
            string message = "set " + path + " " + value.ToString() + "\n";
            setMessages.Enqueue(new SetMessage(value, var, message));
        }


        public SetInfo SetSimulator(Command command)
        {
            InsetMessageToQueue(command.Aileron, "aileron");
            InsetMessageToQueue(command.Rudder, "rudder");
            InsetMessageToQueue(command.Throttle, "throttle");
            InsetMessageToQueue(command.Elevator, "elevator");

            if (Error.CompareTo("") == 0)
            {
                return new SetInfo(false, null);
            }
            return new SetInfo(true, Error);
        }

        public async Task<byte[]> SendRequest(string url)
        {
            try
            {
                string command = url + "/screenshot";
                using var client = new HttpClient();
                TimeSpan timeout = new TimeSpan(0, 0, 0, 50);
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
