using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Bangumi.Controls
{
    public sealed partial class MarqueeTextBlock : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarqueeTextBlock), new PropertyMetadata(string.Empty, OnAppearancePropertyChanged));

        public static readonly DependencyProperty TextForegroundProperty =
            DependencyProperty.Register(nameof(TextForeground), typeof(Brush), typeof(MarqueeTextBlock), new PropertyMetadata(null, OnAppearancePropertyChanged));

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(nameof(TextFontSize), typeof(double), typeof(MarqueeTextBlock), new PropertyMetadata(14d, OnAppearancePropertyChanged));

        public static readonly DependencyProperty TextFontWeightProperty =
            DependencyProperty.Register(nameof(TextFontWeight), typeof(Windows.UI.Text.FontWeight), typeof(MarqueeTextBlock), new PropertyMetadata(Windows.UI.Text.FontWeights.Normal, OnAppearancePropertyChanged));

        public static readonly DependencyProperty BackgroundBrushProperty =
            DependencyProperty.Register(nameof(BackgroundBrush), typeof(Brush), typeof(MarqueeTextBlock), new PropertyMetadata(null, OnAppearancePropertyChanged));

        private readonly DispatcherTimer _animationTimer;
        private bool _isInViewport;
        private double _textWidth;
        private double _textStartX;
        private double _offset;
        private DateTimeOffset _phaseStartedAt;
        private MarqueePhase _phase = MarqueePhase.StartHold;

        private const float LeadingPadding = 3f;
        private const double PixelsPerSecond = 22;
        private const double StartHoldSeconds = 1.4;
        private const double EndHoldSeconds = 1.2;

        public MarqueeTextBlock()
        {
            InitializeComponent();

            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += AnimationTimer_Tick;

            Loaded += MarqueeTextBlock_Loaded;
            Unloaded += MarqueeTextBlock_Unloaded;
            SizeChanged += MarqueeTextBlock_SizeChanged;
            EffectiveViewportChanged += MarqueeTextBlock_EffectiveViewportChanged;
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Brush TextForeground
        {
            get => (Brush)GetValue(TextForegroundProperty);
            set => SetValue(TextForegroundProperty, value);
        }

        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        public Windows.UI.Text.FontWeight TextFontWeight
        {
            get => (Windows.UI.Text.FontWeight)GetValue(TextFontWeightProperty);
            set => SetValue(TextFontWeightProperty, value);
        }

        public Brush BackgroundBrush
        {
            get => (Brush)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        private static void OnAppearancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock control)
            {
                control.RefreshVisual();
            }
        }

        private void MarqueeTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            _isInViewport = true;
            RefreshVisual();
        }

        private void MarqueeTextBlock_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
            _isInViewport = false;
        }

        private void MarqueeTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshVisual();
        }

        private void MarqueeTextBlock_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            bool isVisibleInViewport = args.EffectiveViewport.Width > 0 && args.EffectiveViewport.Height > 0;
            if (isVisibleInViewport && !_isInViewport)
            {
                _isInViewport = true;
                RestartAnimation();
            }
            else if (!isVisibleInViewport)
            {
                _isInViewport = false;
                StopAnimation();
            }
        }

        private void AnimationTimer_Tick(object sender, object e)
        {
            if (!_isInViewport || _textWidth <= ActualWidth + 2)
            {
                return;
            }

            double travelDistance = Math.Max(0, _textWidth - ActualWidth + LeadingPadding + 4);
            double travelSeconds = Math.Max(4.5, travelDistance / PixelsPerSecond);
            double elapsed = (DateTimeOffset.Now - _phaseStartedAt).TotalSeconds;

            switch (_phase)
            {
                case MarqueePhase.StartHold:
                    _offset = 0;
                    if (elapsed >= StartHoldSeconds)
                    {
                        _phase = MarqueePhase.MovingForward;
                        _phaseStartedAt = DateTimeOffset.Now;
                    }
                    break;
                case MarqueePhase.MovingForward:
                    _offset = -travelDistance * Math.Min(1, elapsed / travelSeconds);
                    if (elapsed >= travelSeconds)
                    {
                        _offset = -travelDistance;
                        _phase = MarqueePhase.EndHold;
                        _phaseStartedAt = DateTimeOffset.Now;
                    }
                    break;
                case MarqueePhase.EndHold:
                    _offset = -travelDistance;
                    if (elapsed >= EndHoldSeconds)
                    {
                        _offset = 0;
                        _phase = MarqueePhase.StartHold;
                        _phaseStartedAt = DateTimeOffset.Now;
                    }
                    break;
            }

            TextCanvas?.Invalidate();
        }

        private void RefreshVisual()
        {
            UpdateTextMetrics();

            if (!IsLoaded || TextCanvas == null)
            {
                return;
            }

            RootGrid.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, ActualWidth, ActualHeight)
            };

            if (_textWidth <= ActualWidth + 2 || string.IsNullOrWhiteSpace(Text))
            {
                StopAnimation();
            }
            else
            {
                RestartAnimation();
            }

            TextCanvas.Invalidate();
        }

        private void UpdateTextMetrics()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0 || string.IsNullOrWhiteSpace(Text))
            {
                _textWidth = 0;
                _textStartX = 0;
                return;
            }

            using (var format = CreateTextFormat())
            using (var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), Text ?? string.Empty, format, float.MaxValue, (float)Math.Max(ActualHeight, TextFontSize * 1.6)))
            {
                var drawBounds = layout.DrawBounds;
                _textStartX = Math.Ceiling(Math.Max(0, -drawBounds.X)) + LeadingPadding;
                _textWidth = Math.Ceiling(Math.Max(layout.LayoutBounds.Width, drawBounds.Width + Math.Max(0, -drawBounds.X)) + LeadingPadding + 2);
            }
        }

        private void RestartAnimation()
        {
            _phase = MarqueePhase.StartHold;
            _phaseStartedAt = DateTimeOffset.Now;
            _offset = 0;

            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }

            TextCanvas?.Invalidate();
        }

        private void StopAnimation()
        {
            if (_animationTimer.IsEnabled)
            {
                _animationTimer.Stop();
            }

            _offset = 0;
            _phase = MarqueePhase.StartHold;
            _phaseStartedAt = DateTimeOffset.Now;
            TextCanvas?.Invalidate();
        }

        private void TextCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (sender == null || ActualWidth <= 0 || ActualHeight <= 0 || string.IsNullOrWhiteSpace(Text))
            {
                return;
            }

            using (var format = CreateTextFormat())
            using (var layout = new CanvasTextLayout(sender, Text ?? string.Empty, format, float.MaxValue, (float)Math.Max(ActualHeight, TextFontSize * 1.6)))
            {
                var drawingSession = args.DrawingSession;
                float width = (float)ActualWidth;
                float height = (float)ActualHeight;
                float offset = (float)(_offset + _textStartX);
                var foreground = ResolveForegroundColor();

                using (drawingSession.CreateLayer(1f, new Rect(0, 0, width, height)))
                {
                    drawingSession.DrawTextLayout(layout, offset, 0, foreground);
                }
            }
        }

        private CanvasTextFormat CreateTextFormat()
        {
            return new CanvasTextFormat
            {
                FontSize = (float)TextFontSize,
                FontWeight = TextFontWeight,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };
        }

        private Color ResolveForegroundColor()
        {
            if (TextForeground is SolidColorBrush solidColorBrush)
            {
                return solidColorBrush.Color;
            }

            return Colors.White;
        }

        private enum MarqueePhase
        {
            StartHold,
            MovingForward,
            EndHold
        }
    }
}
