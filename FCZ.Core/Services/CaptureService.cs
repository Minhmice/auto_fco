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
                catch
                {
                    break;
                }
            }
        }

        private Bitmap? CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return null;

            // Get window dimensions
            if (!GetWindowRect(hWnd, out RECT rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                try
                {
                    // Use PrintWindow to capture window content
                    // PW_RENDERFULLCONTENT flag ensures we get content even when window is off-screen
                    PrintWindow(hWnd, hdc, 0x00000003); // PW_CLIENTONLY | PW_RENDERFULLCONTENT
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            return bitmap;
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
