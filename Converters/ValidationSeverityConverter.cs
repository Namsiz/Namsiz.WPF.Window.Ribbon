using System;
using System.Globalization;
using System.Windows.Data;

namespace Namsiz.WPF.Window.Ribbon.Converters
{
    /// <summary>
    /// 把“校验”列渲染值（"OK" / "E:xxx" / "W:xxx"）归一化为行底色分类：
    /// OK → "OK"（浅绿）；以 "E:" 开头 → "Error"（浅红）；以 "W:" 开头 → "Warning"（浅橙）；其余 → "None"。
    /// 仅用于行级 DataTrigger 绑定，不影响原单元格文本。
    /// </summary>
    public class ValidationSeverityConverter : IValueConverter
    {
        /// <summary>校验通过（无标记）的分类值。</summary>
        public const string Ok = "OK";

        /// <summary>错误分类值。</summary>
        public const string Error = "Error";

        /// <summary>警告分类值。</summary>
        public const string Warning = "Warning";

        /// <summary>未知分类值（不着色）。</summary>
        public const string None = "None";

        /// <summary>把校验标记文本归一化为 <see cref="Ok"/> / <see cref="Error"/> / <see cref="Warning"/> / <see cref="None"/>。</summary>
        public static string Resolve(object? value)
        {
            var text = value?.ToString();
            if (string.IsNullOrEmpty(text) || string.Equals(text, Ok, StringComparison.OrdinalIgnoreCase))
                return Ok;

            // 例如 "W:启动时间早于基线标准时间" / "E:机械基线未定义"
            if (text!.StartsWith("E:", StringComparison.Ordinal))
                return Error;
            if (text.StartsWith("W:", StringComparison.Ordinal))
                return Warning;

            return None;
        }

        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Resolve(value);

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
