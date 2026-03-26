using ControllerDesktop.Models;
using ControllerDesktop.Services;
using System.Diagnostics;
using System.Windows;

namespace ControllerDesktop;

public partial class App : System.Windows.Application
{
    private readonly string[] _launchArgs = Environment.GetCommandLineArgs();
    private ConfigurationService? _configurationService;
    private AutostartService? _autostartService;
    private TrayService? _trayService;
    private ControllerPollingService? _pollingService;
    private GameContextDetector? _contextDetector;
    private InputInjectorService? _inputInjector;
    private RuntimeCoordinator? _runtimeCoordinator;
    private WebEditorHost? _webEditorHost;
    private NativeInputCaptureService? _captureService;
    private RuntimeStatus _latestStatus = new() { ActivationState = ActivationState.SuspendedByNoController };

    public AppConfiguration Configuration { get; private set; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _configurationService = new ConfigurationService();
        _autostartService = new AutostartService();
        _captureService = new NativeInputCaptureService();
        Configuration = await _configurationService.LoadAsync();

        var autostartEnabled = _autostartService.IsEnabled();
        if (Configuration.StartWithWindows != autostartEnabled)
        {
            Configuration.StartWithWindows = autostartEnabled;
            await _configurationService.SaveAsync(Configuration);
        }

        _pollingService = new ControllerPollingService();
        _contextDetector = new GameContextDetector();
        _inputInjector = new InputInjectorService();
        _runtimeCoordinator = new RuntimeCoordinator(Configuration, _pollingService, _contextDetector, _inputInjector);
        _runtimeCoordinator.StatusChanged += status => _ = Dispatcher.InvokeAsync(() => ApplyRuntimeStatus(status));

        _webEditorHost = new WebEditorHost(
            getConfiguration: () => Configuration,
            saveConfigurationAsync: configuration => Dispatcher.InvokeAsync(() => ReplaceConfigurationAsync(configuration)).Task.Unwrap(),
            getStatus: () => _latestStatus,
            getConfigPath: () => _configurationService?.ConfigPath ?? string.Empty,
            startCapture: () => Dispatcher.Invoke(() => _captureService?.StartCapture()),
            getCaptureStatus: () => _captureService?.GetSnapshot() ?? CaptureSnapshot.Idle,
            cancelCapture: () => Dispatcher.Invoke(() => _captureService?.CancelCapture()));
        await _webEditorHost.StartAsync();

        _trayService = new TrayService();
        _trayService.OpenEditorRequested += () => _ = Dispatcher.InvokeAsync(OpenEditor);
        _trayService.ExitRequested += () => _ = Dispatcher.InvokeAsync(ExitApplication);
        _trayService.RuntimeToggled += enabled => _ = Dispatcher.InvokeAsync(() =>
        {
            Configuration.RuntimeEnabled = enabled;
            _runtimeCoordinator.PublishStatus(null);
            _ = SaveConfigurationAsync();
        });
        _trayService.AutostartToggled += enabled => _ = Dispatcher.InvokeAsync(() =>
        {
            SetAutostart(enabled);
            _ = SaveConfigurationAsync();
        });

        _runtimeCoordinator.Start();

        if (!_launchArgs.Contains("--background", StringComparer.OrdinalIgnoreCase))
        {
            OpenEditor();
        }
    }

    public async Task SaveConfigurationAsync()
    {
        if (_configurationService is null)
        {
            return;
        }

        await _configurationService.SaveAsync(Configuration);
    }

    private async Task ReplaceConfigurationAsync(AppConfiguration configuration)
    {
        Configuration = configuration;
        _runtimeCoordinator?.UpdateConfiguration(configuration);

        if (_autostartService is not null)
        {
            _autostartService.SetEnabled(configuration.StartWithWindows);
        }

        await SaveConfigurationAsync();
        _trayService?.Update(_latestStatus, Configuration.RuntimeEnabled, Configuration.StartWithWindows, _webEditorHost?.EditorUrl ?? string.Empty);
    }

    public void SetAutostart(bool enabled)
    {
        _autostartService?.SetEnabled(enabled);
        Configuration.StartWithWindows = enabled;
        _trayService?.Update(_latestStatus, Configuration.RuntimeEnabled, enabled, _webEditorHost?.EditorUrl ?? string.Empty);
    }

    public void UpdateRuntimeEnabled()
    {
        _runtimeCoordinator?.PublishStatus(null);
        _trayService?.Update(_latestStatus, Configuration.RuntimeEnabled, Configuration.StartWithWindows, _webEditorHost?.EditorUrl ?? string.Empty);
    }

    private void ApplyRuntimeStatus(RuntimeStatus status)
    {
        _latestStatus = status;
        _trayService?.Update(status, Configuration.RuntimeEnabled, Configuration.StartWithWindows, _webEditorHost?.EditorUrl ?? string.Empty);
    }

    private void OpenEditor()
    {
        var url = _webEditorHost?.EditorUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void ExitApplication()
    {
        _captureService?.Dispose();
        _runtimeCoordinator?.Stop();
        _webEditorHost?.Dispose();
        _trayService?.Dispose();
        Shutdown();
    }
}
