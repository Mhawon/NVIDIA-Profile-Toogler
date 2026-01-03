using Avalonia;
using System;
using System.IO;
using System.Threading;

namespace NVIDIA_Profil_Toogler;

class Program
{
    // A unique name for the mutex. A GUID is used to ensure it's unique across the system.
    private const string AppMutexName = "NVIDIA_Profil_Toogler_Mutex_7E8F6A9D-4B8C-4D2E-9A1F-8C7D6E5B4A3C";
    private static Mutex? _appMutex;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        _appMutex = new Mutex(true, AppMutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running.
            DebugLogger.Log("NVIDIA Profil Toogler is already running. Exiting the new instance.");
            return; // Exit the application.
        }
        
        GC.KeepAlive(_appMutex);

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            DebugLogger.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            DebugLogger.Log("!!!!!!!!!      UNHANDLED EXCEPTION      !!!!!!!!!!");
            DebugLogger.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            DebugLogger.Log(ex?.ToString() ?? "Unknown exception object");
            DebugLogger.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        };

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            File.WriteAllText("AVALONIA_FATAL_ERROR.txt", e.ToString());
        }
    }
    
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
