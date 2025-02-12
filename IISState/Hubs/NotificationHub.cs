using Microsoft.AspNetCore.SignalR;
namespace IISState.Hubs;

public class NotificationHub:Hub
{
    // 向所有连接的客户端发送消息
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}