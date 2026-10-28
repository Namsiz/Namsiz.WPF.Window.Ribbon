using Namsiz.WPF.Window.Ribbon.Controls;
using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// 表格耗时操作（排序 / 筛选 / 批量装载）的蒙板包装。
    ///
    /// 小表（默认 2 万行以内）直接同步执行：结果立即可见，避免"点一下要等一拍"的迟滞感。
    /// 大表先显示所属数据 Tab 的蒙板并让出一帧（Background 优先级，让蒙板先画出来），再执行耗时操作。
    /// 蒙板挂在数据 Tab 自己的内容里，因此只覆盖当前表格，切到其它 Tab 完全不受影响。
    /// </summary>
    internal static class DataGridBusy
    {
        /// <summary>低于该行数视为小表：操作同步完成，不显示蒙板、不延迟。</summary>
        private const int SmallTableThreshold = 20_000;

        /// <summary>带蒙板执行 <paramref name="work"/>；小表或找不到宿主 Tab 时直接执行。</summary>
        public static void Run(DataGrid? grid, int rowCount, string busyText, Action work)
        {
            var tabItem = DataGridTabItem.ResolveHost(grid);

            if (rowCount <= SmallTableThreshold || grid == null || tabItem == null)
            {
                work();
                return;
            }

            tabItem.BusyText = busyText;
            tabItem.IsBusy = true;

            // Background 优先级：当前排队的渲染（更高优先级）先把蒙板画出来，再执行耗时操作
            grid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    work();
                }
                finally
                {
                    tabItem.IsBusy = false;
                }
            }));
        }
    }
}
