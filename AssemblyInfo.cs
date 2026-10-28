using System.Windows;
using System.Windows.Markup;

// 主题资源（Themes/Generic.xaml）位于本源程序集：控件默认样式按类型自动查找，无需应用程序显式合并。
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

// 便于消费方只声明一个 xmlns 即可使用窗口、控件、附加行为与转换器。
[assembly: XmlnsPrefix("http://schemas.namsiz.com/wpf/ribbon", "ribbon")]
[assembly: XmlnsDefinition("http://schemas.namsiz.com/wpf/ribbon", "Namsiz.WPF.Window.Ribbon")]
[assembly: XmlnsDefinition("http://schemas.namsiz.com/wpf/ribbon", "Namsiz.WPF.Window.Ribbon.Controls")]
[assembly: XmlnsDefinition("http://schemas.namsiz.com/wpf/ribbon", "Namsiz.WPF.Window.Ribbon.Behaviors")]
[assembly: XmlnsDefinition("http://schemas.namsiz.com/wpf/ribbon", "Namsiz.WPF.Window.Ribbon.Converters")]
