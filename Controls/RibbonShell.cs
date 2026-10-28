using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 窗口内容区（由 <see cref="RibbonWindow"/> 内部创建）：Tab 控件 + 状态栏 + 忙碌遮罩。
    /// 模板内的绑定统一指向宿主 <see cref="RibbonWindow"/>（见 Themes/RibbonWindow.xaml），本类不持有状态。
    /// </summary>
    [TemplatePart(Name = PartTabs, Type = typeof(TabControl))]
    public class RibbonShell : Control
    {
        internal const string PartTabs = "PART_Tabs";

        static RibbonShell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(RibbonShell),
                new FrameworkPropertyMetadata(typeof(RibbonShell)));
        }
    }
}
