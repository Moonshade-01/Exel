using Exel.Services;
namespace Exel;
public partial class App : Application
{
    public App() { UserAppTheme = AppTheme.Dark; }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();
        var navigation = new NavigationPage(page) { BarBackgroundColor = Palette.Background, BarTextColor = Palette.Text };
        var window = new Window(navigation) { Title = "Exel — Нова таблиця", Width = 1360, Height = 900, MinimumWidth = 1080, MinimumHeight = 680 };
        window.Created += (_, _) => WindowCaption.Apply(window);
        window.Activated += (_, _) => WindowCaption.Apply(window);
        bool closingApproved = false, shortcutsInstalled = false;
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return;
            if (native.Content is Microsoft.UI.Xaml.FrameworkElement content && !shortcutsInstalled)
            {
                content.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark;
                void Shortcut(global::Windows.System.VirtualKey key, global::Windows.System.VirtualKeyModifiers modifiers, Func<bool> run)
                {
                    var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = key, Modifiers = modifiers };
                    accelerator.Invoked += (_, args) => args.Handled = run(); content.KeyboardAccelerators.Add(accelerator);
                }
                Shortcut(global::Windows.System.VirtualKey.A, global::Windows.System.VirtualKeyModifiers.Control, () => page.SelectAllCellText());
                Shortcut(global::Windows.System.VirtualKey.F2, global::Windows.System.VirtualKeyModifiers.None, () => page.SelectAllCellText(false));
                foreach (var binding in new[] { (global::Windows.System.VirtualKey.S, "save"), (global::Windows.System.VirtualKey.O, "open"), (global::Windows.System.VirtualKey.N, "new"), (global::Windows.System.VirtualKey.Z, "undo"), (global::Windows.System.VirtualKey.Y, "redo") })
                {
                    var current = binding;
                    Shortcut(current.Item1, global::Windows.System.VirtualKeyModifiers.Control, () => page.Shortcut(current.Item2));
                }
                foreach (var key in new[] { global::Windows.System.VirtualKey.Left, global::Windows.System.VirtualKey.Right, global::Windows.System.VirtualKey.Up, global::Windows.System.VirtualKey.Down, global::Windows.System.VirtualKey.Enter, global::Windows.System.VirtualKey.Tab, global::Windows.System.VirtualKey.Home, global::Windows.System.VirtualKey.PageUp, global::Windows.System.VirtualKey.PageDown, global::Windows.System.VirtualKey.Delete })
                {
                    var current = key;
                    Shortcut(current, global::Windows.System.VirtualKeyModifiers.None, () => page.WorksheetKey(current, false, false));
                    if (current is global::Windows.System.VirtualKey.Tab or global::Windows.System.VirtualKey.Enter)
                        Shortcut(current, global::Windows.System.VirtualKeyModifiers.Shift, () => page.WorksheetKey(current, true, false));
                }
                Shortcut(global::Windows.System.VirtualKey.Home, global::Windows.System.VirtualKeyModifiers.Control, () => page.WorksheetKey(global::Windows.System.VirtualKey.Home, false, true));
                shortcutsInstalled = true;
            }
            native.AppWindow.Closing += async (_, args) =>
            {
                if (closingApproved) return;
                args.Cancel = true;
                if (await page.CanCloseAsync()) { closingApproved = true; native.Close(); }
            };
        };
        return window;
    }
}
