using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 带下拉菜单的功能区按钮：<c>Content</c> 为按钮文字，XAML 子元素为菜单项
    /// （用内置 <see cref="Button"/> + <c>RibbonProperties.Icon</c> 声明，命令直接绑在菜单项上）。
    /// <code>
    /// &lt;r:RibbonDropDownButton Content="导出到 Excel" ribbon:RibbonProperties.Icon="/Assets/export.png"&gt;
    ///     &lt;Button Content="导出项目信息" ribbon:RibbonProperties.Icon="/Assets/a.png" Command="{Binding ExportProjectCommand}" /&gt;
    /// &lt;/r:RibbonDropDownButton&gt;
    /// </code>
    /// </summary>
    // 子元素进 Items（覆盖 Button 的 Content 内容属性），Content 仍作为按钮文字使用。
    [ContentProperty(nameof(Items))]
    public class RibbonDropDownButton : Button
    {
        /// <summary>下拉面板是否展开。</summary>
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen),
                typeof(bool),
                typeof(RibbonDropDownButton),
                new FrameworkPropertyMetadata(false, null, CoerceIsOpen));

        static RibbonDropDownButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(RibbonDropDownButton),
                new FrameworkPropertyMetadata(typeof(RibbonDropDownButton)));
        }

        /// <summary>下拉面板是否展开。</summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>下拉菜单项集合（XAML 里的直接子元素）。</summary>
        public ObservableCollection<object> Items { get; } = new ObservableCollection<object>();

        /// <summary>创建下拉按钮。</summary>
        public RibbonDropDownButton()
        {
            // 菜单项变化后重新判定能否展开
            Items.CollectionChanged += (_, _) => CoerceValue(IsOpenProperty);
        }

        /// <inheritdoc />
        protected override void OnClick()
        {
            base.OnClick();
            IsOpen = !IsOpen;
        }

        /// <summary>下拉面板不承载内容时不允许展开（避免空面板）。</summary>
        private static object CoerceIsOpen(DependencyObject d, object baseValue)
            => ((RibbonDropDownButton)d).Items.Count == 0 ? false : baseValue;
    }
}
