using System.Globalization;
using _2vdm_spec_generator.Models;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace _2vdm_spec_generator.Converters
{
    public class IconConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && 
                values[0] is FileSystemItem item &&
                values[1] is ObservableCollection<FileSystemItem> items)
            {
                if (item is FileItem)
                {
                    return "≡";
                }
                else if (item is DirectoryItem)
                {
                    // 現在のアイテムのインデックスを取得
                    var currentIndex = items.IndexOf(item);
                    if (currentIndex != -1 && currentIndex + 1 < items.Count)
                    {
                        // 次のアイテムがこのディレクトリの子要素かどうかを確認
                        var nextItem = items[currentIndex + 1];
                        if (nextItem.FullPath.StartsWith(item.FullPath + Path.DirectorySeparatorChar))
                        {
                            return "∨";
                        }
                    }
                    return ">";
                }
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}