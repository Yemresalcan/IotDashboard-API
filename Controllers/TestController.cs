using Microsoft.AspNetCore.Mvc;
using IoTDashboard.API.Models;
using IoTDashboard.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IoTDashboard.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IHubContext<DashboardHub> hubContext,
        ILogger<TestController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "API is running", timestamp = DateTime.UtcNow });
    }

    [HttpPost("sensor-data")]
    public async Task<IActionResult> SendTestSensorData()
    {
        var sensorData = new SensorData
        {
            MachineId = "TEST-001",
            Timestamp = DateTime.UtcNow,
            Sensors = new Dictionary<string, SensorValue>
            {
                ["sicaklik"] = new SensorValue { Value = 75.5, Unit = "°C" },
                ["basinc"] = new SensorValue { Value = 1.1, Unit = "bar" },
                ["titresim"] = new SensorValue { Value = 2.5, Unit = "mm/s" },
                ["gurultu"] = new SensorValue { Value = 85, Unit = "dB" },
                ["motor_hizi"] = new SensorValue { Value = 2500, Unit = "RPM" },
                ["enerji_tuketimi"] = new SensorValue { Value = 350, Unit = "kW" }
            }
        };

        try 
        {
            await _hubContext.Clients.All.SendAsync("ReceiveSensorData", sensorData);
            _logger.LogInformation("Test sensör verisi gönderildi: {MachineId}", sensorData.MachineId);
            return Ok(new { success = true, data = sensorData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR mesajı gönderilirken hata oluştu");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("anomaly-alert")]
    public async Task<IActionResult> SendTestAnomalyAlert()
    {
        var alert = new AnomalyAlert
        {
            MachineId = "TEST-001",
            Timestamp = DateTime.UtcNow,
            IsAnomaly = true,
            Score = 0.75,
            AlarmLevel = "high",
            Messages = new List<string>
            {
                "Sıcaklık normal aralık dışında: 85°C",
                "Titreşim normal aralık dışında: 6.5 mm/s"
            },
            SensorData = new Dictionary<string, SensorValue>
            {
                ["sicaklik"] = new SensorValue { Value = 85, Unit = "°C" },
                ["titresim"] = new SensorValue { Value = 6.5, Unit = "mm/s" }
            }
        };

        try 
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAnomalyAlert", alert);
            _logger.LogInformation("Test anomali uyarısı gönderildi: {MachineId}", alert.MachineId);
            return Ok(new { success = true, data = alert });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR mesajı gönderilirken hata oluştu");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
} 