using System.Globalization;
using System.Windows.Data;

namespace Cam4TrackingMonitor.Converters;

public sealed class NullToNaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() ?? "N/A";
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
