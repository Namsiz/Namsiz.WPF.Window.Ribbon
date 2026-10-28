using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Namsiz.WPF.Window.Ribbon.Controls
{
    /// <summary>
    /// 数据表格 Tab 的列定义（数据列）。
    /// 集合为空时数据表格按数据源自动生成列；非空时以本集合为准。
    /// </summary>
    public class DataGridColumnItem : DependencyObject
    {
        /// <summary>列标题（同时作为表头筛选的名称）。</summary>
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                nameof(Header),
                typeof(string),
                typeof(DataGridColumnItem),
                new PropertyMetadata(string.Empty));

        /// <summary>绑定路径：DataRowView 用列名（如“订单类型”），普通对象用属性名。</summary>
        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(
                nameof(Path),
                typeof(string),
                typeof(DataGridColumnItem),
                new PropertyMetadata(string.Empty));

        /// <summary>列宽（默认 120）。</summary>
        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.Register(
                nameof(Width),
                typeof(double),
                typeof(DataGridColumnItem),
                new PropertyMetadata(120d));

        /// <summary>该列是否只读。</summary>
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(DataGridColumnItem),
                new PropertyMetadata(false));

        /// <summary>列标题（同时作为表头筛选的名称）。</summary>
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        /// <summary>绑定路径：DataRowView 用列名（如“订单类型”），普通对象用属性名。</summary>
        public string Path
        {
            get => (string)GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        /// <summary>列宽（默认 120）。</summary>
        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        /// <summary>该列是否只读。</summary>
        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        /// <summary>按当前定义创建 DataGrid 列。</summary>
        public DataGridColumn CreateColumn()
        {
            return new DataGridTextColumn
            {
                Header = Header,
                Binding = new Binding(Path),
                Width = new DataGridLength(Width),
                IsReadOnly = IsReadOnly
            };
        }
    }
}
