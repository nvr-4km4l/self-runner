using System.IO.Ports;
using Microsoft.Extensions.Logging;
using CardDispenserAgent.Configuration;

namespace CardDispenserAgent.Serial;

public class SerialPortManager : IDisposable
{
    private readonly ILogger<SerialPortManager> _logger;
    private readonly DispenserOptions _options;

    private SerialPort? _serialPort;

    private readonly object _lock = new();

    private readonly List<byte> _receiveBuffer = new();

    private CancellationTokenSource? _connectionCts;
    private Task? _connectionTask;

    private const byte STX = 0xF2;
    private const byte ACK = 0x06;

    private static readonly TimeSpan ReconnectInterval =
        TimeSpan.FromSeconds(5);

    public event EventHandler<byte[]>? DataReceived;

    public bool IsOpen => _serialPort?.IsOpen ?? false;

    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                return _serialPort != null &&
                       _serialPort.IsOpen;
            }
        }
    }

    public string CurrentPortName
    {
        get
        {
            lock (_lock)
            {
                return _serialPort?.PortName ?? _options.ComPort;
            }
        }
    }

    public SerialPortManager(
        ILogger<SerialPortManager> logger,
        DispenserOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// Start the automatic serial connection/reconnection loop.
    public void StartConnectionLoop()
    {
        if (_connectionTask != null)
        {
            return;
        }

        _connectionCts = new CancellationTokenSource();

        _connectionTask = Task.Run(
            () => ConnectionLoopAsync(_connectionCts.Token));

        _logger.LogInformation(
            "Serial connection monitoring started for {Port}",
            _options.ComPort);
    }

    /// Continuously tries to keep the dispenser connected.
    private async Task ConnectionLoopAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!IsConnected)
                {
                    try
                    {
                        Open();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Dispenser is not available on {Port}. " +
                            "Retrying in {Seconds} seconds...",
                            _options.ComPort,
                            ReconnectInterval.TotalSeconds);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error in serial connection loop");
            }

            try
            {
                await Task.Delay(
                    ReconnectInterval,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Serial connection monitoring stopped");
    }

    /// <>
    /// Open RS232 connection to Nidec Sankyo dispenser.
    public void Open()
    {
        lock (_lock)
        {
            if (_serialPort?.IsOpen == true)
            {
                return;
            }

            // Make sure an old/broken SerialPort is disposed.
            DisposeSerialPort();

            var serialPort = new SerialPort
            {
                PortName = _options.ComPort,

                // Nidec SCT3Q8-3A1230
                BaudRate = _options.BaudRate,
                DataBits = 8,
                Parity = Parity.Even,
                StopBits = StopBits.One,

                Handshake = Handshake.None,

                ReadTimeout = 5000,
                WriteTimeout = 5000,

                Encoding = System.Text.Encoding.ASCII
            };

            serialPort.DataReceived += OnDataReceived;

            try
            {
                serialPort.Open();

                _serialPort = serialPort;

                _logger.LogInformation(
                    "Dispenser serial port opened: {Port}",
                    _options.ComPort);
            }
            catch
            {
                serialPort.DataReceived -= OnDataReceived;
                serialPort.Dispose();

                throw;
            }
        }
    }

    /// Close RS232 connection.
    public void Close()
    {
        lock (_lock)
        {
            DisposeSerialPort();

            _logger.LogInformation(
                "Dispenser serial port closed");
        }
    }

    /// Send data to dispenser.
    public void Send(byte[] data)
    {
        lock (_lock)
        {
            if (_serialPort == null ||
                !_serialPort.IsOpen)
            {
                throw new InvalidOperationException(
                    "Serial port is not open");
            }

            try
            {
                _serialPort.Write(
                    data,
                    0,
                    data.Length);

                _logger.LogInformation(
                    "TX: {Bytes}",
                    BitConverter.ToString(data));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed sending data to dispenser");

                HandleConnectionFailure();

                throw;
            }
        }
    }

    private void OnDataReceived(
        object sender,
        SerialDataReceivedEventArgs e)
    {
        try
        {
            lock (_lock)
            {
                if (_serialPort == null ||
                    !_serialPort.IsOpen)
                {
                    return;
                }

                int bytesAvailable =
                    _serialPort.BytesToRead;

                if (bytesAvailable <= 0)
                {
                    return;
                }

                byte[] buffer =
                    new byte[bytesAvailable];

                _serialPort.Read(
                    buffer,
                    0,
                    bytesAvailable);

                _logger.LogInformation(
                    "RX raw: {Bytes}",
                    BitConverter.ToString(buffer));

                _receiveBuffer.AddRange(buffer);

                ProcessReceiveBuffer();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error receiving dispenser data");

            HandleConnectionFailure();
        }
    }

    /// Handle a physical/device connection failure.
    /// The connection loop will recreate the SerialPort.
    private void HandleConnectionFailure()
    {
        lock (_lock)
        {
            DisposeSerialPort();

            _logger.LogWarning(
                "Dispenser connection lost. " +
                "Automatic reconnection will be attempted.");
        }
    }

    /// Dispose only the current SerialPort.
    /// Must be called with _lock held.
    private void DisposeSerialPort()
    {
        if (_serialPort == null)
        {
            _receiveBuffer.Clear();
            return;
        }

        try
        {
            _serialPort.DataReceived -= OnDataReceived;

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error disposing serial port");
        }
        finally
        {
            _serialPort = null;
            _receiveBuffer.Clear();
        }
    }

    /// Drains _receiveBuffer, stripping ACKs and emitting each
    /// complete frame found. Must be called with _lock held.
    private void ProcessReceiveBuffer()
    {
        while (true)
        {
            while (_receiveBuffer.Count > 0 &&
                   _receiveBuffer[0] == ACK)
            {
                _logger.LogInformation(
                    "RX: 06 -> Acknowledge (ACK)");

                _receiveBuffer.RemoveAt(0);

                DataReceived?.Invoke(
                    this,
                    new byte[] { ACK });
            }

            if (_receiveBuffer.Count == 0)
            {
                return;
            }

            // Minimum size:
            // STX + LENH + LENL + 1 byte TEXT + CRC
            if (_receiveBuffer.Count < 5)
            {
                return;
            }

            byte stx = _receiveBuffer[0];

            if (stx != STX)
            {
                _logger.LogWarning(
                    "Invalid STX. Discarding buffer.");

                _receiveBuffer.Clear();

                return;
            }

            int length =
                (_receiveBuffer[1] << 8) |
                _receiveBuffer[2];

            int expectedLength =
                1 + 2 + length + 2;

            if (_receiveBuffer.Count < expectedLength)
            {
                // Wait for the rest of the frame.
                return;
            }

            byte[] fullMessage =
                _receiveBuffer
                    .Take(expectedLength)
                    .ToArray();

            _receiveBuffer.RemoveRange(
                0,
                expectedLength);

            _logger.LogInformation(
                "RX: {Bytes}",
                BitConverter.ToString(fullMessage));

            DataReceived?.Invoke(
                this,
                fullMessage);
        }
    }

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public async Task StopConnectionLoopAsync()
    {
        if (_connectionCts == null)
        {
            Close();
            return;
        }

        _connectionCts.Cancel();

        if (_connectionTask != null)
        {
            try
            {
                await _connectionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _connectionTask = null;

        _connectionCts.Dispose();
        _connectionCts = null;

        Close();
    }

    public void Dispose()
    {
        _connectionCts?.Cancel();

        lock (_lock)
        {
            DisposeSerialPort();
        }

        _connectionCts?.Dispose();

        _connectionCts = null;
        _connectionTask = null;
    }
}