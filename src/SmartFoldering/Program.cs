using Avalonia;
using SmartFoldering.Core;
using SmartFoldering.Services;

namespace SmartFoldering;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        using var instance = new SingleInstanceGuard();
        if (!instance.IsPrimary)
        {
            SingleInstanceGuard.SignalPrimary();
            return 0;
        }

        instance.StartListening();
        App.SingleInstance = instance;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
