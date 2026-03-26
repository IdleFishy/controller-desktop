using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Runtime.Versioning;
using System.Threading;

namespace ControllerDesktop;

[SupportedOSPlatform("windows10.0.19041.0")]
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(args);
        });
    }
}
