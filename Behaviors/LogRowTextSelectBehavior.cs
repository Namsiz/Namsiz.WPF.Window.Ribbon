using Namsiz.WPF.Window.Ribbon.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// 日志行文本选择附加行为：
    /// 双击某行 → 该行切换为只读 TextBox（可拖动选择、不能编辑），并自动聚焦；
    /// 单击其他行 → 恢复为普通 TextBlock。同时最多只有一行处于选择模式，
    /// 因此默认渲染与纯 TextBlock 方案一致，性能开销可忽略。
    /// </summary>
    public static class LogRowTextSelectBehavior
    {
        private static readonly ConditionalWeakTable<ListBox, SelectableLogItem> SelectingRows = new();

        /// <summary>是否启用双击行文本选择。</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(LogRowTextSelectBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListBox listBox)
                return;

            if ((bool)e.NewValue)
            {
                listBox.MouseDoubleClick += OnMouseDoubleClick;
                listBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                listBox.Unloaded += OnListBoxUnloaded;
            }
            else
            {
                listBox.MouseDoubleClick -= OnMouseDoubleClick;
                listBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                listBox.Unloaded -= OnListBoxUnloaded;
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox)
                return;

            if (listBox.ContainerFromElement(e.OriginalSource as DependencyObject) is not ListBoxItem { DataContext: SelectableLogItem item })
                return;

            // 单击其他行：退出当前行的文本选择模式
            if (SelectingRows.TryGetValue(listBox, out var current) && !ReferenceEquals(current, item))
                SetSelectingRow(listBox, null);
        }

        private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox)
                return;

            if (listBox.ContainerFromElement(e.OriginalSource as DependencyObject) is not ListBoxItem { DataContext: SelectableLogItem item })
                return;

            SetSelectingRow(listBox, item);

            // 模板切换完成后聚焦，便于直接 Ctrl+C
            listBox.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                var textBox = VisualTreeExt.FindDescendant<TextBox>(container);
                textBox?.Focus();
            }));
        }

        private static void OnListBoxUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listBox)
                SetSelectingRow(listBox, null);
        }

        private static void SetSelectingRow(ListBox listBox, SelectableLogItem? item)
        {
            if (SelectingRows.TryGetValue(listBox, out var current))
            {
                SelectingRows.Remove(listBox);
                if (current != null && !ReferenceEquals(current, item))
                    current.IsTextSelecting = false;
            }

            if (item != null)
            {
                item.IsTextSelecting = true;
                SelectingRows.Add(listBox, item);
            }
        }
    }
}
