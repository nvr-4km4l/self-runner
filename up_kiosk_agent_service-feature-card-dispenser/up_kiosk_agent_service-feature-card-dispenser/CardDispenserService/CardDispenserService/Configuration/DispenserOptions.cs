namespace CardDispenserAgent.Configuration;


/// Configuration settings for the Nidec Sankyo Card Dispenser.
public sealed class DispenserOptions
{
    public const string SectionName = "Dispenser";

    public string ComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;

    public string Parity { get; set; } = "Even";

    public string StopBits { get; set; } = "One";

    public string Handshake { get; set; } = "None";

    public int ReadTimeoutMs { get; set; } = 5000;

    public int WriteTimeoutMs { get; set; } = 5000;

    public bool AutoDetectPort { get; set; } = false;

    public List<string> CandidatePortsForAutoDetect { get; set; } = new();

    public int ReconnectIntervalMs { get; set; } = 3000;

    /// Maximum reconnect delay.
    public int MaxReconnectIntervalMs { get; set; } = 30000;

    /// payload (hex string) sent with the C00 command.
    public string InitPayloadHex { get; set; } = "3240000000";

    /// Returns the configured parity enum.
    public System.IO.Ports.Parity GetParity()
    {
        return Enum.TryParse<System.IO.Ports.Parity>(Parity, true, out var value)
            ? value
            : System.IO.Ports.Parity.Even;
    }

    /// Returns the configured stop bits enum.
    public System.IO.Ports.StopBits GetStopBits()
    {
        return Enum.TryParse<System.IO.Ports.StopBits>(StopBits, true, out var value)
            ? value
            : System.IO.Ports.StopBits.One;
    }

    /// Returns the configured handshake enum.
    public System.IO.Ports.Handshake GetHandshake()
    {
        return Enum.TryParse<System.IO.Ports.Handshake>(Handshake, true, out var value)
            ? value
            : System.IO.Ports.Handshake.None;
    }
}