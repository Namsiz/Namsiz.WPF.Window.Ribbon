using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 数据表格的加载蒙板（Adorner 实现）。
    /// 刻意不用模板里的叠加 Border：叠加图层会参与每次布局，海量数据首次绑定时实测成倍变慢；
    /// Adorner 只在需要时挂到表格上，空闲状态零开销。
    /// </summary>
    internal sealed class BusyOverlayAdorner : Adorner
    {
        private readonly VisualCollection _children;
        private readonly Border _root;
        private readonly TextBlock _text;

        public BusyOverlayAdorner(UIElement adornedElement, string text)
            : base(adornedElement)
        {
            _text = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var progress = new ProgressBar
            {
                Width = 180,
                Height = 6,
                Margin = new Thickness(0, 10, 0, 0),
                IsIndeterminate = true,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(_text);
            panel.Children.Add(progress);

            _root = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
                Child = panel
            };

            _children = new VisualCollection(this) { _root };
        }

        /// <summary>蒙板提示文本。</summary>
        public string Text
        {
            get => _text.Text;
            set => _text.Text = value;
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index) => _children[index];

        protected override Size MeasureOverride(Size constraint)
        {
            _root.Measure(constraint);
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _root.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
