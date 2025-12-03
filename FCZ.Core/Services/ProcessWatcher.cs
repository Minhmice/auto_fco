using System;
using System.Timers;

namespace FCZ.Core.Services
{
    public class ProcessWatcher
    {
        private readonly IWindowManager _windowManager;
        private System.Timers.Timer? _timer;
        private IntPtr? _currentWindowHandle;

        public event Action<IntPtr>? ProcessFound;
        public event Action? ProcessLost;

        public ProcessWatcher(IWindowManager windowManager)
        {
            _windowManager = windowManager;
        }

        public void Start()
        {
            if (_timer != null)
                return;

            _timer = new System.Timers.Timer(2000); // 2 seconds
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();

            // Check immediately
            OnTimerElapsed(null, EventArgs.Empty);
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _timer = null;
            }
            _currentWindowHandle = null;
        }

        private void OnTimerElapsed(object? sender, EventArgs e)
        {
            var windowHandle = _windowManager.FindTargetWindow();

            if (windowHandle.HasValue)
            {
                // Process found
                if (!_currentWindowHandle.HasValue || _currentWindowHandle.Value != windowHandle.Value)
                {
                    _currentWindowHandle = windowHandle.Value;
                    ProcessFound?.Invoke(windowHandle.Value);
                }
            }
            else
            {
                // Process lost
                if (_currentWindowHandle.HasValue)
                {
                    _currentWindowHandle = null;
                    ProcessLost?.Invoke();
                }
            }
        }
    }
}

