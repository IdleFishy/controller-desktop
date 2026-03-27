using ControllerDesktop.Interop;
using ControllerDesktop.Models;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace ControllerDesktop.Services;

public sealed class ConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _configPath;

    public ConfigurationService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ControllerDesktop");
        Directory.CreateDirectory(root);
        _configPath = Path.Combine(root, "settings.json");
    }

    public string ConfigPath => _configPath;

    public JsonSerializerOptions SerializerOptionsInstance => SerializerOptions;

    public async Task<AppConfiguration> LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            var defaultConfiguration = new AppConfiguration();
            await SaveAsync(defaultConfiguration);
            return defaultConfiguration;
        }

        await using var stream = File.OpenRead(_configPath);
        var loadedConfiguration = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, SerializerOptions);
        return loadedConfiguration ?? new AppConfiguration();
    }

    public async Task SaveAsync(AppConfiguration configuration)
    {
        await using var stream = File.Create(_configPath);
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions);
    }
}

public sealed class AutostartService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ControllerDesktop";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return;
            }

            key.SetValue(ValueName, $"\"{exePath}\" --background");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}

public sealed class TrayService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _runtimeMenuItem;
    private readonly ToolStripMenuItem _autostartMenuItem;
    private readonly Icon _trayIcon;
    private readonly bool _ownsTrayIcon;

    public event Action? OpenEditorRequested;
    public event Action? ExitRequested;
    public event Action<bool>? RuntimeToggled;
    public event Action<bool>? AutostartToggled;

    public TrayService()
    {
        _runtimeMenuItem = new ToolStripMenuItem("暂停映射");
        _runtimeMenuItem.Click += (_, _) => RuntimeToggled?.Invoke(!_runtimeMenuItem.Checked);

        _autostartMenuItem = new ToolStripMenuItem("开机启动") { CheckOnClick = true };
        _autostartMenuItem.Click += (_, _) => AutostartToggled?.Invoke(_autostartMenuItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开配置网页", null, (_, _) => OpenEditorRequested?.Invoke());
        menu.Items.Add(_runtimeMenuItem);
        menu.Items.Add(_autostartMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());

        _trayIcon = LoadTrayIcon(out _ownsTrayIcon);

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = _trayIcon,
            Text = "手柄桌面映射",
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenEditorRequested?.Invoke();
    }

    private static Icon LoadTrayIcon(out bool ownsIcon)
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.png"));
            if (resource?.Stream is not null)
            {
                using var bitmap = new Bitmap(resource.Stream);
                var iconHandle = bitmap.GetHicon();
                try
                {
                    using var icon = Icon.FromHandle(iconHandle);
                    ownsIcon = true;
                    return (Icon)icon.Clone();
                }
                finally
                {
                    DestroyIcon(iconHandle);
                }
            }
        }
        catch
        {
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(processPath);
                if (icon is not null)
                {
                    ownsIcon = true;
                    return icon;
                }
            }
            catch
            {
            }
        }

        ownsIcon = false;
        return SystemIcons.Application;
    }

    public void Update(RuntimeStatus status, bool runtimeEnabled, bool autostartEnabled, string editorUrl)
    {
        _runtimeMenuItem.Checked = runtimeEnabled;
        _runtimeMenuItem.Text = runtimeEnabled ? "暂停映射" : "启用映射";
        _autostartMenuItem.Checked = autostartEnabled;

        var stateText = !runtimeEnabled
            ? "已手动暂停"
            : status.ActivationState switch
            {
                ActivationState.ActiveDesktop => "桌面映射生效中",
                ActivationState.SuspendedByGame => "全屏前台已暂停",
                ActivationState.SuspendedByLockScreen => "系统锁屏中",
                ActivationState.SuspendedByNoController => "等待手柄连接",
                _ => "状态未知"
            };

        _notifyIcon.Text = $"手柄桌面映射 - {stateText}";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _runtimeMenuItem.Dispose();
        _autostartMenuItem.Dispose();
        if (_ownsTrayIcon)
        {
            _trayIcon.Dispose();
        }
    }
}

public sealed class ForegroundContext
{
    public ActivationState ActivationState { get; init; }
    public string ProcessName { get; init; } = "desktop";
}

public sealed class GameContextDetector : IDisposable
{
    private readonly HashSet<string> _allowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "chrome", "msedge", "firefox", "devenv", "code", "applicationframehost",
        "shellexperiencehost", "powerpnt", "obs64", "teams", "notepad", "notepad++",
        "windowsterminal", "wezterm-gui"
    };

    private NativeMethods.WinEventDelegate? _hookDelegate;
    private IntPtr _hookHandle;
    private bool _sessionLocked;

    public event Action<ForegroundContext>? ContextChanged;
    public ForegroundContext CurrentContext { get; private set; } = new() { ActivationState = ActivationState.ActiveDesktop };

    public void Start()
    {
        _hookDelegate = OnForegroundChanged;
        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _hookDelegate,
            0,
            0,
            NativeMethods.WineventOutofcontext);

        SystemEvents.SessionSwitch += OnSessionSwitch;
        Publish(EvaluateCurrentContext());
    }

    public void Stop()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    public ForegroundContext EvaluateCurrentContext()
    {
        if (_sessionLocked)
        {
            return new ForegroundContext { ActivationState = ActivationState.SuspendedByLockScreen, ProcessName = "LockApp" };
        }

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return new ForegroundContext { ActivationState = ActivationState.ActiveDesktop, ProcessName = "desktop" };
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        var processName = TryGetProcessName(processId);
        if (string.Equals(processName, "LockApp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(processName, "LogonUI", StringComparison.OrdinalIgnoreCase))
        {
            return new ForegroundContext { ActivationState = ActivationState.SuspendedByLockScreen, ProcessName = processName };
        }

        return new ForegroundContext
        {
            ActivationState = IsLikelyFullscreenGame(hwnd, processName) ? ActivationState.SuspendedByGame : ActivationState.ActiveDesktop,
            ProcessName = processName
        };
    }

    private static string TryGetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    private bool IsLikelyFullscreenGame(IntPtr hwnd, string processName)
    {
        if (_allowList.Contains(processName))
        {
            return false;
        }

        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd))
        {
            return false;
        }

        if (NativeMethods.GetWindow(hwnd, NativeMethods.GwOwner) != IntPtr.Zero)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var windowRect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(hwnd, 2);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        const int tolerance = 6;
        var coversMonitor = Math.Abs(windowRect.Left - info.rcMonitor.Left) <= tolerance &&
                            Math.Abs(windowRect.Top - info.rcMonitor.Top) <= tolerance &&
                            Math.Abs(windowRect.Right - info.rcMonitor.Right) <= tolerance &&
                            Math.Abs(windowRect.Bottom - info.rcMonitor.Bottom) <= tolerance;

        if (!coversMonitor)
        {
            return false;
        }

        var style = (ulong)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt64();
        var borderless = (style & (NativeMethods.WsCaption | NativeMethods.WsThickframe)) == 0;
        var className = GetClassName(hwnd);

        return borderless || className.Contains("Unreal", StringComparison.OrdinalIgnoreCase) || className.Contains("Unity", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var buffer = new char[256];
        var read = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return read > 0 ? new string(buffer, 0, read) : string.Empty;
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        Publish(EvaluateCurrentContext());
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            _sessionLocked = true;
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            _sessionLocked = false;
        }

        Publish(EvaluateCurrentContext());
    }

    private void Publish(ForegroundContext context)
    {
        CurrentContext = context;
        ContextChanged?.Invoke(context);
    }

    public void Dispose()
    {
        Stop();
    }
}

public sealed class InputInjectorService
{
    private readonly HashSet<ushort> _pressedKeys = new();
    private readonly HashSet<MouseButtonKind> _pressedButtons = new();

    public void MoveCursor(double deltaX, double deltaY)
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        NativeMethods.SetCursorPos(point.X + (int)Math.Round(deltaX), point.Y + (int)Math.Round(deltaY));
    }

    public void ClickMouseButton(MouseButtonKind button, int clickCount = 1)
    {
        for (var index = 0; index < clickCount; index++)
        {
            SetMouseButton(button, true);
            SetMouseButton(button, false);
        }
    }

    public void SetMouseButton(MouseButtonKind button, bool pressed)
    {
        var needsSend = pressed ? _pressedButtons.Add(button) : _pressedButtons.Remove(button);
        if (!needsSend)
        {
            return;
        }

        var flags = button switch
        {
            MouseButtonKind.Left => pressed ? NativeMethods.MouseEventFLeftDown : NativeMethods.MouseEventFLeftUp,
            MouseButtonKind.Right => pressed ? NativeMethods.MouseEventFRightDown : NativeMethods.MouseEventFRightUp,
            MouseButtonKind.Middle => pressed ? NativeMethods.MouseEventFMiddleDown : NativeMethods.MouseEventFMiddleUp,
            _ => 0
        };

        SendMouse((uint)flags, 0);
    }

    public void ScrollVertical(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        SendMouse((uint)NativeMethods.MouseEventFWheel, unchecked((uint)delta));
    }

    public void TapKeyboardChord(string chord)
    {
        var keys = ParseKeys(chord);
        foreach (var key in keys)
        {
            SendKeyboard(key, false);
        }

        for (var index = keys.Count - 1; index >= 0; index--)
        {
            SendKeyboard(keys[index], true);
        }
    }

    public void TapVirtualKey(ushort virtualKey)
    {
        SendKeyboard(virtualKey, false);
        SendKeyboard(virtualKey, true);
    }

    public void SetKeyboardChord(string chord, bool pressed)
    {
        var keys = ParseKeys(chord);
        foreach (var key in keys)
        {
            if (pressed)
            {
                if (_pressedKeys.Add(key))
                {
                    SendKeyboard(key, false);
                }
            }
            else if (_pressedKeys.Remove(key))
            {
                SendKeyboard(key, true);
            }
        }
    }

    public void ReleaseAll()
    {
        foreach (var button in _pressedButtons.ToArray())
        {
            SetMouseButton(button, false);
        }

        foreach (var key in _pressedKeys.ToArray())
        {
            SendKeyboard(key, true);
            _pressedKeys.Remove(key);
        }
    }

    private static void SendMouse(uint flags, uint data)
    {
        var inputs = new[]
        {
            new Input
            {
                type = NativeMethods.InputMouse,
                U = new InputUnion
                {
                    mi = new MouseInput
                    {
                        dwFlags = flags,
                        mouseData = data
                    }
                }
            }
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendKeyboard(ushort virtualKey, bool keyUp)
    {
        var inputs = new[]
        {
            new Input
            {
                type = NativeMethods.InputKeyboard,
                U = new InputUnion
                {
                    ki = new KeyboardInput
                    {
                        wVk = virtualKey,
                        dwFlags = (uint)(keyUp ? NativeMethods.KeyeventfKeyup : 0)
                    }
                }
            }
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static List<ushort> ParseKeys(string chord)
    {
        var keys = new List<ushort>();
        foreach (var rawToken in chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = rawToken.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token.Length > 1 && token[0] == 'F' && int.TryParse(token[1..], out var functionNumber) && functionNumber >= 1 && functionNumber <= 24)
            {
                keys.Add((ushort)(0x6F + functionNumber));
                continue;
            }

            keys.Add(token switch
            {
                "CTRL" or "CONTROL" => 0x11,
                "SHIFT" => 0x10,
                "ALT" => 0x12,
                "TAB" => 0x09,
                "SPACE" => 0x20,
                "ENTER" => 0x0D,
                "ESC" or "ESCAPE" => 0x1B,
                "BACKSPACE" => 0x08,
                "INSERT" => 0x2D,
                "DELETE" => 0x2E,
                "HOME" => 0x24,
                "END" => 0x23,
                "PGUP" or "PAGEUP" => 0x21,
                "PGDN" or "PAGEDOWN" => 0x22,
                "UP" => 0x26,
                "DOWN" => 0x28,
                "LEFT" => 0x25,
                "RIGHT" => 0x27,
                "LWIN" or "WIN" or "WINDOWS" => 0x5B,
                "RWIN" => 0x5C,
                _ when token.Length == 1 => (ushort)char.ToUpperInvariant(token[0]),
                _ => 0x20
            });
        }

        return keys;
    }
}

public sealed class ControllerPollingService : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public event Action<ControllerSnapshot>? SnapshotAvailable;

    public void Start(int preferredSlot)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollLoop(preferredSlot, _cts.Token), _cts.Token);
    }

    public void Stop()
    {
        if (_cts is null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            _pollingTask?.Wait(100);
        }
        catch
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _pollingTask = null;
        }
    }

    private async Task PollLoop(int preferredSlot, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SnapshotAvailable?.Invoke(ReadSnapshot(preferredSlot));
                await Task.Delay(8, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static ControllerSnapshot ReadSnapshot(int preferredSlot)
    {
        if (TryReadSlot(preferredSlot, out var preferredState))
        {
            return BuildSnapshot(preferredSlot, preferredState);
        }

        for (var slot = 0; slot < 4; slot++)
        {
            if (TryReadSlot(slot, out var state))
            {
                return BuildSnapshot(slot, state);
            }
        }

        return new ControllerSnapshot { IsConnected = false, ControllerSlot = -1, Timestamp = DateTimeOffset.UtcNow };
    }

    private static ControllerSnapshot BuildSnapshot(int slot, XInputState state)
    {
        return new ControllerSnapshot
        {
            IsConnected = true,
            ControllerSlot = slot,
            Buttons = (ControllerButtons)state.Gamepad.wButtons,
            LeftTrigger = state.Gamepad.bLeftTrigger / 255f,
            RightTrigger = state.Gamepad.bRightTrigger / 255f,
            LeftStickX = NormalizeThumb(state.Gamepad.sThumbLX),
            LeftStickY = NormalizeThumb(state.Gamepad.sThumbLY),
            RightStickX = NormalizeThumb(state.Gamepad.sThumbRX),
            RightStickY = NormalizeThumb(state.Gamepad.sThumbRY),
            PacketNumber = state.dwPacketNumber,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static bool TryReadSlot(int slot, out XInputState state)
    {
        var result = NativeMethods.XInputGetState((uint)slot, out state);
        return result == NativeMethods.ErrorSuccess;
    }

    private static float NormalizeThumb(short value)
    {
        var normalized = Math.Clamp(value / 32767f, -1f, 1f);
        return Math.Abs(normalized) < 0.04f ? 0f : normalized;
    }

    public void Dispose()
    {
        Stop();
    }
}

public sealed class RuntimeCoordinator : IDisposable
{
    private sealed class RuleExecutionState
    {
        public bool WasActive { get; set; }
        public bool LongPressTriggered { get; set; }
        public DateTimeOffset PressStartedAt { get; set; }
        public DateTimeOffset LastTapAt { get; set; }
    }

    private static readonly TimeSpan DoublePressWindow = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan LongPressDelay = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan StatusPublishInterval = TimeSpan.FromMilliseconds(250);

    private readonly object _syncRoot = new();
    private AppConfiguration _configuration;
    private readonly ControllerPollingService _pollingService;
    private readonly GameContextDetector _contextDetector;
    private readonly InputInjectorService _inputInjector;
    private readonly Dictionary<string, RuleExecutionState> _ruleStates = new();
    private ControllerSnapshot? _previousSnapshot;
    private ForegroundContext _currentContext = new() { ActivationState = ActivationState.ActiveDesktop };
    private RuntimeStatus? _lastPublishedStatus;
    private DateTimeOffset _lastPublishedAt = DateTimeOffset.MinValue;
    private string _recentAction = "Idle";

    public event Action<RuntimeStatus>? StatusChanged;

    public RuntimeCoordinator(
        AppConfiguration configuration,
        ControllerPollingService pollingService,
        GameContextDetector contextDetector,
        InputInjectorService inputInjector)
    {
        _configuration = configuration;
        _pollingService = pollingService;
        _contextDetector = contextDetector;
        _inputInjector = inputInjector;

        _pollingService.SnapshotAvailable += OnSnapshotAvailable;
        _contextDetector.ContextChanged += context =>
        {
            lock (_syncRoot)
            {
                _currentContext = context;
                if (context.ActivationState != ActivationState.ActiveDesktop)
                {
                    _inputInjector.ReleaseAll();
                }

                PublishStatus(_previousSnapshot);
            }
        };
    }

    public void Start()
    {
        _contextDetector.Start();
        _pollingService.Start(_configuration.Profile.ControllerSlot);
        PublishStatus(null);
    }

    public void Stop()
    {
        _pollingService.Stop();
        _contextDetector.Stop();
        _inputInjector.ReleaseAll();
    }

    public void UpdateConfiguration(AppConfiguration configuration)
    {
        lock (_syncRoot)
        {
            _configuration = configuration;
            _ruleStates.Clear();
            _inputInjector.ReleaseAll();
            _pollingService.Start(_configuration.Profile.ControllerSlot);
            PublishStatus(_previousSnapshot);
        }
    }

    public void PublishStatus(ControllerSnapshot? snapshot)
    {
        var nextStatus = new RuntimeStatus
        {
            IsControllerConnected = snapshot?.IsConnected == true,
            ActiveControllerSlot = snapshot?.ControllerSlot ?? -1,
            ActivationState = snapshot?.IsConnected == true ? _currentContext.ActivationState : ActivationState.SuspendedByNoController,
            ForegroundProcessName = _currentContext.ProcessName,
            RecentAction = _recentAction
        };

        var now = DateTimeOffset.UtcNow;
        if (_lastPublishedStatus is not null && RuntimeStatusEquals(_lastPublishedStatus, nextStatus) && now - _lastPublishedAt < StatusPublishInterval)
        {
            return;
        }

        _lastPublishedStatus = nextStatus;
        _lastPublishedAt = now;
        StatusChanged?.Invoke(nextStatus);
    }

    private static bool RuntimeStatusEquals(RuntimeStatus left, RuntimeStatus right)
    {
        return left.IsControllerConnected == right.IsControllerConnected &&
               left.ActiveControllerSlot == right.ActiveControllerSlot &&
               left.ActivationState == right.ActivationState &&
               string.Equals(left.ForegroundProcessName, right.ForegroundProcessName, StringComparison.Ordinal) &&
               string.Equals(left.RecentAction, right.RecentAction, StringComparison.Ordinal);
    }

    private void OnSnapshotAvailable(ControllerSnapshot snapshot)
    {
        lock (_syncRoot)
        {
            if (!_configuration.RuntimeEnabled || !_configuration.Profile.Enabled)
            {
                _inputInjector.ReleaseAll();
                PublishStatus(snapshot);
                _previousSnapshot = snapshot;
                return;
            }

            if (!snapshot.IsConnected)
            {
                _inputInjector.ReleaseAll();
                PublishStatus(snapshot);
                _previousSnapshot = snapshot;
                return;
            }

            if (_currentContext.ActivationState != ActivationState.ActiveDesktop)
            {
                _inputInjector.ReleaseAll();
                PublishStatus(snapshot);
                _previousSnapshot = snapshot;
                return;
            }

            foreach (var rule in _configuration.Profile.Rules.Where(rule => rule.IsEnabled))
            {
                ApplyRule(rule, snapshot);
            }

            PublishStatus(snapshot);
            _previousSnapshot = snapshot;
        }
    }

    private void ApplyRule(BindingRule rule, ControllerSnapshot snapshot)
    {
        var currentValue = GetControlValue(snapshot, rule.Trigger.Control);

        if (rule.RepeatMode == RepeatMode.Analog)
        {
            ApplyAnalogRule(rule, currentValue);
            return;
        }

        var ruleState = GetRuleState(rule.Id);
        var isActive = EvaluateTrigger(rule.Trigger, currentValue);
        var timestamp = snapshot.Timestamp;

        switch (rule.TriggerBehavior)
        {
            case TriggerBehaviorKind.SinglePress:
                ApplySinglePressRule(rule, ruleState, isActive);
                break;
            case TriggerBehaviorKind.DoublePress:
                ApplyDoublePressRule(rule, ruleState, isActive, timestamp);
                break;
            case TriggerBehaviorKind.LongPress:
                ApplyLongPressRule(rule, ruleState, isActive, timestamp);
                break;
            case TriggerBehaviorKind.Hold:
                ApplyHoldRule(rule, ruleState, isActive);
                break;
        }

        ruleState.WasActive = isActive;
    }

    private RuleExecutionState GetRuleState(string ruleId)
    {
        if (!_ruleStates.TryGetValue(ruleId, out var state))
        {
            state = new RuleExecutionState();
            _ruleStates[ruleId] = state;
        }

        return state;
    }

    private void ApplySinglePressRule(BindingRule rule, RuleExecutionState state, bool isActive)
    {
        if (isActive && !state.WasActive)
        {
            FireMomentaryAction(rule);
        }
    }

    private void ApplyDoublePressRule(BindingRule rule, RuleExecutionState state, bool isActive, DateTimeOffset timestamp)
    {
        if (!isActive || state.WasActive)
        {
            return;
        }

        if (state.LastTapAt != default && timestamp - state.LastTapAt <= DoublePressWindow)
        {
            FireMomentaryAction(rule);
            state.LastTapAt = default;
        }
        else
        {
            state.LastTapAt = timestamp;
        }
    }

    private void ApplyLongPressRule(BindingRule rule, RuleExecutionState state, bool isActive, DateTimeOffset timestamp)
    {
        if (isActive && !state.WasActive)
        {
            state.PressStartedAt = timestamp;
            state.LongPressTriggered = false;
        }

        if (isActive && !state.LongPressTriggered && timestamp - state.PressStartedAt >= LongPressDelay)
        {
            if (SupportsHeldAction(rule))
            {
                SetHeldAction(rule, true, false);
            }
            else
            {
                FireMomentaryAction(rule);
            }

            state.LongPressTriggered = true;
        }

        if (!isActive && state.WasActive)
        {
            if (state.LongPressTriggered && SupportsHeldAction(rule))
            {
                SetHeldAction(rule, false, true);
            }

            state.LongPressTriggered = false;
        }
    }

    private void ApplyHoldRule(BindingRule rule, RuleExecutionState state, bool isActive)
    {
        if (rule.RepeatMode == RepeatMode.OnPress)
        {
            if (isActive && !state.WasActive)
            {
                FireMomentaryAction(rule);
            }

            return;
        }

        if (rule.RepeatMode == RepeatMode.WhileHeld)
        {
            SetHeldAction(rule, isActive, state.WasActive);
            if (rule.Action.Type == ActionType.MouseWheel && isActive)
            {
                Scroll(rule);
            }
        }
    }

    private static bool SupportsHeldAction(BindingRule rule)
    {
        return rule.Action.Type is ActionType.KeyboardKey or ActionType.KeyboardChord or ActionType.MouseButton;
    }

    private void ApplyAnalogRule(BindingRule rule, float value)
    {
        var deadZone = _configuration.Profile.CursorSettings.DeadZone;
        if (Math.Abs(value) < deadZone)
        {
            return;
        }

        var normalized = Math.Pow(Math.Abs(value), _configuration.Profile.CursorSettings.Acceleration) * Math.Sign(value);
        switch (rule.Action.Type)
        {
            case ActionType.MouseMove:
                var delta = normalized * _configuration.Profile.CursorSettings.BaseSpeed * rule.Action.Sensitivity;
                if (string.Equals(rule.Action.Parameter, "X", StringComparison.OrdinalIgnoreCase))
                {
                    _inputInjector.MoveCursor(delta, 0);
                    _recentAction = $"Cursor X {delta:0.0}";
                }
                else
                {
                    _inputInjector.MoveCursor(0, delta);
                    _recentAction = $"Cursor Y {delta:0.0}";
                }
                break;
            case ActionType.MouseWheel:
                _inputInjector.ScrollVertical((int)Math.Round(normalized * _configuration.Profile.CursorSettings.ScrollStep * rule.Action.Sensitivity));
                _recentAction = "Scroll";
                break;
        }
    }

    private void FireMomentaryAction(BindingRule rule)
    {
        switch (rule.Action.Type)
        {
            case ActionType.KeyboardKey:
            case ActionType.KeyboardChord:
                _inputInjector.TapKeyboardChord(rule.Action.Parameter);
                _recentAction = rule.Action.Parameter;
                break;
            case ActionType.MouseButton when Enum.TryParse<MouseButtonKind>(rule.Action.Parameter, true, out var button):
                _inputInjector.ClickMouseButton(button);
                _recentAction = $"Mouse {button}";
                break;
            case ActionType.MouseWheel:
                Scroll(rule);
                break;
            case ActionType.SystemAction:
                ExecuteSystemAction(rule.Action.Parameter);
                _recentAction = rule.Action.Parameter;
                break;
        }
    }

    private void SetHeldAction(BindingRule rule, bool isActive, bool wasActive)
    {
        if (isActive == wasActive && rule.Action.Type != ActionType.MouseWheel)
        {
            return;
        }

        switch (rule.Action.Type)
        {
            case ActionType.KeyboardKey:
            case ActionType.KeyboardChord:
                _inputInjector.SetKeyboardChord(rule.Action.Parameter, isActive);
                _recentAction = rule.Action.Parameter;
                break;
            case ActionType.MouseButton when Enum.TryParse<MouseButtonKind>(rule.Action.Parameter, true, out var button):
                _inputInjector.SetMouseButton(button, isActive);
                _recentAction = $"Mouse {button}";
                break;
        }
    }

    private void Scroll(BindingRule rule)
    {
        var delta = GetDiscreteScrollDelta(rule);
        _inputInjector.ScrollVertical(delta);
        _recentAction = delta >= 0 ? "????" : "????";
    }

    private int GetDiscreteScrollDelta(BindingRule rule)
    {
        var direction = ResolveScrollDirection(rule);
        var signedSensitivity = Math.Abs(rule.Action.Sensitivity) * direction;
        return (int)Math.Round(_configuration.Profile.CursorSettings.ScrollStep * signedSensitivity);
    }

    private static int ResolveScrollDirection(BindingRule rule)
    {
        return rule.Trigger.Mode switch
        {
            TriggerMode.AxisNegative => -1,
            TriggerMode.AxisPositive => 1,
            _ => Math.Sign(rule.Action.Sensitivity) == 0 ? 1 : Math.Sign(rule.Action.Sensitivity)
        };
    }

    private void ExecuteSystemAction(string parameter)
    {
        switch (NormalizeSystemActionName(parameter))
        {
            case "VOLUMEUP":
                _inputInjector.TapVirtualKey(NativeMethods.VkVolumeUp);
                break;
            case "VOLUMEDOWN":
                _inputInjector.TapVirtualKey(NativeMethods.VkVolumeDown);
                break;
            case "VOLUMEMUTE":
                _inputInjector.TapVirtualKey(NativeMethods.VkVolumeMute);
                break;
            case "SHOWDESKTOP":
                _inputInjector.TapKeyboardChord("LWIN+D");
                break;
            case "TASKVIEW":
                _inputInjector.TapKeyboardChord("LWIN+TAB");
                break;
            default:
                _inputInjector.TapKeyboardChord(parameter);
                break;
        }
    }

    private static string NormalizeSystemActionName(string parameter)
    {
        var normalized = parameter.Trim().Replace(" ", string.Empty);
        if (normalized.Equals("\u97F3\u91CF\u589E\u52A0", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("\u589E\u5927\u97F3\u91CF", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("VOLUMEUP", StringComparison.OrdinalIgnoreCase))
        {
            return "VOLUMEUP";
        }

        if (normalized.Equals("\u97F3\u91CF\u51CF\u5C11", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("\u964D\u4F4E\u97F3\u91CF", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("VOLUMEDOWN", StringComparison.OrdinalIgnoreCase))
        {
            return "VOLUMEDOWN";
        }

        if (normalized.Equals("\u97F3\u91CF\u9759\u97F3", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("\u9759\u97F3", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("MUTE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("VOLUMEMUTE", StringComparison.OrdinalIgnoreCase))
        {
            return "VOLUMEMUTE";
        }

        if (normalized.Equals("\u663E\u793A\u684C\u9762", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SHOWDESKTOP", StringComparison.OrdinalIgnoreCase))
        {
            return "SHOWDESKTOP";
        }

        if (normalized.Equals("\u4EFB\u52A1\u89C6\u56FE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("TASKVIEW", StringComparison.OrdinalIgnoreCase))
        {
            return "TASKVIEW";
        }

        return parameter.Trim();
    }

    private static bool EvaluateTrigger(TriggerDescriptor trigger, float value)
    {
        return trigger.Mode switch
        {
            TriggerMode.Button => value >= 0.5f,
            TriggerMode.AxisPositive => value >= trigger.Threshold,
            TriggerMode.AxisNegative => value <= -trigger.Threshold,
            _ => false
        };
    }

    private static float GetControlValue(ControllerSnapshot snapshot, string control)
    {
        return control switch
        {
            "LeftStickX" => snapshot.LeftStickX,
            "LeftStickY" => snapshot.LeftStickY,
            "RightStickX" => snapshot.RightStickX,
            "RightStickY" => snapshot.RightStickY,
            "LeftTrigger" => snapshot.LeftTrigger,
            "RightTrigger" => snapshot.RightTrigger,
            _ => snapshot.Buttons.HasFlag(ParseButton(control)) ? 1f : 0f
        };
    }

    private static ControllerButtons ParseButton(string control)
    {
        return control switch
        {
            "DPadUp" => ControllerButtons.DPadUp,
            "DPadDown" => ControllerButtons.DPadDown,
            "DPadLeft" => ControllerButtons.DPadLeft,
            "DPadRight" => ControllerButtons.DPadRight,
            "Start" => ControllerButtons.Start,
            "Back" => ControllerButtons.Back,
            "LeftThumb" => ControllerButtons.LeftThumb,
            "RightThumb" => ControllerButtons.RightThumb,
            "LeftShoulder" => ControllerButtons.LeftShoulder,
            "RightShoulder" => ControllerButtons.RightShoulder,
            "A" => ControllerButtons.A,
            "B" => ControllerButtons.B,
            "X" => ControllerButtons.X,
            "Y" => ControllerButtons.Y,
            _ => ControllerButtons.None
        };
    }

    public void Dispose()
    {
        Stop();
    }
}
