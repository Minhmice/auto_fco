using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FCZ.App.ViewModels;

namespace FCZ.App.Controls
{
    public partial class PreviewSelectorControl : UserControl
    {
        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode), typeof(PreviewSelectorControl),
                new PropertyMetadata(SelectionMode.None, OnSelectionModeChanged));

        public static readonly DependencyProperty CurrentFrameProperty =
            DependencyProperty.Register(nameof(CurrentFrame), typeof(System.Windows.Media.Imaging.BitmapSource), typeof(PreviewSelectorControl));

        public static readonly DependencyProperty SelectedRegionProperty =
            DependencyProperty.Register(nameof(SelectedRegion), typeof(System.Drawing.Rectangle?), typeof(PreviewSelectorControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadata.BindsTwoWayByDefault));

        public static readonly DependencyProperty SelectedPointProperty =
            DependencyProperty.Register(nameof(SelectedPoint), typeof(System.Drawing.Point?), typeof(PreviewSelectorControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadata.BindsTwoWayByDefault));

        public static readonly DependencyProperty ShowCoordinatesProperty =
            DependencyProperty.Register(nameof(ShowCoordinates), typeof(bool), typeof(PreviewSelectorControl),
                new PropertyMetadata(true));

        private bool _isDragging = false;
        private System.Windows.Point _dragStart;
        private System.Windows.Point _currentMousePos;

        public SelectionMode SelectionMode
        {
            get => (SelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public System.Windows.Media.Imaging.BitmapSource? CurrentFrame
        {
            get => (System.Windows.Media.Imaging.BitmapSource?)GetValue(CurrentFrameProperty);
            set => SetValue(CurrentFrameProperty, value);
        }

        public System.Drawing.Rectangle? SelectedRegion
        {
            get => (System.Drawing.Rectangle?)GetValue(SelectedRegionProperty);
            set => SetValue(SelectedRegionProperty, value);
        }

        public System.Drawing.Point? SelectedPoint
        {
            get => (System.Drawing.Point?)GetValue(SelectedPointProperty);
            set => SetValue(SelectedPointProperty, value);
        }

        public bool ShowCoordinates
        {
            get => (bool)GetValue(ShowCoordinatesProperty);
            set => SetValue(ShowCoordinatesProperty, value);
        }

        public PreviewSelectorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
            Focusable = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ClearSelection();
                e.Handled = true;
            }
        }

        private void ClearSelection()
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            CrosshairH.Visibility = Visibility.Collapsed;
            CrosshairV.Visibility = Visibility.Collapsed;
            CoordinatesText.Visibility = Visibility.Collapsed;
            _isDragging = false;
            OverlayCanvas.ReleaseMouseCapture();
            SelectionMode = SelectionMode.None;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PreviewImage.Source = CurrentFrame;
        }

        private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PreviewSelectorControl control)
            {
                control.UpdateSelectionMode();
            }
        }

        private void UpdateSelectionMode()
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            CrosshairH.Visibility = Visibility.Collapsed;
            CrosshairV.Visibility = Visibility.Collapsed;
            CoordinatesText.Visibility = Visibility.Collapsed;

            if (SelectionMode == SelectionMode.Region || SelectionMode == SelectionMode.Point)
            {
                OverlayCanvas.Cursor = Cursors.Cross;
            }
            else
            {
                OverlayCanvas.Cursor = Cursors.Arrow;
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check for Ctrl modifier (hotkey support)
            bool ctrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            
            _currentMousePos = e.GetPosition(OverlayCanvas);
            _dragStart = _currentMousePos;

            if (ctrlPressed)
            {
                // Hotkey mode: Ctrl+Drag = Region, Ctrl+Click = Point
                // We'll determine which one based on mouse movement in OnMouseMove
                // For now, just capture mouse and wait
                OverlayCanvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (SelectionMode == SelectionMode.None)
                return;

            if (SelectionMode == SelectionMode.Region)
            {
                _isDragging = true;
                SelectionRectangle.Visibility = Visibility.Visible;
                UpdateSelectionRectangle();
            }
            else if (SelectionMode == SelectionMode.Point)
            {
                // Point selection on click
                var point = ConvertToImageCoordinates(_currentMousePos);
                if (point.HasValue)
                {
                    SelectedPoint = new System.Drawing.Point((int)point.Value.X, (int)point.Value.Y);
                    SelectionMode = SelectionMode.None;
                }
            }

            OverlayCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _currentMousePos = e.GetPosition(OverlayCanvas);

            if (SelectionMode == SelectionMode.Region && _isDragging)
            {
                UpdateSelectionRectangle();
            }
            else if (SelectionMode == SelectionMode.Point && !_isDragging)
            {
                UpdateCrosshair();
                if (ShowCoordinates)
                {
                    var point = ConvertToImageCoordinates(_currentMousePos);
                    if (point.HasValue)
                    {
                        CoordinatesText.Text = $"X: {(int)point.Value.X}, Y: {(int)point.Value.Y}";
                        CoordinatesText.Visibility = Visibility.Visible;
                        Canvas.SetLeft(CoordinatesText, _currentMousePos.X + 10);
                        Canvas.SetTop(CoordinatesText, _currentMousePos.Y + 10);
                    }
                }
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            bool ctrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            
            if (_isDragging)
            {
                // Region selection completed
                var region = GetSelectionRectangle();
                if (region.HasValue)
                {
                    SelectedRegion = region.Value;
                    if (!ctrlPressed)
                    {
                        SelectionMode = SelectionMode.None;
                    }
                }
            }
            else if (ctrlPressed && e.ClickCount == 1)
            {
                // Ctrl+Click = Point selection
                var point = ConvertToImageCoordinates(_currentMousePos);
                if (point.HasValue)
                {
                    SelectedPoint = new System.Drawing.Point((int)point.Value.X, (int)point.Value.Y);
                }
            }

            _isDragging = false;
            OverlayCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                CrosshairH.Visibility = Visibility.Collapsed;
                CrosshairV.Visibility = Visibility.Collapsed;
                CoordinatesText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSelectionRectangle()
        {
            var rect = GetSelectionRectangle();
            if (rect.HasValue)
            {
                var r = rect.Value;
                Canvas.SetLeft(SelectionRectangle, r.X);
                Canvas.SetTop(SelectionRectangle, r.Y);
                SelectionRectangle.Width = r.Width;
                SelectionRectangle.Height = r.Height;
            }
        }

        private System.Drawing.Rectangle? GetSelectionRectangle()
        {
            var start = ConvertToImageCoordinates(_dragStart);
            var end = ConvertToImageCoordinates(_currentMousePos);

            if (!start.HasValue || !end.HasValue)
                return null;

            var x = Math.Min(start.Value.X, end.Value.X);
            var y = Math.Min(start.Value.Y, end.Value.Y);
            var width = Math.Abs(end.Value.X - start.Value.X);
            var height = Math.Abs(end.Value.Y - start.Value.Y);

            if (width < 5 || height < 5)
                return null;

            return new System.Drawing.Rectangle((int)x, (int)y, (int)width, (int)height);
        }

        private void UpdateCrosshair()
        {
            if (SelectionMode == SelectionMode.Point)
            {
                CrosshairH.Visibility = Visibility.Visible;
                CrosshairV.Visibility = Visibility.Visible;

                CrosshairH.X1 = 0;
                CrosshairH.Y1 = _currentMousePos.Y;
                CrosshairH.X2 = OverlayCanvas.ActualWidth;
                CrosshairH.Y2 = _currentMousePos.Y;

                CrosshairV.X1 = _currentMousePos.X;
                CrosshairV.Y1 = 0;
                CrosshairV.X2 = _currentMousePos.X;
                CrosshairV.Y2 = OverlayCanvas.ActualHeight;
            }
        }

        private System.Windows.Point? ConvertToImageCoordinates(System.Windows.Point canvasPoint)
        {
            if (CurrentFrame == null || PreviewImage.ActualWidth == 0 || PreviewImage.ActualHeight == 0)
                return null;

            // Get the actual rendered image size (with Stretch="Uniform")
            var imageWidth = CurrentFrame.PixelWidth;
            var imageHeight = CurrentFrame.PixelHeight;
            var containerWidth = PreviewImage.ActualWidth;
            var containerHeight = PreviewImage.ActualHeight;

            var scaleX = imageWidth / containerWidth;
            var scaleY = imageHeight / containerHeight;
            var scale = Math.Min(scaleX, scaleY);

            var scaledWidth = imageWidth / scale;
            var scaledHeight = imageHeight / scale;
            var offsetX = (containerWidth - scaledWidth) / 2;
            var offsetY = (containerHeight - scaledHeight) / 2;

            var relativeX = canvasPoint.X - offsetX;
            var relativeY = canvasPoint.Y - offsetY;

            if (relativeX < 0 || relativeY < 0 || relativeX > scaledWidth || relativeY > scaledHeight)
                return null;

            var imageX = relativeX * scale;
            var imageY = relativeY * scale;

            return new System.Windows.Point(imageX, imageY);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == CurrentFrameProperty)
            {
                PreviewImage.Source = CurrentFrame;
            }
        }
    }
}

