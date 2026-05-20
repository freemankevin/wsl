namespace WSLManager.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SkiaSharp;
using WSLManager.Core.Models;
using WSLManager.Core.Services;
using WSLManager.Models;
using WSLManager.Services;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IWslService _wslService;
    private readonly IResourceMonitorService _resourceMonitor;
    private readonly ISettingsService _settingsService;
    private AppSettings _settings = App.Settings;

    public ObservableCollection<DistributionViewModel> Distributions { get; } = new();
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public string LogCountText => string.Format(LocalizationService.Instance["LogCountFormat"], LogEntries.Count);

    [ObservableProperty]
    private DistributionViewModel? _selectedDistribution;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private bool _autoRefresh = true;

    [ObservableProperty]
    private string _refreshInterval = "2s";

    [ObservableProperty]
    private string _serviceStatus = "Unknown";

    [ObservableProperty]
    private bool _isServiceRunning;

    [ObservableProperty]
    private bool _hasChartData;

    // LiveCharts2 series
    public ObservableCollection<ISeries> CpuSeries { get; } = new();
    public ObservableCollection<ISeries> MemorySeries { get; } = new();

    public ObservableCollection<Axis> XAxes { get; } = new()
    {
        new Axis
        {
            Labeler = value => TimeSpan.FromSeconds(value).ToString("mm\\:ss"),
            LabelsRotation = 0,
            TextSize = 10,
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
        }
    };

    public ObservableCollection<Axis> CpuYAxes { get; } = new()
    {
        new Axis
        {
            TextSize = 10,
            Name = "CPU %",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
        }
    };

    public ObservableCollection<Axis> MemoryYAxes { get; } = new()
    {
        new Axis
        {
            TextSize = 10,
            Name = "Mem (MB)",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
        }
    };

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ShutdownCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand UnregisterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand StartServiceCommand { get; }
    public ICommand StopServiceCommand { get; }
    public ICommand RestartServiceCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ShowShortcutsCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand StartDistCommand { get; }
    public ICommand StopDistCommand { get; }
    public ICommand RestartDistCommand { get; }
    public ICommand DeleteDistCommand { get; }
    public ICommand CopyLogCommand { get; }
    public ICommand ClearLogCommand { get; }

    private readonly ObservableCollection<ObservableValue> _cpuValues = new();
    private readonly ObservableCollection<ObservableValue> _memValues = new();
    private readonly System.Timers.Timer _refreshTimer;
    private int _timeCounter = 0;

    public MainViewModel()
    {
        _wslService = App.Services.GetRequiredService<IWslService>();
        _resourceMonitor = App.Services.GetRequiredService<IResourceMonitorService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();

        LoadSettings();

        StartCommand = new AsyncRelayCommand(StartAsync, () => SelectedDistribution?.IsRunning == false);
        StopCommand = new AsyncRelayCommand(StopAsync, () => SelectedDistribution?.IsRunning == true);
        ShutdownCommand = new AsyncRelayCommand(ShutdownAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => SelectedDistribution != null);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        UnregisterCommand = new AsyncRelayCommand(UnregisterAsync, () => SelectedDistribution != null);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        StartServiceCommand = new AsyncRelayCommand(StartServiceAsync);
        StopServiceCommand = new AsyncRelayCommand(StopServiceAsync);
        RestartServiceCommand = new AsyncRelayCommand(RestartServiceAsync);
        ToggleLanguageCommand = new RelayCommand(ToggleLanguage);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        ShowShortcutsCommand = new RelayCommand(ShowShortcuts);
        RestartCommand = new AsyncRelayCommand(RestartAsync, () => SelectedDistribution?.IsRunning == true);
        StartDistCommand = new AsyncRelayCommand<DistributionViewModel>(StartDistAsync);
        StopDistCommand = new AsyncRelayCommand<DistributionViewModel>(StopDistAsync);
        RestartDistCommand = new AsyncRelayCommand<DistributionViewModel>(RestartDistAsync);
        DeleteDistCommand = new AsyncRelayCommand<DistributionViewModel>(DeleteDistAsync);
        CopyLogCommand = new RelayCommand(CopyLog);
        ClearLogCommand = new RelayCommand(ClearLog);

        // Setup chart series
        CpuSeries.Add(new LineSeries<ObservableValue>
        {
            Values = _cpuValues,
            Name = "CPU %",
            Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(30)),
            GeometrySize = 0,
            LineSmoothness = 0.5
        });

        MemorySeries.Add(new LineSeries<ObservableValue>
        {
            Values = _memValues,
            Name = "Mem (MB)",
            Stroke = new SolidColorPaint(SKColors.OrangeRed) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(30)),
            GeometrySize = 0,
            LineSmoothness = 0.5
        });

        _refreshTimer = new System.Timers.Timer(2000);
        _refreshTimer.Elapsed += async (_, _) => await RefreshAsync();
        _refreshTimer.AutoReset = true;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!await _wslService.IsWslInstalledAsync())
        {
            StatusText = $"Error: {LocalizationService.Instance["ErrorWslNotInstalled"]}";
            Log($"[Error] {LocalizationService.Instance["ErrorWslNotInstalled"]}");
            return;
        }

        await RefreshServiceStatusAsync();
        Log($"[Init] {LocalizationService.Instance["LogInit"]}");
        await RefreshAsync();
        _refreshTimer.Start();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var list = await _wslService.GetDistributionsAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Distributions.Clear();
                foreach (var d in list)
                {
                    Distributions.Add(new DistributionViewModel(d));
                }
                StatusText = string.Format(LocalizationService.Instance["StatusCount"], list.Count);
            });

            // Query per-distro resources for running instances
            foreach (var distro in list.Where(d => d.IsRunning))
            {
                try
                {
                    var (cpu, mem, diag) = await _wslService.GetDistributionResourceAsync(distro.Name);
                    distro.CpuUsagePercent = cpu;
                    distro.MemoryUsageMB = mem;
                    if (!string.IsNullOrWhiteSpace(diag))
                    {
                        Log($"[Diag-{distro.Name}]\n{diag}");
                    }
                }
                catch { /* ignore per-distro query errors */ }
            }

            // Update charts based on running distributions
            await UpdateChartAsync();
        }
        catch (Exception ex)
        {
            Log($"[Error] {string.Format(LocalizationService.Instance["ErrorRefreshFailed"], ex.Message)}");
        }
    }

    private Task UpdateChartAsync()
    {
        var running = Distributions.Where(d => d.IsRunning).ToList();
        if (running.Count == 0) return Task.CompletedTask;

        double totalMem = running.Sum(d => d.MemoryUsageMB);
        double avgCpu = running.Count > 0 ? running.Average(d => d.CpuUsagePercent) : 0;

        _timeCounter++;
        HasChartData = true;
        Application.Current.Dispatcher.Invoke(() =>
        {
            _cpuValues.Add(new ObservableValue(avgCpu));
            _memValues.Add(new ObservableValue(totalMem));
            if (_cpuValues.Count > 60) _cpuValues.RemoveAt(0);
            if (_memValues.Count > 60) _memValues.RemoveAt(0);
        });

        return Task.CompletedTask;
    }

    private async Task RefreshServiceStatusAsync()
    {
        var status = await _wslService.GetLxssManagerStatusAsync();
        ServiceStatus = status ?? "Unknown";
        IsServiceRunning = status?.Equals("Running", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private async Task StartAsync()
    {
        var distro = SelectedDistribution;
        if (distro == null) return;
        Log($"[Start] {string.Format(LocalizationService.Instance["LogStart"], distro.Name)}");
        var ok = await _wslService.StartDistributionAsync(distro.Name);
        if (ok)
        {
            Log($"[OK] {string.Format(LocalizationService.Instance["LogStartSuccess"], distro.Name)}");
        }
        else
        {
            Log($"[Fail] {string.Format(LocalizationService.Instance["LogStartFail"], distro.Name)}");
            var detail = await _wslService.ExecuteAsync($"--distribution {distro.Name} -- /bin/true");
            if (!string.IsNullOrWhiteSpace(detail.StdErr)) Log($"[Detail] {detail.StdErr.Trim()}");
        }
        await RefreshAsync();
    }

    private async Task StopAsync()
    {
        var distro = SelectedDistribution;
        if (distro == null) return;
        Log($"[Stop] {string.Format(LocalizationService.Instance["LogStop"], distro.Name)}");
        try
        {
            var ok = await _wslService.StopDistributionAsync(distro.Name);
            if (ok)
            {
                Log($"[OK] {string.Format(LocalizationService.Instance["LogStopSuccess"], distro.Name)}");
            }
            else
            {
                Log($"[Fail] {string.Format(LocalizationService.Instance["LogStopFail"], distro.Name)}");
                var detail = await _wslService.ExecuteAsync($"--terminate {distro.Name}");
                if (!string.IsNullOrWhiteSpace(detail.StdErr)) Log($"[Detail] {detail.StdErr.Trim()}");
            }
        }
        catch (Exception ex)
        {
            Log($"[Error] {string.Format(LocalizationService.Instance["ErrorStopException"], ex.Message)}");
        }
        await RefreshAsync();
    }

    private async Task RestartAsync()
    {
        var distro = SelectedDistribution;
        if (distro == null) return;
        Log($"[Restart] {string.Format(LocalizationService.Instance["LogRestart"], distro.Name)}");
        try
        {
            var stopped = await _wslService.StopDistributionAsync(distro.Name);
            if (!stopped)
            {
                Log($"[Fail] {string.Format(LocalizationService.Instance["LogRestartStopFail"], distro.Name)}");
                await RefreshAsync();
                return;
            }
            Log($"[Restart] {LocalizationService.Instance["LogRestartWait"]}");
            await Task.Delay(1000);
            var started = await _wslService.StartDistributionAsync(distro.Name);
            Log(started ? $"[OK] {string.Format(LocalizationService.Instance["LogRestartSuccess"], distro.Name)}" : $"[Fail] {string.Format(LocalizationService.Instance["LogRestartFail"], distro.Name)}");
        }
        catch (Exception ex)
        {
            Log($"[Error] {string.Format(LocalizationService.Instance["ErrorRestartException"], ex.Message)}");
        }
        await RefreshAsync();
    }

    private async Task StartDistAsync(DistributionViewModel? distro)
    {
        if (distro == null) return;
        SelectedDistribution = distro;
        await StartAsync();
    }

    private async Task StopDistAsync(DistributionViewModel? distro)
    {
        if (distro == null) return;
        SelectedDistribution = distro;
        await StopAsync();
    }

    private async Task RestartDistAsync(DistributionViewModel? distro)
    {
        if (distro == null) return;
        SelectedDistribution = distro;
        await RestartAsync();
    }

    private async Task DeleteDistAsync(DistributionViewModel? distro)
    {
        if (distro == null) return;
        SelectedDistribution = distro;

        // If running, stop first
        if (distro.IsRunning)
        {
            Log($"[Delete] {string.Format(LocalizationService.Instance["LogDeleteStopFirst"], distro.Name)}");
            var stopped = await _wslService.StopDistributionAsync(distro.Name);
            if (!stopped)
            {
                Log($"[Fail] {string.Format(LocalizationService.Instance["LogDeleteStopFail"], distro.Name)}");
                await RefreshAsync();
                return;
            }
            Log($"[Delete] {string.Format(LocalizationService.Instance["LogDeleteStopped"], distro.Name)}");
        }

        var result = MessageBox.Show(string.Format(LocalizationService.Instance["ConfirmDelete"], distro.Name), LocalizationService.Instance["ConfirmDeleteTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        Log($"[Delete] {string.Format(LocalizationService.Instance["LogDelete"], distro.Name)}");
        var ok = await _wslService.UnregisterDistributionAsync(distro.Name);
        Log(ok ? $"[OK] {string.Format(LocalizationService.Instance["LogDeleteSuccess"], distro.Name)}" : $"[Fail] {string.Format(LocalizationService.Instance["LogDeleteFail"], distro.Name)}");
        await RefreshAsync();
    }

    private async Task ShutdownAsync()
    {
        var result = MessageBox.Show(LocalizationService.Instance["ConfirmShutdown"], LocalizationService.Instance["ConfirmShutdownTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        Log("[Shutdown] WSL --shutdown ...");
        var ok = await _wslService.ShutdownAsync();
        Log(ok ? "[OK] WSL shutdown complete" : "[Fail] WSL shutdown failed");
        await RefreshAsync();
    }

    private async Task ExportAsync()
    {
        var distro = SelectedDistribution;
        if (distro == null) return;
        var dlg = new SaveFileDialog { Filter = "TAR (*.tar)|*.tar", FileName = $"{distro.Name}.tar" };
        if (dlg.ShowDialog() != true) return;
        Log($"[Export] {distro.Name} -> {dlg.FileName} ...");
        var ok = await _wslService.ExportDistributionAsync(distro.Name, dlg.FileName);
        Log(ok ? "[OK] Export complete" : "[Fail] Export failed");
    }

    private async Task ImportAsync()
    {
        var dlg = new OpenFileDialog { Filter = "TAR (*.tar)|*.tar" };
        if (dlg.ShowDialog() != true) return;
        var name = PromptForInput(LocalizationService.Instance["DialogImportTitle"], LocalizationService.Instance["DialogImportName"], "ImportedDistro");
        if (string.IsNullOrWhiteSpace(name)) return;
        var folder = new OpenFolderDialog { Title = LocalizationService.Instance["DialogInstallLocation"] };
        if (folder.ShowDialog() != true) return;
        Log($"[Import] {name} <- {dlg.FileName} ...");
        var ok = await _wslService.ImportDistributionAsync(name, folder.FolderName, dlg.FileName);
        Log(ok ? "[OK] Import complete" : "[Fail] Import failed");
        await RefreshAsync();
    }

    private async Task UnregisterAsync()
    {
        var distro = SelectedDistribution;
        if (distro == null) return;
        var result = MessageBox.Show(string.Format(LocalizationService.Instance["ConfirmUnregister"], distro.Name), LocalizationService.Instance["ConfirmUnregisterTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        Log($"[Unregister] {distro.Name} ...");
        var ok = await _wslService.UnregisterDistributionAsync(distro.Name);
        Log(ok ? $"[OK] {distro.Name} unregistered" : "[Fail] Unregister failed");
        await RefreshAsync();
    }

    private async Task StartServiceAsync()
    {
        Log("[Service] Start LxssManager ...");
        var ok = await _wslService.StartLxssManagerAsync();
        Log(ok ? "[OK] Service started" : "[Fail] Start service failed");
        await RefreshServiceStatusAsync();
    }

    private async Task StopServiceAsync()
    {
        Log("[Service] Stop LxssManager ...");
        var ok = await _wslService.StopLxssManagerAsync();
        Log(ok ? "[OK] Service stopped" : "[Fail] Stop service failed");
        await RefreshServiceStatusAsync();
    }

    private async Task RestartServiceAsync()
    {
        Log("[Service] Restart LxssManager ...");
        var stopped = await _wslService.StopLxssManagerAsync();
        if (!stopped)
        {
            Log("[Fail] Stop service failed, cannot restart");
            await RefreshServiceStatusAsync();
            return;
        }
        var started = await _wslService.StartLxssManagerAsync();
        Log(started ? "[OK] Service restarted" : "[Fail] Start service failed");
        await RefreshServiceStatusAsync();
    }


    private void ToggleLanguage()
    {
        App.ToggleLanguage();
        OnPropertyChanged(nameof(LanguageText));
        Log($"[Lang] {LanguageText}");
        OnPropertyChanged(string.Empty);
    }

    private static void ShowShortcuts()
    {
        var title = LocalizationService.Instance["ShortcutsTitle"];
        var global = LocalizationService.Instance["ShortcutsGlobal"];
        var list = LocalizationService.Instance["ShortcutsList"];

        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5)),
        };

        var root = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Title
        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x21, 0x21, 0x21)),
            Margin = new Thickness(0, 0, 0, 16)
        };
        System.Windows.Controls.Grid.SetRow(titleBlock, 0);
        root.Children.Add(titleBlock);

        // Scrollable content
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var content = new System.Windows.Controls.StackPanel();
        scroll.Content = content;
        System.Windows.Controls.Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        void AddSection(string sectionTitle, string[][] rows)
        {
            var sectionHeader = new System.Windows.Controls.TextBlock
            {
                Text = sectionTitle,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2)),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            content.Children.Add(sectionHeader);

            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < rows.Length; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var bg = i % 2 == 0
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA));

                var keyBorder = new Border
                {
                    Background = bg,
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var keyText = new System.Windows.Controls.TextBlock
                {
                    Text = rows[i][0],
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, JetBrains Mono, monospace"),
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                    FontWeight = FontWeights.SemiBold
                };
                keyBorder.Child = keyText;
                System.Windows.Controls.Grid.SetRow(keyBorder, i);
                System.Windows.Controls.Grid.SetColumn(keyBorder, 0);
                grid.Children.Add(keyBorder);

                var descBorder = new Border
                {
                    Background = bg,
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var descText = new System.Windows.Controls.TextBlock
                {
                    Text = rows[i][1],
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55))
                };
                descBorder.Child = descText;
                System.Windows.Controls.Grid.SetRow(descBorder, i);
                System.Windows.Controls.Grid.SetColumn(descBorder, 1);
                grid.Children.Add(descBorder);
            }
            content.Children.Add(grid);
        }

        AddSection(global, new[]
        {
            new[] { "Ctrl + W", LocalizationService.Instance["ShortcutExit"] },
            new[] { "F5", LocalizationService.Instance["ShortcutRefresh"] },
            new[] { "Ctrl + L", LocalizationService.Instance["ShortcutLanguage"] },
            new[] { "F1", LocalizationService.Instance["ShortcutHelp"] },
        });

        AddSection(list, new[]
        {
            new[] { "Enter", LocalizationService.Instance["ShortcutStart"] },
            new[] { "Delete", LocalizationService.Instance["ShortcutDelete"] },
        });

        // OK button
        var btnOk = new System.Windows.Controls.Button
        {
            Content = LocalizationService.Instance["ButtonOK"],
            Width = 80,
            Height = 32,
            IsDefault = true
        };
        btnOk.Click += (_, _) => win.Close();
        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        btnPanel.Children.Add(btnOk);
        System.Windows.Controls.Grid.SetRow(btnPanel, 2);
        root.Children.Add(btnPanel);

        win.Content = root;
        win.ShowDialog();
    }

    public string LanguageText => LocalizationService.Instance.CurrentCulture.Name.StartsWith("zh")
        ? LocalizationService.Instance["LangChinese"]
        : LocalizationService.Instance["LangEnglish"];

    partial void OnSelectedDistributionChanged(DistributionViewModel? value)
    {
        ((AsyncRelayCommand)StartCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)StopCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RestartCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)ExportCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)UnregisterCommand).NotifyCanExecuteChanged();
    }

    private void LoadSettings()
    {
        _settings = App.Settings;
        AutoRefresh = _settings.AutoRefresh;
        RefreshInterval = _settings.RefreshIntervalSeconds switch
        {
            1 => "1s",
            2 => "2s",
            5 => "5s",
            10 => "10s",
            _ => "2s"
        };
    }

    private void SaveSettings()
    {
        _settings.AutoRefresh = AutoRefresh;
        _settings.RefreshIntervalSeconds = RefreshInterval switch
        {
            "1s" => 1,
            "2s" => 2,
            "5s" => 5,
            "10s" => 10,
            _ => 2
        };
    }

    partial void OnAutoRefreshChanged(bool value)
    {
        if (value)
            _refreshTimer.Start();
        else
            _refreshTimer.Stop();
        SaveSettings();
    }

    partial void OnRefreshIntervalChanged(string value)
    {
        var ms = value switch
        {
            "1s" => 1000,
            "2s" => 2000,
            "5s" => 5000,
            "10s" => 10000,
            _ => 2000
        };
        _refreshTimer.Interval = ms;
        SaveSettings();
    }

    private void Log(string message)
    {
        var level = LogLevel.Info;
        if (message.Contains("[Error]") || message.Contains("[Fail]")) level = LogLevel.Error;
        else if (message.Contains("[Warn]")) level = LogLevel.Warning;
        else if (message.Contains("[OK]")) level = LogLevel.Success;
        else if (message.Contains("[Diag]") || message.Contains("[Debug]")) level = LogLevel.Debug;

        var entry = new LogEntry(DateTime.Now, level, message);
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry);
            LogText += $"[{entry.Timestamp:HH:mm:ss}] {message}\r\n";
            OnPropertyChanged(nameof(LogCountText));
        });
    }

    private void CopyLog()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var entry in LogEntries)
        {
            sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] {entry.Message}");
        }
        Clipboard.SetText(sb.ToString());
    }

    private void ClearLog()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Clear();
            LogText = string.Empty;
            OnPropertyChanged(nameof(LogCountText));
        });
    }

    public void Dispose()
    {
        SaveSettings();
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }

    private static string? PromptForInput(string title, string message, string defaultValue)
    {
        var win = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White
        };
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var lbl = new System.Windows.Controls.TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8) };
        System.Windows.Controls.Grid.SetRow(lbl, 0);
        var txt = new System.Windows.Controls.TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 16) };
        System.Windows.Controls.Grid.SetRow(txt, 1);
        var btnPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        System.Windows.Controls.Grid.SetRow(btnPanel, 2);
        string? result = null;
        var btnOk = new System.Windows.Controls.Button { Content = LocalizationService.Instance["ButtonOK"], Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        btnOk.Click += (_, _) => { result = txt.Text; win.DialogResult = true; win.Close(); };
        var btnCancel = new System.Windows.Controls.Button { Content = LocalizationService.Instance["ButtonCancel"], Width = 80, IsCancel = true };
        btnCancel.Click += (_, _) => { win.DialogResult = false; win.Close(); };
        btnPanel.Children.Add(btnOk);
        btnPanel.Children.Add(btnCancel);
        grid.Children.Add(lbl);
        grid.Children.Add(txt);
        grid.Children.Add(btnPanel);
        win.Content = grid;
        win.ShowDialog();
        return result;
    }
}