namespace IoTDashboard.API.Models;

public class AnomalyAlert
{
    public string MachineId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsAnomaly { get; set; }
    public double Score { get; set; }
    public string AlarmLevel { get; set; } = string.Empty;
    public List<string> Messages { get; set; } = new();
    public Dictionary<string, SensorValue> SensorData { get; set; } = new();
} 