using Namsiz.WPF.Window.Ribbon.Behaviors;
using Namsiz.WPF.Window.Ribbon.Collections;
using Namsiz.WPF.Window.Ribbon.Controls;
using Namsiz.WPF.Window.Ribbon.Logging;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RibbonControl = Namsiz.WPF.Window.Ribbon.Controls.Ribbon;

namespace Namsiz.WPF.Window.Ribbon
{
    /// <summary>
    /// 通用带功能区菜单的工具窗口：
    /// 功能栏（功能区页签 + 居中标题 + 置顶按钮）、内容区（Tab 控件，首项为内置日志 Tab）、状态栏与忙碌遮罩，
    /// 窗口外观沿用 Namsiz.WPF.Window（自定义标题栏）样式。
    ///
    /// 使用方式：引用本库后定义窗口类并继承 <see cref="RibbonWindow"/>，
    /// 在 <see cref="RibbonTabs"/> 中声明菜单页（<see cref="RibbonTabItem"/> + <see cref="RibbonGroup"/> + 内置 Button）并绑定命令，
    /// 在 <see cref="TabItems"/> 中声明数据 Tab（<see cref="DataGridTabItem"/> 或自定义派生类），无需再搭建窗口框架。
    /// </summary>
    // 基类显式限定 System.Windows.Window：命名空间 Namsiz.WPF.Window 下，简单名 Window 会解析为命名空间。
    public class RibbonWindow : System.Windows.Window
    {
        private const string IconKeyClearSort = "Namsiz.RibbonWindow.Images.ClearSort";
        private const string IconKeyClearFilter = "Namsiz.RibbonWindow.Images.ClearFilter";
        private const string IconKeyExportLog = "Namsiz.RibbonWindow.Images.ExportLog";

        private readonly List<Button> _exportLogButtons = new();
        private readonly List<Button> _clearFilterButtons = new();
        private readonly List<Button> _clearSortButtons = new();
        private readonly ICommand _defaultExportLogCommand;
        private readonly ICommand _defaultClearFilterCommand;
        private readonly ICommand _defaultClearSortCommand;

        /// <summary>库主题字典的包 URI。</summary>
        private const string ThemeDictionaryUri = "pack://application:,,,/Namsiz.WPF.Window.Ribbon;component/Themes/Generic.xaml";

        /// <summary>默认窗口图标（矢量图表图标）资源键。</summary>
        private const string DefaultIconKey = "Namsiz.RibbonWindow.Images.DefaultIcon";

        static RibbonWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(RibbonWindow),
                new FrameworkPropertyMetadata(typeof(RibbonWindow)));

            EnsureThemeResources();
        }

        public RibbonWindow()
        {
            // 默认居中显示（窗口样式里的同名 Setter 在部分 WPF 版本上无法解析为依赖属性，故在构造时设置；使用方仍可自行覆盖）
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // 默认菜单按钮的动作：默认实现走 On* 虚方法，派生窗口可重写，也可替换对应的 *Command 依赖属性
            _defaultExportLogCommand = new DelegateCommand(OnExportLog);
            _defaultClearFilterCommand = new DelegateCommand(OnClearFilter);
            _defaultClearSortCommand = new DelegateCommand(OnClearSort);

            DefaultMenuTab = BuildDefaultMenuTab();

            // 内容区（Tab 控件 + 状态栏 + 忙碌遮罩）由库统一提供
            Content = new RibbonShell();

            LogTab = new LogTabItem { ItemsSource = LogItems };
            TabItems.Add(LogTab);

            // 默认菜单页固定放在最后（使用方在 XAML 里声明的页排在前面）
            Loaded += (_, _) =>
            {
                if (ShowDefaultMenu && !RibbonTabs.Contains(DefaultMenuTab))
                    RibbonTabs.Add(DefaultMenuTab);
            };

            // 未指定图标时补一个默认图标，避免标题被显示到图标位置（见 ApplyDefaultIcon）
            Loaded += (_, _) => ApplyDefaultIcon();

            // 切换菜单页会重建按钮，切换后同样需要补齐命令参数
            AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnMenuTabSelectionChanged), true);
            ContentRendered += (_, _) => RefreshMenuCommandParameters();
        }

        /// <summary>
        /// 功能栏菜单页集合：直接添加 <see cref="RibbonTabItem"/>（内含 <see cref="RibbonGroup"/> 与按钮并绑定命令）。
        /// </summary>
        public ObservableCollection<RibbonTabItem> RibbonTabs { get; } = new();

        /// <summary>内容区 Tab 集合：首项为内置日志 Tab（见 <see cref="LogTab"/>），其余由使用方添加。</summary>
        public ObservableCollection<TabItem> TabItems { get; } = new();

        /// <summary>内置日志 Tab（默认自带，其数据源为 <see cref="LogItems"/>）。</summary>
        public LogTabItem LogTab { get; }

        /// <summary>日志数据（内置日志 Tab 的展示来源，由 <see cref="CreateLogSink"/> 自动写入）。</summary>
        public RangeObservableCollection<LogItem> LogItems { get; } = new();

        /// <summary>是否忙碌（忙碌时禁用功能栏菜单并显示遮罩）。</summary>
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(
                nameof(IsBusy),
                typeof(bool),
                typeof(RibbonWindow),
                new PropertyMetadata(false));

        /// <summary>是否忙碌（忙碌时禁用功能栏菜单并显示遮罩）。</summary>
        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        /// <summary>忙碌遮罩提示文本（如“数据导入中...”）。</summary>
        public static readonly DependencyProperty BusyTextProperty =
            DependencyProperty.Register(
                nameof(BusyText),
                typeof(string),
                typeof(RibbonWindow),
                new PropertyMetadata(string.Empty));

        /// <summary>忙碌遮罩提示文本（如“数据导入中...”）。</summary>
        public string BusyText
        {
            get => (string)GetValue(BusyTextProperty);
            set => SetValue(BusyTextProperty, value);
        }

        /// <summary>
        /// 忙碌进度百分比（0~100）。默认 <see cref="double.NaN"/> 表示“进度未知”，
        /// 此时遮罩上的进度条显示为不确定态（跑马灯）；给了值即显示确定态进度条。
        /// </summary>
        public static readonly DependencyProperty BusyProgressProperty =
            DependencyProperty.Register(
                nameof(BusyProgress),
                typeof(double),
                typeof(RibbonWindow),
                new PropertyMetadata(double.NaN, OnBusyProgressChanged));

        /// <summary>忙碌进度百分比（0~100）；<see cref="double.NaN"/> 表示进度未知。</summary>
        public double BusyProgress
        {
            get => (double)GetValue(BusyProgressProperty);
            set => SetValue(BusyProgressProperty, value);
        }

        private static readonly DependencyPropertyKey IsBusyProgressIndeterminatePropertyKey =
            DependencyProperty.RegisterReadOnly(
                "IsBusyProgressIndeterminate",
                typeof(bool),
                typeof(RibbonWindow),
                new PropertyMetadata(true));

        /// <summary>进度条是否处于不确定态（等于「未给出进度值」）。</summary>
        public static readonly DependencyProperty IsBusyProgressIndeterminateProperty =
            IsBusyProgressIndeterminatePropertyKey.DependencyProperty;

        /// <summary>进度条是否处于不确定态。</summary>
        public bool IsBusyProgressIndeterminate => (bool)GetValue(IsBusyProgressIndeterminateProperty);

        private static readonly DependencyPropertyKey BusyPercentTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                "BusyPercentText",
                typeof(string),
                typeof(RibbonWindow),
                new PropertyMetadata(string.Empty));

        /// <summary>忙碌遮罩上显示的百分比文本（如 “42%”）；进度未知时为空串。</summary>
        public static readonly DependencyProperty BusyPercentTextProperty =
            BusyPercentTextPropertyKey.DependencyProperty;

        /// <summary>忙碌遮罩上显示的百分比文本。</summary>
        public string BusyPercentText => (string)GetValue(BusyPercentTextProperty);

        private static void OnBusyProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (double)e.NewValue;
            bool unknown = double.IsNaN(value) || value < 0;

            d.SetValue(IsBusyProgressIndeterminatePropertyKey, unknown);
            d.SetValue(
                BusyPercentTextPropertyKey,
                unknown ? string.Empty : $"{Math.Min(100.0, Math.Max(0.0, value)):0}%");
        }

        /// <summary>状态栏空闲文本（未选中数据 Tab 时显示，如“就绪”）。</summary>
        public static readonly DependencyProperty IdleStatusTextProperty =
            DependencyProperty.Register(
                nameof(IdleStatusText),
                typeof(string),
                typeof(RibbonWindow),
                new PropertyMetadata("就绪"));

        /// <summary>状态栏空闲文本（未选中数据 Tab 时显示，如“就绪”）。</summary>
        public string IdleStatusText
        {
            get => (string)GetValue(IdleStatusTextProperty);
            set => SetValue(IdleStatusTextProperty, value);
        }

        /// <summary>产生新日志时是否自动切换到日志 Tab（默认 true）。</summary>
        public static readonly DependencyProperty AutoSwitchToLogTabProperty =
            DependencyProperty.Register(
                nameof(AutoSwitchToLogTab),
                typeof(bool),
                typeof(RibbonWindow),
                new PropertyMetadata(true));

        /// <summary>产生新日志时是否自动切换到日志 Tab（默认 true）。</summary>
        public bool AutoSwitchToLogTab
        {
            get => (bool)GetValue(AutoSwitchToLogTabProperty);
            set => SetValue(AutoSwitchToLogTabProperty, value);
        }

        /// <summary>
        /// 菜单按钮未显式指定命令参数时，是否自动把窗口作为命令参数（默认 true）。
        /// 菜单声明的元素在窗口可视化之前创建，{RelativeSource AncestorType=Window} 这类需要树的绑定无法解析，
        /// 因此库统一在按钮加载时补上窗口（弹窗 Owner 等场景直接可用），需要传别的参数时显式设置 CommandParameter 即可。
        /// </summary>
        public static readonly DependencyProperty UseWindowAsCommandParameterProperty =
            DependencyProperty.Register(
                nameof(UseWindowAsCommandParameter),
                typeof(bool),
                typeof(RibbonWindow),
                new PropertyMetadata(true));

        /// <summary>菜单按钮未显式指定命令参数时，是否自动把窗口作为命令参数（默认 true）。</summary>
        public bool UseWindowAsCommandParameter
        {
            get => (bool)GetValue(UseWindowAsCommandParameterProperty);
            set => SetValue(UseWindowAsCommandParameterProperty, value);
        }

        /// <summary>
        /// 创建写入内置日志 Tab 的 Serilog Sink，用法：
        /// <c>Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(window.CreateLogSink()).CreateLogger();</c>
        /// </summary>
        public ILogEventSink CreateLogSink()
            => new ObservableLogSink(
                LogItems,
                Dispatcher,
                // 运行时判断：AutoSwitchToLogTab 置 false 立即生效（不做无谓的焦点抢占）
                _ =>
                {
                    if (AutoSwitchToLogTab)
                        SwitchToLogTab();
                });

        /// <summary>切换到日志 Tab（新日志到达时的默认联动）。</summary>
        public void SwitchToLogTab()
        {
            LogTab.IsSelected = true;
        }

        /// <summary>
        /// 确保库主题字典在应用级可见（功能区控件的隐式样式与窗口外观都在其中）。
        /// 应用若已自行合并同一个字典（等价做法），此处不会重复添加。
        /// </summary>
        private static void EnsureThemeResources()
        {
            var app = Application.Current;
            if (app == null)
                return;

            foreach (var dictionary in app.Resources.MergedDictionaries)
            {
                if (string.Equals(dictionary.Source?.OriginalString, ThemeDictionaryUri, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            try
            {
                app.Resources.MergedDictionaries.Insert(0, new ResourceDictionary
                {
                    Source = new Uri(ThemeDictionaryUri, UriKind.Absolute)
                });
            }
            catch
            {
                // 主题合并失败时不阻断窗口创建（界面样式不完整，但功能可用）
            }
        }

        private void OnMenuTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshMenuCommandParametersLater();
        }

        /// <summary>菜单按钮在功能栏布局完成后才真正创建，故在布局之后补齐命令参数。</summary>
        private void RefreshMenuCommandParametersLater()
        {
            if (!UseWindowAsCommandParameter)
                return;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshMenuCommandParameters));
        }

        /// <summary>为功能栏内“已绑定命令且未指定参数”的按钮补上窗口本身（见 <see cref="UseWindowAsCommandParameterProperty"/>）。</summary>
        private void RefreshMenuCommandParameters()
        {
            if (!UseWindowAsCommandParameter)
                return;

            var ribbon = VisualTreeExt.FindDescendant<RibbonControl>(this);
            if (ribbon == null)
                return;

            foreach (var button in VisualTreeExt.FindDescendants<ButtonBase>(ribbon))
            {
                if (button.Command != null && button.CommandParameter == null)
                    button.CommandParameter = this;
            }
        }

        /// <summary>是否显示库自带的默认菜单页（「数据」页：组「排序和筛选」），默认 true；默认排在菜单最后。</summary>
        public static readonly DependencyProperty ShowDefaultMenuProperty =
            DependencyProperty.Register(
                nameof(ShowDefaultMenu),
                typeof(bool),
                typeof(RibbonWindow),
                new PropertyMetadata(true));

        /// <summary>是否显示库自带的默认菜单页（「数据」页：组「排序和筛选」）。</summary>
        public bool ShowDefaultMenu
        {
            get => (bool)GetValue(ShowDefaultMenuProperty);
            set => SetValue(ShowDefaultMenuProperty, value);
        }

        /// <summary>
        /// 库自带的默认菜单页「数据」：组「排序和筛选」（清除排序 / 清除筛选，均带默认图标）。
        /// 默认排在菜单最后；不想要时 <see cref="ShowDefaultMenu"/> 置 false。
        /// 导出日志按钮默认不出现，需要时用 <see cref="CreateExportLogButton"/> 放到任意菜单页/分组里。
        /// </summary>
        public RibbonTabItem DefaultMenuTab { get; }

        /// <summary>
        /// 创建「导出日志」菜单按钮（默认图标 + 默认命令），放到指定的 RibbonTab / RibbonGroup 即可，
        /// 也就是"在哪调用就显示在哪"。使用方替换 <see cref="ExportLogCommand"/> 可整体换命令。
        /// </summary>
        public Button CreateExportLogButton()
            => RegisterButton(CreateMenuButton("导出日志", _defaultExportLogCommand), IconKeyExportLog, _exportLogButtons);

        /// <summary>创建「清除排序」菜单按钮（默认图标 + 默认命令）。</summary>
        public Button CreateClearSortButton()
            => RegisterButton(CreateMenuButton("清除排序", _defaultClearSortCommand), IconKeyClearSort, _clearSortButtons);

        /// <summary>创建「清除筛选」菜单按钮（默认图标 + 默认命令）。</summary>
        public Button CreateClearFilterButton()
            => RegisterButton(CreateMenuButton("清除筛选", _defaultClearFilterCommand), IconKeyClearFilter, _clearFilterButtons);

        /// <summary>「导出」组「导出日志」按钮的命令；留空（默认）时用库内实现（导出内置日志 Tab 的全部日志）。</summary>
        public static readonly DependencyProperty ExportLogCommandProperty =
            DependencyProperty.Register(
                nameof(ExportLogCommand),
                typeof(ICommand),
                typeof(RibbonWindow),
                new PropertyMetadata(null, OnMenuCommandChanged));

        /// <summary>「导出」组「导出日志」按钮的命令；留空时用库内实现。</summary>
        public ICommand? ExportLogCommand
        {
            get => (ICommand?)GetValue(ExportLogCommandProperty);
            set => SetValue(ExportLogCommandProperty, value);
        }

        /// <summary>「筛选」组「清除筛选」按钮的命令；留空（默认）时用库内实现（清除当前数据 Tab 的列筛选）。</summary>
        public static readonly DependencyProperty ClearFilterCommandProperty =
            DependencyProperty.Register(
                nameof(ClearFilterCommand),
                typeof(ICommand),
                typeof(RibbonWindow),
                new PropertyMetadata(null, OnMenuCommandChanged));

        /// <summary>「筛选」组「清除筛选」按钮的命令；留空时用库内实现。</summary>
        public ICommand? ClearFilterCommand
        {
            get => (ICommand?)GetValue(ClearFilterCommandProperty);
            set => SetValue(ClearFilterCommandProperty, value);
        }

        /// <summary>「筛选」组「清除排序」按钮的命令；留空（默认）时用库内实现（清除当前数据 Tab 的排序）。</summary>
        public static readonly DependencyProperty ClearSortCommandProperty =
            DependencyProperty.Register(
                nameof(ClearSortCommand),
                typeof(ICommand),
                typeof(RibbonWindow),
                new PropertyMetadata(null, OnMenuCommandChanged));

        /// <summary>「筛选」组「清除排序」按钮的命令；留空时用库内实现。</summary>
        public ICommand? ClearSortCommand
        {
            get => (ICommand?)GetValue(ClearSortCommandProperty);
            set => SetValue(ClearSortCommandProperty, value);
        }

        /// <summary>当前选中的数据 Tab（供默认菜单动作使用；没有则返回 null）。</summary>
        protected DataGridTabItem? CurrentDataTab
        {
            get
            {
                foreach (var tab in TabItems)
                {
                    if (tab is DataGridTabItem dataTab && dataTab.IsSelected)
                        return dataTab;
                }

                return null;
            }
        }

        /// <summary>默认「导出日志」动作：弹保存对话框后调用 <see cref="ExportLogTo"/>；派生窗口可重写。</summary>
        protected virtual void OnExportLog()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出日志",
                FileName = $"日志_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            ExportLogTo(dialog.FileName);
        }

        /// <summary>默认「清除筛选」动作：只清当前数据 Tab 的列筛选（无筛选时不重算）；派生窗口可重写。</summary>
        protected virtual void OnClearFilter() => CurrentDataTab?.ClearFilter();

        /// <summary>默认「清除排序」动作：只清当前数据 Tab 的排序（无排序时不重排）；派生窗口可重写。</summary>
        protected virtual void OnClearSort() => CurrentDataTab?.ClearSort();

        /// <summary>把内置日志 Tab 的日志写入指定文件（时间 / 级别 / 内容，UTF-8）。</summary>
        public void ExportLogTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                using (var writer = new System.IO.StreamWriter(path, false, Encoding.UTF8))
                {
                    foreach (var item in LogItems)
                    {
                        writer.Write('[');
                        writer.Write(item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                        writer.Write("] ");
                        writer.Write(item.Kind switch
                        {
                            LogKind.Success => "成功 ",
                            LogKind.Warning => "警告 ",
                            LogKind.Error => "错误 ",
                            LogKind.Debug => "调试 ",
                            _ => "信息 "
                        });
                        writer.WriteLine(item.Message);
                    }
                }

                Log.Information("✅ 日志已导出：{Path}（{Count} 条）", path, LogItems.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ 导出日志失败：{Path}", path);
            }
        }

        private static void OnMenuCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RibbonWindow)d).UpdateMenuCommands();

        /// <summary>把默认菜单按钮的命令切到"使用方指定命令（若有）或库内默认实现"。</summary>
        private void UpdateMenuCommands()
        {
            foreach (var button in _exportLogButtons)
                button.Command = ExportLogCommand ?? _defaultExportLogCommand;

            foreach (var button in _clearFilterButtons)
                button.Command = ClearFilterCommand ?? _defaultClearFilterCommand;

            foreach (var button in _clearSortButtons)
                button.Command = ClearSortCommand ?? _defaultClearSortCommand;
        }

        private RibbonTabItem BuildDefaultMenuTab()
        {
            var group = new RibbonGroup { Header = "排序和筛选" };

            // 顺序：先清除排序，再清除筛选
            group.Items.Add(CreateClearSortButton());
            group.Items.Add(CreateClearFilterButton());

            var tab = new RibbonTabItem { Header = "数据" };
            tab.Groups.Add(group);
            return tab;
        }

        private Button RegisterButton(Button button, string iconKey, List<Button> registry)
        {
            if (TryFindResource(iconKey) is ImageSource icon)
                RibbonProperties.SetIcon(button, icon);

            registry.Add(button);
            UpdateMenuCommands();
            return button;
        }

        private static Button CreateMenuButton(string content, ICommand command)
            => new Button { Content = content, Command = command };

        /// <summary>
        /// 未指定窗口图标时套用库内置的矢量图表图标。
        /// 必要性：标题槽就在图标右侧，没有图标时标题会被顶到图标位置（观感上"图标区显示成了标题"）。
        /// 使用方通过 Window.Icon（XAML 或样式）指定图标即可覆盖本默认值。
        /// </summary>
        private void ApplyDefaultIcon()
        {
            if (Icon != null)
                return;

            try
            {
                Icon = TryFindResource(DefaultIconKey) as ImageSource;
            }
            catch
            {
                // 资源未就绪时忽略：只影响窗口图标，不影响功能
            }
        }
    }
}
