using Namsiz.WPF.Window.Ribbon.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// 为 DataGrid 指定列安装 Excel 风格表头筛选。
    /// 筛选通过 DataView.RowFilter 实现：不重建表格、不复制集合，保持 Row/Column 虚拟化，
    /// 仅每次筛选变化时对行做一次字符串过滤（行数千级时开销可忽略）。
    /// </summary>
    public static class DataGridExcelFilterBehavior
    {
        /// <summary>启用附加属性：为 <see cref="FilterColumnsProperty"/> 指定的列安装筛选表头。</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DataGridExcelFilterBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>需要启用筛选的列名，逗号分隔（如“客户,区域,科室,机型”）；留空表示全部列。</summary>
        public static readonly DependencyProperty FilterColumnsProperty =
            DependencyProperty.RegisterAttached(
                "FilterColumns",
                typeof(string),
                typeof(DataGridExcelFilterBehavior),
                new PropertyMetadata(string.Empty, OnFilterColumnsChanged));

        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

        public static string GetFilterColumns(DependencyObject d) => (string)d.GetValue(FilterColumnsProperty);

        public static void SetFilterColumns(DependencyObject d, string value) => d.SetValue(FilterColumnsProperty, value);

        /// <summary>默认候选值统计上限。</summary>
        public const int DefaultMaxDistinctValues = 1000;

        /// <summary>
        /// **服务端筛选**处理器（可选）：设置后，点击列头漏斗不再枚举本表数据，
        /// 而是把列名（<see cref="string"/>）交给这个处理器 ——
        /// 由使用方用数据库查询去筛**全部数据**。
        ///
        /// 为什么需要：表格若只显示「前 N 行」，内置的列筛选只能在已加载的 N 行里筛，
        /// 语义就变成"只在这 N 行里筛"，与用户预期（筛全部数据）不符。
        /// </summary>
        public static readonly DependencyProperty ServerFilterHandlerProperty =
            DependencyProperty.RegisterAttached(
                "ServerFilterHandler",
                typeof(Action<string>),
                typeof(DataGridExcelFilterBehavior),
                new PropertyMetadata(null));

        public static Action<string>? GetServerFilterHandler(DependencyObject d)
            => (Action<string>?)d.GetValue(ServerFilterHandlerProperty);

        public static void SetServerFilterHandler(DependencyObject d, Action<string>? value)
            => d.SetValue(ServerFilterHandlerProperty, value);

        /// <summary>
        /// 自定义候选值提供者（可选）。设置后，筛选面板的候选值优先取该委托的返回值；
        /// 委托对某列返回 <c>null</c>（或抛异常）时，该列仍按「列的实际取值」统计。
        /// 用途：把「按数据取值勾选」换成「按业务固定选项勾选」（如布尔列显示「已标记/未标记」）。
        /// </summary>
        public static readonly DependencyProperty CandidateProviderProperty =
            DependencyProperty.RegisterAttached(
                "CandidateProvider",
                typeof(FilterCandidateProvider),
                typeof(DataGridExcelFilterBehavior),
                new PropertyMetadata(null, OnCandidatesRelevantChanged));

        /// <summary>
        /// 业务预设筛选表达式（可选，RowFilter 语法）。会与表头筛选结果用 AND 组合，
        /// 而不是被表头筛选整体覆盖 —— 这样「只看已标记」这类业务预设能与列筛选叠加。
        /// </summary>
        public static readonly DependencyProperty BaseRowFilterProperty =
            DependencyProperty.RegisterAttached(
                "BaseRowFilter",
                typeof(string),
                typeof(DataGridExcelFilterBehavior),
                new PropertyMetadata(string.Empty, OnBaseRowFilterChanged));

        /// <summary>候选值统计上限（默认 1000；<c>0</c> 表示不限制）。仅对「按列实际取值统计」的列生效。</summary>
        public static readonly DependencyProperty MaxDistinctValuesProperty =
            DependencyProperty.RegisterAttached(
                "MaxDistinctValues",
                typeof(int),
                typeof(DataGridExcelFilterBehavior),
                new PropertyMetadata(DefaultMaxDistinctValues, OnCandidatesRelevantChanged));

        public static FilterCandidateProvider? GetCandidateProvider(DependencyObject d)
            => (FilterCandidateProvider?)d.GetValue(CandidateProviderProperty);

        public static void SetCandidateProvider(DependencyObject d, FilterCandidateProvider? value)
            => d.SetValue(CandidateProviderProperty, value);

        public static string GetBaseRowFilter(DependencyObject d)
            => (string?)d.GetValue(BaseRowFilterProperty) ?? string.Empty;

        public static void SetBaseRowFilter(DependencyObject d, string value)
            => d.SetValue(BaseRowFilterProperty, value);

        public static int GetMaxDistinctValues(DependencyObject d)
            => (int)d.GetValue(MaxDistinctValuesProperty);

        public static void SetMaxDistinctValues(DependencyObject d, int value)
            => d.SetValue(MaxDistinctValuesProperty, value);

        /// <summary>候选值来源或上限变了：让各列下次打开面板时重新取候选值。</summary>
        private static void OnCandidatesRelevantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid || !Entries.TryGetValue(grid, out var entries))
                return;

            foreach (var entry in entries)
                entry.Filter.InvalidateCandidates();
        }

        /// <summary>业务预设变化：立刻把「预设 AND 表头筛选」重新应用到 RowFilter。</summary>
        private static void OnBaseRowFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid grid)
                ApplyFilter(grid);
        }

        private sealed class ColumnEntry
        {
            // 均在 Install 时通过对象初始化器赋值（编译器无法感知，置 null! 消除 CS8618）
            public DataGridColumn Column = null!;
            public string ColumnName = string.Empty;
            public string OriginalHeader = string.Empty;
            public ExcelFilterHeader Filter = null!;
            public EventHandler FilterChangedHandler = null!;
            public EventHandler PopupOpenedHandler = null!;
        }

        /// <summary>DataGrid → 已安装的筛选列条目（不持有生命周期，DataGrid 被回收时自动释放）。</summary>
        private static readonly ConditionalWeakTable<DataGrid, List<ColumnEntry>> Entries = new();

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid)
                return;

            if ((bool)e.NewValue)
            {
                // ItemsSource 变化会重建列集，统一在 AutoGeneratedColumns 后安装/恢复
                grid.AutoGeneratedColumns += OnAutoGeneratedColumns;
                Install(grid);
            }
            else
            {
                grid.AutoGeneratedColumns -= OnAutoGeneratedColumns;
                Restore(grid);
            }
        }

        private static void OnFilterColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid grid && GetIsEnabled(grid))
            {
                Restore(grid);
                Install(grid);
            }
        }

        private static void OnAutoGeneratedColumns(object sender, EventArgs e)
        {
            if (sender is DataGrid grid)
                Reinstall(grid);
        }

        /// <summary>
        /// 重新安装筛选表头。
        /// 安装过程会改写列头（先还原成文本、再换成筛选控件），进而触发列头重新生成；
        /// 若在布局/列生成的回调里同步执行，大表会被反复重算（实测 5 万行首次布局差 20 倍），
        /// 因此统一延后到本轮布局结束再装。
        /// </summary>
        internal static void Reinstall(DataGrid grid)
        {
            if (!GetIsEnabled(grid))
                return;

            grid.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    Restore(grid);
                    Install(grid);
                }));
        }

        private static void Install(DataGrid grid)
        {
            var entries = Entries.GetValue(grid, _ => new List<ColumnEntry>());
            entries.Clear();

            var filterCols = (GetFilterColumns(grid) ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToList();
            var nameSet = new HashSet<string>(filterCols, StringComparer.Ordinal);

            foreach (var column in grid.Columns)
            {
                if (column.Header is not string name)
                {
                    // 幂等：表头已经是本列的筛选控件（重装/列重建后再次进入）时直接复用，
                    // 避免"文本 ↔ 控件"来回改写表头触发大表重复布局（实测会让 5 万行首布局差 20 倍）。
                    if (column.Header is ExcelFilterHeader ready && GetIsEnabled(grid))
                    {
                        var readyEntry = new ColumnEntry
                        {
                            Column = column,
                            ColumnName = ready.ColumnName,
                            OriginalHeader = ready.ColumnName,
                            Filter = ready,
                            FilterChangedHandler = null!,
                            PopupOpenedHandler = null!
                        };

                        readyEntry.FilterChangedHandler = (_, _) => ApplyFilter(grid);
                        readyEntry.PopupOpenedHandler = (_, _) => CloseOtherPopups(grid, ready);
                        ready.FilterChanged += readyEntry.FilterChangedHandler;
                        ready.PopupOpened += readyEntry.PopupOpenedHandler;
                        entries.Add(readyEntry);
                    }

                    continue;
                }

                // 留空 → 全部列；否则仅指定列
                if (nameSet.Count > 0 && !nameSet.Contains(name))
                    continue;

                var filter = new ExcelFilterHeader
                {
                    ColumnName = name,
                    Grid = grid
                };

                var entry = new ColumnEntry
                {
                    Column = column,
                    ColumnName = name,
                    OriginalHeader = name,
                    Filter = filter,
                    FilterChangedHandler = (_, _) => ApplyFilter(grid),
                    PopupOpenedHandler = (_, _) => CloseOtherPopups(grid, filter)
                };

                filter.FilterChanged += entry.FilterChangedHandler;
                filter.PopupOpened += entry.PopupOpenedHandler;
                entries.Add(entry);

                column.Header = filter;
            }

            // 刻意不做"预先统计候选值"：多列并发整表扫描会与界面操作争用行集合，
            // 反而让打开筛选面板变慢。改为按需后台统计（见 ExcelFilterHeader.RequestCandidatesAsync）。
        }

        /// <summary>
        /// 候选值预热：在后台线程扫描各筛选列的候选值并缓存，
        /// 这样百万行数据点开筛选面板是即时的（首次扫描不再占界面时间）。
        /// </summary>
        private static void WarmUpCandidates(DataGrid grid, List<ColumnEntry> entries)
        {
            var hostTab = DataGridTabItem.ResolveHost(grid);

            if (entries.Count == 0
                || grid.ItemsSource is not DataView view
                || view.Table == null
                || hostTab == null)
            {
                return;
            }

            var version = hostTab.DataVersion;

            // 放到"界面空闲"之后再扫描：预热会读取整表行集合，
            // 若与首次布局并发，会因争用让大表首布局偶尔慢到 2 秒（实测双模态）。
            grid.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    foreach (var entry in entries)
                    {
                        var filter = entry.Filter;
                        var columnName = entry.ColumnName;

                        System.Threading.Tasks.Task.Run(() =>
                        {
                            List<(string Display, string? Raw)> computed;
                            try
                            {
                                // 走列自己的解析（自定义候选值提供者优先，否则扫描本列取值）
                                computed = filter.ComputeCandidatesFor(view);
                            }
                            catch
                            {
                                return;     // 数据并发变化时放弃预热（打开面板会重新计算）
                            }

                            filter.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Background,
                                new Action(() => filter.SetCachedCandidates(computed, version)));
                        });
                    }
                }));
        }

        /// <summary>恢复原始表头并解绑事件（列重建或禁用时调用）。</summary>
        private static void Restore(DataGrid grid)
        {
            if (!Entries.TryGetValue(grid, out var entries))
                return;

            foreach (var entry in entries)
            {
                entry.Filter.FilterChanged -= entry.FilterChangedHandler;
                entry.Filter.PopupOpened -= entry.PopupOpenedHandler;

                // 刻意不把表头还原成文本：重装时直接复用同一个筛选控件，
                // "文本 → 控件"的中间态会额外触发一次大表布局（5 万行实测差 20 倍）。
            }

            entries.Clear();
        }

        private static void CloseOtherPopups(DataGrid grid, ExcelFilterHeader current)
        {
            if (!Entries.TryGetValue(grid, out var entries))
                return;

            foreach (var entry in entries)
            {
                if (!ReferenceEquals(entry.Filter, current))
                    entry.Filter.ClosePopup();
            }
        }

        /// <summary>
        /// 清除某表格所有列的筛选。
        /// 无任何生效筛选时直接返回——大表上避免白白重算一次 RowFilter；有筛选时只汇总一次并带蒙板执行。
        /// </summary>
        public static void ClearFilters(DataGrid grid)
        {
            if (!Entries.TryGetValue(grid, out var entries) || entries.Count == 0)
                return;

            var changed = false;
            foreach (var entry in entries)
                changed |= entry.Filter.ResetFilterState();

            // 兼容使用方直接给 DataView 设置 RowFilter 的情况
            if (!changed && grid.ItemsSource is DataView view && !string.IsNullOrEmpty(view.RowFilter))
                changed = true;

            if (changed)
                ApplyFilter(grid);
        }

        /// <summary>汇总各列勾选值生成 RowFilter 表达式并应用到 DataView（大表上带蒙板、让出一帧再执行）。</summary>
        private static void ApplyFilter(DataGrid grid)
        {
            if (grid.ItemsSource is not DataView view)
                return;

            var baseFilter = GetBaseRowFilter(grid).Trim();

            if (!Entries.TryGetValue(grid, out var entries) || entries.Count == 0)
            {
                // 没有筛选表头（或未启用筛选）时，业务预设同样要生效
                ApplyRowFilter(grid, view, baseFilter);
                return;
            }

            var columnFilters = new List<(string Column, List<string?> Values)>();
            foreach (var entry in entries)
            {
                var selected = entry.Filter.GetSelectedValues();
                if (selected != null)
                    columnFilters.Add((entry.ColumnName, selected));
            }

            // 没有任何列筛选时，面板候选值可以直接用后台预热好的缓存（百万行打开即时）
            foreach (var entry in entries)
                entry.Filter.CanUseCachedCandidates = columnFilters.Count == 0;

            ApplyRowFilter(grid, view, CombineRowFilters(baseFilter, BuildRowFilter(columnFilters)));
        }

        /// <summary>
        /// 把业务预设与表头筛选结果用 AND 组合。两边都有效时各自加括号，
        /// 避免预设里的顶层 OR 被表头筛选的 AND 改变语义。
        /// </summary>
        private static string CombineRowFilters(string baseFilter, string headerFilter)
        {
            if (string.IsNullOrEmpty(baseFilter))
                return headerFilter;

            if (string.IsNullOrEmpty(headerFilter))
                return baseFilter;

            return "(" + baseFilter + ") AND (" + headerFilter + ")";
        }

        /// <summary>应用 RowFilter（无变化不重算：大表上赋值一次就是一趟整表过滤）。</summary>
        private static void ApplyRowFilter(DataGrid grid, DataView view, string expression)
        {
            if (string.Equals(view.RowFilter ?? string.Empty, expression, StringComparison.Ordinal))
                return;

            DataGridBusy.Run(grid, view.Count, $"正在筛选 {view.Count:N0} 行…", () =>
            {
                try
                {
                    view.RowFilter = expression;
                }
                catch
                {
                    // 表达式文法异常（罕见脏值）时回退为不过滤，避免界面崩溃
                    view.RowFilter = string.Empty;
                }
            });
        }

        /// <summary>
        /// 取某列在筛选面板里的候选值：优先用使用方提供的自定义候选值（业务固定选项），
        /// 未提供或出错时回退为「扫描本列实际取值、去重、按当前区域性升序、空白固定排最后」。
        /// 纯数据操作、不触碰界面对象，可在工作线程调用。
        /// </summary>
        public static IReadOnlyList<FilterCandidate> ResolveCandidates(
            DataView view,
            string columnName,
            FilterCandidateProvider? provider,
            int maxDistinctValues)
        {
            if (provider != null)
            {
                try
                {
                    var provided = provider(view, columnName);
                    if (provided != null)
                        return provided;
                }
                catch
                {
                    // 使用方提供者出错时回退到默认统计，不能因此连筛选面板都打不开
                }
            }

            return ScanCandidates(view, columnName, maxDistinctValues);
        }

        /// <summary>扫描列的实际取值得到候选值（去重、按当前区域性升序、空白固定排最后）。</summary>
        public static IReadOnlyList<FilterCandidate> ScanCandidates(DataView view, string columnName, int maxDistinctValues)
        {
            var result = new List<FilterCandidate>();
            if (view.Table == null)
                return result;

            // RowFilter 非空时走 ToTable 快照，避免在过滤过程中读到不一致的行集合
            var source = !string.IsNullOrWhiteSpace(view.RowFilter) ? view.ToTable() : view.Table;
            var col = source.Columns[columnName];
            if (col == null)
                return result;

            var limit = maxDistinctValues > 0 ? maxDistinctValues : int.MaxValue;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var values = new List<string>();
            var hasBlank = false;

            foreach (DataRow row in source.Rows)
            {
                var raw = row[col];
                if (raw == null || raw == DBNull.Value || string.IsNullOrEmpty(raw.ToString()))
                {
                    hasBlank = true;
                    continue;
                }

                var text = raw.ToString()!;
                if (!seen.Add(text))
                    continue;

                values.Add(text);

                // 高基数列（如序号）不必统计完：候选值多到看不过来，早停即可让面板快速可用
                if (values.Count >= limit)
                    break;
            }

            values.Sort(StringComparer.CurrentCulture);

            foreach (var value in values)
                result.Add(FilterCandidate.OfValue(value));

            if (hasBlank)
                result.Add(FilterCandidate.Blank());

            return result;
        }

        private static string BuildRowFilter(List<(string Column, List<string?> Values)> columnFilters)
        {
            if (columnFilters.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            foreach (var (column, values) in columnFilters)
            {
                var clauses = new List<string>();
                foreach (var value in values)
                {
                    if (value == null)
                    {
                        // 空白单元格：DBNull 或空字符串都视为空白
                        clauses.Add($"([{column}] IS NULL OR [{column}]='')");
                    }
                    else
                    {
                        clauses.Add($"[{column}]='{EscapeLiteral(value)}'");
                    }
                }

                // 全部取消勾选 → 该列无任何匹配行
                parts.Add(clauses.Count > 0 ? "(" + string.Join(" OR ", clauses) + ")" : "(1=0)");
            }

            return string.Join(" AND ", parts);
        }

        /// <summary>RowFilter 字符串字面量转义：单引号翻倍。</summary>
        private static string EscapeLiteral(string value)
        {
            return value.Replace("'", "''");
        }
    }
}
