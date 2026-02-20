using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LogWebViewer.Models;
using Newtonsoft.Json;

namespace LogWebViewer.Controllers
{
    public class LogController : Controller
    {
        [DllImport("LogReaderDLL.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr ReadBinaryLog(string fileName);

        [HttpGet]
        public IActionResult Index()
        {
            return View(new List<LogEntry>());
        }

        [HttpPost]
        public IActionResult Upload(IFormFile logFile)
        {
            if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            {
                ViewBag.Error = "No file uploaded. File may be too large or blocked by server.";
                return View("Index", new List<LogEntry>());
            }

            if (logFile == null || logFile.Length == 0)
            {
                ViewBag.Error = "Please select a valid log file.";
                return View("Index", new List<LogEntry>());
            }

            if (!Path.GetExtension(logFile.FileName)
                .Equals(".log", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Only .log files are allowed.";
                return View("Index", new List<LogEntry>());
            }

            try
            {
                // Save uploaded file temporarily
                var tempPath = Path.Combine(Path.GetTempPath(), logFile.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    logFile.CopyTo(stream);
                }

                // Read DLL output (still using PtrToStringAnsi)
                IntPtr ptr = ReadBinaryLog(tempPath);
                string json = Marshal.PtrToStringAnsi(ptr) ?? "[]";

                // Escape invalid JSON characters (tabs, control chars)
                json = EscapeInvalidJson(json);

                // Stream parse JSON to avoid loading all into memory
                var logs = new List<LogEntry>();
                using (var sr = new StringReader(json))
                using (var reader = new JsonTextReader(sr))
                {
                    var serializer = new JsonSerializer();

                    if (reader.Read() && reader.TokenType == JsonToken.StartArray)
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.StartObject)
                            {
                                var log = serializer.Deserialize<LogEntry>(reader);
                                logs.Add(log);
                            }
                            else if (reader.TokenType == JsonToken.EndArray)
                            {
                                break;
                            }
                        }
                    }
                }

                return View("Index", logs);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Upload failed: {ex.Message}";
                return View("Index", new List<LogEntry>());
            }
        }

        // Escape control characters not allowed in JSON
        private string EscapeInvalidJson(string input)
        {
            return string.Concat(input.Select(c =>
                (char.IsControl(c) && c != '\r' && c != '\n')
                    ? $"\\u{((int)c):x4}"
                    : c.ToString()
            ));
        }
    }
}