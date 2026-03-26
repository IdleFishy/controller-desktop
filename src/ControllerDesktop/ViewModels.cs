using ControllerDesktop.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace ControllerDesktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private string _runtimeHeadline = "桌面映射已就绪";
    private string _runtimeSubhead = "等待首个前台窗口判定。";
    private string _controllerText = "未连接手柄";
    private string _foregroundProcess = "desktop";
    private string _recentAction = "空闲";
    private bool _runtimeEnabled;
    private bool _startWithWindows;
    private bool _startHidden;
    private bool _closeToTray;
    private bool _isControllerConnected;
    private BindingProfile _profile = DefaultProfileFactory.Create();
    private BindingRule? _selectedRule;
    private string _selectedControlToken = string.Empty;
    private string _selectedControlLabel = "A 键";

    public MainViewModel()
    {
        AvailableControls = new ObservableCollection<string>
        {
            "A", "B", "X", "Y", "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
            "LeftShoulder", "RightShoulder", "LeftTrigger", "RightTrigger", "Start", "Back",
            "LeftStickX", "LeftStickY", "RightStickX", "RightStickY"
        };

        AvailableActionTypes = new ObservableCollection<string>
        {
            "KeyboardKey", "KeyboardChord", "MouseMove", "MouseButton", "MouseWheel", "SystemAction"
        };

        AvailableTriggerModes = new ObservableCollection<string>
        {
            "Button", "AxisPositive", "AxisNegative"
        };

        AvailableRepeatModes = new ObservableCollection<string>
        {
            "OnPress", "WhileHeld", "Analog"
        };

        AvailableBehaviorModes = new ObservableCollection<string>
        {
            "SinglePress", "DoublePress", "LongPress", "Hold"
        };
    }

    public ObservableCollection<string> AvailableControls { get; }
    public ObservableCollection<string> AvailableActionTypes { get; }
    public ObservableCollection<string> AvailableTriggerModes { get; }
    public ObservableCollection<string> AvailableRepeatModes { get; }
    public ObservableCollection<string> AvailableBehaviorModes { get; }

    public BindingProfile Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    public BindingRule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (SetProperty(ref _selectedRule, value) && value is not null)
            {
                SelectedControlToken = BuildControlToken(value.Trigger.Control, value.Trigger.Mode);
                SelectedControlLabel = value.DisplayName;
            }
        }
    }

    public string SelectedControlToken
    {
        get => _selectedControlToken;
        set => SetProperty(ref _selectedControlToken, value);
    }

    public string SelectedControlLabel
    {
        get => _selectedControlLabel;
        set => SetProperty(ref _selectedControlLabel, value);
    }

    public string RuntimeHeadline
    {
        get => _runtimeHeadline;
        set => SetProperty(ref _runtimeHeadline, value);
    }

    public string RuntimeSubhead
    {
        get => _runtimeSubhead;
        set => SetProperty(ref _runtimeSubhead, value);
    }

    public string ControllerText
    {
        get => _controllerText;
        set => SetProperty(ref _controllerText, value);
    }

    public string ForegroundProcess
    {
        get => _foregroundProcess;
        set => SetProperty(ref _foregroundProcess, value);
    }

    public string RecentAction
    {
        get => _recentAction;
        set => SetProperty(ref _recentAction, value);
    }

    public bool RuntimeEnabled
    {
        get => _runtimeEnabled;
        set => SetProperty(ref _runtimeEnabled, value);
    }

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

    public bool IsControllerConnected
    {
        get => _isControllerConnected;
        set => SetProperty(ref _isControllerConnected, value);
    }

    public void Load(AppConfiguration configuration, bool autostartEnabled)
    {
        Profile = configuration.Profile;
        RuntimeEnabled = configuration.RuntimeEnabled;
        StartWithWindows = autostartEnabled;
        StartHidden = configuration.StartHidden;
        CloseToTray = configuration.CloseToTray;
        SelectedRule = Profile.Rules.FirstOrDefault();
    }

    public void ApplyRuntimeStatus(RuntimeStatus status, bool runtimeEnabled)
    {
        RuntimeEnabled = runtimeEnabled;
        IsControllerConnected = status.IsControllerConnected;
        ControllerText = status.IsControllerConnected
            ? $"控制器 {status.ActiveControllerSlot + 1} 已连接"
            : "未连接手柄";
        ForegroundProcess = status.ForegroundProcessName;
        RecentAction = status.RecentAction == "Idle" ? "空闲" : status.RecentAction;

        if (!runtimeEnabled)
        {
            RuntimeHeadline = "映射已手动暂停";
            RuntimeSubhead = "重新启用后，桌面动作会继续由当前配置接管。";
            return;
        }

        (RuntimeHeadline, RuntimeSubhead) = status.ActivationState switch
        {
            ActivationState.ActiveDesktop => ("桌面映射生效中", $"当前前台进程：{status.ForegroundProcessName}"),
            ActivationState.SuspendedByGame => ("已暂停映射", $"检测到全屏前台应用：{status.ForegroundProcessName}"),
            ActivationState.SuspendedByLockScreen => ("系统处于锁屏状态", "解锁前不会发送任何键盘或鼠标注入。"),
            ActivationState.SuspendedByNoController => ("等待连接 XInput 手柄", "接入手柄后会恢复方案监听。"),
            _ => ("状态未知", "尚未收到运行时状态。")
        };
    }

    public BindingRule CreateRule()
    {
        return new BindingRule
        {
            DisplayName = "新映射",
            Trigger = new TriggerDescriptor { Control = "A", Mode = TriggerMode.Button, Threshold = 0.45 },
            TriggerBehavior = TriggerBehaviorKind.SinglePress,
            RepeatMode = RepeatMode.OnPress,
            Action = new ActionDescriptor { Type = ActionType.KeyboardKey, Parameter = "SPACE", Sensitivity = 1.0 }
        };
    }

    public BindingRule SelectOrCreateRule(string control, TriggerMode mode, string displayName)
    {
        var existingRule = Profile.Rules.FirstOrDefault(rule =>
            string.Equals(rule.Trigger.Control, control, StringComparison.OrdinalIgnoreCase) &&
            rule.Trigger.Mode == mode);

        if (existingRule is null)
        {
            existingRule = CreateRule();
            existingRule.DisplayName = displayName;
            existingRule.Trigger.Control = control;
            existingRule.Trigger.Mode = mode;
            existingRule.RepeatMode = mode == TriggerMode.Button ? RepeatMode.OnPress : RepeatMode.OnPress;
            Profile.Rules.Add(existingRule);
        }

        SelectedRule = existingRule;
        return existingRule;
    }

    public void SetSelectedRule(BindingRule? rule)
    {
        if (rule is not null)
        {
            SelectedRule = rule;
        }
    }

    public void SetSelectedBehavior(string behaviorText)
    {
        if (SelectedRule is null || !Enum.TryParse<TriggerBehaviorKind>(behaviorText, true, out var behavior))
        {
            return;
        }

        SelectedRule.TriggerBehavior = behavior;
        if (behavior == TriggerBehaviorKind.Hold && SelectedRule.RepeatMode == RepeatMode.OnPress)
        {
            SelectedRule.RepeatMode = RepeatMode.WhileHeld;
        }

        if (behavior != TriggerBehaviorKind.Hold && SelectedRule.RepeatMode == RepeatMode.Analog)
        {
            SelectedRule.RepeatMode = RepeatMode.OnPress;
        }
    }

    public static string BuildControlToken(string control, TriggerMode mode)
    {
        return $"{control}|{mode}";
    }
}
