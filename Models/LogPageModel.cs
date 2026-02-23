using System.Collections.Generic;

namespace LogWebViewer.Models
{
    public class LogPageModel
    {
        public List<LogEntry> Logs { get; set; } = new List<LogEntry>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 0;
        public string FilterLevel { get; set; } = "";
        public string FilterSource { get; set; } = "";
        public string FilterFunction { get; set; } = "";
    }

    public class LogEntry
    {
        public string Time { get; set; }
        public string Level { get; set; }
        public string Source { get; set; }
        public string Function { get; set; }
        public string Description { get; set; }
    }
}