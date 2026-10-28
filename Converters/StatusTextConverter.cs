using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Namsiz.WPF.Window.Ribbon.Converters
{
    /// <summary>
    /// 状态栏文本选择器：优先取选中 Tab 的状态文本（如数据表格的“N 行 × M 列”），
    /// 无则回退到窗口的空闲文本；绑定失败（选中日志 Tab 等）按空值处理。
    /// </summary>
    public class StatusTextConverter : IMultiValueConverter
    {
        /// <inheritdoc />
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is string text && !string.IsNullOrEmpty(text))
                    return text;
            }

            return string.Empty;
        }

        /// <inheritdoc />
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
