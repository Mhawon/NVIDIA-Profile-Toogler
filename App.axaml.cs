using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Controls.Platform;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace NVIDIA_Profil_Toogler;

public partial class App : Application
{
    private AppSettings _settings = new();
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private DateTime _lastClickTime = DateTime.MinValue;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DebugLogger.Log("App: OnFrameworkInitializationCompleted started.");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            LoadConfiguration();
            DebugLogger.Log($"App: StartMinimized setting is '{_settings.StartMinimized}'.");

            _mainWindow = new MainWindow(_settings);
            desktop.MainWindow = _mainWindow;

            _trayIcon = new TrayIcon();
            _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://NVIDIA_Profil_Toogler/Assets/ico.ico")));
            _trayIcon.ToolTipText = "Nvidia Profile Toggle";

            // Create native menu for TrayIcon
            var nativeMenu = new NativeMenu();

            var showNativeMenuItem = new NativeMenuItem("Show");
            showNativeMenuItem.Click += (s, e) =>
            {
                if (_mainWindow != null)
                {
                    if (_mainWindow.IsVisible)
                    {
                        _mainWindow.Hide();
                    }
                    else
                    {
                        _mainWindow.WindowState = WindowState.Normal;
                        _mainWindow.Show();
                        _mainWindow.Activate();
                    }
                }
            };
            nativeMenu.Add(showNativeMenuItem);

            var exitNativeMenuItem = new NativeMenuItem("Exit");
            exitNativeMenuItem.Click += (s, e) => desktop.Shutdown();
            nativeMenu.Add(exitNativeMenuItem);

            _trayIcon.Menu = nativeMenu;
            _trayIcon.IsVisible = true;

            nativeMenu.Opening += (s, e) =>
            {
                if (_mainWindow != null)
                {
                    showNativeMenuItem.Header = _mainWindow.IsVisible ? "Hide" : "Show";
                }
            };

            _trayIcon.Clicked += (s, e) =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
        DebugLogger.Log("App: OnFrameworkInitializationCompleted finished.");
    }

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                DebugLogger.Log("App: Configuration loaded successfully for startup.");
            }
            else
            {
                _settings = new AppSettings();
                DebugLogger.Log("App: No config file found for startup, using default settings.");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ERROR: Failed to load config file at app startup: {ex.Message}. Using default settings.");
            _settings = new AppSettings();
        }
    }
}