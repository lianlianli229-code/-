using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace StickyTickets.Interop
{
    public enum ModifierWakePreset
    {
        CtrlAlt = 0,
        CtrlShift = 1,
        AltShift = 2
    }

    public static class Win32Interop
    {
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const long WS_EX_LAYERED = 0x00080000L;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Removes Current Working Directory (CWD) from the Win32 DLL search order at startup
        /// to mitigate CWD DLL side-loading/hijacking while preserving WPF runtime DLL resolution.
        /// </summary>
        public static void HardenProcessDllSearchPath()
        {
            try
            {
                SetDllDirectory(string.Empty);
            }
            catch
            {
            }
        }

        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_SHIFT = 0x10;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        public const int WM_HOTKEY = 0x0312;
        public const int HOTKEY_ID_HIDE_ALL = 0x2001;
        public const int HOTKEY_ID_HIDE_RAIL_ONLY = 0x2002;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_G = 0x47;
        private const uint VK_H = 0x48;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool RegisterCtrlH(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            return RegisterHotKey(hwnd, HOTKEY_ID_HIDE_ALL, MOD_CONTROL | MOD_NOREPEAT, VK_H);
        }

        public static void UnregisterCtrlH(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(hwnd, HOTKEY_ID_HIDE_ALL);
            }
        }

        public static bool RegisterCtrlG(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            return RegisterHotKey(hwnd, HOTKEY_ID_HIDE_RAIL_ONLY, MOD_CONTROL | MOD_NOREPEAT, VK_G);
        }

        public static void UnregisterCtrlG(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(hwnd, HOTKEY_ID_HIDE_RAIL_ONLY);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static long GetWindowLongPtrSafe(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex).ToInt64()
                : GetWindowLong32(hWnd, nIndex);
        }

        private static void SetWindowLongPtrSafe(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(hWnd, nIndex, new IntPtr(dwNewLong));
            }
            else
            {
                SetWindowLong32(hWnd, nIndex, unchecked((int)dwNewLong));
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetMouseScreenPosition(System.Windows.Media.Visual referenceVisual)
        {
            POINT pt;
            if (!GetCursorPos(out pt)) return new Point(-1000, -1000);
            try
            {
                if (referenceVisual != null)
                {
                    PresentationSource src = PresentationSource.FromVisual(referenceVisual);
                    if (src != null && src.CompositionTarget != null)
                    {
                        return src.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y));
                    }
                }
            }
            catch
            {
            }
            return new Point(pt.X, pt.Y);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOSENDCHANGING = 0x0400;

        /// <summary>
        /// Enforces an exact front-to-back Z-order among Topmost windows so overlapped docked tickets
        /// always return to their original slot layer when another ticket is selected.
        /// </summary>
        public static void ApplyDeterministicTopmostZOrder(System.Collections.Generic.IList<Window> frontToBackWindows)
        {
            if (frontToBackWindows == null || frontToBackWindows.Count == 0) return;
            IntPtr prevHwnd = HWND_TOPMOST;
            uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING;
            for (int i = 0; i < frontToBackWindows.Count; i++)
            {
                Window w = frontToBackWindows[i];
                if (w == null || !w.IsVisible) continue;
                IntPtr hwnd = new WindowInteropHelper(w).Handle;
                if (hwnd == IntPtr.Zero) continue;
                SetWindowPos(hwnd, prevHwnd, 0, 0, 0, 0, flags);
                prevHwnd = hwnd;
            }
        }

        /// <summary>
        /// Sets or clears WS_EX_TRANSPARENT | WS_EX_LAYERED for mouse click-through.
        /// </summary>
        public static void SetWindowClickThrough(Window window, bool clickThrough, bool hideFromAltTab = false)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            long exStyle = GetWindowLongPtrSafe(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            if (hideFromAltTab)
            {
                exStyle |= WS_EX_TOOLWINDOW;
            }

            if (clickThrough)
            {
                exStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }

            SetWindowLongPtrSafe(hwnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// SECURITY NOTE: Strictly inspects modifier key state bitmask only.
        /// Never reads, logs, stores, or transmits any character or normal key input.
        /// Supports user-configurable preset to avoid AltGr conflicts on international layouts.
        /// </summary>
        public static bool AreWakeModifiersPressed(ModifierWakePreset preset)
        {
            bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

            switch (preset)
            {
                case ModifierWakePreset.CtrlShift:
                    return ctrl && shift;
                case ModifierWakePreset.AltShift:
                    return alt && shift;
                case ModifierWakePreset.CtrlAlt:
                default:
                    return ctrl && alt;
            }
        }

        /// <summary>
        /// Detects whether the current foreground window is running in full-screen mode
        /// (e.g., presentations, games, full-screen video) to enable Do-Not-Disturb auto-hide.
        /// </summary>
        public static bool IsForegroundFullScreen(IntPtr ownMainHwnd)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == ownMainHwnd || fg == GetDesktopWindow() || fg == GetShellWindow())
            {
                return false;
            }

            RECT winRect;
            if (!GetWindowRect(fg, out winRect))
            {
                return false;
            }

            IntPtr hMon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            if (hMon == IntPtr.Zero) return false;

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMon, ref mi))
            {
                return false;
            }

            int winW = winRect.Right - winRect.Left;
            int winH = winRect.Bottom - winRect.Top;
            int monW = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int monH = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

            return winRect.Left <= mi.rcMonitor.Left &&
                   winRect.Top <= mi.rcMonitor.Top &&
                   winW >= monW &&
                   winH >= monH;
        }

        /// <summary>
        /// Ensures a window remains visible inside the nearest monitor work area across multi-monitor/DPI/wake changes.
        /// </summary>
        public static void ClampToVisibleWorkArea(ref double left, ref double top, double width, double height)
        {
            Rect workArea = SystemParameters.WorkArea;
            if (left < workArea.Left - width + 60) left = workArea.Left + 16;
            if (top < workArea.Top) top = workArea.Top + 16;
            if (left > workArea.Right - 60) left = Math.Max(workArea.Left + 16, workArea.Right - width - 16);
            if (top > workArea.Bottom - 60) top = Math.Max(workArea.Top + 16, workArea.Bottom - height - 16);
        }

        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunValueName = "StickyTickets_Burgeria";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    return key != null && key.GetValue(AppRunValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoStart(bool enable, string executablePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null) return;
                    if (enable && !string.IsNullOrWhiteSpace(executablePath))
                    {
                        string fullExe = System.IO.Path.GetFullPath(executablePath);
                        if (!System.IO.File.Exists(fullExe) || !fullExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            return;
                        string safeQuoted = "\"" + fullExe.Replace("\"", "") + "\"";
                        key.SetValue(AppRunValueName, safeQuoted, RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(AppRunValueName, false);
                    }
                }
            }
            catch
            {
                // Ignore registry errors cleanly without leaking sensitive info
            }
        }
    }
}
