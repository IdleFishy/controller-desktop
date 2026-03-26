using ControllerDesktop.Models;
using ControllerDesktop.ViewModels;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace ControllerDesktop;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _allowClose;

    private readonly System.Windows.Media.Brush _stageDefaultBackground = ParseBrush("#34363C");
    private readonly System.Windows.Media.Brush _stageDefaultBorder = ParseBrush("#9FA7B4");
    private readonly System.Windows.Media.Brush _stageActiveBackground = ParseBrush("#4A355E");
    private readonly System.Windows.Media.Brush _stageActiveBorder = ParseBrush("#B78FFF");
    private readonly System.Windows.Media.Brush _segmentDefaultBackground = ParseBrush("#303238");
    private readonly System.Windows.Media.Brush _segmentActiveBackground = ParseBrush("#4A355E");
    private readonly System.Windows.Media.Brush _segmentDefaultBorder = ParseBrush("#22FFFFFF");
    private readonly System.Windows.Media.Brush _segmentActiveBorder = ParseBrush("#B78FFF");

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        _app = (App)System.Windows.Application.Current;
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        Closing += OnWindowClosing;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => RefreshSelectionVisuals();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void HideToTray()
    {
        Hide();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private static System.Windows.Media.Brush ParseBrush(string value)
    {
        return (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(value)!;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await _app.SaveConfigurationAsync();
    }

    private async void RuntimeToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.Configuration.RuntimeEnabled = ViewModel.RuntimeEnabled;
        _app.UpdateRuntimeEnabled();
        await _app.SaveConfigurationAsync();
    }

    private async void AutoStartToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.SetAutostart(ViewModel.StartWithWindows);
        await _app.SaveConfigurationAsync();
    }

    private async void StartHiddenToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.Configuration.StartHidden = ViewModel.StartHidden;
        await _app.SaveConfigurationAsync();
    }

    private async void CloseToTrayToggle_Click(object sender, RoutedEventArgs e)
    {
        _app.Configuration.CloseToTray = ViewModel.CloseToTray;
        await _app.SaveConfigurationAsync();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = ViewModel.CreateRule();
        ViewModel.Profile.Rules.Add(rule);
        ViewModel.SetSelectedRule(rule);
        RuleList.SelectedItem = rule;
        RefreshSelectionVisuals();
    }

    private void DeleteSelectedRule_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedRule is null)
        {
            return;
        }

        var removedRule = ViewModel.SelectedRule;
        ViewModel.Profile.Rules.Remove(removedRule);
        ViewModel.SetSelectedRule(ViewModel.Profile.Rules.FirstOrDefault());
        RuleList.SelectedItem = ViewModel.SelectedRule;
        RefreshSelectionVisuals();
    }

    private void StageNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not string tag)
        {
            return;
        }

        var parts = tag.Split('|');
        if (parts.Length != 3 || !Enum.TryParse<TriggerMode>(parts[1], true, out var mode))
        {
            return;
        }

        var rule = ViewModel.SelectOrCreateRule(parts[0], mode, parts[2]);
        RuleList.SelectedItem = rule;
        RefreshSelectionVisuals();
    }

    private void BehaviorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag is string behavior)
        {
            ViewModel.SetSelectedBehavior(behavior);
            RefreshBehaviorButtons();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.SelectedRule) or nameof(MainViewModel.SelectedControlToken))
        {
            RefreshSelectionVisuals();
        }
    }

    private void RefreshSelectionVisuals()
    {
        RefreshStageButtons();
        RefreshBehaviorButtons();
    }

    private void RefreshStageButtons()
    {
        foreach (var button in StageCanvas.Children.OfType<WpfButton>())
        {
            if (button.Tag is not string tag)
            {
                continue;
            }

            var parts = tag.Split('|');
            if (parts.Length < 2)
            {
                continue;
            }

            var token = MainViewModel.BuildControlToken(parts[0], Enum.Parse<TriggerMode>(parts[1], true));
            var isSelected = string.Equals(token, ViewModel.SelectedControlToken, StringComparison.OrdinalIgnoreCase);
            button.Background = isSelected ? _stageActiveBackground : _stageDefaultBackground;
            button.BorderBrush = isSelected ? _stageActiveBorder : _stageDefaultBorder;
        }
    }

    private void RefreshBehaviorButtons()
    {
        var behavior = ViewModel.SelectedRule?.TriggerBehaviorText ?? string.Empty;
        UpdateBehaviorButton(BehaviorSingleButton, behavior == "SinglePress");
        UpdateBehaviorButton(BehaviorDoubleButton, behavior == "DoublePress");
        UpdateBehaviorButton(BehaviorLongButton, behavior == "LongPress");
        UpdateBehaviorButton(BehaviorHoldButton, behavior == "Hold");
    }

    private void UpdateBehaviorButton(WpfButton button, bool active)
    {
        button.Background = active ? _segmentActiveBackground : _segmentDefaultBackground;
        button.BorderBrush = active ? _segmentActiveBorder : _segmentDefaultBorder;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && ViewModel.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
        }
    }
}
