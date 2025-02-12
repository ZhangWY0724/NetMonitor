using System.Net.NetworkInformation;
using IISState.Hubs;
using IISState.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IISState.Services
{
    public class MonitorService
    {
        private readonly MonitorContext _dbContext;
        private readonly IMemoryCache _memoryCache;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly WeChatRobotService _weChatRobotService;

        public MonitorService(
            MonitorContext dbContext,
            IHubContext<NotificationHub> hubContext,
            IMemoryCache memoryCache,
            WeChatRobotService weChatRobotService)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _memoryCache = memoryCache;
            _weChatRobotService = weChatRobotService;
        }

        public async Task DoWork()
        {
            var items = await _dbContext.MonitorItem
                .Include(m => m.History.Where(n => n.Delfalg == false))
                .ToListAsync();

            if (items.Count == 0)
            {
                return;
            }

            foreach (var item in items)
            {
                bool isAvailable = false;
                string errorMessage = "";

                try
                {
                    var (isSuccess, message) = await CheckAvailabilityAsync(item.Type, item.Target);
                    isAvailable = isSuccess;
                    errorMessage = message;
                }
                catch (Exception ex)
                {
                    isAvailable = false;
                    errorMessage = ex.Message;
                }

                // 更新历史记录
                UpdateHistory(item, isAvailable, errorMessage);

                // 清理历史记录
                CleanupHistory(item);

                // 更新监控项状态
                UpdateMonitorItem(item, isAvailable);

                // 处理失败或成功逻辑
                if (!isAvailable)
                {
                    await HandleFailureAsync(item, errorMessage);
                }
                else
                {
                    await HandleSuccessAsync(item);
                }
                
                await _dbContext.SaveChangesAsync();
                
            }
            //只返回item中前90条数据
            var itemsToReturn = items.Select(item => new MonitorItem
            {
                Id = item.Id,
                Name = item.Name,
                Type = item.Type,
                Target = item.Target,
                IsAvailable = item.IsAvailable,
                Uptime = item.Uptime,
                LastChecked = item.LastChecked,
                History = item.History.OrderByDescending(h => h.Timestamp).Take(90).ToList()
            }).ToList();
            // 推送更新到客户端
            await _hubContext.Clients.All.SendAsync("ReceiveMonitorUpdate", items);
        }

        public async Task Del48HoursData()
        {
            // 删除 48 小时前的数据
            var threshold = DateTime.Now.AddHours(-48);
            var items =  _dbContext.HistoryEntry.Where(h => h.Timestamp < threshold).ToList();
            _dbContext.HistoryEntry.RemoveRange(items);
            await _dbContext.SaveChangesAsync();
        }

        private async Task<(bool isSuccess, string message)> CheckAvailabilityAsync(string type, string target)
        {
            return type switch
            {
                "IP" => PingTarget(target),
                "API" => await CheckApiAvailabilityAsync(target),
                "SQL" => await CheckDatabaseAvailabilityAsync(target),
                _ => throw new NotSupportedException($"Unsupported type: {type}")
            };
        }

        private void UpdateHistory(MonitorItem item, bool isAvailable, string errorMessage)
        {
            item.History.Add(new HistoryEntry
            {
                IsSuccess = isAvailable,
                Timestamp = DateTime.Now,
                ErrorMessage = errorMessage,
                Delfalg = false
            });
        }

        private void CleanupHistory(MonitorItem item)
        {
            if (item.History.Count > 90)
            {
                var deleteList = item.History
                    .OrderBy(h => h.Timestamp)
                    .Take(item.History.Count - 90)
                    .ToList();

                // 标记删除
                foreach (var deleteItem in deleteList)
                {
                    deleteItem.Delfalg = true;
                }
            }
        }

        private void UpdateMonitorItem(MonitorItem item, bool isAvailable)
        {
            item.IsAvailable = isAvailable;
            item.Uptime = CalculateUptime(item.History);
            item.LastChecked = DateTime.Now;
        }

        private async Task HandleFailureAsync(MonitorItem item, string errorMessage)
        {
            string cacheKey = $"{item.Type}_{item.Target}";
            var failureTimestamps = _memoryCache.Get<List<DateTime>>(cacheKey) ?? new List<DateTime>();

            // 添加当前失败时间戳
            failureTimestamps.Add(DateTime.Now);

            // 清理超过 6 小时的失败记录
            failureTimestamps = failureTimestamps
                .Where(t => t > DateTime.Now.AddHours(-6))
                .ToList();

            // 更新缓存
            _memoryCache.Set(cacheKey, failureTimestamps, TimeSpan.FromHours(6));

            // 判断是否满足推送条件
            if (failureTimestamps.Count >= 3 && !_memoryCache.TryGetValue($"{cacheKey}_pushed", out _))
            {
                string alertMessage =
                    $@"### 监控告警
- **目标类型**: {item.Type}
- **目标名称**: {item.Name}
- **错误信息**: '{errorMessage}'
- **时间**: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`";
                await _weChatRobotService.SendMessageAsync(alertMessage);

                // 标记为已推送
                _memoryCache.Set($"{cacheKey}_pushed", true, TimeSpan.FromHours(6));
            }
        }

        private async Task HandleSuccessAsync(MonitorItem item)
        {
            string cacheKey = $"{item.Type}_{item.Target}";
            _memoryCache.Remove(cacheKey); // 清除失败记录
            _memoryCache.Remove($"{cacheKey}_pushed"); // 清除推送标记
        }

        private async Task<(bool isSuccess, string message)> CheckDatabaseAvailabilityAsync(string target)
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
                optionsBuilder.UseSqlServer(target);
                using (var dbContext = new DbContext(optionsBuilder.Options))
                {
                    var result = await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;");
                    return (true, "");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private (bool IsSuccess, string ErrorMessage) PingTarget(string target)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(target);
                    if (reply.Status == IPStatus.Success)
                    {
                        return (true, "");
                    }
                    else
                    {
                        return (false, reply.Status.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool IsSuccess, string ErrorMessage)> CheckApiAvailabilityAsync(string target)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // 设置超时时间（例如 5 秒）
                    httpClient.Timeout = TimeSpan.FromSeconds(5);

                    // 发送空的 POST 请求
                    var response = await httpClient.PostAsync(target, null);

                    // 只要请求成功（无论状态码是什么），都认为接口存活
                    return (true, "");
                }
            }
            catch (HttpRequestException ex)
            {
                // 请求失败（例如无法连接）
                return (false, ex.Message);
            }
            catch (TaskCanceledException)
            {
                // 请求超时
                return (false, "请求超时");
            }
            catch (Exception ex)
            {
                // 其他异常
                return (false, ex.Message);
            }
        }

        private double CalculateUptime(List<HistoryEntry> history)
        {
            if (history.Count == 0) return 0;
            int successCount = history.Count(result => result.IsSuccess);
            return Math.Round((double)successCount / history.Count * 100, 2);
        }
    }
}