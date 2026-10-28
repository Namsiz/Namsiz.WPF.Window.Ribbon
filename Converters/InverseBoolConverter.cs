using System;
using System.Globalization;
using System.Windows.Data;

namespace Namsiz.WPF.Window.Ribbon.Converters
{
    /// <summary>布尔取反转换器（如把 <c>IsBusy</c> 反过来当 <c>IsEnabled</c> 用）。</summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public sealed class InverseBoolConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool flag ? !flag : true;

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool flag ? !flag : false;
    }
}
