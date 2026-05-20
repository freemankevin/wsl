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

            TrayIcon.ContextMenu = BuildTrayContextMenu();

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
        // Allow window to close normally; tray icon is disposed in OnExit
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

    private static System.Windows.Controls.ContextMenu BuildTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x2D)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)),
            BorderThickness = new System.Windows.Thickness(1),
            Padding = new System.Windows.Thickness(6),
        };

        var itemStyle = new System.Windows.Style(typeof(System.Windows.Controls.MenuItem));
        itemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.MenuItem.ForegroundProperty, System.Windows.Media.Brushes.White));
        itemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.MenuItem.FontSizeProperty, 13.0));
        itemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.MenuItem.PaddingProperty, new System.Windows.Thickness(10, 8, 10, 8)));
        itemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.MenuItem.HorizontalContentAlignmentProperty, System.Windows.HorizontalAlignment.Stretch));
        var hoverTrigger = new System.Windows.Trigger
        {
            Property = System.Windows.UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.MenuItem.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2))));
        hoverTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.MenuItem.ForegroundProperty, System.Windows.Media.Brushes.White));
        itemStyle.Triggers.Add(hoverTrigger);
        menu.Resources[typeof(System.Windows.Controls.MenuItem)] = itemStyle;

        var sepStyle = new System.Windows.Style(typeof(System.Windows.Controls.Separator));
        sepStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Separator.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44))));
        sepStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Separator.HeightProperty, 1.0));
        sepStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Separator.MarginProperty, new System.Windows.Thickness(6, 4, 6, 4)));
        menu.Resources[typeof(System.Windows.Controls.Separator)] = sepStyle;

        System.Windows.Controls.MenuItem CreateItem(string header, MaterialDesignThemes.Wpf.PackIconKind iconKind, Action action)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = header,
                Icon = new MaterialDesignThemes.Wpf.PackIcon
                {
                    Kind = iconKind,
                    Width = 16,
                    Height = 16,
                    Foreground = System.Windows.Media.Brushes.White,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };
            item.Click += (_, _) => action();
            return item;
        }

        menu.Items.Add(CreateItem(LocalizationService.Instance["TrayMenuShow"], MaterialDesignThemes.Wpf.PackIconKind.Application, ShowMainWindow));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(CreateItem(LocalizationService.Instance["TrayMenuExit"], MaterialDesignThemes.Wpf.PackIconKind.ExitToApp, () => Current.Shutdown()));

        return menu;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IWslService, WslService>();
        services.AddSingleton<IResourceMonitorService, ResourceMonitorService>();
        services.AddSingleton<MainViewModel>();
    }
}
