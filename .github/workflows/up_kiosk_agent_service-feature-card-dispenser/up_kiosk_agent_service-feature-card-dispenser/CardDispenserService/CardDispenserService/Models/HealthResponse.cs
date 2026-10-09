namespace CardDispenserAgent.Models;

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;

    public bool Connected { get; set; }

    public string Port { get; set; } = string.Empty;
}