namespace CardDispenserAgent.Dto.Response
{
    public class CommissionResponseDto
    {
        public string DeviceId { get; set; }

        public string DeviceName { get; set; } = string.Empty;

        public string MacAddress { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;
    }
}
