using System;
using System.Text;

namespace HaierAC
{
    public static class HexadecimalEncoding
    {
        public static string ToHexString(this string str)
        {
            StringBuilder stringBuilder = new StringBuilder();
            byte[] bytes = Encoding.Unicode.GetBytes(str);

            foreach (byte t in bytes)
            {
                stringBuilder.Append(t.ToString("X2"));
            }

            // returns: "48656C6C6F20776F726C64" for "Hello world"
            return stringBuilder.ToString();
        }

        public static string FromHexString(this string hexString)
        {
            byte[] bytes = new byte[hexString.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            // returns: "Hello world" for "48656C6C6F20776F726C64"
            return Encoding.Unicode.GetString(bytes);
        }
    }
}
