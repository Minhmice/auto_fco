using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace FCZ.Core.Services
{
    public interface IInputService
    {
        void ClickScreen(Point screenPoint);
        void ClickWindowPoint(IntPtr hWnd, Point windowPoint);
        void TypeText(string text);
        void SendKey(ushort virtualKey);
        void ClearInputField(int backspaceCount = 16);
    }

    public class InputService : IInputService
    {
        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public void ClickScreen(Point screenPoint)
        {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Convert to absolute coordinates (0-65535)
            int absX = (int)((double)screenPoint.X * 65535.0 / screenWidth);
            int absY = (int)((double)screenPoint.Y * 65535.0 / screenHeight);

            var inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_MOUSE,
                    U = new INPUTUNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = absX,
                            dy = absY,
                            dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTDOWN,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                },
                new INPUT
                {
                    type = INPUT_MOUSE,
                    U = new INPUTUNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = absX,
                            dy = absY,
                            dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                }
            };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void ClickWindowPoint(IntPtr hWnd, Point windowPoint)
        {
            POINT pt = new POINT { X = windowPoint.X, Y = windowPoint.Y };
            if (ClientToScreen(hWnd, ref pt))
            {
                ClickScreen(new Point(pt.X, pt.Y));
            }
        }

        public void TypeText(string text)
        {
            foreach (char c in text)
            {
                ushort vk = GetVirtualKey(c);
                if (vk != 0)
                {
                    SendKey(vk);
                    System.Threading.Thread.Sleep(10); // Small delay between keys
                }
            }
        }

        public void SendKey(ushort virtualKey)
        {
            var inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                },
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = 0,
                            dwFlags = KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                }
            };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void ClearInputField(int backspaceCount = 16)
        {
            const ushort VK_BACK = 0x08;
            for (int i = 0; i < backspaceCount; i++)
            {
                SendKey(VK_BACK);
                System.Threading.Thread.Sleep(10);
            }
        }

        private ushort GetVirtualKey(char c)
        {
            // Simple mapping for common characters
            // For production, use VkKeyScan or a more comprehensive mapping
            if (char.IsLetter(c))
            {
                return (ushort)(char.ToUpper(c));
            }
            else if (char.IsDigit(c))
            {
                return (ushort)c;
            }
            else
            {
                // Use VkKeyScan for other characters
                short vk = VkKeyScan(c);
                if (vk != -1)
                {
                    return (ushort)(vk & 0xFF);
                }
            }
            return 0;
        }

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);
    }
}

