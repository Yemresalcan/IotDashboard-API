using IoTDashboard.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IoTDashboard.API.Services;

public class TestDataGenerator : BackgroundService
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly Random _random = new Random();
    private readonly Dictionary<string, (double min, double max, double current)> _sensorRanges;

    public TestDataGenerator(IHubContext<DashboardHub> hubContext)
    {
        _hubContext = hubContext;
        _sensorRanges = new Dictionary<string, (double min, double max, double current)>
        {
            { "Sicaklik1", (60, 85, 72) },
            { "Sicaklik2", (55, 80, 68) },
            { "Basinc1", (1.8, 3.2, 2.5) },
            { "Basinc2", (1.5, 3.0, 2.2) },
            { "Nem1", (35, 65, 50) },
            { "MotorHizi", (1000, 1500, 1200) }
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateAndSendData();
                await CheckForAnomalies();
                await Task.Delay(5000, stoppingToken); // 5 saniyede bir veri gönder
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Veri üretme hatası: {ex.Message}");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task GenerateAndSendData()
    {
        var sensorData = new Dictionary<string, double>();

        foreach (var sensor in _sensorRanges)
        {
            // Mevcut değer etrafında küçük değişiklikler yap
            var change = _random.NextDouble() * 0.1 - 0.05; // -0.05 ile +0.05 arası
            var newValue = _sensorRanges[sensor.Key].current * (1 + change);

            // Değeri sınırlar içinde tut
            newValue = Math.Max(sensor.Value.min, Math.Min(sensor.Value.max, newValue));
            
            // Yeni değeri güncelle ve kaydet
            _sensorRanges[sensor.Key] = (sensor.Value.min, sensor.Value.max, newValue);
            sensorData[sensor.Key] = Math.Round(newValue, 2);
        }

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

        await _hubContext.Clients.All.SendAsync("ReceiveSensorData", formattedData);
    }

    private async Task CheckForAnomalies()
    {
        foreach (var sensor in _sensorRanges)
        {
            var currentValue = sensor.Value.current;
            var range = sensor.Value.max - sensor.Value.min;
            var threshold = range * 0.8; // Maksimum değerin %80'i

            if (currentValue > sensor.Value.min + threshold)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var alert = new
                {
                    machineId = "TEST-001",
                    timestamp = timestamp,
                    isAnomaly = true,
                    score = Math.Round((currentValue - (sensor.Value.min + threshold)) / (range * 0.2), 2),
                    alarmLevel = "Yüksek",
                    messages = new[] { $"{sensor.Key} sensöründe yüksek değer: {Math.Round(currentValue, 2)}" },
                    sensorData = new Dictionary<string, object>
                    {
                        { sensor.Key, new { value = currentValue, unit = GetSensorUnit(sensor.Key) } }
                    }
                };

                await _hubContext.Clients.All.SendAsync("ReceiveAnomalyAlert", alert);
            }
        }
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
} 