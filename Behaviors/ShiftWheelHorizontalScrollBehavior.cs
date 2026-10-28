using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// ItemsControl 附加行为：按住 Shift 滚动鼠标滚轮时改为水平滚动。
    /// 未按 Shift 时不做任何处理，保持原有垂直滚动。
    /// 内部的 ScrollViewer 通过视觉树查找一次后缓存，避免每次滚轮事件重复遍历。
    /// </summary>
    public static class ShiftWheelHorizontalScrollBehavior
    {
        private static readonly ConditionalWeakTable<ItemsControl, ScrollViewer> ScrollViewerCache = new();

        /// <summary>是否启用 Shift+滚轮水平滚动。</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ShiftWheelHorizontalScrollBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ItemsControl itemsControl)
                return;

            if ((bool)e.NewValue)
                itemsControl.PreviewMouseWheel += OnPreviewMouseWheel;
            else
                itemsControl.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 仅按住 Shift 时接管滚轮事件（隧道阶段标记 Handled，阻止垂直滚动）
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                return;

            if (sender is not ItemsControl itemsControl)
                return;

            if (!ScrollViewerCache.TryGetValue(itemsControl, out var scrollViewer))
            {
                scrollViewer = VisualTreeExt.FindDescendant<ScrollViewer>(itemsControl);
                if (scrollViewer != null)
                    ScrollViewerCache.Add(itemsControl, scrollViewer);
            }

            if (scrollViewer == null)
                return;

            e.Handled = true;
            // 滚轮向上（Delta 为正）→ 水平向左滚动，与常见表格软件习惯一致
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
        }
    }
}
