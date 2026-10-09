namespace CardDispenserAgent.Models
{
    public class DeviceIdentity
    {
        public string DeviceId { get; set; } = string.Empty;

        public string DeviceName { get; set; } = string.Empty;

        public string MacAddress { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;
    }
}