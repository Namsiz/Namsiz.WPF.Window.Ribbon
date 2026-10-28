using System.Windows;
using System.Windows.Media;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 功能区控件的附加属性：让 WPF 内置控件（<see cref="System.Windows.Controls.Button"/>、
    /// <see cref="System.Windows.Controls.TextBox"/> 等）不必换成自定义类型，也能表达 Ribbon 需要的信息。
    ///
    /// 用法：
    /// <code>
    /// &lt;Button Content="新增图纸" ribbon:RibbonProperties.Icon="/Assets/xxx.png" Command="{Binding AddCommand}" /&gt;
    /// &lt;TextBox Width="170" ribbon:RibbonProperties.Header="成品编码" Text="{Binding Code}" /&gt;
    /// </code>
    /// 图标统一只有一个（不再区分大小图标），按钮外观由功能区内的隐式样式自动呈现。
    /// </summary>
    public static class RibbonProperties
    {
        /// <summary>控件图标（唯一图标，不再区分 LargeIcon）。</summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.RegisterAttached(
                "Icon",
                typeof(ImageSource),
                typeof(RibbonProperties),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>获取控件图标。</summary>
        public static ImageSource? GetIcon(DependencyObject element)
            => element == null ? null : (ImageSource?)element.GetValue(IconProperty);

        /// <summary>设置控件图标。</summary>
        public static void SetIcon(DependencyObject element, ImageSource? value)
            => element?.SetValue(IconProperty, value);

        /// <summary>编辑类控件的标题文本（显示在控件上方）。</summary>
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.RegisterAttached(
                "Header",
                typeof(object),
                typeof(RibbonProperties),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>获取编辑类控件的标题文本。</summary>
        public static object? GetHeader(DependencyObject element)
            => element == null ? null : element.GetValue(HeaderProperty);

        /// <summary>设置编辑类控件的标题文本。</summary>
        public static void SetHeader(DependencyObject element, object? value)
            => element?.SetValue(HeaderProperty, value);
    }
}
