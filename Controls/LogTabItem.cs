using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 日志 TabItem（库内置）：内容区为结构化日志列表，标题默认为“📋日志”。
    /// 由 <see cref="RibbonWindow"/> 创建并置于内容区首位，无需使用方搭建。
    /// </summary>
    public class LogTabItem : TabItem
    {
        public LogTabItem()
        {
            Header = "📋日志";

            // 内容区由库提供（TabControl 只呈现选中项的内容，故日志列表放在 Content 中）
            Content = new LogTabContent { DataContext = this };
        }

        /// <summary>日志数据源（通常为 <c>RibbonWindow.LogItems</c>）。</summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(LogTabItem),
                new PropertyMetadata(null));

        /// <summary>日志数据源（通常为 <c>RibbonWindow.LogItems</c>）。</summary>
        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
    }
}
