using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// DataGrid 附加行为：自动生成列时统一使用固定列宽，
    /// 避免默认 Auto 列宽在大数据量下反复测量单元格内容。
    /// </summary>
    public static class DataGridAutoColumnBehavior
    {
        /// <summary>是否关闭自动列宽（改为固定列宽）。</summary>
        public static readonly DependencyProperty AutoWidthProperty =
            DependencyProperty.RegisterAttached(
                "AutoWidth",
                typeof(bool),
                typeof(DataGridAutoColumnBehavior),
                new PropertyMetadata(false, OnAutoWidthChanged));

        public static bool GetAutoWidth(DependencyObject obj) => (bool)obj.GetValue(AutoWidthProperty);

        public static void SetAutoWidth(DependencyObject obj, bool value) => obj.SetValue(AutoWidthProperty, value);

        private static void OnAutoWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid)
                return;

            if ((bool)e.NewValue)
                grid.AutoGeneratingColumn += OnAutoGeneratingColumn;
            else
                grid.AutoGeneratingColumn -= OnAutoGeneratingColumn;
        }

        private static void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Width = e.PropertyType == typeof(double)
                ? new DataGridLength(70)
                : new DataGridLength(120);
        }
    }
}
