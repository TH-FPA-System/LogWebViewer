using System;
using System.Runtime.InteropServices;

namespace LogWebApi
{
    public static class LogReader
    {
        [DllImport("LogReaderDLL.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ReadBinaryLog(string fileName);

        public static string ReadLog(string filePath)
        {
            IntPtr ptr = ReadBinaryLog(filePath);
            string result = Marshal.PtrToStringAnsi(ptr) ?? "{}";
            return result;
        }
    }
}