using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LogWebViewer.Models;
using Newtonsoft.Json;

namespace LogWebViewer.Controllers
{
    public class LogController : Controller
    {
        [DllImport("LogReaderDLL.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr ReadBinaryLog(string fileName);

        private const int PageSize = 50;

        [HttpGet]
        public IActionResult Index()
        {
            return View(new LogPageModel());
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Upload(IFormFile logFile)
        {
            if (logFile == null || logFile.Length == 0)
            {
                ViewBag.Error = "Please select a valid log file.";
                return View("Index", new LogPageModel());
            }

            if (!Path.GetExtension(logFile.FileName)
                .Equals(".log", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Only .log files are allowed.";
                return View("Index", new LogPageModel());
            }

            try
            {
                var tempLogPath = Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + ".log");

                await using (var fs = new FileStream(tempLogPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    useAsync: true))
                {
                    await logFile.CopyToAsync(fs);
                }

                // Heavy work AFTER upload
                await Task.Run(() =>
                {
                    IntPtr ptr = ReadBinaryLog(tempLogPath);
                    string json = Marshal.PtrToStringAnsi(ptr) ?? "[]";
                    json = EscapeInvalidJson(json);

                    var jsonPath = Path.Combine(Path.GetTempPath(), "logstream.json");
                    System.IO.File.WriteAllText(jsonPath, json);
                });

                return RedirectToAction(nameof(Page), new { page = 1 });
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Upload failed: {ex.Message}";
                return View("Index", new LogPageModel());
            }
        }

        [HttpGet]
        public IActionResult Table()
        {
            var jsonPath = Path.Combine(Path.GetTempPath(), "logstream.json");
            var logs = new List<LogEntry>();

            if (System.IO.File.Exists(jsonPath))
            {
                using var sr = new StreamReader(jsonPath);
                using var reader = new JsonTextReader(sr);
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
                            break;
                    }
                }
            }

            var model = new LogPageModel
            {
                Logs = logs,
                CurrentPage = 1,
                TotalPages = 1
            };

            return PartialView("_LogTablePartial", model);
        }

        [HttpGet]
        public IActionResult Page(int page = 1,
                                  string level = "",
                                  string source = "",
                                  string function = "")
        {
            var jsonPath = Path.Combine(Path.GetTempPath(), "logstream.json");
            if (!System.IO.File.Exists(jsonPath))
            {
                ViewBag.Error = "No uploaded log file found. Please upload first.";
                return View("Index", new LogPageModel());
            }

            var logs = new List<LogEntry>();
            int totalLogs = 0;

            using var sr = new StreamReader(jsonPath);
            using var reader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();

            if (reader.Read() && reader.TokenType == JsonToken.StartArray)
            {
                int skip = (page - 1) * PageSize;
                int index = 0;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        var log = serializer.Deserialize<LogEntry>(reader);

                        if (!string.IsNullOrEmpty(level) &&
                            !string.Equals(log.Level, level, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrEmpty(source) &&
                            !log.Source.Contains(source, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrEmpty(function) &&
                            !log.Function.Contains(function, StringComparison.OrdinalIgnoreCase))
                            continue;

                        totalLogs++;
                        if (index >= skip && logs.Count < PageSize)
                            logs.Add(log);
                        index++;
                    }
                    else if (reader.TokenType == JsonToken.EndArray)
                        break;
                }
            }

            var model = new LogPageModel
            {
                Logs = logs,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalLogs / (double)PageSize),
                FilterLevel = level,
                FilterSource = source,
                FilterFunction = function
            };

            return View("Index", model);
        }

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