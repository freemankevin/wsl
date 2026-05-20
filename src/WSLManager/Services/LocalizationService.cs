using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace WSLManager.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private readonly ResourceManager _resourceManager = new("WSLManager.Resources.Strings", typeof(LocalizationService).Assembly);
    private CultureInfo _currentCulture = new("en-US");

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value)) return;
            _currentCulture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.CurrentUICulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public string this[string key] => _resourceManager.GetString(key, _currentCulture) ?? key;

    public void SetLanguage(string languageCode)
    {
        CurrentCulture = new CultureInfo(languageCode);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
