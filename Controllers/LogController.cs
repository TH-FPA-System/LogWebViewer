using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LogWebViewer.Models;
using Newtonsoft.Json;

namespace LogWebViewer.Controllers
{
    public class LogController : Controller
    {
        [DllImport("LogReaderDLL.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr ReadBinaryLog(string fileName);

        private const int PageSize = 150;
        private string JsonPath => Path.Combine(Path.GetTempPath(), "logstream.json");

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
                return BadRequest(new { message = "No file selected." });

            if (!Path.GetExtension(logFile.FileName).Equals(".log", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Only .log files are allowed." });

            try
            {
                var tempLogPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".log");
                await using (var fs = new FileStream(tempLogPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                    await logFile.CopyToAsync(fs);

                IntPtr ptr = ReadBinaryLog(tempLogPath);
                string json = Marshal.PtrToStringAnsi(ptr) ?? "[]";
                json = EscapeInvalidJson(json);
                System.IO.File.WriteAllText(JsonPath, json);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Upload failed: {ex.Message}" });
            }
        }
        [HttpGet]
        public IActionResult Page(
     int page = 1,
     string level = "",
     string description = "",
     TimeSpan? fromTime = null,
     TimeSpan? toTime = null)
        {
            if (!System.IO.File.Exists(JsonPath))
                return View("Index", new LogPageModel { Logs = new List<LogEntry>() });

            var logs = new List<LogEntry>();
            int totalLogs = 0;

            using var sr = new StreamReader(JsonPath);
            using var reader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();

            if (reader.Read() && reader.TokenType == JsonToken.StartArray)
            {
                int index = 0;
                int skip = (page - 1) * PageSize;

                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.StartObject) continue;

                    var log = serializer.Deserialize<LogEntry>(reader);

                    // Level filter
                    if (!string.IsNullOrEmpty(level) &&
                        !string.Equals(log.Level, level, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Description filter
                    if (!string.IsNullOrEmpty(description) &&
                        !log.Description.Contains(description, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Time-of-day filter
                    if (DateTime.TryParse(log.Time, out var logDateTime))
                    {
                        var logTime = logDateTime.TimeOfDay;

                        if (fromTime.HasValue && logTime < fromTime.Value)
                            continue;

                        if (toTime.HasValue && logTime > toTime.Value)
                            continue;
                    }

                    totalLogs++;
                    if (index >= skip && logs.Count < PageSize)
                        logs.Add(log);
                    index++;
                }
            }

            var model = new LogPageModel
            {
                Logs = logs,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalLogs / (double)PageSize),
                FilterLevel = level,
                FilterDescription = description,
                FromTime = fromTime.HasValue ? DateTime.Today + fromTime.Value : (DateTime?)null,
                ToTime = toTime.HasValue ? DateTime.Today + toTime.Value : (DateTime?)null
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_LogTablePartial", model);

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