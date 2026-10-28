using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// 日志列表附加行为：新日志产生时自动滚动到底部。
    /// 直接操作列表内部 ScrollViewer 的 ScrollToEnd()，相比 ScrollIntoView（需先定位末项容器）开销更小。
    /// </summary>
    public static class AutoScrollBehavior
    {
        private sealed class CollectionHook
        {
            public INotifyCollectionChanged Collection = null!;
            public NotifyCollectionChangedEventHandler Handler = null!;
        }

        private static readonly ConditionalWeakTable<ItemsControl, CollectionHook> Hooks = new();

        /// <summary>是否启用“新日志自动滚动到底部”。</summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ItemsControl host)
                return;

            if ((bool)e.NewValue)
            {
                host.DataContextChanged += OnDataContextChanged;
                host.Loaded += OnLoaded;
                host.Unloaded += OnUnloaded;
            }
            else
            {
                host.DataContextChanged -= OnDataContextChanged;
                host.Loaded -= OnLoaded;
                host.Unloaded -= OnUnloaded;
                Unhook(host);
            }
        }

        private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ItemsControl host)
                Hook(host);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl host)
            {
                Hook(host);
                ScrollToEndLater(host);
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl host)
                Unhook(host);
        }

        private static void Hook(ItemsControl host)
        {
            if (Hooks.TryGetValue(host, out _))
                return;

            if (host.ItemsSource is not INotifyCollectionChanged collection)
                return;

            var hook = new CollectionHook
            {
                Collection = collection,
                Handler = (_, _) => ScrollToEndLater(host)
            };

            collection.CollectionChanged += hook.Handler;
            Hooks.Add(host, hook);
        }

        private static void Unhook(ItemsControl host)
        {
            if (!Hooks.TryGetValue(host, out var hook))
                return;

            Hooks.Remove(host);
            hook.Collection.CollectionChanged -= hook.Handler;
        }

        private static void ScrollToEndLater(ItemsControl host)
        {
            // 等待布局完成（Reset 后滚动范围才更新），Loaded 优先级下执行
            host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                VisualTreeExt.FindDescendant<ScrollViewer>(host)?.ScrollToEnd();
            }));
        }
    }
}
