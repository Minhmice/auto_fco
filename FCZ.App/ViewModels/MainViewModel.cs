using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCZ.Core.Configuration;
using FCZ.Core.Engine;
using FCZ.Core.Models;
using FCZ.Core.Services;
using FCZ.Scenarios;
using FCZ.Vision;

namespace FCZ.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IWindowManager _windowManager;
        private readonly ProcessWatcher _processWatcher;
        private readonly ICaptureService _captureService;
        private readonly RuleEngine _ruleEngine;
        private readonly TemplateStore _templateStore;
        private readonly IInputService _inputService;
        private readonly ImageMatcher _imageMatcher;
        private readonly IGameLauncher _gameLauncher;

        private IntPtr? _targetWindowHandle;
        private CancellationTokenSource? _scenarioCancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<Scenario> _scenarios = new();

        [ObservableProperty]
        private Scenario? _selectedScenario;

        [ObservableProperty]
        private BitmapSource? _currentFrame;

        [ObservableProperty]
        private bool _isProcessFound;

        [ObservableProperty]
        private bool _isBackgroundMode;

        [ObservableProperty]
        private string _statusMessage = "Đang chờ FC ONLINE (fczf.exe)...";

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private ObservableCollection<Step> _currentSteps = new();

        [ObservableProperty]
        private string _logText = string.Empty;

        [ObservableProperty]
        private bool _isCapturing;

        [ObservableProperty]
        private string _captureStatus = "Not capturing";

        [ObservableProperty]
        private int _fps;

        [ObservableProperty]
        private string _processStatus = "Waiting...";

        [ObservableProperty]
        private ObservableCollection<WindowSizePreset> _windowSizePresets = new();

        [ObservableProperty]
        private WindowSizePreset? _selectedWindowSize;


        public MainViewModel(
            IWindowManager windowManager,
            ProcessWatcher processWatcher,
            ICaptureService captureService,
            RuleEngine ruleEngine,
            TemplateStore templateStore,
            IInputService inputService,
            ImageMatcher imageMatcher,
            IGameLauncher gameLauncher)
        {
            _windowManager = windowManager;
            _processWatcher = processWatcher;
            _captureService = captureService;
            _ruleEngine = ruleEngine;
            _templateStore = templateStore;
            _inputService = inputService;
            _imageMatcher = imageMatcher;
            _gameLauncher = gameLauncher;

            _processWatcher.ProcessFound += OnProcessFound;
            _processWatcher.ProcessLost += OnProcessLost;
            _captureService.FrameArrived += OnFrameArrived;
            _ruleEngine.StepStarted += OnStepStarted;
            _ruleEngine.StepCompleted += OnStepCompleted;
            _ruleEngine.ScenarioCompleted += OnScenarioCompleted;

            LoadScenarios();
            InitializeWindowSizePresets();
            _processWatcher.Start();
        }

        private void InitializeWindowSizePresets()
        {
            WindowSizePresets.Clear();
            WindowSizePresets.Add(new WindowSizePreset { Name = "800x600", Size = new Size(800, 600) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1024x768", Size = new Size(1024, 768) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1280x720 (HD)", Size = new Size(1280, 720) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1366x768", Size = new Size(1366, 768) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1600x900", Size = new Size(1600, 900) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1920x1080 (Full HD)", Size = new Size(1920, 1080) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1280x1024", Size = new Size(1280, 1024) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1440x900", Size = new Size(1440, 900) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "1680x1050", Size = new Size(1680, 1050) });
            WindowSizePresets.Add(new WindowSizePreset { Name = "2560x1440 (2K)", Size = new Size(2560, 1440) });
            
            // Set default
            SelectedWindowSize = WindowSizePresets.FirstOrDefault(p => p.Size.Width == 1600 && p.Size.Height == 900);
        }

        private void OnProcessFound(IntPtr hWnd)
        {
            _targetWindowHandle = hWnd;
            IsProcessFound = true;
            StatusMessage = "FC ONLINE found";
            ProcessStatus = "Connected";
            AppendLog("Process fczf.exe found");
            
            AppendLog($"Attempting to start capture for window handle: {hWnd}");
            if (_captureService.StartCapture(hWnd))
            {
                IsCapturing = true;
                CaptureStatus = "Capturing";
                AppendLog("Capture started successfully");
            }
            else
            {
                IsCapturing = false;
                CaptureStatus = "Failed to start";
                AppendLog("Failed to start capture - invalid window handle");
            }
            
            _ruleEngine.SetTargetWindow(hWnd);
        }

        private void OnProcessLost()
        {
            _targetWindowHandle = null;
            IsProcessFound = false;
            StatusMessage = "Đang chờ FC ONLINE (fczf.exe)...";
            ProcessStatus = "Disconnected";
            IsCapturing = false;
            CaptureStatus = "Stopped";
            _captureService.StopCapture();
            IsRunning = false;
            AppendLog("Process fczf.exe lost");
        }

        private DateTime _lastFrameTime = DateTime.Now;
        private int _frameCount = 0;

        private void OnFrameArrived(Bitmap frame)
        {
            if (frame == null)
            {
                AppendLog("WARNING: Received null frame");
                return;
            }

            try
            {
                var app = System.Windows.Application.Current;
                if (app == null)
                {
                    AppendLog("WARNING: Application.Current is null");
                    return;
                }

                app.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var bitmapSource = ConvertBitmapToBitmapSource(frame);
                        if (bitmapSource != null)
                        {
                            CurrentFrame = bitmapSource;
                            
                            // Calculate FPS
                            _frameCount++;
                            var elapsed = (DateTime.Now - _lastFrameTime).TotalSeconds;
                            if (elapsed >= 1.0)
                            {
                                Fps = (int)(_frameCount / elapsed);
                                _frameCount = 0;
                                _lastFrameTime = DateTime.Now;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"ERROR converting frame: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR in OnFrameArrived: {ex.Message}");
            }
        }

        private BitmapSource? ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

            try
            {
                if (bitmap.Width <= 0 || bitmap.Height <= 0)
                {
                    AppendLog($"WARNING: Invalid bitmap size: {bitmap.Width}x{bitmap.Height}");
                    return null;
                }

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    if (bitmapData.Scan0 == IntPtr.Zero)
                    {
                        AppendLog("WARNING: BitmapData.Scan0 is null");
                        return null;
                    }

                    // Copy pixel data to managed array before unlocking
                    int bufferSize = bitmapData.Stride * bitmapData.Height;
                    byte[] pixelData = new byte[bufferSize];
                    Marshal.Copy(bitmapData.Scan0, pixelData, 0, bufferSize);

                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width,
                        bitmapData.Height,
                        bitmap.HorizontalResolution,
                        bitmap.VerticalResolution,
                        System.Windows.Media.PixelFormats.Bgra32,
                        null,
                        pixelData,
                        bitmapData.Stride);

                    if (bitmapSource != null)
                    {
                        bitmapSource.Freeze();
                    }

                    return bitmapSource;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR in ConvertBitmapToBitmapSource: {ex.Message}");
                return null;
            }
        }

        private void OnStepStarted(Step step)
        {
            var app = System.Windows.Application.Current;
            if (app == null)
                return;

            app.Dispatcher.Invoke(() =>
            {
                AppendLog($"Step started: {step.Id} ({step.Type})");
            });
        }

        private void OnStepCompleted(Step step, StepResult result)
        {
            var app = System.Windows.Application.Current;
            if (app == null)
                return;

            app.Dispatcher.Invoke(() =>
            {
                AppendLog($"Step completed: {step.Id} - Success: {result.Success}, Message: {result.Message}");
            });
        }

        private void OnScenarioCompleted(ScenarioResult result)
        {
            var app = System.Windows.Application.Current;
            if (app == null)
                return;

            app.Dispatcher.Invoke(() =>
            {
                IsRunning = false;
                AppendLog($"Scenario completed: {result.Message}");
            });
        }

        private void AppendLog(string message)
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }

        private void LoadScenarios()
        {
            try
            {
                var scenarios = ScenarioSerializer.LoadAll(AppConfig.ScenariosPath);
                Scenarios.Clear();
                foreach (var scenario in scenarios)
                {
                    Scenarios.Add(scenario);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading scenarios: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RunScenarioAsync()
        {
            if (SelectedScenario == null || !IsProcessFound || _targetWindowHandle == null)
            {
                AppendLog("Cannot run scenario: No scenario selected or process not found");
                return;
            }

            IsRunning = true;
            _scenarioCancellationTokenSource = new CancellationTokenSource();

            if (IsBackgroundMode)
            {
                _windowManager.MoveOffScreen(_targetWindowHandle.Value, SelectedScenario.WindowSize);
            }
            else
            {
                _windowManager.NormalizeWindow(_targetWindowHandle.Value, SelectedScenario.WindowSize);
            }

            CurrentSteps.Clear();
            foreach (var step in SelectedScenario.Steps)
            {
                CurrentSteps.Add(step);
            }

            AppendLog($"Starting scenario: {SelectedScenario.Name}");

            await _ruleEngine.RunScenarioAsync(
                SelectedScenario,
                _captureService,
                _scenarioCancellationTokenSource.Token);
        }

        [RelayCommand]
        private void StopScenario()
        {
            _ruleEngine.Stop();
            _scenarioCancellationTokenSource?.Cancel();
            IsRunning = false;
            AppendLog("Scenario stopped");
        }

        [RelayCommand]
        private void ToggleBackgroundMode()
        {
            if (_targetWindowHandle == null)
            {
                AppendLog("ERROR: No target window handle");
                IsBackgroundMode = false;
                return;
            }

            // Get current window size or use default
            Size targetSize = SelectedScenario?.WindowSize ?? new Size(1280, 720);

            if (IsBackgroundMode)
            {
                // Move window off-screen (Background Mode ON)
                AppendLog($"Attempting to move window off-screen (Background Mode), handle: {_targetWindowHandle.Value}");
                bool success = _windowManager.MoveOffScreen(_targetWindowHandle.Value, targetSize);
                
                if (success)
                {
                    AppendLog("SUCCESS: Window moved off-screen - Background Mode active");
                    AppendLog("Note: Window is hidden but still rendering. Capture and automation continue to work.");
                }
                else
                {
                    IsBackgroundMode = false; // Revert checkbox
                    AppendLog("ERROR: Failed to move window off-screen");
                }
            }
            else
            {
                // Bring window back to screen (Background Mode OFF)
                AppendLog($"Attempting to bring window back to screen, handle: {_targetWindowHandle.Value}");
                
                // First check if window still exists
                if (!IsWindow(_targetWindowHandle.Value))
                {
                    AppendLog("WARNING: Window handle invalid, trying to find window again...");
                    var newHandle = _windowManager.FindTargetWindow();
                    if (newHandle.HasValue)
                    {
                        _targetWindowHandle = newHandle.Value;
                        AppendLog($"Found new window handle: {newHandle.Value}");
                    }
                    else
                    {
                        IsBackgroundMode = true; // Revert checkbox
                        AppendLog("ERROR: Cannot find window - game may have closed");
                        return;
                    }
                }
                
                bool success = _windowManager.BringToScreen(_targetWindowHandle.Value, targetSize);
                
                if (success)
                {
                    AppendLog("SUCCESS: Window brought back to screen - Background Mode disabled");
                }
                else
                {
                    IsBackgroundMode = true; // Revert checkbox
                    AppendLog("ERROR: Failed to bring window back to screen");
                }
            }
        }

        [RelayCommand]
        private void RestoreWindow()
        {
            if (_targetWindowHandle == null)
            {
                AppendLog("ERROR: No target window handle");
                return;
            }

            Size targetSize = SelectedScenario?.WindowSize ?? new Size(1280, 720);
            AppendLog($"Restoring window to screen, handle: {_targetWindowHandle.Value}");
            
            bool success = _windowManager.BringToScreen(_targetWindowHandle.Value, targetSize);
            
            if (success)
            {
                IsBackgroundMode = false;
                AppendLog("SUCCESS: Window restored to screen");
            }
            else
            {
                AppendLog("ERROR: Failed to restore window");
            }
        }

        [RelayCommand]
        private void MoveWindowToOrigin()
        {
            if (_targetWindowHandle == null)
            {
                AppendLog("ERROR: No target window handle");
                return;
            }

            AppendLog($"Attempting to move window to (0,0), handle: {_targetWindowHandle.Value}");
            var result = _windowManager.MoveToOrigin(_targetWindowHandle.Value);
            
            if (result.Success)
            {
                AppendLog($"SUCCESS: Window moved to (0, 0)");
            }
            else
            {
                AppendLog($"ERROR: Failed to move window to origin - {result.ErrorMessage}");
                AppendLog("TIP: Try running as Administrator or check if game blocks window movement");
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [RelayCommand]
        private async Task LaunchGameAsync()
        {
            AppendLog("Checking if game is already running...");
            
            // Check if game is already running
            var existingWindow = _windowManager.FindTargetWindow();
            if (existingWindow.HasValue)
            {
                AppendLog("Game is already running - no need to launch again");
                return;
            }

            AppendLog("Game not found. Starting launch sequence...");
            AppendLog("Step 1: Checking Garena launcher...");
            
            try
            {
                bool success = await _gameLauncher.LaunchGameAsync();
                
                if (success)
                {
                    AppendLog("SUCCESS: Game launch sequence completed");
                    AppendLog("Waiting for game window to appear...");
                }
                else
                {
                    AppendLog("ERROR: Game launch sequence failed");
                    AppendLog("Please check:");
                    AppendLog("  1. Garena launcher path is correct");
                    AppendLog("  2. FC ONLINE is installed");
                    AppendLog("  3. Try launching manually first");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ApplyWindowSize()
        {
            if (_targetWindowHandle == null)
            {
                AppendLog("ERROR: No target window handle");
                return;
            }

            if (SelectedWindowSize == null)
            {
                AppendLog("ERROR: No window size selected");
                return;
            }

            AppendLog($"Attempting to resize window to {SelectedWindowSize.Name} ({SelectedWindowSize.Size.Width}x{SelectedWindowSize.Size.Height}), handle: {_targetWindowHandle.Value}");
            var result = _windowManager.ResizeWindow(_targetWindowHandle.Value, SelectedWindowSize.Size);
            
            if (result.Success)
            {
                AppendLog($"SUCCESS: Window resized to {SelectedWindowSize.Name}");
            }
            else
            {
                AppendLog($"ERROR: Failed to resize window - {result.ErrorMessage}");
                AppendLog("TIP: If game has protection, try:");
                AppendLog("  1. Run this app as Administrator");
                AppendLog("  2. Check game settings for window restrictions");
                AppendLog("  3. Some games block window manipulation for anti-cheat");
            }
        }
    }

    public class WindowSizePreset
    {
        public string Name { get; set; } = string.Empty;
        public Size Size { get; set; }
    }
}

