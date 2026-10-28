# Namsiz.WPF.Window.Ribbon

带**功能区（Ribbon）菜单**的 WPF 工具窗口库。引用后只需声明「菜单按钮 + 命令」与「数据源 + 列」，窗口骨架、日志页、数据页、状态栏、忙碌遮罩全部由库提供。

- 目标框架：.NET Framework 4.7.2（`net472`）
- 依赖：[Namsiz.WPF.Window](https://www.nuget.org/packages/Namsiz.WPF.Window)（自定义标题栏）、[Serilog](https://www.nuget.org/packages/Serilog)
- 2.0.0 起**不再依赖 Fluent.Ribbon**：功能区由库自身实现，按钮回归 WPF 内置 `Button`

## 安装

```powershell
dotnet add package Namsiz.WPF.Window.Ribbon
```

## 快速上手

```xml
<r:RibbonWindow
    x:Class="MyApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:r="http://schemas.namsiz.com/wpf/ribbon"
    Title="我的工具"
    Width="1200"
    Height="760">

    <r:RibbonWindow.RibbonTabs>
        <r:RibbonTabItem Header="数据处理">
            <r:RibbonGroup Header="导入">
                <Button
                    Command="{Binding ImportCommand}"
                    Content="导入数据"
                    r:RibbonProperties.Icon="/Assets/import.png" />
                <Button Command="{Binding ExportCommand}" Content="导出数据" />
            </r:RibbonGroup>

            <r:RibbonGroup Header="查询">
                <TextBox Width="200"
                         Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}"
                         r:RibbonProperties.Header="关键字" />
            </r:RibbonGroup>
        </r:RibbonTabItem>
    </r:RibbonWindow.RibbonTabs>

    <r:RibbonWindow.TabItems>
        <r:DataGridTabItem Header="结果" ItemsSource="{Binding Rows}" />
    </r:RibbonWindow.TabItems>
</r:RibbonWindow>
```

代码里只需 `public partial class MainWindow : RibbonWindow`。

## 功能区

功能区内部只需写 **WPF 内置控件**，外观由库的隐式样式统一呈现：

| 你想放 | 写法 |
| --- | --- |
| 命令按钮 | `<Button Content="标题" r:RibbonProperties.Icon="图标" />` —— 图标只有 `Icon` 一个（不再区分 `LargeIcon`），没设图标自动变成纯文字按钮 |
| 分组 | `<r:RibbonGroup Header="组名">子项</r:RibbonGroup>`，子项**竖排优先**、排满列高才换列 |
| 下拉菜单 | `<r:RibbonDropDownButton Content="导出">` + 若干 `<Button Content="…" />` |
| 输入框 | `<TextBox r:RibbonProperties.Header="标签" />`，标签显示在输入框左侧 |
| 分隔线 | `<Separator />` |

其他能力：

- **最小化**：页签栏右侧箭头或双击页签栏；最小化后鼠标**悬停即下拉**，点击钉住，点过具体命令后停止悬停预览，双击页签栏恢复默认交互
- **标题自动居中**：标题在「页签按钮」与「右上角窗口按钮（含置顶）」之间自动居中，窗口里不需要写功能栏模板
- **默认菜单页**：`RibbonDefaults.Buttons="SortAndFilter"` 可把库自带的「清除排序 / 清除筛选」注入任意分组；不需要时 `ShowDefaultMenu="False"`

## 数据页

`DataGridTabItem`（或派生类，如 `DataPreviewTabItem`）一行声明即可获得：只读表格、Excel 风格列头筛选、行号列、行/列虚拟化、按校验列着色、空数据自动隐藏页签。

```xml
<r:DataGridTabItem Header="明细"
                   ItemsSource="{Binding DetailRows}"
                   ValidationColumn="校验"
                   FilterColumns="客户,科室,机型" />
```

## 状态栏与忙碌遮罩

- 状态栏：选中数据页时显示该页 `StatusText`，否则显示 `IdleStatusText`
- 忙碌遮罩：`IsBusy` + `BusyText` + `BusyProgress`（0~100 显示实心进度条与百分比；`NaN` 表示进度未知，显示不确定态流动条 + 转圈指示），忙碌期间功能区自动禁用

## 版本要点

| 版本 | 变更 |
| --- | --- |
| 2.1.0 | 新增 `Ribbon.WindowIcon`、`RibbonGroup.ContentHeight`、`RibbonItemsPanel`；最小化悬停即下拉/点击钉住/双击恢复；忙碌遮罩支持确定进度与转圈；功能区左右留白、图标移至功能区左上角、输入框标签左置 |
| 2.0.0 | **移除 Fluent.Ribbon 依赖**，功能区自实现；图标统一为 `RibbonProperties.Icon`；支持最小化与标题自动居中 |
| 1.x | 基于 Fluent.Ribbon 的封装版本 |

## 许可

MIT
