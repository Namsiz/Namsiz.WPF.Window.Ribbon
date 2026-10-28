using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 日志 Tab 的内容区（由 <see cref="LogTabItem"/> 内部创建）：
    /// 结构化日志列表（级别着色、自动滚动、Ctrl+C 复制、双击行文本选择），默认样式见 Themes/LogTabItem.xaml。
    /// 其 DataContext 为所属的 <see cref="LogTabItem"/>，模板内绑定直接指向 Tab 的属性。
    /// </summary>
    public class LogTabContent : Control
    {
        static LogTabContent()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(LogTabContent),
                new FrameworkPropertyMetadata(typeof(LogTabContent)));
        }
    }
}
