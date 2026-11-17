using Avalonia;

namespace CobiHw.App.Linux;

internal static class Program
{
    /// <summary>Starts the app, or, when it's already running, asks that instance to show its widget and exits.</summary>
    [STAThread]
    public static int Main(string[] args)
    {
        using SingleInstance? instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            return 0;
        }

        App.Instance = instance;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
    }

    /// <summary>Avalonia setup; also used by the XAML previewer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
