using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cam4TrackingMonitor.Converters;

public sealed class ValuesDifferConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is null || values.Any(value => ReferenceEquals(value, DependencyProperty.UnsetValue)))
            return targetType == typeof(Visibility) ? Visibility.Collapsed : false;
        var differs = !string.Equals(values[0]?.ToString(), values[1]?.ToString(), StringComparison.OrdinalIgnoreCase);
        return targetType == typeof(Visibility) ? (differs ? Visibility.Visible : Visibility.Collapsed) : differs;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
