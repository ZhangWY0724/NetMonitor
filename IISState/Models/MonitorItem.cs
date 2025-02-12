namespace IISState.Models
{
    public class MonitorItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public string Type { get; set; } // 类型：IP 或 API
        public bool IsAvailable { get; set; }
        public DateTime LastChecked { get; set; }
        public double Uptime { get; set; } // 可用性百分比

        // 导航属性，表示与 HistoryEntry 的一对多关系
        public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();
    }
}