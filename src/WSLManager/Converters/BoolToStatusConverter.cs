namespace WSLManager.Converters;

using System.Globalization;
using System.Windows.Data;
using WSLManager.Services;

public sealed class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b
            ? LocalizationService.Instance["StatusYes"]
            : LocalizationService.Instance["StatusNo"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
