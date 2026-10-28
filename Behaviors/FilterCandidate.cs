using System.Collections.Generic;
using System.Data;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>
    /// 筛选面板里的一个候选项。
    /// <see cref="Display"/> 是勾选列表里显示的文字，<see cref="Raw"/> 是参与 <c>RowFilter</c> 的实际取值。
    /// 两者分开是为了支持「显示业务用词、过滤真实值」，例如布尔列显示「已标记」而过滤值仍是 <c>True</c>；
    /// <see cref="Raw"/> 为 <c>null</c> 表示过滤「空白单元格」（生成 <c>IS NULL</c> 条件）。
    /// </summary>
    public sealed class FilterCandidate
    {
        /// <summary>空白单元格在勾选列表里的显示文字。</summary>
        public const string BlankDisplay = "(空白)";

        public FilterCandidate(string display, string? raw)
        {
            Display = display ?? string.Empty;
            Raw = raw;
        }

        /// <summary>勾选列表里显示的文字（也是该候选项在面板内的唯一键）。</summary>
        public string Display { get; }

        /// <summary>参与 RowFilter 的实际值；null 表示空白单元格。</summary>
        public string? Raw { get; }

        /// <summary>用「显示文字即实际值」的常见情形构造候选项。</summary>
        public static FilterCandidate OfValue(string value) => new FilterCandidate(value, value);

        /// <summary>空白单元格候选项（显示 <see cref="BlankDisplay"/>，过滤 IS NULL）。</summary>
        public static FilterCandidate Blank() => new FilterCandidate(BlankDisplay, null);
    }

    /// <summary>
    /// 由使用方提供某列的候选值。
    /// 参数为数据视图与列名；返回 <c>null</c> 表示「本列不用自定义候选值，按列的实际取值统计」。
    /// 注意：该方法可能在工作线程上被调用（筛选面板的候选值统计与预热），实现里不要触碰界面对象。
    /// </summary>
    /// <param name="view">当前数据视图（已带上其它列的筛选效果）。</param>
    /// <param name="columnName">列名。</param>
    public delegate IReadOnlyList<FilterCandidate>? FilterCandidateProvider(DataView view, string columnName);
}
