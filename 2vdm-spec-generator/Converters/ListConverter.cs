using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace _2vdm_spec_generator.ViewModel
{
    public class LevelToIndentConverter : IValueConverter
    {
        private const double IndentSize = 20;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
                return new Thickness(level * IndentSize, 0, 0, 0);
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToExpandCollapseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "▾" : "▸";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
