using System.Runtime.InteropServices;
using System.Text;

namespace CardDispenserAgent.Services;

public class CardReaderService : IDisposable
{
    private readonly ILogger<CardReaderService> _logger;

    private readonly object _lock = new();

    private readonly StringBuilder _cardBuffer = new();

    private Thread? _hookThread;

    private uint _hookThreadId;

    private IntPtr _hookHandle = IntPtr.Zero;

    private LowLevelKeyboardProc? _keyboardCallback;

    private TaskCompletionSource<string?>? _cardReadTcs;

    private bool _isReading;

    private bool _disposed;

    // Windows low-level keyboard hook
    private const int WH_KEYBOARD_LL = 13;

    // Keyboard messages
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Used to stop the hook thread
    private const uint WM_QUIT = 0x0012;

    // Enter key (scan complete)
    private const int VK_RETURN = 0x0D;

    public CardReaderService(
        ILogger<CardReaderService> logger)
    {
        _logger = logger;

        StartKeyboardHook();
    }

    private void StartKeyboardHook()
    {
        _hookThread = new Thread(KeyboardHookThread)
        {
            IsBackground = true,
            Name = "CardReaderKeyboardHook"
        };

        _hookThread.Start();

        _logger.LogInformation(
            "Card reader keyboard hook thread started.");
    }

    private void KeyboardHookThread()
    {
        try
        {
            _hookThreadId = GetCurrentThreadId();

            _keyboardCallback = KeyboardHookCallback;

            _hookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _keyboardCallback,
                GetModuleHandle(null),
                0);

            if (_hookHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();

                _logger.LogError(
                    "Failed to install keyboard hook. Win32 Error: {Error}",
                    error);

                return;
            }

            _logger.LogInformation(
                "Card reader keyboard hook installed successfully.");

            // Windows message loop.
            while (GetMessage(
                       out MSG msg,
                       IntPtr.Zero,
                       0,
                       0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Card reader keyboard hook thread failed.");
        }
        finally
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);

                _hookHandle = IntPtr.Zero;
            }

            _logger.LogInformation(
                "Card reader keyboard hook stopped.");
        }
    }

    private IntPtr KeyboardHookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == (IntPtr)WM_KEYDOWN ||
                wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                try
                {
                    int virtualKeyCode =
                        Marshal.ReadInt32(lParam);

                    ProcessKey(virtualKeyCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process keyboard input.");
                }
            }
        }

        return CallNextHookEx(
            _hookHandle,
            nCode,
            wParam,
            lParam);
    }

    private void ProcessKey(int virtualKeyCode)
    {
        lock (_lock)
        {
            if (!_isReading)
                return;

            if (virtualKeyCode == VK_RETURN)
            {
                if (_cardBuffer.Length == 0)
                    return;

                //get card number
                string cardNumber =
                    _cardBuffer.ToString().Trim();

                _cardBuffer.Clear();

                //stop capturing keyboard input
                _isReading = false;

                _logger.LogInformation("Card number captured: {CardNumber}",cardNumber);

                _cardReadTcs?.TrySetResult(cardNumber);

                return;
            }

            //number handling
            if (virtualKeyCode >= 0x30 &&
                virtualKeyCode <= 0x39)
            {
                char number =
                    (char)('0' +
                    (virtualKeyCode - 0x30));

                _cardBuffer.Append(number);

                _logger.LogDebug(
                    "Card reader key: {Key}",
                    number);

                return;
            }

            if (virtualKeyCode >= 0x60 &&
                virtualKeyCode <= 0x69)
            {
                char number =
                    (char)('0' +
                    (virtualKeyCode - 0x60));

                _cardBuffer.Append(number);

                _logger.LogDebug(
                    "Card reader keypad key: {Key}",
                    number);

                return;
            }
        }
    }

    public async Task<string?> ReadCardNumberAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<string?> tcs;

        lock (_lock)
        {
            // Clear previous card data.
            _cardBuffer.Clear();

            // Create a new waiting task.
            _cardReadTcs =
                new TaskCompletionSource<string?>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

            tcs = _cardReadTcs;

            // Start capturing keyboard input.
            _isReading = true;
        }

        _logger.LogInformation(
            "Card reader is ready. Waiting for card number...");

        try
        {
            using var timeoutCts =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            timeoutCts.CancelAfter(timeout);

            using var registration =
                timeoutCts.Token.Register(() =>
                {
                    lock (_lock)
                    {
                        if (!_isReading)
                            return;

                        _isReading = false;

                        _cardBuffer.Clear();
                    }

                    //_logger.LogWarning(
                    //    "Card reader timed out after {Timeout} seconds.",
                    //    timeout.TotalSeconds);

                    tcs.TrySetResult(null);
                });

            return await tcs.Task;
        }
        finally
        {
            lock (_lock)
            {
                _isReading = false;

                _cardBuffer.Clear();

                if (ReferenceEquals(
                    _cardReadTcs,
                    tcs))
                {
                    _cardReadTcs = null;
                }
            }
        }
    }

    // =========================================================
    // CHECK WHETHER READER IS CURRENTLY READING
    // =========================================================

    public bool IsReading
    {
        get
        {
            lock (_lock)
            {
                return _isReading;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            lock (_lock)
            {
                _isReading = false;

                _cardBuffer.Clear();

                _cardReadTcs?.TrySetResult(null);

                _cardReadTcs = null;
            }

            // Stop Windows message loop.
            if (_hookThreadId != 0)
            {
                PostThreadMessage(
                    _hookThreadId,
                    WM_QUIT,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }

            if (_hookThread != null &&
                _hookThread.IsAlive)
            {
                _hookThread.Join(1000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to dispose CardReaderService.");
        }
    }


    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr GetModuleHandle(
        string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out MSG lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(
        ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(
        ref MSG lpMsg);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(
        uint idThread,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam);


    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;

        public uint message;

        public UIntPtr wParam;

        public IntPtr lParam;

        public uint time;

        public POINT pt;
    }
}