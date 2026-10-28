using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 功能区（Ribbon）：页签栏 + 命令区，自带最小化能力。
    ///
    /// 与窗口标题的关系：标题在页签按钮与右上角窗口按钮（含内置的置顶按钮）之间的空白区域内自动居中，
    /// 因此本控件接管了功能栏标题的显示，使用方不必再写功能栏模板。
    ///
    /// 使用方式（通常由 <c>RibbonWindow</c> 在内部创建，使用方无需直接使用）：
    /// <code>
    /// &lt;r:Ribbon Title="我的工具" ItemsSource="{Binding RibbonTabs}" /&gt;
    /// </code>
    /// </summary>
    [TemplatePart(Name = PartHeaders, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartContentHost, Type = typeof(ContentControl))]
    [TemplatePart(Name = PartPopupHost, Type = typeof(ContentControl))]
    [TemplatePart(Name = PartGroups, Type = typeof(ItemsControl))]
    [TemplatePart(Name = PartPopup, Type = typeof(Popup))]
    public class Ribbon : TabControl
    {
        private const string PartHeaders = "PART_Headers";
        private const string PartContentHost = "PART_ContentHost";
        private const string PartPopupHost = "PART_PopupHost";
        private const string PartGroups = "PART_Groups";
        private const string PartPopup = "PART_Popup";
        private const string PartTabScroll = "PART_TabScroll";
        private const string PartMinimizeButton = "PART_MinimizeButton";
        private const string PartTopmostButton = "PART_TopmostButton";
        private const string PartTopmostSeparator = "PART_TopmostSeparator";
        private const string PartWindowIcon = "PART_WindowIcon";

        /// <summary>窗口再窄也要给标题留出的宽度。</summary>
        private const double MinTitleWidth = 60;

        /// <summary>页签区的下限宽度（再窄就只能横向滚动）。</summary>
        private const double MinTabStripWidth = 80;

        private FrameworkElement? _headers;
        private ContentControl? _contentHost;
        private ContentControl? _popupHost;
        private ItemsControl? _groups;
        private Popup? _popup;
        private ScrollViewer? _tabScroll;
        private FrameworkElement? _minimizeButton;
        private FrameworkElement? _topmostButton;
        private FrameworkElement? _topmostSeparator;
        private FrameworkElement? _windowIcon;
        private System.Windows.Window? _hostWindow;

        /// <summary>当前弹出面板对应的页签（用于"再点一下同一页签就收起"）。</summary>
        private RibbonTabItem? _popupOwnerTab;

        /// <summary>
        /// 最小化时是否允许"悬停即下拉"。点击过任意页签后置 false（鼠标移到别的页签也不再自动下拉），
        /// 双击页签栏恢复默认状态时重新置 true。
        /// </summary>
        private bool _hoverPreview = true;

        /// <summary>当前面板是"悬停打开"的还是"点击钉住"的（决定再点一次是否收起）。</summary>
        private bool _hoverOpened;

        /// <summary>鼠标当前悬停的页签（用于避免重复触发）。</summary>
        private RibbonTabItem? _hoverTab;

        /// <summary>模板里声明的页签区上限宽度（宽松环境下按它显示）。</summary>
        private double _tabStripMaxWidth = double.NaN;

        static Ribbon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(Ribbon),
                new FrameworkPropertyMetadata(typeof(Ribbon)));
        }

        /// <summary>功能栏标题（通常绑定窗口标题）。</summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(string.Empty));

        /// <summary>功能栏标题（通常绑定窗口标题）。</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>功能区是否最小化（只显示页签，命令区折叠）。</summary>
        public static readonly DependencyProperty IsMinimizedProperty =
            DependencyProperty.Register(
                nameof(IsMinimized),
                typeof(bool),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(false, OnIsMinimizedChanged));

        /// <summary>功能区是否最小化（只显示页签，命令区折叠）。</summary>
        public bool IsMinimized
        {
            get => (bool)GetValue(IsMinimizedProperty);
            set => SetValue(IsMinimizedProperty, value);
        }

        /// <summary>最小化状态下弹出的命令面板是否展开。</summary>
        public static readonly DependencyProperty IsPopupOpenProperty =
            DependencyProperty.Register(
                nameof(IsPopupOpen),
                typeof(bool),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(false));

        /// <summary>最小化状态下弹出的命令面板是否展开。</summary>
        public bool IsPopupOpen
        {
            get => (bool)GetValue(IsPopupOpenProperty);
            set => SetValue(IsPopupOpenProperty, value);
        }

        /// <summary>窗口是否置顶（与窗口的 <c>Topmost</c> 双向绑定）。</summary>
        public static readonly DependencyProperty IsTopmostProperty =
            DependencyProperty.Register(
                nameof(IsTopmost),
                typeof(bool),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>窗口是否置顶。</summary>
        public bool IsTopmost
        {
            get => (bool)GetValue(IsTopmostProperty);
            set => SetValue(IsTopmostProperty, value);
        }

        /// <summary>画在功能区左上角的窗口图标（由 <c>RibbonWindow.Icon</c> 自动绑定）。</summary>
        public static readonly DependencyProperty WindowIconProperty =
            DependencyProperty.Register(
                nameof(WindowIcon),
                typeof(ImageSource),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>画在功能区左上角的窗口图标。</summary>
        public ImageSource? WindowIcon
        {
            get => (ImageSource?)GetValue(WindowIconProperty);
            set => SetValue(WindowIconProperty, value);
        }

        /// <summary>是否显示内置的置顶按钮（默认显示）。</summary>
        public static readonly DependencyProperty ShowTopmostButtonProperty =
            DependencyProperty.Register(
                nameof(ShowTopmostButton),
                typeof(bool),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(true));

        /// <summary>是否显示内置的置顶按钮（默认显示）。</summary>
        public bool ShowTopmostButton
        {
            get => (bool)GetValue(ShowTopmostButtonProperty);
            set => SetValue(ShowTopmostButtonProperty, value);
        }

        /// <summary>
        /// 右侧为窗口按钮（最小化/最大化/关闭）预留的宽度，默认 103（3×33 + 间隔）。
        /// 该宽度参与标题居中计算，因此标题不会与窗口按钮重叠。
        /// </summary>
        public static readonly DependencyProperty WindowButtonsWidthProperty =
            DependencyProperty.Register(
                nameof(WindowButtonsWidth),
                typeof(double),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(103.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>右侧为窗口按钮预留的宽度。</summary>
        public double WindowButtonsWidth
        {
            get => (double)GetValue(WindowButtonsWidthProperty);
            set => SetValue(WindowButtonsWidthProperty, value);
        }

        /// <summary>页签栏高度，默认 33（与窗口按钮同高，标题与按钮自然对齐）。</summary>
        public static readonly DependencyProperty HeaderHeightProperty =
            DependencyProperty.Register(
                nameof(HeaderHeight),
                typeof(double),
                typeof(Ribbon),
                new FrameworkPropertyMetadata(33.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>页签栏高度。</summary>
        public double HeaderHeight
        {
            get => (double)GetValue(HeaderHeightProperty);
            set => SetValue(HeaderHeightProperty, value);
        }

        /// <summary>创建功能区。</summary>
        public Ribbon()
        {
            // 页签集合由 ItemsSource 绑定进来时，TabControl 的"自动选中首项"可能还没来得及执行
            // （绑定在建树之前完成，容器尚未生成），这里在容器就绪后补一次默认选中。
            ItemContainerGenerator.StatusChanged += (_, _) => EnsureSelection();
        }

        /// <inheritdoc />
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            EnsureSelection();
        }

        /// <summary>没有选中项时默认选中第一页（命令区才有内容可显示）。</summary>
        private void EnsureSelection()
        {
            if (SelectedIndex >= 0 || Items.Count == 0)
                return;

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (SelectedIndex < 0 && Items.Count > 0)
                        SelectedIndex = 0;
                }),
                DispatcherPriority.Loaded);
        }

        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            if (_headers != null)
            {
                _headers.PreviewMouseLeftButtonDown -= OnHeadersMouseDown;
                _headers.PreviewMouseLeftButtonUp -= OnHeadersMouseUp;
                _headers.MouseMove -= OnHeadersMouseMove;
                _headers.MouseLeave -= OnHeadersMouseLeave;
            }

            if (_popup != null)
                _popup.Closed -= OnPopupClosed;

            base.OnApplyTemplate();

            _headers = GetTemplateChild(PartHeaders) as FrameworkElement;
            _contentHost = GetTemplateChild(PartContentHost) as ContentControl;
            _popupHost = GetTemplateChild(PartPopupHost) as ContentControl;
            _groups = GetTemplateChild(PartGroups) as ItemsControl;
            _popup = GetTemplateChild(PartPopup) as Popup;
            _tabScroll = GetTemplateChild(PartTabScroll) as ScrollViewer;
            _minimizeButton = GetTemplateChild(PartMinimizeButton) as FrameworkElement;
            _topmostButton = GetTemplateChild(PartTopmostButton) as FrameworkElement;
            _topmostSeparator = GetTemplateChild(PartTopmostSeparator) as FrameworkElement;
            _windowIcon = GetTemplateChild(PartWindowIcon) as FrameworkElement;

            // 模板里的上限宽度是"宽松环境"的取值，窄窗口下由代码收窄（见 UpdateTabStripWidth）
            if (_tabScroll != null)
                _tabStripMaxWidth = _tabScroll.MaxWidth;

            if (_headers != null)
            {
                _headers.PreviewMouseLeftButtonDown += OnHeadersMouseDown;
                _headers.PreviewMouseLeftButtonUp += OnHeadersMouseUp;
                _headers.MouseMove += OnHeadersMouseMove;
                _headers.MouseLeave += OnHeadersMouseLeave;
            }

            if (_popup != null)
                _popup.Closed += OnPopupClosed;

            // 模板里命令区默认挂在内容宿主上，这里统一交给 UpdateContentLocation 定位
            if (_contentHost != null && ReferenceEquals(_contentHost.Content, _groups))
                _contentHost.Content = null;

            UpdateContentLocation();
            UpdateTabStripWidth();

            Loaded -= OnRibbonLoaded;
            Unloaded -= OnRibbonUnloaded;
            Loaded += OnRibbonLoaded;
            Unloaded += OnRibbonUnloaded;

            if (_groups != null)
            {
                _groups.PreviewMouseUp -= OnGroupsPreviewMouseUp;
                _groups.PreviewMouseUp += OnGroupsPreviewMouseUp;
            }
        }

        private void OnRibbonLoaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow != null)
                return;

            _hostWindow = System.Windows.Window.GetWindow(this);
            if (_hostWindow == null)
                return;

            // 弹层是 StaysOpen=True（为了保留"悬停即切换"），因此"点别处收起"由这里统一负责
            _hostWindow.PreviewMouseDown += OnHostWindowMouseDown;
            _hostWindow.Deactivated += OnHostWindowDeactivated;
        }

        private void OnRibbonUnloaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow == null)
                return;

            _hostWindow.PreviewMouseDown -= OnHostWindowMouseDown;
            _hostWindow.Deactivated -= OnHostWindowDeactivated;
            _hostWindow = null;
        }

        private void OnHostWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 忙碌时功能区整条被禁用（IsEnabled=false）：禁用元素不参与命中测试，
            // 点击事件根本到不了功能区，所以这里按坐标兜底，保证标题栏仍能拖动窗口。
            if (!IsEnabled)
            {
                TryDragFromDisabledHeader(e);
                return;
            }

            if (!IsPopupOpen)
                return;

            // 点在页签上：交给页签逻辑（钉住 / 收起 / 切换）
            if (FindTabItem(e.OriginalSource as DependencyObject) != null)
                return;

            IsPopupOpen = false;
        }

        /// <summary>功能区被禁用时的标题栏拖拽兜底：只认页签栏那一行的空白处。</summary>
        private void TryDragFromDisabledHeader(MouseButtonEventArgs e)
        {
            if (_headers == null || _hostWindow == null)
                return;

            var point = e.GetPosition(this);
            var headerHeight = _headers.ActualHeight > 0 ? _headers.ActualHeight : HeaderHeight;
            if (point.X < 0 || point.X > ActualWidth || point.Y < 0 || point.Y > headerHeight + 4)
                return;

            // 页签/按钮区域即使被禁用也不当拖拽区，避免"点标签页变成拖窗口"的错觉
            if (HasInteractiveAncestor(e.OriginalSource as DependencyObject))
                return;

            if (e.ClickCount == 2)
            {
                _hostWindow.WindowState = _hostWindow.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;

                e.Handled = true;
                return;
            }

            try
            {
                if (!_hostWindow.IsActive)
                    _hostWindow.Activate();

                _hostWindow.DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
                // 鼠标已抬起或窗口未激活：忽略
            }
        }

        private static bool HasInteractiveAncestor(DependencyObject? node)
        {
            while (node != null)
            {
                if (node is ButtonBase || node is RibbonTabItem)
                    return true;

                node = node is Visual
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }

            return false;
        }

        private void OnHostWindowDeactivated(object? sender, EventArgs e)
        {
            if (IsPopupOpen)
                IsPopupOpen = false;
        }

        /// <summary>
        /// 点了面板里的具体命令：收起面板，并关掉"悬停自动下拉"——
        /// 之后鼠标移到别的页签不会再把功能区弹出来，需要点页签或双击页签栏恢复（与 Office 一致）。
        /// </summary>
        private void OnGroupsPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsMinimized || !IsPopupOpen)
                return;

            var button = FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject);

            // 下拉按钮点开的是自己的子菜单，不能把整个命令面板一起关掉
            if (button == null || button is RibbonDropDownButton)
                return;

            IsPopupOpen = false;
            _hoverPreview = false;
        }

        /// <inheritdoc />
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateTabStripWidth();
        }

        /// <summary>
        /// 按窗口宽度收窄页签区，保证右上角「置顶 + 窗口按钮」永远完整可见、标题仍有一席之地：
        /// 空间不够时页签横向滚动，而不是把按钮挤出窗口。
        /// </summary>
        private void UpdateTabStripWidth()
        {
            if (_tabScroll == null || double.IsNaN(ActualWidth) || ActualWidth <= 0)
                return;

            var cap = double.IsNaN(_tabStripMaxWidth) || _tabStripMaxWidth <= 0
                ? double.PositiveInfinity
                : _tabStripMaxWidth;

            var reserved = WindowButtonsWidth
                + (_topmostButton == null ? 0 : _topmostButton.ActualWidth)
                + (_topmostSeparator == null || _topmostSeparator.Visibility != Visibility.Visible ? 0 : _topmostSeparator.ActualWidth)
                + (_windowIcon == null || _windowIcon.Visibility != Visibility.Visible ? 0 : _windowIcon.ActualWidth + _windowIcon.Margin.Left + _windowIcon.Margin.Right)
                + MinTitleWidth;

            var available = ActualWidth
                - reserved
                - (_minimizeButton == null ? 0 : _minimizeButton.ActualWidth)
                - 8;

            var max = Math.Max(MinTabStripWidth, Math.Min(cap, available));
            if (double.IsNaN(_tabScroll.MaxWidth) || Math.Abs(_tabScroll.MaxWidth - max) > 0.5)
                _tabScroll.MaxWidth = max;
        }

        private static void OnIsMinimizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ribbon = (Ribbon)d;

            // 每次在"最小化 / 展开"之间切换都回到默认交互（含双击页签栏恢复默认状态）
            ribbon._hoverPreview = true;
            ribbon._hoverOpened = false;
            ribbon._hoverTab = null;

            if (!ribbon.IsMinimized)
            {
                ribbon.IsPopupOpen = false;
                ribbon._popupOwnerTab = null;
            }

            ribbon.UpdateContentLocation();
        }

        /// <summary>命令区（ItemsControl）在"内容区"和"最小化弹出面板"之间移动，同一时刻只存在于一处。</summary>
        private void UpdateContentLocation()
        {
            if (_groups == null || _contentHost == null || _popupHost == null)
                return;

            if (IsMinimized)
            {
                if (ReferenceEquals(_contentHost.Content, _groups))
                    _contentHost.Content = null;

                if (!ReferenceEquals(_popupHost.Content, _groups))
                    _popupHost.Content = _groups;
            }
            else
            {
                if (ReferenceEquals(_popupHost.Content, _groups))
                    _popupHost.Content = null;

                if (!ReferenceEquals(_contentHost.Content, _groups))
                    _contentHost.Content = _groups;
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            _popupOwnerTab = null;
            _hoverOpened = false;

            // 点到面板外面时 Popup 自己关闭，把状态同步回依赖属性
            if (IsPopupOpen)
                IsPopupOpen = false;
        }

        private void OnHeadersMouseLeave(object sender, MouseEventArgs e) => _hoverTab = null;

        /// <summary>
        /// 最小化时的"悬停即下拉"：鼠标移到哪个页签就换成哪个页签的命令面板。
        /// 点击过任意页签之后 <see cref="_hoverPreview"/> 变成 false，此逻辑即失效（与常见菜单栏一致）。
        /// </summary>
        private void OnHeadersMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsMinimized || !_hoverPreview)
                return;

            var tab = FindTabItem(e.OriginalSource as DependencyObject);
            if (ReferenceEquals(tab, _hoverTab))
                return;

            _hoverTab = tab;
            if (tab == null)
                return;

            if (!ReferenceEquals(SelectedItem, tab))
                SelectedItem = tab;

            _popupOwnerTab = tab;
            _hoverOpened = true;
            IsPopupOpen = true;
        }

        private void OnHeadersMouseDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            var tab = FindTabItem(source);

            // 双击页签：最小化 / 展开功能区（与 Office 一致）
            if (tab != null)
            {
                if (e.ClickCount == 2)
                {
                    IsMinimized = !IsMinimized;
                    e.Handled = true;
                }

                // 单击页签交给 MouseUp 处理：在 MouseDown 里开弹层会被随后的鼠标抬起立刻关掉
                return;
            }

            // 右上角的置顶/最小化/关闭等按钮自己处理点击，不能当成标题栏拖拽区
            if (FindAncestor<ButtonBase>(source) != null)
                return;

            // 空白区域 = 标题栏：拖动移动窗口，双击最大化/还原
            // 显式限定 System.Windows.Window：命名空间 Namsiz.WPF.Window 下简单名会被解析成命名空间
            var window = System.Windows.Window.GetWindow(this);
            if (window == null)
                return;

            if (e.ClickCount == 2)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;

                e.Handled = true;
                return;
            }

            try
            {
                // 窗口未激活时 DragMove 会抛异常；先激活再拖，首次点击就能拖动
                if (!window.IsActive)
                    window.Activate();

                window.DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
                // 鼠标已提前抬起（或窗口未激活）时 DragMove 会抛异常，忽略即可
            }
        }

        private void OnHeadersMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsMinimized)
                return;

            var tab = FindTabItem(e.OriginalSource as DependencyObject);
            if (tab == null)
                return;

            // 再点同一个页签 = 收起；点别的页签 = 直接换成那一页的命令面板
            // 点页签只是"钉住"，不关掉悬停预览：钉住状态下横向移到别的页签仍然会自动切换
            // （关掉悬停预览只发生在"点了面板里的具体命令"之后，见 OnGroupsPreviewMouseUp）

            // 悬停打开的面板：点一下 = 钉住；点击钉住的面板：再点一下 = 收起
            if (IsPopupOpen && ReferenceEquals(_popupOwnerTab, tab) && !_hoverOpened)
            {
                IsPopupOpen = false;
                return;
            }

            _popupOwnerTab = tab;
            _hoverOpened = false;
            IsPopupOpen = true;
        }

        private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                    return match;

                source = source is Visual
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }

            return null;
        }

        private static RibbonTabItem? FindTabItem(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is RibbonTabItem tab)
                    return tab;

                source = source is Visual
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}
