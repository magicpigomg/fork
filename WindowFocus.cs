using System.Diagnostics;
using System.Text;

namespace MultiTool;

internal static class WindowFocus
{
    /// <summary>
    /// Ищет окно по имени процесса (без .exe) или по части заголовка окна.
    /// </summary>
    public static IntPtr FindWindow(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return IntPtr.Zero;

        string procName = query.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? query[..^4] : query;
        foreach (var p in Process.GetProcessesByName(procName))
        {
            if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle;
        }

        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((h, _) =>
        {
            if (!NativeMethods.IsWindowVisible(h)) return true;
            int len = NativeMethods.GetWindowTextLength(h);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            NativeMethods.GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// Выводит окно на передний план и ждёт, пока оно реально станет активным.
    /// </summary>
    public static bool Focus(IntPtr hWnd)
    {
        if (NativeMethods.IsIconic(hWnd))
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);

        IntPtr fg = NativeMethods.GetForegroundWindow();
        uint fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
        uint myThread = NativeMethods.GetCurrentThreadId();

        // Windows запрещает «чужому» процессу красть фокус — подключаемся к потоку активного окна.
        bool attached = fgThread != myThread && NativeMethods.AttachThreadInput(myThread, fgThread, true);
        try
        {
            NativeMethods.BringWindowToTop(hWnd);
            NativeMethods.SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached) NativeMethods.AttachThreadInput(myThread, fgThread, false);
        }

        for (int i = 0; i < 20; i++)
        {
            if (NativeMethods.GetForegroundWindow() == hWnd) return true;
            Thread.Sleep(50);
        }
        return false;
    }
}
