using Namsiz.WPF.Window.Ribbon.Behaviors;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// Excel 风格列头筛选控件（漏斗按钮 + 弹层勾选 + 搜索 + 确定/取消/清除筛选）。
    /// 由 <see cref="DataGridExcelFilterBehavior"/> 为指定列安装。
    ///
    /// 状态模型：勾选仅为“待定”，点【确定】才生效；【取消】或点击外部关闭回退。
    /// 已提交的“排除值”保存在 <see cref="_filteredOut"/>，独立于每次重建的列表，
    /// 因此重开弹层不会丢失筛选状态（漏斗颜色、勾选、筛选语义均稳定）。
    ///
    /// 候选值规则：本列未筛选 → 基于其他列已生效筛选的可见行收窄（级联联动，每次打开重算）；
    /// 本列已筛选 → 列表冻结在提交筛选那一刻的候选，之后自身筛选或其他列变化均不影响本列列表
    /// （详见 <see cref="_frozenDisplays"/>）。
    /// </summary>
    public partial class ExcelFilterHeader : UserControl
    {
        /// <summary>列名（“客户”/“区域”/“科室”/“机型”等，也作为表头标题显示）。</summary>
        public static readonly DependencyProperty ColumnNameProperty =
            DependencyProperty.Register(
                nameof(ColumnName),
                typeof(string),
                typeof(ExcelFilterHeader),
                new PropertyMetadata(string.Empty));

        /// <summary>列名（也作为表头标题显示）。</summary>
        public string ColumnName
        {
            get => (string)GetValue(ColumnNameProperty);
            set => SetValue(ColumnNameProperty, value);
        }

        /// <summary>所属 DataGrid（用于读取 DataTable 去重值与设置 RowFilter）。</summary>
        public static readonly DependencyProperty GridProperty =
            DependencyProperty.Register(
                nameof(Grid),
                typeof(DataGrid),
                typeof(ExcelFilterHeader),
                new PropertyMetadata(null));

        /// <summary>所属 DataGrid。</summary>
        public DataGrid Grid
        {
            get => (DataGrid)GetValue(GridProperty);
            set => SetValue(GridProperty, value);
        }

        /// <summary>筛选提交事件（点【确定】/【清除筛选】后触发，由行为汇总后重建 RowFilter）。</summary>
        public event EventHandler? FilterChanged;

        /// <summary>弹层打开事件（用于关闭同一表格内其他列的筛选弹层）。</summary>
        public event EventHandler? PopupOpened;

        /// <summary>空白单元格在勾选列表里的显示文字（与 <see cref="FilterCandidate.Blank"/> 同一来源）。</summary>
        private const string BlankDisplay = FilterCandidate.BlankDisplay;

        private sealed class Option
        {
            public CheckBox Box { get; set; } = null!;
            public string? RawValue { get; set; } // null → 空白单元格
            public string Display { get; set; } = string.Empty;

            public bool IsBlank => RawValue == null;
        }

        private readonly List<Option> _options = new();

        /// <summary>已提交为“排除”（未勾选）的值显示键集合；持久化、跨弹层开关保留。</summary>
        private readonly HashSet<string> _filteredOut = new(StringComparer.Ordinal);

        /// <summary>
        /// 本列已筛选时应展示的候选键集合（冻结列表）：
        /// 在第一次提交筛选的瞬间快照，之后重开弹层一直用它重建，不会被自身筛选或其他列变化再改动。
        /// </summary>
        private List<string> _frozenDisplays = new();

        /// <summary>是否允许使用候选值缓存（由筛选行为在"当前没有任何列筛选"时置 true，此时候选值不受其它列影响）。</summary>
        internal bool CanUseCachedCandidates { get; set; } = true;

        private int _cachedVersion = -1;
        private List<(string Display, string? Raw)> _cachedCandidates = new();

        /// <summary>本次弹层打开时的初始勾选快照（取消/外部关闭时回退用）。</summary>
        private readonly Dictionary<string, bool> _reopenChecked = new(StringComparer.Ordinal);

        private bool _suppressToggle;

        /// <summary>弹层因【确定】/【取消】/【清除筛选】主动关闭（跳过 Closed 回退）。</summary>
        private bool _skipCancelOnClosed;

        public ExcelFilterHeader()
        {
            InitializeComponent();
        }

        private static string KeyOf(Option option) => option.IsBlank ? BlankDisplay : option.Display;

        /// <summary>
        /// 获取已提交的选中值集合（供行为构造 RowFilter）：
        /// 全选 → null（该列不参与筛选）；
        /// 部分勾选 → 选中值列表（含 null 表示“空白”单元格）；
        /// 全部取消 → 空列表（该列筛选结果为空，即无匹配行）。
        /// </summary>
        public List<string?>? GetSelectedValues()
        {
            if (_options.Count == 0)
                return null;

            var selected = _options
                .Where(o => !_filteredOut.Contains(KeyOf(o)))
                .Select(o => o.RawValue)
                .ToList();

            return selected.Count == _options.Count ? null : selected;
        }

        /// <summary>关闭本列的筛选弹层。</summary>
        public void ClosePopup()
        {
            FilterPopup.IsOpen = false;
        }

        /// <summary>本列当前是否有生效筛选（供"清除筛选"判断是否需要重算）。</summary>
        public bool HasActiveFilter => _filteredOut.Count > 0;

        /// <summary>清除本列筛选并通知宿主重新汇总（无筛选时不做任何事，返回 false）。</summary>
        public bool ClearFilter()
        {
            if (!ResetFilterState())
                return false;

            UpdateFilterIcon();
            FilterChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>仅重置筛选状态（不通知宿主）：供一次性清除所有列时集中汇总，避免逐列重算大表。</summary>
        internal bool ResetFilterState()
        {
            if (_filteredOut.Count == 0 && _frozenDisplays.Count == 0)
                return false;

            _filteredOut.Clear();
            _frozenDisplays.Clear();
            UpdateFilterIcon();
            return true;
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            // 阻止冒泡到 DataGridColumnHeader（避免触发排序）
            e.Handled = true;

            // 服务端筛选模式：不打开"本表数据候选值"弹层，改为把列名交给使用方。
            // 用途：表格只显示前 N 行时，筛选必须作用于**全部数据**（由使用方用 SQL 筛），
            // 否则只在已加载的 N 行里筛，语义就错了。
            Action<string>? serverFilter = Grid != null
                ? DataGridExcelFilterBehavior.GetServerFilterHandler(Grid)
                : null;
            if (serverFilter != null)
            {
                ClosePopup();
                serverFilter(ColumnName);
                return;
            }

            if (!FilterPopup.IsOpen)
            {
                RefreshOptions();
                PopupOpened?.Invoke(this, EventArgs.Empty);
            }
            FilterPopup.IsOpen = !FilterPopup.IsOpen;
        }

        /// <summary>
        /// 打开弹层时重建勾选列表。
        /// 候选值规则：
        /// - 本列已有生效筛选 → 直接用【冻结列表】重建（筛选确定时快照），
        ///   自身是否筛选过都不影响本列列表（不会因自身筛选变成全量，也不被后续变化改动）；
        /// - 本列未筛选 → 基于其他列已生效筛选的可见行收窄（级联联动，每次打开重新计算）。
        /// 勾选恢复：已排除的值保持未勾选；之前可见且未被排除的值保持勾选；
        /// 新出现的候选默认勾选（未筛选列打开即全选）。
        /// </summary>
        private void RefreshOptions()
        {
            // 是否有生效筛选：以持久化的排除集合为准（不依赖当前列表，避免状态随重建漂移）
            var isSelfFiltered = _filteredOut.Count > 0;

            _reopenChecked.Clear();
            _suppressToggle = true;
            try
            {
                _options.Clear();
                ItemsPanel.Children.Clear();

                if (Grid?.ItemsSource is DataView view && view.Table != null)
                {
                    if (isSelfFiltered)
                    {
                        // 已筛选列：使用冻结的候选列表，不再读取表格（自身/其他列变化都不影响本列列表）
                        foreach (var key in _frozenDisplays)
                        {
                            AddOption(key, key == BlankDisplay ? null : key);
                        }
                    }
                    else if (GetCandidateProvider() != null)
                    {
                        // 使用方提供了自定义候选值：不扫描数据、UI 线程直接填充（面板即时可用）
                        foreach (var (display, raw) in ComputeCandidatesFor(view))
                            AddOption(display, raw);
                    }
                    else
                    {
                        // 未筛选列：优先命中"数据版本"缓存（后台预热过）。
                        // 关键：缓存未命中时**绝不在 UI 线程整表扫描** —— 先弹出面板显示占位，
                        // 后台统计完成后再回填，否则百万行会卡在"点了漏斗但面板不弹"上。
                        var cached = CanUseCachedCandidates ? TryGetCachedCandidates() : null;
                        if (cached != null)
                        {
                            foreach (var (display, raw) in cached)
                                AddOption(display, raw);
                        }
                        else
                        {
                            RequestCandidatesAsync(view);
                        }
                    }
                }

                // 恢复勾选：已排除 → 未勾选；其余（曾见过或新候选）→ 勾选
                foreach (var opt in _options)
                {
                    var key = KeyOf(opt);
                    var wasChecked = !_filteredOut.Contains(key);
                    opt.Box.IsChecked = wasChecked;
                    _reopenChecked[key] = wasChecked;
                }
            }
            finally
            {
                _suppressToggle = false;
            }

            UpdateSelectAllVisual();
            UpdateFilterIcon();
        }

        /// <summary>只读缓存：数据版本一致且已有内容时返回，未命中返回 null（由调用方决定如何异步补齐）。</summary>
        private List<(string Display, string? Raw)>? TryGetCachedCandidates()
        {
            var version = DataGridTabItem.ResolveHost(Grid)?.DataVersion ?? 0;
            return _cachedVersion == version && _cachedCandidates.Count > 0 ? _cachedCandidates : null;
        }

        /// <summary>
        /// 缓存未命中时的异步统计：面板立刻打开并显示占位，后台线程扫描完成后回填。
        /// 这是"百万行也能立刻弹出筛选面板"的关键。
        /// </summary>
        private void RequestCandidatesAsync(DataView view)
        {
            var version = DataGridTabItem.ResolveHost(Grid)?.DataVersion ?? 0;
            var dispatcher = Grid?.Dispatcher ?? Dispatcher;

            _options.Clear();
            ItemsPanel.Children.Clear();
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = "正在统计候选项…",
                Margin = new Thickness(10, 6, 8, 6),
                Foreground = Brushes.Gray
            });

            System.Threading.Tasks.Task.Run(() =>
            {
                List<(string Display, string? Raw)> computed;
                try
                {
                    computed = ComputeCandidatesFor(view);
                }
                catch
                {
                    return;     // 数据并发变化时放弃，下次打开再算
                }

                dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        SetCachedCandidates(computed, version);

                        // 注意：这里只能看"面板是否还开着"和"数据版本是否还是当初那个"。
                        // 曾经误加了"其它列有筛选就不回填"的条件，会导致面板永远停在"正在统计候选项…"。
                        if (!FilterPopup.IsOpen || (DataGridTabItem.ResolveHost(Grid)?.DataVersion ?? 0) != version)
                            return;

                        _suppressToggle = true;
                        try
                        {
                            _options.Clear();
                            ItemsPanel.Children.Clear();
                            foreach (var (display, raw) in computed)
                                AddOption(display, raw);

                            foreach (var option in _options)
                            {
                                var key = KeyOf(option);
                                option.Box.IsChecked = !_filteredOut.Contains(key);
                                _reopenChecked[key] = option.Box.IsChecked == true;   // 供【取消】回退
                            }
                        }
                        finally
                        {
                            _suppressToggle = false;
                        }

                        UpdateSelectAllVisual();
                    }));
            });
        }

        /// <summary>取本列候选值：命中缓存（数据版本一致）则直接复用，否则扫描一次并缓存。</summary>
        private List<(string Display, string? Raw)> GetCachedCandidates(DataView view)
        {
            var version = DataGridTabItem.ResolveHost(Grid)?.DataVersion ?? 0;
            if (_cachedVersion == version && _cachedCandidates.Count > 0)
                return _cachedCandidates;

            var computed = ComputeCandidatesFor(view);
            _cachedVersion = version;
            _cachedCandidates = computed;
            return computed;
        }

        /// <summary>把预热结果写入缓存（在 UI 线程调用）。</summary>
        internal void SetCachedCandidates(List<(string Display, string? Raw)> candidates, int version)
        {
            _cachedCandidates = candidates;
            _cachedVersion = version;
        }

        /// <summary>取本列候选值：自定义候选值提供者优先，否则扫描本列实际取值。
        /// 纯数据操作、不触碰界面对象，可在后台线程执行（供预热使用）。</summary>
        internal List<(string Display, string? Raw)> ComputeCandidatesFor(DataView view)
        {
            var resolved = DataGridExcelFilterBehavior.ResolveCandidates(
                view,
                ColumnName,
                GetCandidateProvider(),
                Grid != null ? DataGridExcelFilterBehavior.GetMaxDistinctValues(Grid) : DataGridExcelFilterBehavior.DefaultMaxDistinctValues);

            var result = new List<(string Display, string? Raw)>(resolved.Count);
            foreach (var candidate in resolved)
                result.Add((candidate.Display, candidate.Raw));

            return result;
        }

        /// <summary>候选值来源（提供者或上限）变化后让缓存失效，下次打开重新取。</summary>
        internal void InvalidateCandidates()
        {
            _cachedVersion = -1;
            _cachedCandidates = new List<(string Display, string? Raw)>();
        }

        /// <summary>本表格是否配置了自定义候选值提供者。有提供者时无需扫描数据、面板即时可用。</summary>
        private FilterCandidateProvider? GetCandidateProvider()
            => Grid != null ? DataGridExcelFilterBehavior.GetCandidateProvider(Grid) : null;

        private void AddOption(string display, string? rawValue)
        {
            var box = new CheckBox
            {
                Content = display,
                Margin = new Thickness(10, 1, 8, 1),
                VerticalContentAlignment = VerticalAlignment.Center,
                IsChecked = true
            };

            _options.Add(new Option { Box = box, RawValue = rawValue, Display = display });
            ItemsPanel.Children.Add(box);

            box.Checked += OnOptionToggled;
            box.Unchecked += OnOptionToggled;
        }

        /// <summary>勾选变化仅更新待定状态与全选联动，不触发筛选。</summary>
        private void OnOptionToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle)
                return;

            UpdateSelectAllVisual();
        }

        private void SelectAllBox_Click(object sender, RoutedEventArgs e)
        {
            var state = SelectAllBox.IsChecked == true;
            _suppressToggle = true;
            try
            {
                foreach (var opt in _options)
                {
                    opt.Box.IsChecked = state;
                }
            }
            finally
            {
                _suppressToggle = false;
            }

            UpdateSelectAllVisual();
        }

        /// <summary>同步“全选”复选框：全勾 → 选中；部分勾 → 不确定；全不勾 → 未选中。</summary>
        private void UpdateSelectAllVisual()
        {
            if (_options.Count == 0)
            {
                SelectAllBox.IsChecked = true;
                return;
            }

            var checkedCount = _options.Count(o => o.Box.IsChecked == true);
            _suppressToggle = true;
            try
            {
                SelectAllBox.IsChecked = checkedCount == _options.Count ? true
                    : checkedCount == 0 ? false
                    : (bool?)null;
            }
            finally
            {
                _suppressToggle = false;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = (SearchBox.Text ?? string.Empty).Trim();
            foreach (var opt in _options)
            {
                var visible = keyword.Length == 0
                    || opt.Display.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                opt.Box.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>清除筛选：立即清空本列筛选（重置排除集合与冻结列表）、更新 DataGrid 并关闭弹层。</summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            _suppressToggle = true;
            try
            {
                foreach (var opt in _options)
                {
                    opt.Box.IsChecked = true;
                }
            }
            finally
            {
                _suppressToggle = false;
            }

            _filteredOut.Clear();
            _frozenDisplays.Clear();

            UpdateSelectAllVisual();
            RaiseFilterChanged();

            // 先关面板再触发筛选：大表整表筛选期间面板不会“挂着不动”，用户能立刻看到反馈
            _skipCancelOnClosed = true;
            FilterPopup.IsOpen = false;
            RaiseFilterChanged();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CommitPending();
        }

        /// <summary>提交：按当前勾选修正持久化的排除集合；提交后若本列生效筛选，冻结当前候选列表（重开不受影响），并触发筛选关闭弹层。</summary>
        private void CommitPending()
        {
            _suppressToggle = true;
            try
            {
                foreach (var opt in _options)
                {
                    var key = KeyOf(opt);
                    if (opt.Box.IsChecked == true)
                        _filteredOut.Remove(key);
                    else
                        _filteredOut.Add(key);
                }

                // 生效筛选 → 冻结当前候选列表；清空筛选 → 解冻（下次打开重新按其他列计算）
                if (_filteredOut.Count > 0)
                    _frozenDisplays = _options.Select(KeyOf).ToList();
                else
                    _frozenDisplays.Clear();
            }
            finally
            {
                _suppressToggle = false;
            }

            // 先关面板再触发筛选：大表筛选较慢，面板立即消失、蒙板立刻给出反馈
            _skipCancelOnClosed = true;
            FilterPopup.IsOpen = false;
            RaiseFilterChanged();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelPending();

            _skipCancelOnClosed = true;
            FilterPopup.IsOpen = false;
        }

        /// <summary>点击弹层外部关闭（未按确定/取消/清除）时视为取消。</summary>
        private void FilterPopup_Closed(object sender, EventArgs e)
        {
            if (_skipCancelOnClosed)
            {
                _skipCancelOnClosed = false;
                return;
            }

            CancelPending();
        }

        /// <summary>回退勾选到本次打开时的初始状态，不触发筛选。</summary>
        private void CancelPending()
        {
            SearchBox.Clear();
            _suppressToggle = true;
            try
            {
                foreach (var opt in _options)
                {
                    opt.Box.IsChecked = !_reopenChecked.TryGetValue(KeyOf(opt), out var checkedAtOpen)
                        || checkedAtOpen;
                }
            }
            finally
            {
                _suppressToggle = false;
            }

            UpdateSelectAllVisual();
        }

        /// <summary>
        /// 延迟一拍再通知宿主执行筛选。
        /// Popup 的关闭是异步的：若在关面板后立刻同步执行整表筛选，面板会一直"挂"在屏幕上直到筛选结束
        /// （用户看到的就是"面板没关掉 + 界面卡住"）。ApplicationIdle 保证渲染与消息处理都完成后再开始计算，
        /// 这样蒙板（IsBusy）能先画出来。
        /// </summary>
        private void RaiseFilterChanged()
        {
            UpdateFilterIcon();

            var dispatcher = Grid?.Dispatcher ?? Dispatcher;
            dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => FilterChanged?.Invoke(this, EventArgs.Empty)));
        }

        /// <summary>按已提交的排除集合刷新漏斗图标：有生效筛选 → 橙色，无 → 灰色。</summary>
        /// <summary>
        /// 服务端筛选是否对该列生效（由使用方设置）。
        /// 用于「表格只显示前 N 行、筛选下推数据库」的场景：库自身看不到本地筛选结果，
        /// 需要外部告诉它这一列已生效，漏斗才会变色。
        /// </summary>
        public static readonly System.Windows.DependencyProperty IsServerFilterActiveProperty =
            System.Windows.DependencyProperty.Register(
                nameof(IsServerFilterActive),
                typeof(bool),
                typeof(ExcelFilterHeader),
                new System.Windows.PropertyMetadata(false, OnIsServerFilterActiveChanged));

        /// <summary>服务端筛选是否对该列生效（true 时漏斗显示为高亮色）。</summary>
        public bool IsServerFilterActive
        {
            get => (bool)GetValue(IsServerFilterActiveProperty);
            set => SetValue(IsServerFilterActiveProperty, value);
        }

        private static void OnIsServerFilterActiveChanged(
            System.Windows.DependencyObject d,
            System.Windows.DependencyPropertyChangedEventArgs e)
            => ((ExcelFilterHeader)d).UpdateFilterIcon();

        private void UpdateFilterIcon()
        {
            var active = IsServerFilterActive
                         || (_options.Count > 0 && _options.Any(o => _filteredOut.Contains(KeyOf(o))));
            FilterIcon.Fill = active
                ? Brushes.Orange
                : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
        }
    }
}
