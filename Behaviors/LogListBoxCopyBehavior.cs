using Namsiz.WPF.Window.Ribbon.Logging;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// 日志列表附加行为：支持选中日志行后 Ctrl+C 复制文本（只选择，不编辑）。
    /// 复制内容为选中行的「[HH:mm:ss] 消息」，与界面显示一致；多行按行分隔。
    /// 显式绑定 Copy 命令并标记 Handled，避免 ListBox 内置复制行为（XAML 格式）干扰。
    /// </summary>
    public static class LogListBoxCopyBehavior
    {
        /// <summary>是否启用 Ctrl+C 复制日志文本。</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(LogListBoxCopyBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListBox listBox)
                return;

            if ((bool)e.NewValue)
            {
                listBox.CommandBindings.Add(new CommandBinding(
                    ApplicationCommands.Copy,
                    OnCopyExecuted,
                    OnCopyCanExecute));
            }
            else
            {
                for (var i = listBox.CommandBindings.Count - 1; i >= 0; i--)
                {
                    if (listBox.CommandBindings[i].Command == ApplicationCommands.Copy)
                        listBox.CommandBindings.RemoveAt(i);
                }
            }
        }

        private static void OnCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is ListBox box && box.SelectedItems.Count > 0;
            e.Handled = true;
        }

        private static void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.SelectedItems.Count == 0)
                return;

            e.Handled = true;

            var sb = new StringBuilder();
            foreach (var item in listBox.SelectedItems)
            {
                if (item is not LogItem log || string.IsNullOrEmpty(log.Message))
                    continue;

                sb.Append('[')
                  .Append(log.Timestamp.ToString("HH:mm:ss"))
                  .Append("] ")
                  .AppendLine(log.Message);
            }

            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString().TrimEnd('\r', '\n'));
        }
    }
}
