using Microsoft.AspNetCore.SignalR.Client;
using CardDispenserAgent.Services;
using CardDispenserAgent.DTOs;

namespace CardDispenserAgent.Hubs
{
    public class VmsSignalRClient
    {
        private readonly HubConnection _connection;
        private readonly CardDispenserService _cardDispenserService;
        private readonly ILogger<VmsSignalRClient> _logger;

        public VmsSignalRClient(
            CardDispenserService cardDispenserService,
            ILogger<VmsSignalRClient> logger)
        {
            _cardDispenserService = cardDispenserService;
            _logger = logger;

            _connection = new HubConnectionBuilder()
                .WithUrl(
                    "https://localhost:7001/hubs/kiosk-agent")
                .WithAutomaticReconnect()
                .Build();

            _connection.On(
                "Ping",
                () =>
                {
                    _logger.LogInformation(
                        "Received Ping from VMS Service");
                });

            _connection.On<string>(
                "InitializeCard",
                async commandId =>
                {
                    await HandleInitializeCardAsync(commandId);
                });

            _connection.On<string>(
                "PushCard",
                async commandId =>
                {
                    await HandlePushCardAsync(commandId);
                });

            _connection.On<string>(
                "DispenseCard",
                async commandId =>
                {
                    await HandleDispenseCardAsync(commandId);
                });

            _connection.On<string>(
                "EjectCard",
                async commandId =>
                {
                    await HandleEjectAsync(commandId);
                });

            _connection.Reconnecting += error =>
            {
                _logger.LogWarning(
                    error,
                    "SignalR connection lost. " +
                    "Attempting to reconnect...");

                return Task.CompletedTask;
            };

            _connection.Reconnected += async connectionId =>
            {
                _logger.LogInformation(
                    "SignalR reconnected. " +
                    "Connection ID: {ConnectionId}",
                    connectionId);

                try
                {
                    await _connection.InvokeAsync(
                        "JoinKiosk",
                        "KIOSK-001");

                    _logger.LogInformation(
                        "Joined kiosk group after reconnect: KIOSK-001");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to rejoin kiosk group after reconnect.");
                }
            };

            _connection.Closed += async error =>
            {
                if (error != null)
                {
                    _logger.LogError(
                        error,
                        "SignalR connection closed.");
                }
                else
                {
                    _logger.LogWarning(
                        "SignalR connection closed.");
                }

                await Task.CompletedTask;
            };
        }

        public async Task StartAsync()
        {
            _logger.LogInformation(
                "Starting VMS SignalR connection...");

            while (true)
            {
                try
                {
                    if (_connection.State ==
                        HubConnectionState.Disconnected)
                    {
                        _logger.LogInformation(
                            "Attempting to connect to VMS SignalR...");

                        await _connection.StartAsync();

                        _logger.LogInformation(
                            "Connected to VMS SignalR. " +
                            "Connection ID: {ConnectionId}",
                            _connection.ConnectionId);

                        await _connection.InvokeAsync(
                            "JoinKiosk",
                            "KIOSK-001");

                        _logger.LogInformation(
                            "Joined kiosk group: KIOSK-001");

                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to connect to VMS SignalR. " +
                        "Retrying in 5 seconds...");

                    await Task.Delay(
                        TimeSpan.FromSeconds(5));
                }
            }
        }

        private async Task HandleInitializeCardAsync(
            string commandId)
        {
            _logger.LogInformation(
                "Received InitializeCard command: {CommandId}",
                commandId);

            var result =
                await _cardDispenserService.InitializeAsync();

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Card initialize successful. " +
                    "CommandId: {CommandId}",
                    commandId);
            }
            else
            {
                _logger.LogError(
                    "Card initialize failed. " +
                    "CommandId: {CommandId}, " +
                    "Message: {Message}",
                    commandId,
                    result.Message);
            }
        }

        private async Task HandlePushCardAsync(
            string commandId)
        {
            _logger.LogInformation(
                "Received PushCard command: {CommandId}",
                commandId);

            var result =
                await _cardDispenserService.PushCardAsync();

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Card pushed successfully. " +
                    "CommandId: {CommandId}, " +
                    "CardNumber: {CardNumber}",
                    commandId,
                    result.CardNumber);
            }
            else
            {
                _logger.LogError(
                    "Push card failed. " +
                    "CommandId: {CommandId}, " +
                    "Message: {Message}",
                    commandId,
                    result.Message);
            }

            var response = new DispenseCardResponseDto
            {
                CommandId = commandId,
                KioskId = "KIOSK-001",
                IsSuccess = result.IsSuccess,
                Message = result.Message,
                CardNumber = result.CardNumber,
                ErrorCode = result.ErrorCode
            };

            await _connection.InvokeAsync(
                "ReceivePushCardResult",
                response);
        }

        private async Task HandleDispenseCardAsync(
    string commandId)
        {
            _logger.LogInformation(
                "Received DispenseCard command: {CommandId}",
                commandId);

            var result =
                await _cardDispenserService
                    .DispenseCardAsync();

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Card dispense successful. " +
                    "CommandId: {CommandId}",
                    commandId);
            }
            else
            {
                _logger.LogError(
                    "Card dispense failed. " +
                    "CommandId: {CommandId}, " +
                    "Message: {Message}",
                    commandId,
                    result.Message);
            }

            var response = new DispenseCardResponseDto
            {
                CommandId = commandId,
                KioskId = "KIOSK-001",
                IsSuccess = result.IsSuccess,
                Message = result.Message,
                CardNumber = result.CardNumber,
                ErrorCode = result.ErrorCode
            };

            await _connection.InvokeAsync(
                "ReceiveDispenseCardResult",
                response);
        }

        private async Task HandleEjectAsync(
            string commandId)
        {
            _logger.LogInformation(
                "Received Eject command: {CommandId}",
                commandId);

            var result =
                await _cardDispenserService
                    .EjectCardAsync();

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Dispenser eject successful. " +
                    "CommandId: {CommandId}",
                    commandId);
            }
            else
            {
                _logger.LogError(
                    "Dispenser eject failed. " +
                    "CommandId: {CommandId}, " +
                    "Message: {Message}",
                    commandId,
                    result.Message);
            }

            var response = new DispenseCardResponseDto
            {
                CommandId = commandId,
                KioskId = "KIOSK-001",
                IsSuccess = result.IsSuccess,
                Message = result.Message,
                CardNumber = result.CardNumber,
                ErrorCode = result.ErrorCode
            };

            await _connection.InvokeAsync(
                "ReceiveEjectCardResult",
                response);
        }
    }
}