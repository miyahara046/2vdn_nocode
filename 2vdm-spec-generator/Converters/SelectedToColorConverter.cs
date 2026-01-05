using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using _2vdm_spec_generator.ViewModel;

namespace _2vdm_spec_generator.ViewModel
{
    public class SelectedToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var folderItem = value as FolderItem;
            NoCodePageViewModel vm = null;

            // parameter が ViewModel の場合
            if (parameter is NoCodePageViewModel pvm)
            {
                vm = pvm;
            }
            else if (parameter is VisualElement ve)
            {
                // parameter に Page（ContentPage）などが渡されている場合は BindingContext を試す
                vm = ve.BindingContext as NoCodePageViewModel;
            }
            else
            {
                // フォールバック: Application.Current.MainPage の BindingContext を試す
                vm = Application.Current?.MainPage?.BindingContext as NoCodePageViewModel;
            }

            if (folderItem == null || vm == null)
                return Colors.Transparent;

            var selected = vm.SelectedItem;

            // 参照一致または FullPath による一致で選択を判定
            bool isSelected = false;
            if (ReferenceEquals(folderItem, selected))
                isSelected = true;
            else if (selected != null &&
                     !string.IsNullOrWhiteSpace(folderItem.FullPath) &&
                     !string.IsNullOrWhiteSpace(selected.FullPath) &&
                     string.Equals(folderItem.FullPath.Trim(), selected.FullPath.Trim(), StringComparison.OrdinalIgnoreCase))
                isSelected = true;

            if (isSelected)
            {
                // 視認性の高い背景色を返す（テキストは既定のまま黒で良い）
                return Colors.MediumPurple;
            }

            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
