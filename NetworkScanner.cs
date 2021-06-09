using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaierAC
{
    public static class NetworkScanner
    {
        private static string GetIPAddress()
        {
            string localIP = string.Empty;
            IPHostEntry ipHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ipAddress in ipHostEntry.AddressList)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ipAddress.ToString();
                }
            }

            return localIP;
        }

        public static async Task<List<dynamic>> GetAircoIPAddressesAsync()
        {
            IPHostEntry ipHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            List<dynamic> results = new List<dynamic>();

            foreach (IPAddress ipAddress in ipHostEntry.AddressList)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    string localIP = ipAddress.ToString();

                    string ipBase = localIP.Substring(0, localIP.LastIndexOf('.') + 1);

                    Console.WriteLine($"Scanning network: {localIP}");

                    int ipsScanned = 0;

                    int maxThreads = 100;
                    ConcurrentQueue<int> ips = new ConcurrentQueue<int>(Enumerable.Range(1, 255));
                    List<Task> tasks = new List<Task>();

                    for (int n = 0; n < maxThreads; n++)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            while (ips.TryDequeue(out int i))
                            {
                                string ip = $"{ipBase}{i}";

                                using (Ping ping = new Ping())
                                {
                                    PingReply pingReply = ping.Send(ip);

                                    if (pingReply.Status == IPStatus.Success)
                                    {
                                        string macAddress = GetMacAddress(ip);

                                        if (!string.IsNullOrWhiteSpace(macAddress))
                                        {
                                            if (IsPortOpen(ip, 56800, TimeSpan.FromSeconds(10)))
                                            {
                                                results.Add(new
                                                {
                                                    IP = ip,
                                                    MacAddress = macAddress
                                                });

                                                Console.WriteLine($"Found Airco: {ip}, {macAddress.Replace("-", ":").ToUpper()}");
                                            }
                                        }
                                    }
                                }

                                int ipsScannedNew = Interlocked.Increment(ref ipsScanned);
                                Console.Title = $"Haier Airco! Scanning network {ipsScannedNew}/{255} @ {ipsScannedNew / 255m * 100:F0}%";
                            }
                        }));
                    }
                    
                    await Task.WhenAll(tasks);
                }
            }

            return results;
        }

        private static bool IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    IAsyncResult result = tcpClient.BeginConnect(host, port, null, null);

                    bool success = result.AsyncWaitHandle.WaitOne(timeout);
                    tcpClient.EndConnect(result);

                    return success;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetMacAddress(string ipAddress)
        {
            using Process process = Process.Start(new ProcessStartInfo("arp", $"-a {ipAddress}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            string strOutput = process.StandardOutput.ReadToEnd();
            string[] substrings = strOutput.Split('-');

            if (substrings.Length >= 8)
            {
                return $"{substrings[3].Substring(Math.Max(0, substrings[3].Length - 2))}-{substrings[4]}-{substrings[5]}-{substrings[6]}-{substrings[7]}-{substrings[8].Substring(0, 2)}";
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
