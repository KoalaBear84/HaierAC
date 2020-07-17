using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Unclassified.Net;

namespace HaierAC
{
    class Program
    {
        static byte[] LastState = new byte[127];
        static readonly object LogLock = new object();

        static HaierResponse LastResponse { get; set; }

        static void Log(string message)
        {
            lock (LogLock)
            {
                string logLine = $"{DateTime.Now} | {message}";
                Console.WriteLine(logLine);

                using (StreamWriter streamWriter = File.AppendText("Haier.log"))
                {
                    streamWriter.WriteLine(logLine);
                }
            }
        }

        static async Task Main(string[] args)
        {
            Console.Title = "Haier Airco!";
            Console.WriteLine("Started!");

            HaierConfiguration haierConfiguration = JsonSerializer.Deserialize<HaierConfiguration>(File.ReadAllText("Haier.json"));

            if (string.IsNullOrWhiteSpace(haierConfiguration.IpAddress))
            {
                Console.WriteLine("No settings found in Haier.json, scanning network for Haier Airco...");

                List<dynamic> results = NetworkScanner.GetAircoIPAddresses();

                Console.WriteLine("Please fill in one of the found airco's in Haier.json");
                Console.WriteLine("Press a key to exit");
                Console.ReadKey(intercept: true);
                return;
            }

            AsyncTcpClient asyncTcpClient = new AsyncTcpClient
            {
                IPAddress = IPAddress.Parse(haierConfiguration.IpAddress),
                Port = haierConfiguration.Port,
                ConnectTimeout = TimeSpan.FromSeconds(60),
                AutoReconnect = true,
                ClosedCallback = async (client, closedRemotely) =>
                {
                    Log($"ClosedCallback, closedRemotely: {closedRemotely}");
                    //Console.Beep(3000, 250);
                    //Console.Beep(1000, 250);
                },
                ConnectedCallback = async (client, isReconnected) =>
                {
                    Log("Haier AC Connected!");
                    //Console.Beep(1000, 250);
                    //Console.Beep(3000, 250);
                },
                ReceivedCallback = async (client, count) =>
                {
                    // Custom connection logic
                    Log($"ReceivedCallback ({count} bytes)");
                    byte[] receivedBytes = client.ByteBuffer.Dequeue(count);

                    //File.WriteAllBytes(@"E:\Haier.dat", receivedBytes);
                    //Console.WriteLine(BitConverter.ToString(receivedBytes));
                    //Console.WriteLine(Encoding.ASCII.GetString(receivedBytes));
                    //Console.WriteLine(receivedBytes);

                    if (IsState(receivedBytes))
                    {
                        if (BitConverter.ToString(receivedBytes) != BitConverter.ToString(LastState))
                        {
                            Log(Encoding.ASCII.GetString(receivedBytes));
                            Console.WriteLine(BitConverter.ToString(receivedBytes));

                            HaierResponse haierResponse = Parser.BytesToStruct<HaierResponse>(receivedBytes);
                            Log($"_Temperature: {haierResponse.Temperature} [{haierResponse._Temperature}]");
                            Log($"_RoomTemperature: {haierResponse.RoomTemperature} [{haierResponse._RoomTemperature}] {ConvertToBinary(haierResponse._RoomTemperature)}");
                            Log($"FlowDown: {haierResponse.FlowDownOrHighPowerMode}");
                            Log($"Quiet: {haierResponse.Quiet}");
                            Log($"_FlowUpDown: {haierResponse.FlowUpDown} [{haierResponse._FlowUpDown}] [{ConvertToBinary(haierResponse._FlowUpDown)}]");
                            Log($"_FlowLeftRight: {haierResponse.FlowLeftRight} [{haierResponse._FlowLeftRight}] [{ConvertToBinary(haierResponse._FlowLeftRight)}]");
                            //Log($"MacAddress: {new string(haierResponse.MacAddress)}");
                            Log($"PoweredOn: {haierResponse.PoweredOn}");
                            Log($"Purify: {haierResponse.Purify}");
                            Log($"FanSpeed: {Enum.GetName(typeof(FanSpeed), haierResponse.FanSpeed)} [{ConvertToBinary(haierResponse._FanSpeedAndOperationMode)}]");
                            Log($"OperationMode: {Enum.GetName(typeof(OperationMode), haierResponse.OperationMode)}");

                            Console.Title = $"{(haierResponse.PoweredOn ? "✔" : "❌")} {haierResponse.RoomTemperature}° 🡆 {haierResponse.Temperature}° @ {Enum.GetName(typeof(FanSpeed), haierResponse.FanSpeed)} | Haier Airco! {DateTime.Now:HH:mm}";

                            if (LastResponse.MacAddress != null &&
                               (LastResponse.RoomTemperature != haierResponse.RoomTemperature || LastResponse.Temperature != haierResponse.Temperature || LastResponse.PoweredOn != haierResponse.PoweredOn))
                            {
                                FlashWindow.Flash(count: 10);
                            }

                            if (!haierResponse.PoweredOn)
                            {
                                // This all doesn't work
                                //Console.WriteLine("Turning AC on!");
                                //Parser.SetBit(ref haierResponse._PoweredOn, 0, true);

                                // Set as request
                                //haierResponse.Reserved0_39[2] = 39;
                                //haierResponse.Reserved0_39[2] = 20;

                                //byte[] responseBytes = Parser.StructToBytes(haierResponse);
                                //await SendCommandAsync(client, responseBytes);

                                //byte[] bytesToSend = Parser.HexStringToBytes(
                                //    Commands.Request +
                                //    Commands.Zero16 +
                                //    Commands.Zero16 +
                                //    $"{new string(haierResponse.MacAddress):X2}" +
                                //    Commands.Zero4 +
                                //    Commands.Zero16 +
                                //    // Sequence 1
                                //    Parser.OrderByte(1) +
                                //    Parser.HexStringLength(Commands.On) +
                                //    Commands.On
                                //);

                                //await client.Send(bytesToSend);
                                //await SendCommandAsync(client, bytesToSend);
                            }

                            ShowChangedBytes(LastState, receivedBytes);

                            LastResponse = haierResponse;
                            LastState = receivedBytes;
                        }
                    }
                }
            };
            asyncTcpClient.Message += AsyncTcpClient_Message;
            asyncTcpClient.MaxConnectTimeout = TimeSpan.FromSeconds(100);
            await asyncTcpClient.RunAsync();

            Log("Finished!");
            Console.ReadKey(intercept: true);
        }

        private static async Task SendCommandAsync(AsyncTcpClient asyncTcpClient, byte[] command)
        {
            byte crc = GetCrc(command);
            await asyncTcpClient.Send(Combine(command, new byte[] { crc }));
        }

        private static byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;

            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }

            return rv;
        }

        static byte GetCrc(byte[] req)
        {
            byte crc = 0;

            for (int i = 2; i < req.Length; i++)
            {
                crc += req[i];
            }

            return crc;
        }

        private static void ShowChangedBytes(byte[] previousState, byte[] newState)
        {
            for (int i = 0; i < previousState.Length; i++)
            {
                byte previousByte = previousState[i];
                byte newByte = newState[i];

                if (previousByte != newByte)
                {
                    if ((i < 40 || i > 51) && i != 124 && i != 125 && i != 126)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Log($"Byte {i} changed from {previousByte} [{ConvertToBinary(previousByte)}] -> {newByte} [{ConvertToBinary(newByte)}]");
                        Console.ResetColor();
                    }
                }
            }
        }

        private static bool IsState(byte[] receivedBytes)
        {
            return receivedBytes.Length >= 79 && receivedBytes[79] == 0x2F;
        }

        private static string ConvertToBinary(int value) => $"{int.Parse(Convert.ToString(value, 2)):00000000}";

        private static void AsyncTcpClient_Message(object sender, AsyncTcpEventArgs e)
        {
            Log($"AsyncTcpClient_Message | {e.Message} | {e.Exception}");
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HaierResponse
    {
        // 0/1
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Unused0_1;

        // 2/3
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] MessageHeader;

        // 4/39
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
        public byte[] Unused4_39;

        // 40/51
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public char[] MacAddress;

        // 52
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 27)]
        public byte[] Unused52_78;

        // 79
        // 0_00000000 --> 47_00101111
        public byte Unknown79;

        // 80 always 255 [11111111]
        public byte Unknown80;

        // 81 always 255 [11111111]
        public byte Unknown81;

        // 82 always 42 [00101010]
        public byte Unknown82;

        // 83 always 64 [01000000]
        public byte Unknown83;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Unused84_87;

        // 88
        public byte Unused88;

        // 89 always 6 [00000110]
        public byte Unknown89;

        // 90 always 109 [01101101]
        public byte Unknown90;

        // 91 always 1 [00000001]
        public byte Unknown91;

        // 92
        public byte _Temperature;
        public int Temperature => _Temperature + 16;

        // 02_00000010 == ???
        // 03_00000011 == ???
        // 12_00001100 == Flow Up Down
        // 93
        public byte _FlowUpDown;
        public bool FlowUpDown => Parser.HasMask(_FlowUpDown, Constants.FlowUpDown);

        // 94
        public byte _FanSpeedAndOperationMode;

        // Looks like they use the last 4 (or first 4?) from this byte (94)
        // Low to Auto:  Byte 94 changed from 35 [xxx00011] -> 37 [xxx00101]
        // High to Auto: Byte 94 changed from 33 [xxx00001] -> 37 [xxx00101]
        // Auto to Low:  Byte 94 changed from 37 [xxx00101] -> 35 [xxx00011]
        // Low to Mid:   Byte 94 changed from 35 [xxx00011] -> 34 [xxx00010]
        // Mid to High:  Byte 94 changed from 34 [xxx00010] -> 33 [xxx00001]
        public FanSpeed FanSpeed
        {
            get
            {
                FanSpeed fanSpeed = FanSpeed.Unknown;

                // If anybody know a better way..
                if (Parser.IsBitSet(_FanSpeedAndOperationMode, 0) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 1) && Parser.IsBitSet(_FanSpeedAndOperationMode, 2))
                {
                    fanSpeed = FanSpeed.Auto;
                }
                else if (Parser.IsBitSet(_FanSpeedAndOperationMode, 0) && Parser.IsBitSet(_FanSpeedAndOperationMode, 1) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 2))
                {
                    fanSpeed = FanSpeed.Low;
                }
                else if (!Parser.IsBitSet(_FanSpeedAndOperationMode, 0) && Parser.IsBitSet(_FanSpeedAndOperationMode, 1) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 2))
                {
                    fanSpeed = FanSpeed.Medium;
                }
                else if (Parser.IsBitSet(_FanSpeedAndOperationMode, 0) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 1) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 2))
                {
                    fanSpeed = FanSpeed.High;
                }

                return fanSpeed;
            }
        }

        // Looks like they use the first 3 (or last 3?) from this byte (94)
        // Cool.  Byte 94 changed from 5   [000xxxxx] -> 37  [001xxxxx]
        // Heat.  Byte 94 changed from 34  [001xxxxx] -> 130 [100xxxxx]
        // Dry.   Byte 94 changed from 130 [100xxxxx] -> 66  [010xxxxx]
        // Fan.   Byte 94 changed from 66  [010xxxxx] -> 194 [110xxxxx]
        // Smart. Byte 94 changed from 194 [110xxxxx] -> 5   [000xxxxx]
        public OperationMode OperationMode
        {
            get
            {
                OperationMode operationMode = OperationMode.Unknown;

                // If anybody know a better way..
                if (!Parser.IsBitSet(_FanSpeedAndOperationMode, 7) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 6) && Parser.IsBitSet(_FanSpeedAndOperationMode, 5))
                {
                    operationMode = OperationMode.Cool;
                }
                else if (Parser.IsBitSet(_FanSpeedAndOperationMode, 7) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 6) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 5))
                {
                    operationMode = OperationMode.Heat;
                }
                else if (!Parser.IsBitSet(_FanSpeedAndOperationMode, 7) && Parser.IsBitSet(_FanSpeedAndOperationMode, 6) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 5))
                {
                    operationMode = OperationMode.Dry;
                }
                else if (Parser.IsBitSet(_FanSpeedAndOperationMode, 7) && Parser.IsBitSet(_FanSpeedAndOperationMode, 6) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 5))
                {
                    operationMode = OperationMode.Fan;
                }
                else if (!Parser.IsBitSet(_FanSpeedAndOperationMode, 7) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 6) && !Parser.IsBitSet(_FanSpeedAndOperationMode, 5))
                {
                    operationMode = OperationMode.Smart;
                }

                return operationMode;
            }
        }

        // 95/96
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Unused95_96;

        // 97
        public byte _PoweredOn;
        public bool PoweredOn => Parser.IsBitSet(_PoweredOn, 0);
        public bool Purify => Parser.IsBitSet(_PoweredOn, 1);
        public bool FlowDownOrHighPowerMode => Parser.IsBitSet(_PoweredOn, 4);
        public bool Quiet => Parser.IsBitSet(_PoweredOn, 5);

        // 98
        public byte Unused98;

        // 99
        public byte _FlowLeftRight;
        public bool FlowLeftRight => Parser.HasMask(_FlowLeftRight, Constants.FlowLeftRight);

        // 100/101
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Unused100_101;

        // 102
        public byte _RoomTemperature;
        public int RoomTemperature => _RoomTemperature >> 1;

        // 103
        public byte Unused103;

        // 104
        // 0_00000000 --> 79_01001111
        // 0_00000000 --> 80_01010000
        // 0_00000000 --> 81_01010001
        // 0_00000000 --> 82_01010010
        // 0_00000000 --> 83_01010011
        // 0_00000000 --> 84_01010100
        // 0_00000000 --> 87_01010111
        // 79_01001111 --> 87_01010111
        public byte Unknown_104;

        // 105/106
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Unused105_106;

        // 107
        // 3_00000011 --> 1_00000001
        // 0_00000000 --> 3_00000011
        // 1_00000001 --> 3_00000011
        public byte Unknown_107;

        // 108/123
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public byte[] Unused108_123;

        // 124/126
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] ChecksumBytes;
    }

    // These are the old commands, probably not going to work for the new firmware
    public class Commands
    {
        public const string Request = "00 00 27 14 00 00 00 00 ";
        public const string Response = "00 00 27 15 00 00 00 00 ";
        public const string Zero4 = "00 00 00 00 ";
        public const string Zero16 = Zero4 + Zero4 + Zero4 + Zero4;
        public const string Hello = "ff ff 0a 00 00 00 00 00 00 01 4d 01 59 ";
        public const string On = "ff ff 0a 00 00 00 00 00 00 01 4d 02 5a ";
        public const string Off = "ff ff 0a 00 00 00 00 00 00 01 4d 03 5b ";
        public const string Init = "ff ff 08 00 00 00 00 00 00 73 7b ";
    }

    public class Constants
    {
        public const int FlowLeftRight = 7;
        public const int FlowUpDown = 12;
        public const int FanSpeedLow = 0b001;
        public const int FanSpeedMedium = 0b010;
        public const int FanSpeedHigh = 0b011;
    }

    [Flags]
    public enum FanSpeed
    {
        Unknown,
        Auto,
        Low,
        Medium,
        High
    }

    [Flags]
    public enum OperationMode
    {
        Unknown,
        Cool,
        Heat,
        Dry,
        Fan,
        Smart
    }
}
