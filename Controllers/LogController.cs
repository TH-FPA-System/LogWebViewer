using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using LogWebViewer.Models;
namespace LogWebViewer.Controllers
{
    public class LogController : Controller
    {
        [DllImport("LogReaderDLL.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr ReadBinaryLog(string fileName);

        public IActionResult Index()
        {
            string path = @"C:\output\18Feb26.log"; // your log path

            IntPtr ptr = ReadBinaryLog(path);
            string json = Marshal.PtrToStringAnsi(ptr) ?? "[]";

            // Deserialize JSON into list of log objects
            var logs = System.Text.Json.JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();

            return View(logs);
        }

   
    }
}