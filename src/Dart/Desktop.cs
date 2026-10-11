using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Dart;
internal static class Desktop
{
    internal delegate void WinEvent(IntPtr hook, uint evt, IntPtr hwnd, int obj, int child, uint thread, uint time);
    internal delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    [DllImport("user32.dll")] static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    internal static bool Available()
    {
        var d = OpenInputDesktop(0, false, 0x0100); // DESKTOP_SWITCHDESKTOP: no attempt to access secure desktop.
        if (d == IntPtr.Zero) return false;
        CloseDesktop(d); return true;
    }
    internal static object[] Windows()
    {
        var windows = new List<object>();
        var foreground = GetForegroundWindow();
        EnumWindows((hwnd, _) => {
            GetWindowThreadProcessId(hwnd, out var pid);
            var title = new StringBuilder(4096);
            GetWindowText(hwnd, title, title.Capacity);
            if (!IsWindowVisible(hwnd) || pid == Environment.ProcessId || !GetWindowRect(hwnd, out var r)) return true;
            DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int));
            windows.Add(new { handle = hwnd.ToInt64(), processId = pid, title = title.ToString(),
                x = r.Left, y = r.Top, width = r.Right-r.Left, height = r.Bottom-r.Top,
                minimized = IsIconic(hwnd), cloaked = cloaked != 0, foreground = hwnd == foreground,
                zOrder = windows.Count });
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }
}
