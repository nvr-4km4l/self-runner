using System.Formats.Tar;
using System.Reflection.PortableExecutable;
using CardDispenserAgent.Models;
using CardDispenserAgent.Protocol;
using CardDispenserAgent.Serial;

namespace CardDispenserAgent.Services;

public class CardDispenserService
{
    private readonly SerialPortManager _serial;
    private readonly ILogger<CardDispenserService> _logger;
    private readonly CardReaderService _cardReader;

    private TaskCompletionSource<string>? _pendingResponse;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);


    public CardDispenserService(
        SerialPortManager serial,
        CardReaderService reader,
        ILogger<CardDispenserService> logger)
    {
        _serial = serial;
        _cardReader = reader;
        _logger = logger;

        _serial.DataReceived += OnDataReceived;
    }


    public HealthResponse GetHealth()
    {
        return new HealthResponse
        {
            Status = "Healthy",
            Connected = _serial.IsConnected,
            Port = _serial.CurrentPortName
        };
    }


    private async Task<CommandResult> SendAndAwaitResponseAsync( string command, string payload, string successVerb)
    {
        await _sendLock.WaitAsync();

        try
        {
            var tcs = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _pendingResponse = tcs;

            var bytes = CommandBuilder.Build(command, payload);

            _serial.Send(bytes);

            using var cts = new CancellationTokenSource(ResponseTimeout);

            cts.Token.Register(() =>
                tcs.TrySetException(
                    new TimeoutException()));

            string raw = await tcs.Task;

            return BuildResult(raw, successVerb);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);

            return CommandResult.Fail(ex.Message);
        }
        finally
        {
            _pendingResponse = null;
            _sendLock.Release();
        }
    }

    private CommandResult BuildResult(string raw, string successVerb)
    {
        string message = ResponseTranslator.Translate(raw);

        if (raw.StartsWith("N") && raw.Length >= 5)
        {
            string errorCode = raw.Substring(3, 2);
            var fail = CommandResult.Fail(message, errorCode, raw);
            fail.RawResponse = raw;
            return fail;
        }

        if (raw.StartsWith("P"))
        {
            var ok = CommandResult.Success($"{successVerb}. {message}", null);
            ok.RawResponse = raw;
            return ok;
        }

        var other = CommandResult.Fail(message, null, raw);
        other.RawResponse = raw;
        return other;
    }

    public async Task<CommandResult> InitializeAsync()
    {
        return await SendAndAwaitResponseAsync("C00", "32400000000", "Dispenser initialized");
    }


    public async Task<CommandResult> PushCardAsync()
    {
        _logger.LogInformation("Starting card reader...");

        // Start listening before pushing the card.
        var cardNumberTask = _cardReader.ReadCardNumberAsync(
            TimeSpan.FromSeconds(10));

        _logger.LogInformation("Card reader listening. Sending C221...");

        // Push card into the reader.
        var result = await SendAndAwaitResponseAsync(
            "C221",
            "",
            "Card pushed to reader");

        // Dispenser failed.
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "C221 failed. Message: {Message}",
                result.Message);

            return result;
        }

        _logger.LogInformation(
            "C221 successful. Waiting for card number...");

        try
        {
            // Wait for CardReaderService to capture the card number.
            var cardNumber = await cardNumberTask;

            _logger.LogInformation(
                "CardReaderService returned card number: {CardNumber}",
                cardNumber);

            if (string.IsNullOrWhiteSpace(cardNumber))
            {
                return CommandResult.Fail(
                    "Card was pushed successfully, but card number was not captured.",
                    null,
                    result.RawResponse);
            }

            // Put card number into the existing result.
            result.CardNumber = cardNumber;

            result.Message =
                "Card pushed and card number read successfully.";

            return result;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Card reader timed out while waiting for card number.");

            return CommandResult.Fail(
                "Card was pushed successfully, but the card reader timed out.",
                "CARD_READER_TIMEOUT",
                result.RawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to read card number.");

            return CommandResult.Fail(
                "Card was pushed successfully, but failed to read card number.",
                "CARD_READER_ERROR",
                result.RawResponse);
        }
    }
    public async Task<CommandResult> DispenseCardAsync()
    {
        return await SendAndAwaitResponseAsync("Cl@", "", "Card dispensing"); // lowercase L
    }

    public async Task<CommandResult> EjectCardAsync()
    {
        return await SendAndAwaitResponseAsync("C31", "", "Card captured to reject-stacker");
    }

    public async Task<CommandResult> ResetAsync()
    {
        return await SendAndAwaitResponseAsync("C00", "32400000000", "Dispenser reset");
    }

    public async Task<DeviceStatus> GetStatusAsync()
    {
        // "C10" = command C + cm '1' (Status Request) + pm '0'. No extra payload.
        var result = await SendAndAwaitResponseAsync("C10", "", "Status Request");
        var raw = result.RawResponse;

        return new DeviceStatus
        {
            Success = result.IsSuccess,
            Connected = _serial.IsConnected,
            Port = _serial.CurrentPortName,
            DeviceStatusText = result.Message ?? string.Empty,
            HopperStatus = ParseHopper(raw),
            CardPosition = ParseCardPosition(raw),
            ShutterStatus = ParseShutter(raw),
            RawResponse = raw,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string ParseCardPosition(string? raw)
    {
        if (raw is null || raw.Length < 5 || raw[0] != 'P') return "Unknown";

        return raw.Substring(3, 2) switch
        {
            "00" => "NoCard",
            "01" => "CardAtGate",
            "02" => "CardInsideICRW",
            _ => "Unknown"
        };
    }

    private static string ParseHopper(string? raw)
    {
        if (raw is null || raw.Length < 6 || raw[0] != 'P') return "Unknown";

        int st2 = (byte)raw[5];
        bool hasCards = (st2 & 0x10) != 0;
        bool nearEnd = (st2 & 0x20) != 0;

        if (!hasCards) return "Empty";
        return nearEnd ? "LowCards" : "HasCards";
    }

    private static string ParseShutter(string? raw)
    {
        if (raw is null || raw.Length < 7 || raw[0] != 'P') return "Unknown";
        return ((byte)raw[6] & 0x08) != 0 ? "Open" : "Closed";
    }

    public async Task<CommandResult> GetHopperStatusAsync()
    {
        return await SendAndAwaitResponseAsync("C11", "", "Hopper status retrieved");
    }


    private void OnDataReceived(object? sender, byte[] data)
    {
        try
        {
            if (CommandParser.IsAck(data))
            {
                _logger.LogInformation("Dispenser ACK received");
                return;
            }

            var raw = CommandParser.Parse(data);
            var message = ResponseTranslator.Translate(raw);

            _logger.LogInformation("Dispenser response: {Message} (raw: {Raw})", message, raw);

            _pendingResponse?.TrySetResult(raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse dispenser response failed");
            _pendingResponse?.TrySetException(ex);
        }
    }
}