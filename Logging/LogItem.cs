using Serilog.Events;
using System;
using System.ComponentModel;

namespace Namsiz.WPF.Window.Ribbon.Logging
{
    /// <summary>日志展示类别（决定日志行的文字颜色）。</summary>
    public enum LogKind
    {
        /// <summary>普通过程消息（默认黑色）</summary>
        Default = 0,

        /// <summary>成功/完成消息（绿色）</summary>
        Success = 1,

        /// <summary>警告消息（橙色）</summary>
        Warning = 2,

        /// <summary>错误消息（红色）</summary>
        Error = 3,

        /// <summary>调试/内部细节消息（灰色，仅 DEBUG 模式产生）</summary>
        Debug = 4
    }

    /// <summary>日志行（库内置日志 Tab 的数据项）。</summary>
    public class LogItem
    {
        public string? Message { get; set; }

        /// <summary>原始日志级别（Serilog）。</summary>
        public LogEventLevel Level { get; set; }

        /// <summary>展示类别（按级别+消息前缀推导，见 <see cref="ObservableLogSink"/>）。</summary>
        public LogKind Kind { get; set; }

        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 日志行（UI 侧扩展）：携带“文本选择模式”显示状态。
    /// 双击行后该行切换为只读 TextBox 供选择复制，其余行保持轻量 TextBlock。
    /// </summary>
    public class SelectableLogItem : LogItem, INotifyPropertyChanged
    {
        private bool _isTextSelecting;

        /// <summary>是否处于文本选择模式（只读 TextBox）。</summary>
        public bool IsTextSelecting
        {
            get => _isTextSelecting;
            set
            {
                if (_isTextSelecting == value)
                    return;

                _isTextSelecting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTextSelecting)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
