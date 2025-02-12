using System.Text.Json.Serialization;

namespace IISState.Models;

public class HistoryEntry
{
   
    public int Id { get; set; } // 主键
    public bool IsSuccess { get; set; } // 请求是否成功
    public DateTime Timestamp { get; set; } // 请求时间
    public string ErrorMessage { get; set; } // 错误信息（如果请求失败）
    public int MonitorItemId { get; set; }
    public bool Delfalg { get; set; } //删除标识
    
    [JsonIgnore]
    public MonitorItem MonitorItem { get; set; } // 导航属性
}