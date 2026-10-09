namespace CardDispenserAgent.DTOs;

public class DispenseCardResponseDto
{
    public string CommandId { get; set; } = string.Empty;

    public string KioskId { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? CardNumber { get; set; }

    public string? ErrorCode { get; set; }
}