using System.Globalization;
using Microsoft.Maui.Controls;

namespace _2vdm_spec_generator.Converters
{
    public class IndentConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is string fullPath && values[1] is string rootPath)
            {
                // パスの区切り文字でスプリットして階層の深さを計算
                // ルートパスからの相対パスを計算してインデントを決定
                var relativePath = fullPath.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar);
                var depth = relativePath.Count(c => c == Path.DirectorySeparatorChar);
                return new Thickness(20 * depth, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}