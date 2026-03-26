using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControllerDesktop.Models;

public enum ActivationState
{
    ActiveDesktop,
    SuspendedByGame,
    SuspendedByLockScreen,
    SuspendedByNoController
}

[Flags]
public enum ControllerButtons : uint
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

public enum TriggerMode
{
    Button,
    AxisPositive,
    AxisNegative
}

public enum RepeatMode
{
    OnPress,
    WhileHeld,
    Analog
}

public enum TriggerBehaviorKind
{
    SinglePress,
    DoublePress,
    LongPress,
    Hold
}

public enum ActionType
{
    KeyboardKey,
    KeyboardChord,
    MouseMove,
    MouseButton,
    MouseWheel,
    SystemAction
}

public enum MouseButtonKind
{
    Left,
    Right,
    Middle
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ControllerSnapshot
{
    public bool IsConnected { get; init; }
    public int ControllerSlot { get; init; }
    public ControllerButtons Buttons { get; init; }
    public float LeftTrigger { get; init; }
    public float RightTrigger { get; init; }
    public float LeftStickX { get; init; }
    public float LeftStickY { get; init; }
    public float RightStickX { get; init; }
    public float RightStickY { get; init; }
    public uint PacketNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class TriggerDescriptor : ObservableObject
{
    private string _control = "A";
    private TriggerMode _mode = TriggerMode.Button;
    private double _threshold = 0.45;

    public string Control
    {
        get => _control;
        set => SetProperty(ref _control, value);
    }

    public TriggerMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(ModeText));
            }
        }
    }

    public string ModeText
    {
        get => Mode.ToString();
        set
        {
            if (Enum.TryParse<TriggerMode>(value, true, out var parsed))
            {
                Mode = parsed;
            }
        }
    }

    public double Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value);
    }
}

public sealed class ActionDescriptor : ObservableObject
{
    private ActionType _type = ActionType.KeyboardKey;
    private string _parameter = "SPACE";
    private double _sensitivity = 1.0;

    public ActionType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeText));
            }
        }
    }

    public string TypeText
    {
        get => Type.ToString();
        set
        {
            if (Enum.TryParse<ActionType>(value, true, out var parsed))
            {
                Type = parsed;
            }
        }
    }

    public string Parameter
    {
        get => _parameter;
        set => SetProperty(ref _parameter, value);
    }

    public double Sensitivity
    {
        get => _sensitivity;
        set => SetProperty(ref _sensitivity, value);
    }
}

public sealed class BindingRule : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _displayName = "新映射";
    private bool _isEnabled = true;
    private RepeatMode _repeatMode = RepeatMode.OnPress;
    private TriggerBehaviorKind _triggerBehavior = TriggerBehaviorKind.SinglePress;
    private TriggerDescriptor _trigger = new();
    private ActionDescriptor _action = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public RepeatMode RepeatMode
    {
        get => _repeatMode;
        set
        {
            if (SetProperty(ref _repeatMode, value))
            {
                OnPropertyChanged(nameof(RepeatModeText));
            }
        }
    }

    public string RepeatModeText
    {
        get => RepeatMode.ToString();
        set
        {
            if (Enum.TryParse<RepeatMode>(value, true, out var parsed))
            {
                RepeatMode = parsed;
            }
        }
    }

    public TriggerBehaviorKind TriggerBehavior
    {
        get => _triggerBehavior;
        set
        {
            if (SetProperty(ref _triggerBehavior, value))
            {
                OnPropertyChanged(nameof(TriggerBehaviorText));
            }
        }
    }

    public string TriggerBehaviorText
    {
        get => TriggerBehavior.ToString();
        set
        {
            if (Enum.TryParse<TriggerBehaviorKind>(value, true, out var parsed))
            {
                TriggerBehavior = parsed;
            }
        }
    }

    public TriggerDescriptor Trigger
    {
        get => _trigger;
        set => SetProperty(ref _trigger, value);
    }

    public ActionDescriptor Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }
}

public sealed class CursorSettings : ObservableObject
{
    private double _baseSpeed = 24;
    private double _acceleration = 1.35;
    private double _scrollStep = 120;
    private double _deadZone = 0.18;

    public double BaseSpeed
    {
        get => _baseSpeed;
        set => SetProperty(ref _baseSpeed, value);
    }

    public double Acceleration
    {
        get => _acceleration;
        set => SetProperty(ref _acceleration, value);
    }

    public double ScrollStep
    {
        get => _scrollStep;
        set => SetProperty(ref _scrollStep, value);
    }

    public double DeadZone
    {
        get => _deadZone;
        set => SetProperty(ref _deadZone, value);
    }
}

public sealed class BindingProfile : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "配置 1";
    private bool _enabled = true;
    private int _controllerSlot;
    private CursorSettings _cursorSettings = new();
    private ObservableCollection<BindingRule> _rules = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public int ControllerSlot
    {
        get => _controllerSlot;
        set => SetProperty(ref _controllerSlot, value);
    }

    public CursorSettings CursorSettings
    {
        get => _cursorSettings;
        set => SetProperty(ref _cursorSettings, value);
    }

    public ObservableCollection<BindingRule> Rules
    {
        get => _rules;
        set => SetProperty(ref _rules, value);
    }
}

public sealed class AppConfiguration : ObservableObject
{
    private bool _startWithWindows = true;
    private bool _startHidden = true;
    private bool _closeToTray = true;
    private bool _runtimeEnabled = true;
    private BindingProfile _profile = DefaultProfileFactory.Create();

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool StartHidden
    {
        get => _startHidden;
        set => SetProperty(ref _startHidden, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set => SetProperty(ref _closeToTray, value);
    }

    public bool RuntimeEnabled
    {
        get => _runtimeEnabled;
        set => SetProperty(ref _runtimeEnabled, value);
    }

    public BindingProfile Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }
}

public static class DefaultProfileFactory
{
    public static BindingProfile Create()
    {
        return new BindingProfile
        {
            Name = "配置 1",
            ControllerSlot = 0,
            CursorSettings = new CursorSettings
            {
                BaseSpeed = 24,
                Acceleration = 1.35,
                ScrollStep = 120,
                DeadZone = 0.18
            },
            Rules = new ObservableCollection<BindingRule>
            {
                CreateRule("左摇杆横向", "LeftStickX", TriggerMode.AxisPositive, RepeatMode.Analog, TriggerBehaviorKind.Hold, ActionType.MouseMove, "X", 1.0, 0.01),
                CreateRule("左摇杆纵向", "LeftStickY", TriggerMode.AxisPositive, RepeatMode.Analog, TriggerBehaviorKind.Hold, ActionType.MouseMove, "Y", -1.0, 0.01),
                CreateRule("右摇杆上", "RightStickY", TriggerMode.AxisPositive, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardKey, "UP"),
                CreateRule("右摇杆下", "RightStickY", TriggerMode.AxisNegative, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardKey, "DOWN"),
                CreateRule("A 键", "A", TriggerMode.Button, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.MouseButton, "Left"),
                CreateRule("B 键", "B", TriggerMode.Button, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.MouseButton, "Right"),
                CreateRule("X 键", "X", TriggerMode.Button, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardChord, "LWIN+TAB"),
                CreateRule("Y 键", "Y", TriggerMode.Button, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardChord, "LWIN+D"),
                CreateRule("方向键上", "DPadUp", TriggerMode.Button, RepeatMode.WhileHeld, TriggerBehaviorKind.Hold, ActionType.KeyboardKey, "UP"),
                CreateRule("方向键下", "DPadDown", TriggerMode.Button, RepeatMode.WhileHeld, TriggerBehaviorKind.Hold, ActionType.KeyboardKey, "DOWN"),
                CreateRule("方向键左", "DPadLeft", TriggerMode.Button, RepeatMode.WhileHeld, TriggerBehaviorKind.Hold, ActionType.KeyboardKey, "LEFT"),
                CreateRule("方向键右", "DPadRight", TriggerMode.Button, RepeatMode.WhileHeld, TriggerBehaviorKind.Hold, ActionType.KeyboardKey, "RIGHT"),
                CreateRule("LB", "LeftShoulder", TriggerMode.Button, RepeatMode.WhileHeld, TriggerBehaviorKind.Hold, ActionType.MouseWheel, "Vertical", 1.0),
                CreateRule("RB", "RightShoulder", TriggerMode.Button, RepeatMode.WhileHeld, TriggerBehaviorKind.Hold, ActionType.MouseWheel, "Vertical", -1.0),
                CreateRule("LT", "LeftTrigger", TriggerMode.AxisPositive, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardKey, "CTRL", 1.0, 0.45),
                CreateRule("RT", "RightTrigger", TriggerMode.AxisPositive, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardKey, "ENTER", 1.0, 0.45),
                CreateRule("开始键", "Start", TriggerMode.Button, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardKey, "ENTER"),
                CreateRule("返回键", "Back", TriggerMode.Button, RepeatMode.OnPress, TriggerBehaviorKind.SinglePress, ActionType.KeyboardKey, "ESC")
            }
        };
    }

    private static BindingRule CreateRule(
        string name,
        string control,
        TriggerMode mode,
        RepeatMode repeatMode,
        TriggerBehaviorKind behavior,
        ActionType actionType,
        string parameter,
        double sensitivity = 1.0,
        double threshold = 0.45)
    {
        return new BindingRule
        {
            DisplayName = name,
            RepeatMode = repeatMode,
            TriggerBehavior = behavior,
            Trigger = new TriggerDescriptor { Control = control, Mode = mode, Threshold = threshold },
            Action = new ActionDescriptor { Type = actionType, Parameter = parameter, Sensitivity = sensitivity }
        };
    }
}

public sealed class RuntimeStatus
{
    public bool IsControllerConnected { get; init; }
    public int ActiveControllerSlot { get; init; } = -1;
    public ActivationState ActivationState { get; init; }
    public string ForegroundProcessName { get; init; } = "n/a";
    public string RecentAction { get; init; } = "Idle";
}
