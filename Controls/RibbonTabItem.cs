using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 功能区页签：<c>Header</c> 为页签标题，XAML 子元素是 <see cref="RibbonGroup"/> 分组。
    /// <code>
    /// &lt;r:RibbonTabItem Header="设计数据维护"&gt;
    ///     &lt;r:RibbonGroup Header="产品规则"&gt;
    ///         &lt;Button Content="产品及模组分类" ribbon:RibbonProperties.Icon="/Assets/x.png" /&gt;
    ///     &lt;/r:RibbonGroup&gt;
    /// &lt;/r:RibbonTabItem&gt;
    /// </code>
    /// </summary>
    // 子元素直接进 Groups（覆盖 TabItem 的 Content 内容属性），页签内容由功能区统一呈现。
    [ContentProperty(nameof(Groups))]
    public class RibbonTabItem : TabItem
    {
        static RibbonTabItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(RibbonTabItem),
                new FrameworkPropertyMetadata(typeof(RibbonTabItem)));
        }

        /// <summary>本页签的分组集合（XAML 里的直接子元素）。</summary>
        public ObservableCollection<RibbonGroup> Groups { get; } = new ObservableCollection<RibbonGroup>();
    }
}
