namespace LogWebViewer.Models
{
    // Paging model
    public class LogPageModel
    {
        public List<LogEntry> Logs { get; set; } = new List<LogEntry>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        // Filters
        public string FilterLevel { get; set; } = "";
        public string FilterSource { get; set; } = "";
        public string FilterFunction { get; set; } = "";
    }


}
