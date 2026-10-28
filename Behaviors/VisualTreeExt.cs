using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Namsiz.WPF.Window.Ribbon.Behaviors
{
    /// <summary>视觉树工具：查找指定类型的后代元素。</summary>
    internal static class VisualTreeExt
    {
        /// <summary>深度优先枚举所有类型为 <typeparamref name="T"/> 的后代元素（含自身）。</summary>
        public static IEnumerable<T> FindDescendants<T>(DependencyObject? root) where T : DependencyObject
        {
            if (root == null)
                yield break;

            if (root is T match)
                yield return match;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                foreach (var descendant in FindDescendants<T>(VisualTreeHelper.GetChild(root, i)))
                    yield return descendant;
            }
        }

        /// <summary>深度优先查找第一个类型为 <typeparamref name="T"/> 的后代元素（含自身）。</summary>
        public static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
        {
            if (root == null)
                return null;

            if (root is T match)
                return match;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var found = FindDescendant<T>(VisualTreeHelper.GetChild(root, i));
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
