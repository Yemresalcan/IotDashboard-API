namespace IoTDashboard.API.Models;

public class SensorData
{
    public string MachineId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, SensorValue> Sensors { get; set; } = new();
}

public class SensorValue
{
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
} 