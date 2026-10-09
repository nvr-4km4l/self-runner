namespace CardDispenserAgent.Models;

public class DeviceStatus
{
    public bool Success { get; set; }

    public bool Connected { get; set; }

    public string Port { get; set; } = string.Empty;

    public string DeviceStatusText { get; set; } = "Unknown";

    public string HopperStatus { get; set; } = "Unknown";

    public string ShutterStatus { get; set; } = "Unknown";

    public string CardPosition { get; set; } = "Unknown";

    public string? RawResponse { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}