using System.Runtime.InteropServices;
namespace Exel.Services;

internal static class WindowCaption
{
    private const uint WmSetText = 0x000C, WmGetText = 0x000D, WmGetTextLength = 0x000E, WmNcDestroy = 0x0082;
    private const nuint SubclassId = 0x4558454C;
    // Keep the callback alive for the lifetime of every native Exel window.
    private static readonly SubclassProcedure CaptionProcedure = HandleCaption;
    public static void Set(Window window, string title)
    {
        window.Title = title;
        Apply(window);
    }
    public static void Apply(Window window)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return;
        nint handle = WinRT.Interop.WindowNative.GetWindowHandle(native);
        if (SetWindowSubclass(handle, CaptionProcedure, SubclassId, 0))
            SetWindowTextW(handle, window.Title ?? "Exel");
    }
    private static nint HandleCaption(nint window, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        // MAUI's ANSI forwarding corrupts Unicode caption messages. All other messages keep their normal route.
        if (message is WmSetText or WmGetText or WmGetTextLength)
            return DefWindowProcW(window, message, wParam, lParam);
        if (message == WmNcDestroy) RemoveWindowSubclass(window, CaptionProcedure, id);
        return DefSubclassProc(window, message, wParam, lParam);
    }
    private delegate nint SubclassProcedure(nint window, uint message, nint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, SubclassProcedure callback, nuint id, nuint data);
    [DllImport("comctl32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProcedure callback, nuint id);
    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern nint DefSubclassProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowTextW(nint window, string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);
}
