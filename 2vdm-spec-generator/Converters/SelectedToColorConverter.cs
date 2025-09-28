using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace _2vdm_spec_generator.ViewModel
{
    public class SelectedToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var folderItem = value as FolderItem;
            var vm = parameter as NoCodePageViewModel;

            if (folderItem == null || vm == null)
                return Colors.Transparent;

            return folderItem == vm.SelectedItem ? Colors.LightGray : Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
