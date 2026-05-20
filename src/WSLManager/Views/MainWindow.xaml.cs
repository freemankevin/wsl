namespace WSLManager.Views;

using System.Windows;
using WSLManager.Core.Models;
using WSLManager.ViewModels;

public partial class MainWindow : Window
{
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = App.Settings;

        Loaded += OnLoaded;
        Closing += OnWindowClosing;
        SizeChanged += OnSizeChanged;
        LocationChanged += OnLocationChanged;
    }

    private void OnDataGridPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.SelectedDistribution == null) return;

        switch (e.Key)
        {
            case System.Windows.Input.Key.Enter:
                e.Handled = true;
                if (vm.SelectedDistribution.CanStart)
                    vm.StartDistCommand.Execute(vm.SelectedDistribution);
                break;
            case System.Windows.Input.Key.Delete:
                e.Handled = true;
                vm.DeleteDistCommand.Execute(vm.SelectedDistribution);
                break;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Restore window state
        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        if (_settings.WindowWidth > 0) Width = _settings.WindowWidth;
        if (_settings.WindowHeight > 0) Height = _settings.WindowHeight;
        if (_settings.IsMaximized) WindowState = WindowState.Maximized;

        // Auto-scroll log to bottom when new entries are added
        if (DataContext is MainViewModel vm)
        {
            vm.LogEntries.CollectionChanged += (_, __) =>
            {
                LogScrollViewer.ScrollToEnd();
            };
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window geometry into shared settings
        _settings.IsMaximized = WindowState == WindowState.Maximized;
        if (WindowState != WindowState.Maximized)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = ActualWidth;
            _settings.WindowHeight = ActualHeight;
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (WindowState != WindowState.Maximized)
        {
            _settings.WindowWidth = ActualWidth;
            _settings.WindowHeight = ActualHeight;
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Maximized)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }
    }
}
