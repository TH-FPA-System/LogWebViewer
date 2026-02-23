using Microsoft.AspNetCore.Mvc;
using LogWebViewer.Models;
using System.Collections.Generic;
using System.IO;

namespace LogWebViewer.Controllers
{
    public class HostController : Controller
    {
        private readonly string HostFilePath = @"E:\Temp\@Woot\VNC\winterm-vnc.txt"; // your txt file path

        [HttpGet]
        public IActionResult Index()
        {
            var hosts = new List<HostInfo>();

            if (System.IO.File.Exists(HostFilePath))
            {
                var lines = System.IO.File.ReadAllLines(HostFilePath);

                // Skip header
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');

                    hosts.Add(new HostInfo
                    {
                        Host = parts.Length > 0 ? parts[0] : "",
                        IpAddress = parts.Length > 1 ? parts[1] : "",
                        Group = parts.Length > 2 ? parts[2] : "",
                        Afuns = parts.Length > 3 ? parts[3] : "",
                        Etc = parts.Length > 4 ? parts[4] : ""
                    });
                }
            }

            return View(hosts);
        }
    }
}