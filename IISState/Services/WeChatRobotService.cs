using IISState.Models;

namespace IISState.Services;

public class WeChatRobotService
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public WeChatRobotService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _webhookUrl = configuration["WeChatRobot:WebhookUrl"];
    }
    
    public async Task SendMessageAsync(string message)
    {
        var payload = new
        {
            msgtype = "markdown", // 使用 markdown 格式
            markdown = new
            {
                content = message // markdown 内容
            }
        };

        var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
        response.EnsureSuccessStatusCode();
    }
}