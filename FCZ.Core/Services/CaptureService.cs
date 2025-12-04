using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FCZ.Core.Services
{
    public interface ICaptureService : IDisposable
    {
        bool StartCapture(IntPtr hWnd);
        void StopCapture();
        event Action<Bitmap>? FrameArrived;
        Bitmap? LatestFrame { get; }
    }

    public class CaptureService : ICaptureService
    {
        private readonly ReaderWriterLockSlim _frameLock = new ReaderWriterLockSlim();
        private Bitmap? _latestFrame;
        private IntPtr _targetWindowHandle;
        private bool _isCapturing;
        private Task? _captureTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<Bitmap>? FrameArrived;

        public Bitmap? LatestFrame
        {
            get
            {
                _frameLock.EnterReadLock();
                try
                {
                    return _latestFrame?.Clone() as Bitmap;
                }
                finally
                {
                    _frameLock.ExitReadLock();
                }
            }
        }

        public bool StartCapture(IntPtr hWnd)
        {
            if (_isCapturing)
            {
                StopCapture();
            }

            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            _targetWindowHandle = hWnd;
            _isCapturing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            _captureTask = Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));
            return true;
        }

        public void StopCapture()
        {
            _isCapturing = false;
            _cancellationTokenSource?.Cancel();
            _captureTask?.Wait(1000);

            _frameLock.EnterWriteLock();
            try
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }
            finally
            {
                _frameLock.ExitWriteLock();
            }
        }

        private void CaptureLoop(CancellationToken token)
        {
            // Using PrintWindow as a fallback method for window capture
            // This works even when window is off-screen (not minimized)
            while (_isCapturing && !token.IsCancellationRequested)
            {
                try
                {
                    var bitmap = CaptureWindow(_targetWindowHandle);
                    if (bitmap != null)
                    {
                        _frameLock.EnterWriteLock();
                        try
                        {
                            _latestFrame?.Dispose();
                            _latestFrame = bitmap.Clone() as Bitmap;
                        }
                        finally
                        {
                            _frameLock.ExitWriteLock();
                        }

                        FrameArrived?.Invoke(bitmap);
                        bitmap.Dispose();
                    }

                    Thread.Sleep(33); // ~30 FPS
                }
                catch (Exception ex)
                {
                    // Log error but continue trying
                    System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
                    Thread.Sleep(100); // Wait a bit before retrying
                }
            }
        }

        private Bitmap? CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return null;

            // Check if window is valid
            if (!IsWindow(hWnd))
                return null;

            // Get window dimensions
            if (!GetWindowRect(hWnd, out RECT rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // Try multiple capture methods for better compatibility
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bool success = false;

            using (var graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                try
                {
                    // Method 1: Try PrintWindow with PW_RENDERFULLCONTENT (works for off-screen windows)
                    if (PrintWindow(hWnd, hdc, 0x00000003)) // PW_CLIENTONLY | PW_RENDERFULLCONTENT
                    {
                        success = true;
                    }
                    else
                    {
                        // Method 2: Fallback to BitBlt if PrintWindow fails
                        IntPtr windowDc = GetWindowDC(hWnd);
                        if (windowDc != IntPtr.Zero)
                        {
                            try
                            {
                                if (BitBlt(hdc, 0, 0, width, height, windowDc, 0, 0, 0x00CC0020)) // SRCCOPY
                                {
                                    success = true;
                                }
                            }
                            finally
                            {
                                ReleaseDC(hWnd, windowDc);
                            }
                        }
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            return success ? bitmap : null;
        }

        public void Dispose()
        {
            StopCapture();
            _frameLock.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
