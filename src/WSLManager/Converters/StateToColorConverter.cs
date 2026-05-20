namespace WSLManager.Converters;

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

public sealed class StateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string state)
        {
            return state.Equals("Running", StringComparison.OrdinalIgnoreCase)
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
                : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
