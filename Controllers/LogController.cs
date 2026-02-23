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

        private const int PageSize = 150;
        private string JsonPath => Path.Combine(Path.GetTempPath(), "logstream.json");

        // --------------------------
        // Index page
        // --------------------------
        [HttpGet]
        public IActionResult Index(string area = null, string date = null)
        {
            var model = new LogPageModel();
            ViewBag.LogNotFound = false;

            if (!string.IsNullOrEmpty(area) && !string.IsNullOrEmpty(date))
            {
                area = area.Trim('\'', '"');
                var logFolder = Path.Combine(@"E:\shr\lislog", area);

                if (Directory.Exists(logFolder))
                {
                    // Find log file by date: match *date*.log
                    var files = Directory.GetFiles(logFolder, $"*{date}*.log");
                    if (files.Length > 0)
                    {
                        var logFilePath = files[0];
                        try
                        {
                            IntPtr ptr = ReadBinaryLog(logFilePath);
                            string json = Marshal.PtrToStringAnsi(ptr) ?? "[]";
                            System.IO.File.WriteAllText(JsonPath, json);

                            model = GetLogsPage(1, "", "", null, null);
                        }
                        catch
                        {
                            ViewBag.LogNotFound = true;
                        }
                    }
                    else
                    {
                        ViewBag.LogNotFound = true;
                    }
                }
                else
                {
                    ViewBag.LogNotFound = true;
                }
            }

            return View(model);
        }

        // --------------------------
        // Upload log file
        // --------------------------
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
                System.IO.File.WriteAllText(JsonPath, json);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Upload failed: {ex.Message}" });
            }
        }

        // --------------------------
        // AJAX paging
        // --------------------------
        [HttpGet]
        public IActionResult Page(int page = 1, string level = "", string description = "", TimeSpan? fromTime = null, TimeSpan? toTime = null)
        {
            var model = GetLogsPage(page, level, description, fromTime, toTime);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_LogTablePartial", model);

            return View("Index", model);
        }

        // --------------------------
        // Export to TXT
        // --------------------------
        [HttpGet]
        public IActionResult ExportTxt(string area, string date)
        {
            if (string.IsNullOrEmpty(area) || string.IsNullOrEmpty(date))
                return BadRequest("Area or date missing");

            area = area.Trim('\'', '"');
            var logFolder = Path.Combine(@"E:\shr\lislog", area);
            if (!Directory.Exists(logFolder))
                return NotFound("Area folder not found");

            var files = Directory.GetFiles(logFolder, $"*{date}*.log");
            if (files.Length == 0)
                return NotFound("Log file not found");

            var logFilePath = files[0];
            var fileName = $"{area}_{date}.txt";

            try
            {
                // Call DLL to read binary log
                IntPtr ptr = ReadBinaryLog(logFilePath);
                string json = Marshal.PtrToStringAnsi(ptr) ?? "[]";

                // Optionally, parse JSON to create readable text
                var logs = JsonConvert.DeserializeObject<List<LogEntry>>(json) ?? new List<LogEntry>();
                var txtLines = logs.Select(l => $"{l.Time}\t{l.Level}\t{l.Description}");
                var finalText = string.Join(Environment.NewLine, txtLines);

                var bytes = System.Text.Encoding.UTF8.GetBytes(finalText);
                return File(bytes, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Export failed: {ex.Message}");
            }
        }
        // --------------------------
        // Helper: paginate logs
        // --------------------------
        private LogPageModel GetLogsPage(int page, string level, string description, TimeSpan? fromTime, TimeSpan? toTime)
        {
            var logs = new List<LogEntry>();
            int totalLogs = 0;

            if (!System.IO.File.Exists(JsonPath))
                return new LogPageModel { Logs = logs };

            try
            {
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

                        LogEntry log = null;
                        try { log = serializer.Deserialize<LogEntry>(reader); } catch { continue; }
                        if (log == null) continue;

                        if (!string.IsNullOrEmpty(level) && !string.Equals(log.Level, level, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrEmpty(description) && !log.Description.Contains(description, StringComparison.OrdinalIgnoreCase)) continue;

                        if (DateTime.TryParse(log.Time, out var logDateTime))
                        {
                            var logTime = logDateTime.TimeOfDay;
                            if (fromTime.HasValue && logTime < fromTime.Value) continue;
                            if (toTime.HasValue && logTime > toTime.Value) continue;
                        }

                        totalLogs++;
                        if (index >= skip && logs.Count < PageSize) logs.Add(log);
                        index++;
                    }
                }
            }
            catch
            {
                logs = new List<LogEntry>();
                totalLogs = 0;
            }

            return new LogPageModel
            {
                Logs = logs,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalLogs / (double)PageSize),
                FilterLevel = level,
                FilterDescription = description,
                FromTime = fromTime.HasValue ? DateTime.Today + fromTime.Value : (DateTime?)null,
                ToTime = toTime.HasValue ? DateTime.Today + toTime.Value : (DateTime?)null
            };
        }
    }
}