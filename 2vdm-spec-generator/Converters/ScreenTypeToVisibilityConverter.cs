using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.Converters
{
    public class ScreenTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GuiElementType type)
            {
                return type == GuiElementType.Screen;
                return type == GuiElementType.Button;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
