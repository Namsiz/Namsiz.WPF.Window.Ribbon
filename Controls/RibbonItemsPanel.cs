using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 功能区分组的子项面板：**竖排优先**。
    ///
    /// 规则：先在一列里从上往下排，累计高度超过 <see cref="RibbonGroup.ContentHeight"/> 时另起一列，
    /// 同一列里的元素按该列最宽者对齐（视觉上成一条竖列）。
    /// 因此大按钮（约整列高）天然一列一个，输入框（约半列高）会两两成列——
    /// 使用方不需要为"排几行"做任何声明。
    /// </summary>
    public class RibbonItemsPanel : Panel
    {
        /// <summary>取不到所属分组时的兜底列高。</summary>
        private const double DefaultRowHeight = 92;

        private readonly List<List<UIElement>> _columns = new();
        private readonly List<double> _columnWidths = new();

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            var rowHeight = ResolveRowHeight();

            _columns.Clear();
            _columnWidths.Clear();

            List<UIElement>? current = null;
            double columnWidth = 0;
            double columnHeight = 0;
            double totalWidth = 0;
            double maxHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var size = child.DesiredSize;

                // 这一列已经放不下 → 收尾当前列，另起一列
                if (current != null && columnHeight + size.Height > rowHeight + 0.5)
                {
                    _columns.Add(current);
                    _columnWidths.Add(columnWidth);
                    totalWidth += columnWidth;

                    current = null;
                    columnWidth = 0;
                    columnHeight = 0;
                }

                current ??= new List<UIElement>();
                current.Add(child);

                columnHeight += size.Height;
                columnWidth = Math.Max(columnWidth, size.Width);
                maxHeight = Math.Max(maxHeight, columnHeight);
            }

            if (current != null)
            {
                _columns.Add(current);
                _columnWidths.Add(columnWidth);
                totalWidth += columnWidth;
            }

            return new Size(totalWidth, Math.Max(maxHeight, rowHeight));
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0.0;

            for (var i = 0; i < _columns.Count; i++)
            {
                var width = _columnWidths[i];
                var y = 0.0;

                foreach (var child in _columns[i])
                {
                    var height = child.DesiredSize.Height;
                    child.Arrange(new Rect(x, y, width, height));
                    y += height;
                }

                x += width;
            }

            return finalSize;
        }

        /// <summary>列高取所属分组的 <see cref="RibbonGroup.ContentHeight"/>。</summary>
        private double ResolveRowHeight()
        {
            var height = FindOwner()?.ContentHeight ?? DefaultRowHeight;
            return double.IsNaN(height) || height <= 0 ? DefaultRowHeight : height;
        }

        private RibbonGroup? FindOwner()
        {
            DependencyObject? node = this;
            while (node != null)
            {
                if (node is RibbonGroup group)
                    return group;

                node = VisualTreeHelper.GetParent(node);
            }

            return null;
        }
    }
}
