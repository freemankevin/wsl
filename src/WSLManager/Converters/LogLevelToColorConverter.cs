namespace WSLManager.Converters;

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WSLManager.Models;

public sealed class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Error   => new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)),
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(0xF1, 0xFA, 0x8C)),
                LogLevel.Success => new SolidColorBrush(Color.FromRgb(0x50, 0xFA, 0x7B)),
                LogLevel.Debug   => new SolidColorBrush(Color.FromRgb(0x8B, 0xE9, 0xFD)),
                _                => new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF2)),
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
