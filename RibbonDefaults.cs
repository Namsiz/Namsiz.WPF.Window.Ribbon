using Namsiz.WPF.Window.Ribbon.Controls;
using System;
using System.Windows;

namespace Namsiz.WPF.Window.Ribbon
{
    /// <summary>库自带的默认菜单按钮，用于在 XAML 里按需注入到指定 <see cref="RibbonGroup"/>。</summary>
    [Flags]
    public enum RibbonDefaultButtons
    {
        /// <summary>不注入任何按钮。</summary>
        None = 0,

        /// <summary>「清除排序」按钮（带默认图标，作用于当前数据 Tab）。</summary>
        ClearSort = 1,

        /// <summary>「清除筛选」按钮（带默认图标，作用于当前数据 Tab）。</summary>
        ClearFilter = 2,

        /// <summary>「导出日志」按钮（带默认图标，导出内置日志 Tab 的日志）。</summary>
        ExportLog = 4,

        /// <summary>「排序和筛选」两个按钮（顺序：清除排序 → 清除筛选）。</summary>
        SortAndFilter = ClearSort | ClearFilter
    }

    /// <summary>
    /// 在 XAML 里直接调用库自带菜单按钮的附加属性，例如：
    /// <code>
    /// &lt;r:RibbonGroup Header="导出" ribbon:RibbonDefaults.Buttons="ExportLog" /&gt;
    /// &lt;r:RibbonGroup Header="排序和筛选" ribbon:RibbonDefaults.Buttons="SortAndFilter" /&gt;
    /// </code>
    /// 按钮在所属分组加载后注入（此时能定位到宿主窗口）；按钮命令仍可用窗口上的
    /// <c>ExportLogCommand</c> / <c>ClearFilterCommand</c> / <c>ClearSortCommand</c> 替换。
    /// </summary>
    public static class RibbonDefaults
    {
        /// <summary>要注入到该分组的默认按钮（可用逗号组合，如 "ClearSort,ClearFilter"）。</summary>
        public static readonly DependencyProperty ButtonsProperty =
            DependencyProperty.RegisterAttached(
                "Buttons",
                typeof(RibbonDefaultButtons),
                typeof(RibbonDefaults),
                new PropertyMetadata(RibbonDefaultButtons.None, OnButtonsChanged));

        public static RibbonDefaultButtons GetButtons(DependencyObject obj)
            => (RibbonDefaultButtons)obj.GetValue(ButtonsProperty);

        public static void SetButtons(DependencyObject obj, RibbonDefaultButtons value)
            => obj.SetValue(ButtonsProperty, value);

        private static void OnButtonsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RibbonGroup group || (RibbonDefaultButtons)e.NewValue == RibbonDefaultButtons.None)
                return;

            var requested = (RibbonDefaultButtons)e.NewValue;

            // XAML 声明分组时还没有可视父级，等 Loaded 之后才能拿到宿主窗口
            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                group.Loaded -= handler;
                Inject(group, requested);
            };

            group.Loaded += handler;
        }

        private static void Inject(RibbonGroup group, RibbonDefaultButtons requested)
        {
            // 显式限定 System.Windows.Window：本命名空间下简单名 Window 会解析成命名空间 Namsiz.WPF.Window
            if (System.Windows.Window.GetWindow(group) is not RibbonWindow host)
                return;

            if ((requested & RibbonDefaultButtons.ClearSort) != 0)
                group.Items.Add(host.CreateClearSortButton());

            if ((requested & RibbonDefaultButtons.ClearFilter) != 0)
                group.Items.Add(host.CreateClearFilterButton());

            if ((requested & RibbonDefaultButtons.ExportLog) != 0)
                group.Items.Add(host.CreateExportLogButton());
        }
    }
}
