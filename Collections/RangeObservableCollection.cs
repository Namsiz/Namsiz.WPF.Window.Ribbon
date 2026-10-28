using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Namsiz.WPF.Window.Ribbon.Collections
{
    /// <summary>
    /// 支持批量追加的 ObservableCollection。
    ///
    /// 通知策略说明（踩过两个坑）：
    /// · 用 Reset 通知：ItemsControl 会丢弃并重建全部行容器 —— 日志列表几百行就明显卡顿；
    /// · 用"范围 Add"通知：WPF 不支持（抛 NotSupportedException，实测报"不支持范围操作"）；
    /// · 因此逐条 Add：每次通知都是增量插入，只为新条目创建容器，既有列表不重建。
    /// </summary>
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>批量追加（逐条增量插入，不重建已有行容器）。</summary>
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
                Add(item);
        }
    }
}
