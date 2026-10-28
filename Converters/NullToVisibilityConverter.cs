using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Namsiz.WPF.Window.Ribbon.Converters
{
    /// <summary>
    /// “有值就显示、没值就折叠”的转换器：功能区按钮靠它决定图标是否占位
    /// （图标只有一个附加属性，没设置时按钮自动变成纯文字按钮）。
    /// </summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null || value == DependencyProperty.UnsetValue
                ? Visibility.Collapsed
                : Visibility.Visible;

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
