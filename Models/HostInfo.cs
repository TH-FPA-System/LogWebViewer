namespace LogWebViewer.Models
{
    public class HostInfo
    {
        public string Host { get; set; }
        public string IpAddress { get; set; }
        public string Group { get; set; }
        public string Afuns { get; set; } // raw string like "30500|30501|30502"
        public string Etc { get; set; }
    }
}