using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
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

        public MainViewModel(
            IWindowManager windowManager,
            ProcessWatcher processWatcher,
            ICaptureService captureService,
            RuleEngine ruleEngine,
            TemplateStore templateStore,
            IInputService inputService,
            ImageMatcher imageMatcher)
        {
            _windowManager = windowManager;
            _processWatcher = processWatcher;
            _captureService = captureService;
            _ruleEngine = ruleEngine;
            _templateStore = templateStore;
            _inputService = inputService;
            _imageMatcher = imageMatcher;

            _processWatcher.ProcessFound += OnProcessFound;
            _processWatcher.ProcessLost += OnProcessLost;
            _captureService.FrameArrived += OnFrameArrived;
            _ruleEngine.StepStarted += OnStepStarted;
            _ruleEngine.StepCompleted += OnStepCompleted;
            _ruleEngine.ScenarioCompleted += OnScenarioCompleted;

            LoadScenarios();
            _processWatcher.Start();
        }

        private void OnProcessFound(IntPtr hWnd)
        {
            _targetWindowHandle = hWnd;
            IsProcessFound = true;
            StatusMessage = "FC ONLINE found";
            _captureService.StartCapture(hWnd);
            _ruleEngine.SetTargetWindow(hWnd);
        }

        private void OnProcessLost()
        {
            _targetWindowHandle = null;
            IsProcessFound = false;
            StatusMessage = "Đang chờ FC ONLINE (fczf.exe)...";
            _captureService.StopCapture();
            IsRunning = false;
        }

        private void OnFrameArrived(Bitmap frame)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentFrame = ConvertBitmapToBitmapSource(frame);
            });
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width,
                    bitmapData.Height,
                    bitmap.HorizontalResolution,
                    bitmap.VerticalResolution,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);

                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private void OnStepStarted(Step step)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"Step started: {step.Id} ({step.Type})");
            });
        }

        private void OnStepCompleted(Step step, StepResult result)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"Step completed: {step.Id} - Success: {result.Success}, Message: {result.Message}");
            });
        }

        private void OnScenarioCompleted(ScenarioResult result)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
            if (_targetWindowHandle == null || SelectedScenario == null)
            {
                return;
            }

            if (IsBackgroundMode)
            {
                // Move window off-screen
                _windowManager.MoveOffScreen(_targetWindowHandle.Value, SelectedScenario.WindowSize);
            }
            else
            {
                // Bring window back to screen
                _windowManager.BringToScreen(_targetWindowHandle.Value, SelectedScenario.WindowSize);
            }
        }

        [RelayCommand]
        private void RestoreWindow()
        {
            if (_targetWindowHandle == null || SelectedScenario == null)
            {
                return;
            }

            _windowManager.BringToScreen(_targetWindowHandle.Value, SelectedScenario.WindowSize);
            IsBackgroundMode = false;
        }
    }
}

