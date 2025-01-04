using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.IO;

namespace test_rat_server
{
    class Logger
    {
        private static string logFile = "logs/listener.log";

        public static void Log(string message, string type = "*")
        {
            try
            {
                Directory.CreateDirectory("logs");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {message}";
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch { }
        }
    }

    class Listener
    {
        public Dictionary<int, _listener> listeners = new Dictionary<int, _listener>();
        private Func<Socket, Task> ConnectCallBack;

        public Listener(Func<Socket, Task> _ConnectCallBack)
        {
            ConnectCallBack = _ConnectCallBack;
        }

        public bool PortInUse(int port)
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();
            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    Logger.Log($"Port {port} already in use", "-");
                    return true;
                }
            }
            return false;
        }

        public void CreateListener(int port)
        {
            if (PortInUse(port))
            {
                MessageBox.Show("That port is currently in use!");
                Logger.Log($"Failed to create listener on port {port} - Port in use", "-");
            }
            else
            {
                if (!listeners.ContainsKey(port))
                {
                    listeners[port] = new _listener(port);
                }
                try
                {
                    listeners[port].StartListening(ConnectCallBack);
                }
                catch (Exception ex)
                {
                    listeners[port].StopListening();
                    MessageBox.Show("There was an error using this port!");
                    Logger.Log($"Error starting listener on port {port}: {ex.Message}", "-");
                }
            }
        }

        public void StopListener(int port)
        {
            listeners[port].StopListening();
        }
    }

    class _listener
    {
        private Socket listener;
        private int port;
        public bool listening = false;

        public _listener(int _port)
        {
            port = _port;
        }

        public async Task StartListening(Func<Socket, Task> connectCallBack)
        {
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);
            listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(localEndPoint);
            listener.Listen(100);
            listening = true;

            while (true)
            {
                try
                {
                    Socket handler = await listener.AcceptAsync();
                    Logger.Log($"New connection from {handler.RemoteEndPoint}", "+");
                    connectCallBack(handler);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void StopListening()
        {
            listening = false;
            try
            {
                listener.Shutdown(SocketShutdown.Both);
            }
            catch { }
            try
            {
                listener.Close();
            }
            catch { }
            try
            {
                listener.Dispose();
            }
            catch { }
        }
    }
}
