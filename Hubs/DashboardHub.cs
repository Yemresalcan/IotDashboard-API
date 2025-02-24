using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace IoTDashboard.API.Hubs;

public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public async Task SendSensorData(Dictionary<string, double> sensorData)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var formattedData = new
        {
            machineId = "TEST-001",
            timestamp = timestamp,
            sensors = sensorData.ToDictionary(
                kvp => kvp.Key,
                kvp => new { value = kvp.Value, unit = GetSensorUnit(kvp.Key) }
            )
        };

        await Clients.All.SendAsync("ReceiveSensorData", formattedData);
    }

    public async Task SendAnomalyAlert(string sensorId, double value)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var alert = new
        {
            machineId = "TEST-001",
            timestamp = timestamp,
            isAnomaly = true,
            score = 0.95,
            alarmLevel = "Yüksek",
            messages = new[] { $"{sensorId} sensöründe anormal değer tespit edildi: {value}" },
            sensorData = new Dictionary<string, object>
            {
                { sensorId, new { value = value, unit = GetSensorUnit(sensorId) } }
            }
        };

        await Clients.All.SendAsync("ReceiveAnomalyAlert", alert);
    }

    private string GetSensorUnit(string sensorId)
    {
        return sensorId.ToLower() switch
        {
            var s when s.Contains("sicaklik") => "°C",
            var s when s.Contains("basinc") => "bar",
            var s when s.Contains("nem") => "%",
            var s when s.Contains("hiz") => "RPM",
            _ => "birim"
        };
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"🟢 Yeni bağlantı: {Context.ConnectionId}");
        await Clients.All.SendAsync("ReceiveMessage", $"Yeni kullanıcı bağlandı: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"🔴 Bağlantı koptu: {Context.ConnectionId}");
        await Clients.All.SendAsync("ReceiveMessage", $"Kullanıcı ayrıldı: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}