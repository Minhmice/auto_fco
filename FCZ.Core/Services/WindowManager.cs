using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using FCZ.Core.Configuration;

namespace FCZ.Core.Services
{
    public interface IWindowManager
    {
        IntPtr? FindTargetWindow();
        bool NormalizeWindow(IntPtr hWnd, Size targetSize);
        bool MoveOffScreen(IntPtr hWnd, Size targetSize);
        bool BringToScreen(IntPtr hWnd, Size targetSize);
    }

    public class WindowManager : IWindowManager
    {
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOZORDER = 0x0004;
        private const int HWND_TOP = 0;
        private const int HWND_NOTOPMOST = -2;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public IntPtr? FindTargetWindow()
        {
            var processes = Process.GetProcessesByName("fczf");
            foreach (var process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
            return null;
        }

        public bool NormalizeWindow(IntPtr hWnd, Size targetSize)
        {
            return SetWindowPos(
                hWnd,
                new IntPtr(HWND_TOP),
                0,
                0,
                targetSize.Width,
                targetSize.Height,
                SWP_SHOWWINDOW);
        }

        public bool MoveOffScreen(IntPtr hWnd, Size targetSize)
        {
            // Move window off-screen but DO NOT minimize or hide
            // This allows Windows to continue rendering the window
            return SetWindowPos(
                hWnd,
                new IntPtr(HWND_NOTOPMOST),
                -10000,
                -10000,
                targetSize.Width,
                targetSize.Height,
                SWP_NOACTIVATE | SWP_NOZORDER);
        }

        public bool BringToScreen(IntPtr hWnd, Size targetSize)
        {
            return NormalizeWindow(hWnd, targetSize);
        }
    }
}

