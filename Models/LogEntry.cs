namespace LogWebViewer.Models
{
    public class LogEntry
    {
        public string Time { get; set; } = "";
        public string Level { get; set; } = "";
        public string Source { get; set; } = "";
        public string Function { get; set; } = "";
        public string Description { get; set; } = "";
    }
}