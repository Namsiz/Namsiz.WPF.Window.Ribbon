using Namsiz.WPF.Window.Ribbon.Controls;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// DataGrid 附加行为：按 <see cref="DataGridColumnItem"/> 集合重建列定义。
    /// 集合为空时保持自动生成列（DataTable/数据类直接绑定即可）；非空时关闭自动生成并以集合为准，
    /// 重建后自动重新安装表头筛选（见 <see cref="DataGridExcelFilterBehavior"/>）。
    /// </summary>
    public static class DataGridColumnsBehavior
    {
        /// <summary>自定义列定义集合（元素类型 <see cref="DataGridColumnItem"/>）。</summary>
        public static readonly DependencyProperty ColumnsSourceProperty =
            DependencyProperty.RegisterAttached(
                "ColumnsSource",
                typeof(IEnumerable),
                typeof(DataGridColumnsBehavior),
                new PropertyMetadata(null, OnColumnsSourceChanged));

        public static IEnumerable? GetColumnsSource(DependencyObject d) => (IEnumerable?)d.GetValue(ColumnsSourceProperty);

        public static void SetColumnsSource(DependencyObject d, IEnumerable? value) => d.SetValue(ColumnsSourceProperty, value);

        /// <summary>DataGrid → 集合变更处理器（不持有生命周期，DataGrid 被回收时自动释放）。</summary>
        private static readonly ConditionalWeakTable<DataGrid, NotifyCollectionChangedEventHandler> Hooks = new();

        private static void OnColumnsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid)
                return;

            Unhook(grid);

            if (e.NewValue is INotifyCollectionChanged collection)
            {
                NotifyCollectionChangedEventHandler handler = (_, _) => Rebuild(grid);
                collection.CollectionChanged += handler;
                Hooks.Add(grid, handler);
            }

            Rebuild(grid);
        }

        private static void Unhook(DataGrid grid)
        {
            if (!Hooks.TryGetValue(grid, out var handler))
                return;

            Hooks.Remove(grid);
            if (GetColumnsSource(grid) is INotifyCollectionChanged collection)
                collection.CollectionChanged -= handler;
        }

        /// <summary>
        /// 按集合重建列定义：集合为空 → 保持/恢复自动生成列。
        /// 只在确有变化时才动 <c>Columns</c>／<c>AutoGenerateColumns</c>：
        /// 在布局过程中清空列或改写列头，会让大表反复重算（实测 5 万行差 20 倍）。
        /// </summary>
        private static void Rebuild(DataGrid grid)
        {
            var items = GetColumnsSource(grid)?
                .OfType<DataGridColumnItem>()
                .ToList() ?? new System.Collections.Generic.List<DataGridColumnItem>();

            var shouldAutoGenerate = items.Count == 0;

            if (items.Count == 0)
            {
                // 自动列：数据源换了才会走到这里，列由 DataGrid 自己生成，无需干预
                if (grid.AutoGenerateColumns != shouldAutoGenerate)
                    grid.AutoGenerateColumns = shouldAutoGenerate;
            }
            else
            {
                if (grid.AutoGenerateColumns)
                    grid.AutoGenerateColumns = false;

                grid.Columns.Clear();
                foreach (var item in items)
                    grid.Columns.Add(item.CreateColumn());
            }

            DataGridExcelFilterBehavior.Reinstall(grid);
        }
    }
}
