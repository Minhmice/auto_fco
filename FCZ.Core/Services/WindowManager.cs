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
        WindowOperationResult MoveToOrigin(IntPtr hWnd);
        WindowOperationResult ResizeWindow(IntPtr hWnd, Size newSize);
        WindowOperationResult MinimizeWindow(IntPtr hWnd);
        WindowOperationResult HideWindow(IntPtr hWnd);
        WindowOperationResult ShowWindow(IntPtr hWnd);
    }

    public class WindowManager : IWindowManager
    {
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int HWND_TOP = 0;
        private const int HWND_TOPMOST = -1;
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

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_SIZEBOX = 0x00040000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_DISABLED = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        
        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint SC_MINIMIZE = 0xF020;
        private const uint SC_RESTORE = 0xF120;
        private const uint WM_MOVE = 0x0003;
        private const uint WM_SIZE = 0x0005;

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

        public WindowOperationResult MoveToOrigin(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return new WindowOperationResult { Success = false, ErrorMessage = "Invalid window handle" };

            if (!IsWindow(hWnd))
                return new WindowOperationResult { Success = false, ErrorMessage = "Window handle is not valid" };

            // Restore if minimized/maximized
            if (IsIconic(hWnd) || IsZoomed(hWnd))
            {
                ShowWindow(hWnd, 9); // SW_RESTORE
                System.Threading.Thread.Sleep(100);
            }

            if (!GetWindowRect(hWnd, out RECT rect))
            {
                int error = Marshal.GetLastWin32Error();
                return new WindowOperationResult { Success = false, ErrorMessage = $"GetWindowRect failed (Error: {error})" };
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return new WindowOperationResult { Success = false, ErrorMessage = $"Invalid window size: {width}x{height}" };

            // Simple move - just SetWindowPos
            bool result = SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0,
                0,
                width,
                height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            if (result)
            {
                return new WindowOperationResult { Success = true, ErrorMessage = $"Window moved to (0, 0)" };
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                return new WindowOperationResult { Success = false, ErrorMessage = $"SetWindowPos failed (Error: {error})" };
            }
        }

        public WindowOperationResult ResizeWindow(IntPtr hWnd, Size newSize)
        {
            if (hWnd == IntPtr.Zero)
                return new WindowOperationResult { Success = false, ErrorMessage = "Invalid window handle" };

            if (!IsWindow(hWnd))
                return new WindowOperationResult { Success = false, ErrorMessage = "Window handle is not valid" };

            if (newSize.Width <= 0 || newSize.Height <= 0)
                return new WindowOperationResult { Success = false, ErrorMessage = $"Invalid size: {newSize.Width}x{newSize.Height}" };

            // Restore if minimized/maximized
            if (IsIconic(hWnd) || IsZoomed(hWnd))
            {
                ShowWindow(hWnd, 9); // SW_RESTORE
                System.Threading.Thread.Sleep(100);
            }

            if (!GetWindowRect(hWnd, out RECT rect))
            {
                int error = Marshal.GetLastWin32Error();
                return new WindowOperationResult { Success = false, ErrorMessage = $"GetWindowRect failed (Error: {error})" };
            }

            // Simple resize - just SetWindowPos
            bool result = SetWindowPos(
                hWnd,
                IntPtr.Zero,
                rect.Left,
                rect.Top,
                newSize.Width,
                newSize.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            if (result)
            {
                return new WindowOperationResult { Success = true, ErrorMessage = $"Window resized to {newSize.Width}x{newSize.Height}" };
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                return new WindowOperationResult { Success = false, ErrorMessage = $"SetWindowPos failed (Error: {error})" };
            }
        }

        public WindowOperationResult MinimizeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return new WindowOperationResult { Success = false, ErrorMessage = "Invalid window handle" };

            if (!IsWindow(hWnd))
                return new WindowOperationResult { Success = false, ErrorMessage = "Window handle is not valid" };

            // Check window state
            bool isMinimized = IsIconic(hWnd);
            bool isMaximized = IsZoomed(hWnd);

            if (isMinimized)
            {
                return new WindowOperationResult { Success = true, ErrorMessage = "Window is already minimized" };
            }

            // Check if window can be minimized
            int style = GetWindowLong(hWnd, GWL_STYLE);
            bool canMinimize = (style & WS_MINIMIZEBOX) != 0;
            bool isDisabled = (style & WS_DISABLED) != 0;

            // Enable window if disabled
            if (isDisabled)
            {
                EnableWindow(hWnd, true);
                System.Threading.Thread.Sleep(50);
            }

            // Try multiple methods
            bool result = false;
            const int SW_MINIMIZE = 6;
            const int SW_FORCEMINIMIZE = 11;
            const int SW_SHOWMINIMIZED = 2;
            
            result = ShowWindow(hWnd, SW_MINIMIZE);
            
            if (!result)
            {
                result = ShowWindow(hWnd, SW_FORCEMINIMIZE);
            }

            if (!result)
            {
                result = ShowWindow(hWnd, SW_SHOWMINIMIZED);
            }

            if (!result)
            {
                // Method 4: Try SendMessage with WM_SYSCOMMAND
                IntPtr msgResult = SendMessage(hWnd, WM_SYSCOMMAND, new IntPtr(SC_MINIMIZE), IntPtr.Zero);
                result = msgResult != IntPtr.Zero;
            }

            if (!result)
            {
                // Method 5: Try PostMessage (async)
                PostMessage(hWnd, WM_SYSCOMMAND, new IntPtr(SC_MINIMIZE), IntPtr.Zero);
                System.Threading.Thread.Sleep(200);
                result = IsIconic(hWnd); // Check if actually minimized
            }

            if (!result)
            {
                // Method 6: Try attaching to window thread
                uint windowThreadId = GetWindowThreadProcessId(hWnd, out _);
                uint currentThreadId = GetCurrentThreadId();
                bool attached = false;
                
                if (windowThreadId != currentThreadId)
                {
                    attached = AttachThreadInput(currentThreadId, windowThreadId, true);
                }

                try
                {
                    result = ShowWindow(hWnd, SW_MINIMIZE);
                }
                finally
                {
                    if (attached)
                    {
                        AttachThreadInput(currentThreadId, windowThreadId, false);
                    }
                }
            }

            if (!result)
            {
                string stateInfo = $"Maximized: {isMaximized}, Disabled: {isDisabled}, Style: 0x{style:X8}, CanMinimize: {canMinimize}";
                return new WindowOperationResult { Success = false, ErrorMessage = $"All minimize methods failed. {stateInfo}. Game likely has protection preventing minimize. Try running as administrator." };
            }

            return new WindowOperationResult { Success = true, ErrorMessage = string.Empty };
        }

        public WindowOperationResult HideWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return new WindowOperationResult { Success = false, ErrorMessage = "Invalid window handle" };

            if (!IsWindow(hWnd))
                return new WindowOperationResult { Success = false, ErrorMessage = "Window handle is not valid" };

            // Use SW_MINIMIZE instead of SW_HIDE to keep window accessible
            // SW_HIDE makes window completely invisible and hard to restore
            const int SW_MINIMIZE = 6;
            bool result = ShowWindow(hWnd, SW_MINIMIZE);

            if (!result)
            {
                // Fallback to SW_HIDE if minimize fails
                const int SW_HIDE = 0;
                result = ShowWindow(hWnd, SW_HIDE);
            }

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                return new WindowOperationResult { Success = false, ErrorMessage = $"Hide window failed (Error: {error})" };
            }

            return new WindowOperationResult { Success = true, ErrorMessage = string.Empty };
        }

        public WindowOperationResult ShowWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return new WindowOperationResult { Success = false, ErrorMessage = "Invalid window handle" };

            // First check if window still exists
            if (!IsWindow(hWnd))
            {
                // Try to find window again
                return new WindowOperationResult { Success = false, ErrorMessage = "Window handle is not valid - window may have been closed" };
            }

            // Try multiple show methods
            const int SW_SHOW = 5;
            const int SW_RESTORE = 9;
            const int SW_SHOWNORMAL = 1;
            
            bool result = ShowWindow(hWnd, SW_RESTORE);
            
            if (!result)
            {
                result = ShowWindow(hWnd, SW_SHOW);
            }

            if (!result)
            {
                result = ShowWindow(hWnd, SW_SHOWNORMAL);
            }

            if (!result)
            {
                // Force bring to front
                SetWindowPos(hWnd, new IntPtr(HWND_TOP), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                result = ShowWindow(hWnd, SW_SHOW);
            }

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                return new WindowOperationResult { Success = false, ErrorMessage = $"Show window failed (Error: {error})" };
            }

            return new WindowOperationResult { Success = true, ErrorMessage = string.Empty };
        }
    }

    public class WindowOperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

