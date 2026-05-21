namespace WSLManager;

using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using WSLManager.Core.Models;
using WSLManager.Core.Services;
using WSLManager.Services;
using WSLManager.ViewModels;
using WSLManager.Views;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = new();
    public static TaskbarIcon? TrayIcon { get; private set; }

    private MainWindow? _mainWindow;
    private System.Windows.Controls.Primitives.Popup? _trayPopup;

    protected override void OnStartup(StartupEventArgs e)
    {
        SetupExceptionHandlers();

        // Default language: English
        LocalizationService.Instance.SetLanguage("en-US");

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        // Load global settings instance
        Settings = Services.GetRequiredService<ISettingsService>().Load();

        base.OnStartup(e);

        // Setup tray icon
        try
        {
            TrayIcon = new TaskbarIcon
            {
                Icon = LoadIconFromPng("Images/linux.png"),
                ToolTipText = LocalizationService.Instance["AppTitle"],
                Visibility = Visibility.Visible
            };

            TrayIcon.TrayRightMouseUp += (_, _) => ShowTrayPopup();
            TrayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray icon init failed: {ex.Message}");
        }

        // Create main window (DataContext must be set via DI, not XAML)
        _mainWindow = new MainWindow();
        _mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
        Current.MainWindow = _mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        try
        {
            _mainWindow.Icon = new BitmapImage(new Uri("pack://application:,,,/Images/linux.png"));
        }
        catch { }
        _mainWindow.Show();
    }

    private static System.Drawing.Icon LoadIconFromPng(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        using var bitmap = new System.Drawing.Bitmap(path);
        using var resized = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(16, 16));
        var hIcon = resized.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    private static void SetupExceptionHandlers()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
        void WriteCrash(string type, string detail)
        {
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}\n{detail}\n\n");
            }
            catch { }
        }

        Current.DispatcherUnhandledException += (s, e) =>
        {
            WriteCrash("DispatcherUnhandledException", e.Exception.ToString());
            MessageBox.Show($"Unhandled exception:\n{e.Exception.Message}\n\nRecorded to crash.log", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            WriteCrash("AppDomain.UnhandledException", ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "unknown");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            WriteCrash("TaskScheduler.UnobservedTaskException", e.Exception.ToString());
            e.SetObserved();
        };
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        _mainWindow!.Hide();
    }

    public static void ShowMainWindow()
    {
        if (Current.MainWindow is Views.MainWindow mw)
        {
            mw.Show();
            mw.WindowState = WindowState.Normal;
            mw.Activate();
        }
    }

    public static void ToggleLanguage()
    {
        var current = LocalizationService.Instance.CurrentCulture.Name;
        var next = current.StartsWith("zh") ? "en-US" : "zh-CN";
        LocalizationService.Instance.SetLanguage(next);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Services.GetRequiredService<ISettingsService>().Save(Settings);
        TrayIcon?.Dispose();
    }

    private void ShowTrayPopup()
    {
        if (_trayPopup != null)
            _trayPopup.IsOpen = false;

        var panel = new System.Windows.Controls.StackPanel();

        void AddItem(string text, Action action)
        {
            var border = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Padding = new System.Windows.Thickness(14, 10, 14, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                CornerRadius = new System.Windows.CornerRadius(4)
            };
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = text,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC)),
                FontSize = 12,
                TextAlignment = System.Windows.TextAlignment.Left
            };
            border.Child = tb;
            border.MouseEnter += (_, _) =>
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x2A));
            border.MouseLeave += (_, _) =>
                border.Background = System.Windows.Media.Brushes.Transparent;
            border.MouseLeftButtonUp += (_, _) =>
            {
                _trayPopup!.IsOpen = false;
                action();
            };
            panel.Children.Add(border);
        }

        AddItem(LocalizationService.Instance["TrayMenuShow"], ShowMainWindow);
        panel.Children.Add(new System.Windows.Controls.Separator
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
            Height = 1,
            Margin = new System.Windows.Thickness(0, 2, 0, 2)
        });
        AddItem(LocalizationService.Instance["TrayMenuExit"], () => Current.Shutdown());

        var innerPanel = new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x18)),
            CornerRadius = new System.Windows.CornerRadius(6),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(2),
            Width = 120,
            Child = panel,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromArgb(0x33, 0x00, 0x00, 0x00),
                Direction = 270,
                ShadowDepth = 2,
                BlurRadius = 8,
                Opacity = 0.4
            }
        };

        var root = new System.Windows.Controls.Border
        {
            Background = System.Windows.Media.Brushes.Transparent,
            Padding = new System.Windows.Thickness(4),
            Child = innerPanel
        };

        root.MouseLeave += (_, _) =>
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_trayPopup != null && !root.IsMouseOver)
                    _trayPopup.IsOpen = false;
            };
            timer.Start();
        };

        _trayPopup = new System.Windows.Controls.Primitives.Popup
        {
            Child = root,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
            StaysOpen = true,
            AllowsTransparency = true
        };
        _trayPopup.IsOpen = true;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IWslService, WslService>();
        services.AddSingleton<IResourceMonitorService, ResourceMonitorService>();
        services.AddSingleton<MainViewModel>();
    }
}
