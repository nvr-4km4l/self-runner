namespace CardDispenserAgent.Models;

public class CommandResult
{
    public string? CommandId { get; set; }

    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? CardNumber { get; set; }

    public string? RawResponse { get; set; }

    public string? ErrorCode { get; set; }

    public static CommandResult Success(
        string message,
        string? cardNumber = null,
        string? rawResponse = null)
    {
        return new CommandResult
        {
            IsSuccess = true,
            Message = message,
            CardNumber = cardNumber,
            RawResponse = rawResponse
        };
    }

    public static CommandResult Fail(
        string message,
        string? errorCode = null,
        string? rawResponse = null)
    {
        return new CommandResult
        {
            IsSuccess = false,
            Message = message,
            ErrorCode = errorCode,
            RawResponse = rawResponse
        };
    }
}