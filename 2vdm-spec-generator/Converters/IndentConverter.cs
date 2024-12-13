using System.Globalization;
using Microsoft.Maui.Controls;

namespace _2vdm_spec_generator.Converters
{
    public class IndentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int indentLevel)
            {
                return new Thickness(20 * indentLevel, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}