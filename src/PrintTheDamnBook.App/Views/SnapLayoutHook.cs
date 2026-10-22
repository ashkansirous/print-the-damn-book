using System.Windows;
using System.Windows.Interop;

namespace PrintTheDamnBook.App.Views;

/// <summary>
/// Makes the custom maximize button behave like the system one: Windows 11 shows its snap layouts when the cursor is
/// over a caption button, which it decides from <c>WM_NCHITTEST</c>. The mouse then arrives as non-client messages, so
/// hover and click are handled here too.
/// </summary>
internal sealed class SnapLayoutHook(MainWindow window, FrameworkElement maximizeButton)
{
    private const int WmNcHitTest = 0x0084;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int WmNcLeftButtonUp = 0x00A2;
    private const int WmNcMouseLeave = 0x02A2;
    private const int HitMaximizeButton = 9;

    public void Attach()
    {
        HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case WmNcHitTest when IsOverButton(lParam):
                Ui.SetIsPointerOver(maximizeButton, true);
                handled = true;
                return HitMaximizeButton;
            case WmNcHitTest:
            case WmNcMouseLeave:
                Ui.SetIsPointerOver(maximizeButton, false);
                break;
            case WmNcLeftButtonDown when wParam == HitMaximizeButton:
                handled = true;
                break;
            case WmNcLeftButtonUp when wParam == HitMaximizeButton:
                window.ToggleMaximized();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private bool IsOverButton(IntPtr lParam)
    {
        var packed = lParam.ToInt64();
        var screen = new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));
        var local = maximizeButton.PointFromScreen(screen);
        return local.X >= 0 && local.Y >= 0 && local.X < maximizeButton.ActualWidth && local.Y < maximizeButton.ActualHeight;
    }
}
