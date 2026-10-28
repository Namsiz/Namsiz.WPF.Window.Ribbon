using System.Windows;
using System.Windows.Controls;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 功能区分组：<c>Header</c> 显示在分组底部，XAML 子元素**先竖排、排满再换列**（见 <see cref="ContentHeight"/>）。
    /// 分组右侧自动画一条浅色竖线，用于在视觉上分隔相邻分组。
    /// </summary>
    public class RibbonGroup : HeaderedItemsControl
    {
        /// <summary>分组内容区高度（默认 92）：元素先从上往下排，排不下才另起一列。</summary>
        public static readonly DependencyProperty ContentHeightProperty =
            DependencyProperty.Register(
                nameof(ContentHeight),
                typeof(double),
                typeof(RibbonGroup),
                new FrameworkPropertyMetadata(92.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        static RibbonGroup()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(RibbonGroup),
                new FrameworkPropertyMetadata(typeof(RibbonGroup)));
        }

        /// <summary>分组内容区高度：决定一列能竖排多少元素，也决定功能区整体的命令区高度。</summary>
        public double ContentHeight
        {
            get => (double)GetValue(ContentHeightProperty);
            set => SetValue(ContentHeightProperty, value);
        }
    }
}
