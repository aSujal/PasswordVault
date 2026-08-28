using Avalonia;
using System;
using PasswordVault.Helper;

namespace PasswordVault;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (!SingleInstanceGuard.TryAcquire())
        {
            SingleInstanceGuard.NotifyRunningInstance();
            return;
        }

        try
        {
            Velopack.VelopackApp.Build().Run();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception: {ex}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .LogToTrace();
    }

}
