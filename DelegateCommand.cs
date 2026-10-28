using System;
using System.Windows.Input;

namespace Namsiz.WPF.Window.Ribbon
{
    /// <summary>
    /// 库内部使用的简单命令（默认菜单按钮用）。
    /// 刻意不公开：库不强制使用方采用任何命令实现，公开的命令由使用方自己提供或绑定。
    /// </summary>
    internal sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;

        public DelegateCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
