using System.Globalization;
using System.Windows.Data;

namespace ChaosOrder.Converters
{
    // Subtracts a fixed offset (ConverterParameter) from a bound coordinate,
    // used to center a fixed-size drag handle on a point.
    public class OffsetConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double v = value is double d ? d : 0;
            double offset = parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
            return v - offset;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
