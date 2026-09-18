// 全局类型别名：本程序集同时启用 WPF 与 WinForms（托盘），
// SDK 隐式注入的 System.Drawing / System.Windows.Forms using 会与 WPF 类型冲突。
// 此处统一钉死歧义类型，避免在每个文件重复写全限定名。
global using Application = System.Windows.Application;
global using WindowState = DesktopReminder.Core.StateMachine.WindowState;
global using Color = System.Windows.Media.Color;
global using Brush = System.Windows.Media.Brush;
global using Point = System.Windows.Point;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using RoutedEventArgs = System.Windows.RoutedEventArgs;
global using Button = System.Windows.Controls.Button;
global using Pen = System.Windows.Media.Pen;
global using FlowDirection = System.Windows.FlowDirection;
global using NativeWindow = DesktopReminder.App.WindowsApi.NativeWindow;
global using MessageBox = System.Windows.MessageBox;
global using FormattedText = System.Windows.Media.FormattedText;
global using Typeface = System.Windows.Media.Typeface;
global using ContextMenu = System.Windows.Controls.ContextMenu;
global using MenuItem = System.Windows.Controls.MenuItem;
global using SystemParameters = System.Windows.SystemParameters;
