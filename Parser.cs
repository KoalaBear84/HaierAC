using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HaierAC
{
    [AttributeUsage(AttributeTargets.Field)]
    public class EndianAttribute : Attribute
    {
        public Endianness Endianness { get; private set; }

        public EndianAttribute(Endianness endianness)
        {
            Endianness = endianness;
        }
    }

    // I have no idea what to do with this Endianess :| (And if it works, or is needed)
    public enum Endianness
    {
        BigEndian,
        LittleEndian
    }

    public class Parser
    {
        public static void RespectEndianness(Type type, byte[] data)
        {
            var fields = type.GetFields().Where(f => f.IsDefined(typeof(EndianAttribute), false))
                .Select(f => new
                {
                    Field = f,
                    Attribute = (EndianAttribute)f.GetCustomAttributes(typeof(EndianAttribute), false)[0],
                    Offset = Marshal.OffsetOf(type, f.Name).ToInt32()
                }).ToList();

            foreach (var field in fields)
            {
                if ((field.Attribute.Endianness == Endianness.BigEndian && BitConverter.IsLittleEndian) ||
                    (field.Attribute.Endianness == Endianness.LittleEndian && !BitConverter.IsLittleEndian))
                {
                    Array.Reverse(data, field.Offset, Marshal.SizeOf(field.Field.FieldType));
                }
            }
        }

        public static T BytesToStruct<T>(byte[] rawData) where T : struct
        {
            T result = default;

            RespectEndianness(typeof(T), rawData);

            GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

            try
            {
                IntPtr rawDataPtr = handle.AddrOfPinnedObject();
                result = (T)Marshal.PtrToStructure(rawDataPtr, typeof(T));
            }
            finally
            {
                handle.Free();
            }

            return result;
        }

        public static byte[] StructToBytes<T>(T data) where T : struct
        {
            byte[] rawData = new byte[Marshal.SizeOf(data)];
            GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

            try
            {
                IntPtr rawDataPtr = handle.AddrOfPinnedObject();
                Marshal.StructureToPtr(data, rawDataPtr, false);
            }
            finally
            {
                handle.Free();
            }

            RespectEndianness(typeof(T), rawData);

            return rawData;
        }

        public static bool HasMask(byte input, int mask)
        {
            return (input & mask) == mask;
        }

        public static bool IsBitSet(byte b, byte nPos)
        {
            return new BitArray(new[] { b })[nPos];
        }

        public static void SetBit(ref byte b, byte nPos, bool onOff)
        {
            BitArray bitArray = new BitArray(new[] { b });

            bitArray[nPos] = onOff;

            byte[] bytes = new byte[1];
            bitArray.CopyTo(bytes, 0);

            b = bytes[0];
        }

        public static byte[] HexStringToBytes(string hexString)
        {
            hexString = StringWhitespace(hexString);

            // Yes, performance wise not good, but it works
            return Enumerable.Range(0, hexString.Length / 2).Select(x => Convert.ToByte(hexString.Substring(x * 2, 2), 16)).ToArray();
        }

        private static string StringWhitespace(string hexString) => Regex.Replace(hexString, @"\s+", string.Empty);
        public static string OrderByte(int n) => n % 256 < 16 ? $"00 00 00 0{n % 256:X2}" : $"00 00 00 {n % 256:X2}";
        public static string HexStringLength(string cmd) => OrderByte(StringWhitespace(Regex.Replace(cmd, "/[^0-f]/", string.Empty)).Length / 2);
    }
}
