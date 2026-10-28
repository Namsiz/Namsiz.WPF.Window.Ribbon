using Namsiz.WPF.Window.Ribbon.Behaviors;
using Namsiz.WPF.Window.Ribbon.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 数据表格 TabItem：筛选、行号、固定列宽、虚拟化、状态栏行数统计等通用能力默认提供，
    /// 使用方只需给出数据列（<see cref="Columns"/>，留空则按数据源自动生成）与数据源 <see cref="ItemsSource"/>。
    ///
    /// 行着色为可选项：<see cref="ValidationColumn"/> 指定校验属性/列（默认“校验”），
    /// 取值 "OK" → 浅绿、以 "E:" 开头 → 浅红、以 "W:" 开头 → 浅橙；
    /// 需要自定义规则时替换 <see cref="ValidationConverter"/>，需要完全自定义外观时替换 <see cref="RowStyle"/>。
    ///
    /// 增加一个数据 Tab 的推荐方式：派生本类并在构造函数中声明列与默认属性，例如
    /// <c>class WeeklyManpowerTabItem : DataGridTabItem { public WeeklyManpowerTabItem() { Header = "每周人力"; ... } }</c>。
    /// </summary>
    public class DataGridTabItem : TabItem
    {
        /// <summary>库的表格默认样式键。</summary>
        private const string DataGridStyleKey = "Namsiz.RibbonWindow.Styles.DataGrid";

        /// <summary>校验通过的行底色（浅绿）。</summary>
        public const string ValidationOkBrushKey = "Namsiz.RibbonWindow.Brushes.ValidationRow.Ok";

        /// <summary>校验警告的行底色（浅橙）。</summary>
        public const string ValidationWarningBrushKey = "Namsiz.RibbonWindow.Brushes.ValidationRow.Warning";

        /// <summary>校验错误的行底色（浅红）。</summary>
        public const string ValidationErrorBrushKey = "Namsiz.RibbonWindow.Brushes.ValidationRow.Error";

        private static readonly Color FallbackOkColor = Color.FromRgb(0xE5, 0xF5, 0xE5);
        private static readonly Color FallbackWarningColor = Color.FromRgb(0xFF, 0xF1, 0xDC);
        private static readonly Color FallbackErrorColor = Color.FromRgb(0xFD, 0xE7, 0xE7);

        private DataView? _view;
        private DataGrid? _grid;
        private BusyOverlayAdorner? _overlay;
        private bool _isBulkLoading;
        private bool _statusUpdateQueued;

        public DataGridTabItem()
        {
            // 表格直接作为本 Tab 的内容，不再套一层自定义内容控件：
            // 实测"选中态 Tab + 自带内容宿主"这一组合会让海量数据首次布局成倍变慢（5 万行 2.2s vs 0.1s）。
            Table = new DataGrid
            {
                AutoGenerateColumns = true,
                HeadersVisibility = DataGridHeadersVisibility.All,
                CanUserAddRows = false,
                CanUserDeleteRows = false
            };

            _grid = Table;
            ApplyContentLayout();
            Table.SetValue(HostTabProperty, this);
            Table.Sorting += OnGridSorting;

            ApplySourceDependentState();
            RefreshEffectiveRowStyle();
            SyncGrid();

            // 样式与表格行为都在首次加载时补齐（资源字典此时一定已就绪）
            Loaded += (_, _) =>
            {
                EnsureThemeStyle();
                SyncGrid();
            };

            // 被切走时重新判定"无数据隐藏"（选中的 Tab 始终保留）
            AddHandler(Selector.SelectedEvent, new RoutedEventHandler((_, _) => ApplyEmptyVisibility()));
        }

        /// <summary>本 Tab 承载的表格（可直接设置其选中模式、右键菜单等细节）。</summary>
        public DataGrid Table { get; }

        /// <summary>
        /// 表格上方的工具条内容（可选）。为空时内容区就是表格本身（与旧行为一致）；
        /// 非空时内容区变成「工具条 + 表格」，表格占满剩余空间。
        /// 用于放按需加载/自定义筛选这类**与具体数据表相关**的小工具，避免占用 Ribbon 空间。
        /// </summary>
        public static readonly DependencyProperty ToolBarContentProperty =
            DependencyProperty.Register(
                nameof(ToolBarContent),
                typeof(object),
                typeof(DataGridTabItem),
                new PropertyMetadata(null, OnToolBarContentChanged));

        /// <summary>表格上方的工具条内容（可选）。</summary>
        public object? ToolBarContent
        {
            get => GetValue(ToolBarContentProperty);
            set => SetValue(ToolBarContentProperty, value);
        }

        private static void OnToolBarContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((DataGridTabItem)d).ApplyContentLayout();

        /// <summary>
        /// 表格**下方**的内容（可选）。用于放「显示行数」「每页行数」这类页脚控件 ——
        /// 放在页脚而不是 Ribbon，可以少占菜单空间，也更贴近数据本身。
        /// </summary>
        public static readonly DependencyProperty FooterContentProperty =
            DependencyProperty.Register(
                nameof(FooterContent),
                typeof(object),
                typeof(DataGridTabItem),
                new PropertyMetadata(null, OnFooterContentChanged));

        /// <summary>表格下方的内容（可选）。</summary>
        public object? FooterContent
        {
            get => GetValue(FooterContentProperty);
            set => SetValue(FooterContentProperty, value);
        }

        private static void OnFooterContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((DataGridTabItem)d).ApplyContentLayout();

        private ContentPresenter? _footerPresenter;

        /// <summary>按是否配置了工具条决定内容布局（切回空值时把表格放回内容区）。</summary>
        private void ApplyContentLayout()
        {
            // 换父容器前必须先把表格从当前容器摘下来，否则 WPF 会因"已有逻辑父级"抛异常
            // （首次进入时表格的逻辑父级就是本 TabItem —— 构造时设过 Content = Table）。
            if (ReferenceEquals(Content, Table))
            {
                Content = null;
            }
            else if (_toolBarPanel != null && _toolBarPanel.Children.Contains(Table))
            {
                _toolBarPanel.Children.Remove(Table);
            }

            if (ToolBarContent == null && FooterContent == null)
            {
                _toolBarPanel = null;
                _footerPresenter = null;
                Content = Table;
                return;
            }

            var panel = new DockPanel();

            if (ToolBarContent != null)
            {
                var top = new ContentPresenter { Content = ToolBarContent };
                DockPanel.SetDock(top, Dock.Top);
                panel.Children.Add(top);
            }

            if (FooterContent != null)
            {
                _footerPresenter = new ContentPresenter { Content = FooterContent };
                DockPanel.SetDock(_footerPresenter, Dock.Bottom);
                panel.Children.Add(_footerPresenter);
            }

            panel.Children.Add(Table);
            _toolBarPanel = panel;
            Content = panel;
        }

        private DockPanel? _toolBarPanel;

        /// <summary>宿主 Tab 关联（表格的 DataContext 是业务数据，不能靠它反查所在 Tab）。</summary>
        private static readonly DependencyProperty HostTabProperty =
            DependencyProperty.RegisterAttached(
                "HostTab",
                typeof(DataGridTabItem),
                typeof(DataGridTabItem),
                new PropertyMetadata(null));

        /// <summary>
        /// 反查某个表格所属的数据 Tab（供加载蒙板、筛选候选值缓存等反查宿主）。
        /// 表格的 DataContext 通常是业务数据，所以先看附加属性，再退回 DataContext。
        /// </summary>
        internal static DataGridTabItem? ResolveHost(DataGrid? grid)
            => grid == null
                ? null
                : grid.DataContext as DataGridTabItem ?? (DataGridTabItem?)grid.GetValue(HostTabProperty);

        /// <summary>
        /// 把 Tab 上的属性同步到表格。刻意使用"代码同步"而不是模板绑定：
        /// 模板绑定 + 自定义内容控件会让大表在实现内容时反复布局（实测 5 万行差 20 倍）。
        /// </summary>
        private void SyncGrid()
        {
            if (Table == null)
                return;

            EnsureThemeStyle();

            if (!ReferenceEquals(Table.ItemsSource, ItemsSource))
                Table.ItemsSource = ItemsSource;

            if (Table.IsReadOnly != IsReadOnly)
                Table.IsReadOnly = IsReadOnly;

            if (!ReferenceEquals(Table.RowStyle, EffectiveRowStyle))
                Table.RowStyle = EffectiveRowStyle;

            if (!ReferenceEquals(DataGridColumnsBehavior.GetColumnsSource(Table), Columns))
                DataGridColumnsBehavior.SetColumnsSource(Table, Columns);

            if (DataGridExcelFilterBehavior.GetFilterColumns(Table) != (FilterColumns ?? string.Empty))
                DataGridExcelFilterBehavior.SetFilterColumns(Table, FilterColumns ?? string.Empty);

            if (DataGridExcelFilterBehavior.GetIsEnabled(Table) != IsFilterEnabled)
                DataGridExcelFilterBehavior.SetIsEnabled(Table, IsFilterEnabled);

            if (!ReferenceEquals(DataGridExcelFilterBehavior.GetCandidateProvider(Table), FilterCandidateProvider))
                DataGridExcelFilterBehavior.SetCandidateProvider(Table, FilterCandidateProvider);

            if (DataGridExcelFilterBehavior.GetMaxDistinctValues(Table) != MaxDistinctValues)
                DataGridExcelFilterBehavior.SetMaxDistinctValues(Table, MaxDistinctValues);

            if (DataGridExcelFilterBehavior.GetBaseRowFilter(Table) != (BaseRowFilter ?? string.Empty))
                DataGridExcelFilterBehavior.SetBaseRowFilter(Table, BaseRowFilter ?? string.Empty);
        }

        /// <summary>套用库的表格默认样式（资源未就绪时留到 Loaded 再补）。</summary>
        private void EnsureThemeStyle()
        {
            if (Table == null || Table.Style != null)
                return;

            try
            {
                if (TryFindResource(DataGridStyleKey) is Style style)
                    Table.Style = style;
            }
            catch
            {
                // 库主题尚未合并时忽略，Loaded 时会再试一次
            }
        }

        /// <summary>数据源（DataView / DataTable / 任意集合）。</summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(DataGridTabItem),
                new PropertyMetadata(null, OnItemsSourceChanged));

        /// <summary>数据源（DataView / DataTable / 任意集合）；为 <c>null</c> 时按 <see cref="AutoHideWhenEmpty"/> 自动隐藏本 Tab。</summary>
        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>是否只读（默认 false，可编辑）。</summary>
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(DataGridTabItem),
                new PropertyMetadata(false, OnSyncRequired));

        /// <summary>是否只读（默认 false，可编辑）。</summary>
        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        /// <summary>是否启用表头筛选（默认启用；仅 DataView 数据源生效）。</summary>
        public static readonly DependencyProperty IsFilterEnabledProperty =
            DependencyProperty.Register(
                nameof(IsFilterEnabled),
                typeof(bool),
                typeof(DataGridTabItem),
                new PropertyMetadata(true, OnSyncRequired));

        /// <summary>是否启用表头筛选（默认启用；仅 DataView 数据源生效）。</summary>
        public bool IsFilterEnabled
        {
            get => (bool)GetValue(IsFilterEnabledProperty);
            set => SetValue(IsFilterEnabledProperty, value);
        }

        /// <summary>启用筛选的列名（逗号分隔）；留空表示全部列。</summary>
        public static readonly DependencyProperty FilterColumnsProperty =
            DependencyProperty.Register(
                nameof(FilterColumns),
                typeof(string),
                typeof(DataGridTabItem),
                new PropertyMetadata(string.Empty, OnSyncRequired));

        /// <summary>启用筛选的列名（逗号分隔）；留空表示全部列。</summary>
        public string FilterColumns
        {
            get => (string)GetValue(FilterColumnsProperty);
            set => SetValue(FilterColumnsProperty, value);
        }

        /// <summary>
        /// 自定义候选值提供者（可选）：筛选面板的候选值优先取它，返回 null 的列仍按实际取值统计。
        /// 用于把「按数据取值」换成「按业务固定选项」。
        /// </summary>
        public static readonly DependencyProperty FilterCandidateProviderProperty =
            DependencyProperty.Register(
                nameof(FilterCandidateProvider),
                typeof(FilterCandidateProvider),
                typeof(DataGridTabItem),
                new PropertyMetadata(null, OnSyncRequired));

        /// <summary>自定义候选值提供者（可选）。</summary>
        public FilterCandidateProvider? FilterCandidateProvider
        {
            get => (FilterCandidateProvider?)GetValue(FilterCandidateProviderProperty);
            set => SetValue(FilterCandidateProviderProperty, value);
        }

        /// <summary>
        /// 业务预设筛选表达式（可选，RowFilter 语法）：与表头筛选结果用 AND 叠加，而不是被覆盖。
        /// </summary>
        public static readonly DependencyProperty BaseRowFilterProperty =
            DependencyProperty.Register(
                nameof(BaseRowFilter),
                typeof(string),
                typeof(DataGridTabItem),
                new PropertyMetadata(string.Empty, OnSyncRequired));

        /// <summary>业务预设筛选表达式（可选，RowFilter 语法）。</summary>
        public string BaseRowFilter
        {
            get => (string)GetValue(BaseRowFilterProperty);
            set => SetValue(BaseRowFilterProperty, value);
        }

        /// <summary>候选值统计上限（默认 1000；0 表示不限制）。仅对「按实际取值统计」的列生效。</summary>
        public static readonly DependencyProperty MaxDistinctValuesProperty =
            DependencyProperty.Register(
                nameof(MaxDistinctValues),
                typeof(int),
                typeof(DataGridTabItem),
                new PropertyMetadata(DataGridExcelFilterBehavior.DefaultMaxDistinctValues, OnSyncRequired));

        /// <summary>候选值统计上限（默认 1000；0 表示不限制）。</summary>
        public int MaxDistinctValues
        {
            get => (int)GetValue(MaxDistinctValuesProperty);
            set => SetValue(MaxDistinctValuesProperty, value);
        }

        /// <summary>校验列/属性名（默认“校验”）；留空表示不按校验着色。</summary>
        public static readonly DependencyProperty ValidationColumnProperty =
            DependencyProperty.Register(
                nameof(ValidationColumn),
                typeof(string),
                typeof(DataGridTabItem),
                new PropertyMetadata("校验", OnRowStyleRelevantChanged));

        /// <summary>校验列/属性名（默认“校验”）；留空表示不按校验着色。</summary>
        public string ValidationColumn
        {
            get => (string)GetValue(ValidationColumnProperty);
            set => SetValue(ValidationColumnProperty, value);
        }

        /// <summary>自定义校验取值转换器（默认使用内置规则，见 <see cref="ValidationSeverityConverter"/>）。</summary>
        public static readonly DependencyProperty ValidationConverterProperty =
            DependencyProperty.Register(
                nameof(ValidationConverter),
                typeof(IValueConverter),
                typeof(DataGridTabItem),
                new PropertyMetadata(null, OnRowStyleRelevantChanged));

        /// <summary>自定义校验取值转换器（默认使用内置规则，见 <see cref="ValidationSeverityConverter"/>）。</summary>
        public IValueConverter? ValidationConverter
        {
            get => (IValueConverter?)GetValue(ValidationConverterProperty);
            set => SetValue(ValidationConverterProperty, value);
        }

        /// <summary>自定义行样式；为 <c>null</c> 时使用按 <see cref="ValidationColumn"/> 生成的内置着色样式。</summary>
        public static readonly DependencyProperty RowStyleProperty =
            DependencyProperty.Register(
                nameof(RowStyle),
                typeof(Style),
                typeof(DataGridTabItem),
                new PropertyMetadata(null, OnRowStyleRelevantChanged));

        /// <summary>自定义行样式；为 <c>null</c> 时使用按 <see cref="ValidationColumn"/> 生成的内置着色样式。</summary>
        public Style? RowStyle
        {
            get => (Style?)GetValue(RowStyleProperty);
            set => SetValue(RowStyleProperty, value);
        }

        /// <summary>数据源为空时是否自动隐藏本 Tab（默认 true，与既有工具行为一致）。</summary>
        public static readonly DependencyProperty AutoHideWhenEmptyProperty =
            DependencyProperty.Register(
                nameof(AutoHideWhenEmpty),
                typeof(bool),
                typeof(DataGridTabItem),
                new PropertyMetadata(true, OnAutoHideWhenEmptyChanged));

        /// <summary>数据源为空时是否自动隐藏本 Tab（默认 true，与既有工具行为一致）。</summary>
        public bool AutoHideWhenEmpty
        {
            get => (bool)GetValue(AutoHideWhenEmptyProperty);
            set => SetValue(AutoHideWhenEmptyProperty, value);
        }

        /// <summary>是否正在加载/筛选/排序（本表格蒙板的显示依据；使用方也可驱动自己的耗时操作）。</summary>
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(
                nameof(IsBusy),
                typeof(bool),
                typeof(DataGridTabItem),
                new PropertyMetadata(false, OnBusyChanged));

        private static void OnBusyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((DataGridTabItem)d).SyncOverlay();

        /// <summary>是否正在加载/筛选/排序（本表格蒙板的显示依据；使用方也可驱动自己的耗时操作）。</summary>
        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        /// <summary>蒙板提示文本（如“正在筛选 1,000,000 行…”）。</summary>
        public static readonly DependencyProperty BusyTextProperty =
            DependencyProperty.Register(
                nameof(BusyText),
                typeof(string),
                typeof(DataGridTabItem),
                new PropertyMetadata("正在处理数据…"));

        /// <summary>蒙板提示文本。</summary>
        public string BusyText
        {
            get => (string)GetValue(BusyTextProperty);
            set => SetValue(BusyTextProperty, value);
        }

        private static readonly DependencyPropertyKey StatusTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(StatusText),
                typeof(string),
                typeof(DataGridTabItem),
                new PropertyMetadata(string.Empty));

        /// <summary>状态栏文本（如“「每周人力」：显示 12 / 30 行 × 8 列”），随数据源与筛选变化自动更新。</summary>
        public static readonly DependencyProperty StatusTextProperty = StatusTextPropertyKey.DependencyProperty;

        /// <summary>状态栏文本，随数据源与筛选变化自动更新。</summary>
        public string StatusText => (string)GetValue(StatusTextProperty);

        /// <summary>自定义数据列；留空时按数据源自动生成列。</summary>
        public ObservableCollection<DataGridColumnItem> Columns { get; } = new();

        /// <summary>数据版本号：每次数据变化 +1，供筛选面板的候选值缓存判断是否需要重算。</summary>
        public int DataVersion { get; private set; }

        /// <summary>
        /// 加载蒙板：以 Adorner 按需挂载，只在 <see cref="IsBusy"/> 为 true 时存在，空闲零开销。
        /// 蒙板只覆盖本表格，切换其它 Tab 不受影响。
        /// </summary>
        private void SyncOverlay()
        {
            if (Table == null)
                return;

            if (IsBusy)
            {
                if (_overlay == null)
                {
                    var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(Table);
                    if (layer == null)
                        return;

                    _overlay = new BusyOverlayAdorner(Table, BusyText);
                    layer.Add(_overlay);
                }

                _overlay.Text = BusyText;
                return;
            }

            if (_overlay != null)
            {
                System.Windows.Documents.AdornerLayer.GetAdornerLayer(Table)?.Remove(_overlay);
                _overlay = null;
            }
        }

        /// <summary>
        /// 接管 DataView 数据源的排序：先出蒙板、让出一帧再执行 <c>DataView.Sort</c>，
        /// 避免百万行排序时界面"卡住不动"的观感；非 DataView 数据源保持 DataGrid 默认排序（数据量小）。
        /// </summary>
        private void OnGridSorting(object? sender, DataGridSortingEventArgs e)
        {
            // 服务端排序模式：库**不接管**（也不能设 Handled），把事件留给派生类。
            // 否则库先执行并 Handled=true，派生类注册的 Sorting 处理器根本收不到事件，
            // 结果是"排序只对已加载的 N 行本地生效、表头箭头也由库的路径决定"。
            if (IsServerSortEnabled)
                return;

            if (ItemsSource is not DataView view || _grid == null)
                return;

            e.Handled = true;

            var ascending = e.Column.SortDirection != ListSortDirection.Ascending;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                ResetColumnSortDirections();     // 不按 Shift 时单列排序

            e.Column.SortDirection = ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            var expression = BuildSortExpression(_grid);
            DataGridBusy.Run(_grid, view.Count, $"正在排序 {view.Count:N0} 行…", () => view.Sort = expression);
        }

        /// <summary>按各列 SortDirection 生成 DataView 排序表达式（列名取排序成员/绑定路径/表头）。</summary>
        private static string BuildSortExpression(DataGrid grid)
        {
            var parts = new List<string>();
            foreach (var column in grid.Columns)
            {
                if (column.SortDirection == null)
                    continue;

                var name = GetColumnName(column);
                if (!string.IsNullOrEmpty(name))
                    parts.Add($"[{name}] {(column.SortDirection == ListSortDirection.Descending ? "DESC" : "ASC")}");
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// 取列对应的「数据字段名」。派生类（如自定义排序）应复用它，**不要**用
        /// <c>column.Header.ToString()</c> —— 开启列头筛选后 Header 是 <see cref="ExcelFilterHeader"/> 控件，
        /// 取到的是控件类名而不是字段名。
        /// </summary>
        protected static string? GetColumnName(DataGridColumn column)
        {
            if (!string.IsNullOrEmpty(column.SortMemberPath))
                return column.SortMemberPath;

            if (column is DataGridBoundColumn bound
                && bound.Binding is Binding binding
                && !string.IsNullOrEmpty(binding.Path?.Path))
            {
                return binding.Path!.Path;
            }

            return column.Header switch
            {
                ExcelFilterHeader filter => filter.ColumnName,
                string text => text,
                _ => null
            };
        }

        /// <summary>属性变化后把值同步到表格（只读、筛选列、是否启用筛选等）。</summary>
        private static void OnSyncRequired(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tabItem = (DataGridTabItem)d;
            tabItem.SyncGrid();
            tabItem.ApplyEmptyVisibility();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tabItem = (DataGridTabItem)d;
            tabItem.ApplySourceDependentState();
        }

        private static void OnAutoHideWhenEmptyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGridTabItem)d).ApplySourceDependentState();
        }

        private static void OnRowStyleRelevantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tabItem = (DataGridTabItem)d;
            tabItem.RefreshEffectiveRowStyle();
        }

        /// <summary>
        /// 实际生效的行样式（自定义 <see cref="RowStyle"/> 优先，否则用按校验列生成的内置着色样式）。
        /// 由模板直接绑定到 DataGrid 的 <c>RowStyle</c>：
        /// 若改在模板应用之后赋值（AttachGrid 里），大表会因此重算一遍触发器与行高（实测 5 万行差 20 倍）。
        /// </summary>
        private static readonly DependencyPropertyKey EffectiveRowStylePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(EffectiveRowStyle),
                typeof(Style),
                typeof(DataGridTabItem),
                new PropertyMetadata(null));

        /// <summary>实际生效的行样式（供模板绑定）。</summary>
        public static readonly DependencyProperty EffectiveRowStyleProperty = EffectiveRowStylePropertyKey.DependencyProperty;

        /// <summary>实际生效的行样式（自定义 <see cref="RowStyle"/> 优先，否则为内置校验着色样式）。</summary>
        public Style? EffectiveRowStyle => (Style?)GetValue(EffectiveRowStyleProperty);

        private void RefreshEffectiveRowStyle()
        {
            SetValue(EffectiveRowStylePropertyKey, RowStyle ?? BuildValidationRowStyle());

            if (Table != null && !ReferenceEquals(Table.RowStyle, EffectiveRowStyle))
                Table.RowStyle = EffectiveRowStyle;
        }

        /// <summary>数据源变化后的联动：筛选联动订阅、自动显隐、状态栏文本。</summary>
        private void ApplySourceDependentState()
        {
            SyncGrid();

            if (_view != null)
                _view.ListChanged -= OnViewListChanged;

            _view = ItemsSource as DataView;
            if (_view != null)
                _view.ListChanged += OnViewListChanged;

            DataVersion++;     // 数据源换了：筛选候选值缓存随之失效

            ApplyEmptyVisibility();
            UpdateStatusText();
        }

        /// <summary>
        /// 无数据时隐藏本 Tab：ItemsSource 为 null，或集合/视图为 0 行，都算无数据。
        /// 当前选中（正在看）的 Tab 不隐藏——否则数据短暂刷新成 0 行时会把用户正在看的页面顶掉。
        /// </summary>
        private void ApplyEmptyVisibility()
        {
            var hide = AutoHideWhenEmpty && !IsSelected && IsEmpty(ItemsSource);
            Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        private static bool IsEmpty(IEnumerable? source) => source switch
        {
            null => true,
            DataView view => view.Count == 0,
            DataTable table => table.Rows.Count == 0,
            ICollection collection => collection.Count == 0,
            _ => false
        };

        private void OnViewListChanged(object? sender, ListChangedEventArgs e)
        {
            DataVersion++;

            // 批量装载期间的逐行通知直接忽略，统计在 BulkLoad 结束时统一刷新一次
            if (!_isBulkLoading)
                RequestStatusUpdate();
        }

        /// <summary>
        /// 状态栏统计合并刷新：同一轮消息循环内的多次数据变化只统计一次，
        /// 避免大表逐行编辑（或未包 <see cref="BulkLoad"/> 的批量追加）时反复全表计数拖慢界面。
        /// </summary>
        private void RequestStatusUpdate()
        {
            if (_statusUpdateQueued)
                return;

            _statusUpdateQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _statusUpdateQueued = false;
                UpdateStatusText();
                ApplyEmptyVisibility();   // 行数变化会影响"无数据隐藏"的判断
            }));
        }

        /// <summary>
        /// 大数据量加载包装（建议 10 万行以上、且数据表已绑定到界面时使用）：
        /// 期间忽略逐行通知、对 DataTable/DataView 数据源启用 <c>BeginLoadData</c>，结束时统一提交一次，
        /// 百万行批量追加不会产生百万次界面刷新。
        ///
        /// 更彻底的方案是「先离线构建、再一次性绑定」：在后台线程把数据表建好（可自行 BeginLoadData），
        /// 再赋值给 <see cref="ItemsSource"/>，界面只构建一次。
        /// 用法：<c>tab.BulkLoad(() =&gt; { for (...) table.Rows.Add(row); });</c>
        /// </summary>
        public void BulkLoad(Action load)
        {
            if (load == null)
                throw new ArgumentNullException(nameof(load));

            if (_isBulkLoading)
            {
                load();
                return;
            }

            var table = GetSourceTable();
            var loadDataActive = false;

            _isBulkLoading = true;
            BusyText = "正在批量装载数据…";
            IsBusy = true;
            try
            {
                if (table != null)
                {
                    try
                    {
                        table.BeginLoadData();
                        loadDataActive = true;
                    }
                    catch
                    {
                        // 表结构不支持批量模式（唯一约束、表达式列等）时退化为普通追加
                    }
                }

                load();
            }
            finally
            {
                if (loadDataActive)
                {
                    try
                    {
                        table!.EndLoadData();
                    }
                    catch
                    {
                        // 结束批量模式失败不影响后续刷新
                    }
                }

                _isBulkLoading = false;
                IsBusy = false;
                UpdateStatusText();
                ApplyEmptyVisibility();
            }
        }

        /// <summary>
        /// 内置「清除筛选」完成后触发。
        /// 服务端筛选的使用方（如只加载前 N 行、筛选下推数据库）必须订阅它清理自己的条件，
        /// 否则点上层的「清除筛选」看起来毫无反应。
        /// </summary>
        public event EventHandler? FilterCleared;

        /// <summary>内置「清除排序」时触发（服务端排序的使用方据此清掉自己的排序）。</summary>
        public event EventHandler? SortCleared;

        /// <summary>清除本表所有列的筛选（库按"无筛选就不重算"处理，大表上不做无用功）。</summary>
        public void ClearFilter()
        {
            if (_grid != null)
                DataGridExcelFilterBehavior.ClearFilters(_grid);

            FilterCleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 服务端排序模式：为 true 时库不接管列头排序（不设 Handled），
        /// 由派生类自行排序并维护表头箭头。适用于"只显示前 N 行、排序下推数据库"的场景。
        /// </summary>
        public bool IsServerSortEnabled { get; set; }

        /// <summary>清除本表排序：有排序时会重排一次（自动带蒙板），无排序时只复位表头标记。</summary>
        public void ClearSort()
        {
            // 先通知使用方（服务端排序时库这边没有 DataView.Sort 可清，必须由使用方重置并重新取数）
            SortCleared?.Invoke(this, EventArgs.Empty);

            if (ItemsSource is not DataView view || string.IsNullOrEmpty(view.Sort))
            {
                ResetColumnSortDirections();
                return;
            }

            DataGridBusy.Run(_grid, view.Count, $"正在清除排序（{view.Count:N0} 行）…", () =>
            {
                view.Sort = string.Empty;
                ResetColumnSortDirections();
            });
        }

        private void ResetColumnSortDirections()
        {
            if (_grid == null)
                return;

            foreach (var column in _grid.Columns)
                column.SortDirection = null;
        }

        /// <summary>取数据源对应的 DataTable（DataView 的底层表 / 直接绑定的表），非表结构返回 null。</summary>
        private DataTable? GetSourceTable() => ItemsSource switch
        {
            DataView view => view.Table,
            DataTable table => table,
            _ => null
        };

        /// <summary>状态栏文本：DataView 感知筛选（显示 N / 总行数），其余按集合元素数。</summary>
        private void UpdateStatusText()
        {
            var title = Header?.ToString() ?? string.Empty;

            var text = ItemsSource switch
            {
                DataView view => FormatViewStatus(title, view),
                DataTable table => $"「{title}」：{table.Rows.Count} 行 × {table.Columns.Count} 列",
                ICollection collection => $"「{title}」：{collection.Count} 行",
                _ => string.Empty
            };

            SetValue(StatusTextPropertyKey, text);
        }

        private static string FormatViewStatus(string title, DataView view)
        {
            var total = view.Table?.Rows.Count ?? 0;
            var columns = view.Table?.Columns.Count ?? 0;

            return view.Count != total
                ? $"「{title}」：显示 {view.Count} / {total} 行 × {columns} 列"
                : $"「{title}」：{view.Count} 行 × {columns} 列";
        }

        /// <summary>
        /// 生成内置行着色样式：按校验取值绑定触发器设置整行底色。
        /// 仅使用行级触发器（不重建行、不影响虚拟化）；校验列为空时返回空样式（不着色）。
        /// </summary>
        private Style BuildValidationRowStyle()
        {
            var style = new Style(typeof(DataGridRow));
            if (string.IsNullOrEmpty(ValidationColumn))
                return style;

            var binding = new Binding(ValidationColumn)
            {
                Converter = ValidationConverter ?? new ValidationSeverityConverter()
            };

            AddSeverityTrigger(style, binding, ValidationSeverityConverter.Ok, ValidationOkBrushKey, FallbackOkColor);
            AddSeverityTrigger(style, binding, ValidationSeverityConverter.Warning, ValidationWarningBrushKey, FallbackWarningColor);
            AddSeverityTrigger(style, binding, ValidationSeverityConverter.Error, ValidationErrorBrushKey, FallbackErrorColor);
            return style;
        }

        private void AddSeverityTrigger(Style style, Binding binding, string severity, string brushKey, Color fallback)
        {
            var trigger = new DataTrigger
            {
                Binding = binding,
                Value = severity
            };
            trigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, ResolveBrush(brushKey, fallback)));
            style.Triggers.Add(trigger);
        }

        /// <summary>取主题画刷（消费方可在应用资源中覆盖同键值），无则退回内置色值。</summary>
        private Brush ResolveBrush(string brushKey, Color fallback)
        {
            if (TryFindResource(brushKey) is Brush brush)
                return brush;

            var solid = new SolidColorBrush(fallback);
            solid.Freeze();
            return solid;
        }
    }
}
