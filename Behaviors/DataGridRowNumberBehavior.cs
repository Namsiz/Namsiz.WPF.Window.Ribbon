using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// DataGrid 附加行为：行头自动显示行号（从 1 开始）。
    /// 仅在行被实现（进入可视区）时设置 Header，与行虚拟化配合开销可忽略。
    ///
    /// <see cref="AutoWidthProperty"/> 打开后行头宽度会随「当前实现的行号」自动调整：
    /// 按最大行号的**位数**估算宽度（不逐行测量文本），并且在行加载/卸载时只做一次
    /// O(已实现行数) 的求最大值 —— 已实现行只有可视区那几十行，因此滚动开销可忽略。
    /// </summary>
    public static class DataGridRowNumberBehavior
    {
        /// <summary>是否启用行号。</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DataGridRowNumberBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>
        /// 行头宽度是否随当前实现的行号自动调整（默认关闭，保持固定宽度 36 的原行为）。
        /// 例：行号到 6 位（100000+）时自动加宽到 52，滚回小行号时再收窄。
        /// </summary>
        public static readonly DependencyProperty AutoWidthProperty =
            DependencyProperty.RegisterAttached(
                "AutoWidth",
                typeof(bool),
                typeof(DataGridRowNumberBehavior),
                new PropertyMetadata(false));

        /// <summary>默认宽度：够放 4 位行号（与原固定值一致）。</summary>
        private const double BaseWidth = 36;

        /// <summary>默认宽度能容纳的行号位数。</summary>
        private const int BaseDigits = 4;

        /// <summary>每多一位行号增加的宽度。</summary>
        private const double WidthPerDigit = 8;

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static bool GetAutoWidth(DependencyObject obj) => (bool)obj.GetValue(AutoWidthProperty);

        public static void SetAutoWidth(DependencyObject obj, bool value) => obj.SetValue(AutoWidthProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                grid.LoadingRow += OnLoadingRow;
                grid.UnloadingRow += OnUnloadingRow;
                return;
            }

            grid.LoadingRow -= OnLoadingRow;
            grid.UnloadingRow -= OnUnloadingRow;
            States.Remove(grid);
        }

        private static void OnLoadingRow(object sender, DataGridRowEventArgs e)
        {
            int index = e.Row.GetIndex();
            if (index < 0)
            {
                return;
            }

            int number = index + 1;
            e.Row.Header = number.ToString();

            if (sender is DataGrid grid && GetAutoWidth(grid))
            {
                State state = States.GetValue(grid, g => new State(g));
                state.Numbers[e.Row] = number;
                UpdateWidth(state);
            }
        }

        private static void OnUnloadingRow(object sender, DataGridRowEventArgs e)
        {
            if (sender is not DataGrid grid || !GetAutoWidth(grid))
            {
                return;
            }

            if (States.TryGetValue(grid, out State? state) && state.Numbers.Remove(e.Row))
            {
                UpdateWidth(state);
            }
        }

        /// <summary>按当前实现行的最大行号位数调整行头宽度（宽度没变时不赋值，避免触发布局）。</summary>
        private static void UpdateWidth(State state)
        {
            int max = 0;
            foreach (int number in state.Numbers.Values)
            {
                if (number > max)
                {
                    max = number;
                }
            }

            if (max <= 0)
            {
                return;     // 当前没有已实现的行（例如刚换数据源）：保持现状，等新行进来再算
            }

            int digits = CountDigits(max);
            double width = digits <= BaseDigits
                ? BaseWidth
                : BaseWidth + (digits - BaseDigits) * WidthPerDigit;

            if (Math.Abs(state.Grid.RowHeaderWidth - width) > 0.1)
            {
                state.Grid.RowHeaderWidth = width;
            }
        }

        private static int CountDigits(int value)
        {
            int digits = 1;
            while (value >= 10)
            {
                value /= 10;
                digits++;
            }

            return digits;
        }

        /// <summary>每个表格一份状态（不持有表格生命周期：表格被回收时自动释放）。</summary>
        private sealed class State
        {
            public State(DataGrid grid)
            {
                Grid = grid;
            }

            public DataGrid Grid { get; }

            /// <summary>当前已实现的行 → 行号（只有可视区那几十行）。</summary>
            public Dictionary<DataGridRow, int> Numbers { get; } = new Dictionary<DataGridRow, int>();
        }

        private static readonly ConditionalWeakTable<DataGrid, State> States = new ConditionalWeakTable<DataGrid, State>();
    }
}
