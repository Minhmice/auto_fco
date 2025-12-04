using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FCZ.Core.Services
{
    public interface IGameLauncher
    {
        Task<bool> LaunchGameAsync();
    }

    public class GameLauncher : IGameLauncher
    {
        private readonly IWindowManager _windowManager;
        private readonly IInputService _inputService;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;

        public GameLauncher(IWindowManager windowManager, IInputService inputService)
        {
            _windowManager = windowManager;
            _inputService = inputService;
        }

        public async Task<bool> LaunchGameAsync()
        {
            try
            {
                // Step 1: Check if game is already running
                var fcoWindow = FindFCOWindow();
                if (fcoWindow != null)
                {
                    // Game already running - return success with message
                    return true; // Caller will log the message
                }

                // Step 2: Check if Garena launcher is running
                var garenaLauncher = FindGarenaLauncher();
                if (garenaLauncher == null)
                {
                    // Step 3: Launch Garena launcher
                    const string garenaPath = @"C:\Program Files (x86)\Garena\Garena\Garena.exe";
                    if (!System.IO.File.Exists(garenaPath))
                    {
                        throw new Exception($"Garena launcher not found at: {garenaPath}");
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = garenaPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    
                    // Wait for launcher to start
                    await Task.Delay(3000);
                    
                    // Try to find launcher window
                    for (int i = 0; i < 10; i++)
                    {
                        garenaLauncher = FindGarenaLauncher();
                        if (garenaLauncher != null)
                            break;
                        await Task.Delay(500);
                    }

                    if (garenaLauncher == null)
                    {
                        throw new Exception("Garena launcher failed to start");
                    }
                }
                else
                {
                    // Launcher is running, restore it
                    ShowWindow(garenaLauncher.Value, SW_RESTORE);
                    await Task.Delay(500);
                }

                // Step 4: Select FC ONLINE game if not already selected
                // Use percentage-based coordinates to click on game tile
                // Example: FC ONLINE tile is typically at ~30% width, ~40% height from top-left
                if (!await SelectFCOGameByPercentage(garenaLauncher.Value))
                {
                    return false;
                }

                await Task.Delay(1000);

                // Step 5: Click Play button using percentage-based coordinates
                // Play button is typically at bottom center or bottom left
                if (!await ClickPlayButtonByPercentage(garenaLauncher.Value))
                {
                    return false;
                }

                // Step 6: Wait for game to launch
                await Task.Delay(3000);
                for (int i = 0; i < 20; i++)
                {
                    fcoWindow = FindFCOWindow();
                    if (fcoWindow != null)
                    {
                        return true;
                    }
                    await Task.Delay(1000);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LaunchGameAsync error: {ex.Message}");
                throw;
            }
        }

        private IntPtr? FindGarenaWindow()
        {
            var processes = Process.GetProcessesByName("Garena");
            foreach (var process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
            return null;
        }

        private IntPtr? FindFCOWindow()
        {
            return _windowManager.FindTargetWindow(); // This finds fczf.exe
        }

        private IntPtr? FindGarenaLauncher()
        {
            // Try to find Garena launcher window by class name or title
            IntPtr hWnd = FindWindow(null, "Garena");
            if (hWnd != IntPtr.Zero && IsWindow(hWnd))
            {
                return hWnd;
            }

            // Try alternative: find by process
            var processes = Process.GetProcessesByName("Garena");
            foreach (var process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    if (process.MainWindowTitle.Contains("Garena") || process.MainWindowTitle.Contains("Garena"))
                    {
                        return process.MainWindowHandle;
                    }
                }
            }

            return null;
        }

        private async Task<bool> SelectFCOGameByPercentage(IntPtr hWnd)
        {
            if (!GetWindowRect(hWnd, out RECT rect))
                return false;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // FC ONLINE game tile is typically positioned at:
            // - X: ~30% from left (0.3 * width)
            // - Y: ~40% from top (0.4 * height)
            // Adjust these percentages based on your actual Garena launcher layout
            
            int clickX = (int)(width * 0.30);  // 30% from left
            int clickY = (int)(height * 0.40);  // 40% from top
            
            _inputService.ClickWindowPoint(hWnd, new Point(clickX, clickY));
            await Task.Delay(500);
            
            return true;
        }

        private async Task<bool> ClickPlayButtonByPercentage(IntPtr hWnd)
        {
            if (!GetWindowRect(hWnd, out RECT rect))
                return false;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // Play button is typically at bottom center or bottom left
            // Try multiple percentage-based positions:
            // 1. Bottom left: ~25% width, ~95% height
            // 2. Bottom center: ~50% width, ~95% height
            // 3. Slightly above bottom left: ~20% width, ~90% height
            
            Point[] playButtonPositions = new[]
            {
                new Point((int)(width * 0.25), (int)(height * 0.95)),  // Bottom left (25%, 95%)
                new Point((int)(width * 0.50), (int)(height * 0.95)),  // Bottom center (50%, 95%)
                new Point((int)(width * 0.20), (int)(height * 0.90)),  // Slightly above bottom left (20%, 90%)
            };

            foreach (var pos in playButtonPositions)
            {
                _inputService.ClickWindowPoint(hWnd, pos);
                await Task.Delay(500);
                
                // Check if FCO window appeared (game launched)
                await Task.Delay(1000);
                if (FindFCOWindow() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

