using Namsiz.WPF.Window.Ribbon.Collections;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Namsiz.WPF.Window.Ribbon.Logging
{
    /// <summary>
    /// Serilog Sink：把日志批量提交到 ObservableCollection。
    /// 后台线程只负责入队（无跨线程调度开销），由 UI 线程定时器每 200ms 统一刷新，
    /// 避免大数据量分析时每条日志都触发一次 Dispatcher 调用与界面刷新。
    /// </summary>
    public class ObservableLogSink : ILogEventSink
    {
        private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(200);

        private readonly RangeObservableCollection<LogItem> _logItems;
        private readonly Action<LogItem>? _onAdded;
        private readonly object _syncLock = new();
        private readonly List<LogItem> _pendingItems = new();
        private readonly DispatcherTimer _flushTimer;

        /// <summary>
        /// 创建 Sink。
        /// </summary>
        /// <param name="logItems">目标日志集合（通常为 <c>RibbonWindow.LogItems</c>）。</param>
        /// <param name="dispatcher">刷新日志所用的 UI 线程 Dispatcher。</param>
        /// <param name="onAdded">每批日志写入后的回调（用于自动切换到日志 Tab），可为 null。</param>
        public ObservableLogSink(
            RangeObservableCollection<LogItem> logItems,
            Dispatcher dispatcher,
            Action<LogItem>? onAdded = null)
        {
            _logItems = logItems;
            _onAdded = onAdded;

            // 定时器在 UI 线程创建并持续运行：每次 Tick 检查待刷新队列，无数据时开销可忽略
            _flushTimer = new DispatcherTimer(FlushInterval, DispatcherPriority.Background, Flush, dispatcher);
            _flushTimer.Start();
        }

        /// <summary>任意线程调用：仅入队，不进行跨线程调度。</summary>
        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var logItem = new SelectableLogItem
            {
                Message = message,
                Level = logEvent.Level,
                Kind = ResolveKind(logEvent.Level, message),
                Timestamp = logEvent.Timestamp.DateTime
            };

            lock (_syncLock)
            {
                _pendingItems.Add(logItem);
            }
        }

        /// <summary>
        /// 推导日志展示类别：
        /// Error/Fatal → 红；Warning → 橙；Debug/Verbose → 灰；
        /// Information 以 ✅ 开头的成功消息 → 绿，其余 → 默认黑。
        /// </summary>
        private static LogKind ResolveKind(LogEventLevel level, string message)
        {
            switch (level)
            {
                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                    return LogKind.Error;

                case LogEventLevel.Warning:
                    return LogKind.Warning;

                case LogEventLevel.Debug:
                case LogEventLevel.Verbose:
                    return LogKind.Debug;

                default: // Information
                    return message.StartsWith("✅", StringComparison.Ordinal)
                        ? LogKind.Success
                        : LogKind.Default;
            }
        }

        /// <summary>UI 线程批量刷新：一次取出全部待处理日志，统一写入集合，并只触发一次回调。</summary>
        private void Flush(object? sender, EventArgs e)
        {
            LogItem[] batch;
            lock (_syncLock)
            {
                if (_pendingItems.Count == 0)
                    return;

                batch = _pendingItems.ToArray();
                _pendingItems.Clear();
            }

            // 整批一次提交：只触发一次 Reset 通知，虚拟化列表只重建一次可视区容器
            _logItems.AddRange(batch);

            _onAdded?.Invoke(batch[batch.Length - 1]);
        }
    }
}
