#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ControlCatalog.Pages;

public class PointersPage : Decorator
{
    public PointersPage()
    {
        Child = new TabControl
        {
            Items = new[]
            {
                new TabItem() { Header = "Contacts", Content = new PointerContactsTab() },
                new TabItem() { Header = "IntermediatePoints", Content = new PointerIntermediatePointsTab() },
                new TabItem() { Header = "Pressure", Content = new PointerPressureTab() }
            }
        };
    }
    
    
    class PointerContactsTab : Control
    {
        class PointerInfo
        {
            public Point Point { get; set; }
            public Color Color { get; set; }
        }

        private static Color[] AllColors = new[]
        {
            Colors.Aqua,
            Colors.Beige, 
            Colors.Chartreuse, 
            Colors.Coral,
            Colors.Fuchsia,
            Colors.Crimson,
            Colors.Lavender, 
            Colors.Orange,
            Colors.Orchid,
            Colors.ForestGreen,
            Colors.SteelBlue,
            Colors.PapayaWhip,
            Colors.PaleVioletRed,
            Colors.Goldenrod,
            Colors.Maroon,
            Colors.Moccasin,
            Colors.Navy,
            Colors.Wheat,
            Colors.Violet,
            Colors.Sienna,
            Colors.Indigo,
            Colors.Honeydew
        };
        
        private Dictionary<IPointer, PointerInfo> _pointers = new Dictionary<IPointer, PointerInfo>();

        public PointerContactsTab()
        {
            ClipToBounds = true;
        }
        
        void UpdatePointer(PointerEventArgs e)
        {
            if (!_pointers.TryGetValue(e.Pointer, out var info))
            {
                if (e.RoutedEvent == PointerMovedEvent)
                    return;
                var colors = AllColors.Except(_pointers.Values.Select(c => c.Color)).ToArray();
                var color = colors[new Random().Next(0, colors.Length - 1)];
                _pointers[e.Pointer] = info = new PointerInfo {Color = color};
            }

            info.Point = e.GetPosition(this);
            InvalidateVisual();
        }
        
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            UpdatePointer(e);
            e.Pointer.Capture(this);
            e.Handled = true;
            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            UpdatePointer(e);
            e.Handled = true;
            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _pointers.Remove(e.Pointer);
            e.Handled = true;
            InvalidateVisual();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            _pointers.Remove(e.Pointer);
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.Transparent, new Rect(default, Bounds.Size));
            foreach (var pt in _pointers.Values)
            {
                var brush = new ImmutableSolidColorBrush(pt.Color);

                context.DrawEllipse(brush, null, pt.Point, 75, 75);
            }
        }
    }

    public class PointerIntermediatePointsTab : Decorator
    {
        public PointerIntermediatePointsTab()
        {
            this[TextElement.ForegroundProperty] = Brushes.Black;
            var slider = new Slider
            {
                Margin = new Thickness(5),
                Minimum = 0,
                Maximum = 500
            };

            var status = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Child = new Grid
            {
                Children =
                {
                    new PointerCanvas(slider, status, true),
                    new Border
                    {
                        Background = Brushes.LightYellow,
                        Child = new StackPanel
                        {
                            Children =
                            {
                                new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Children =
                                    {
                                        new TextBlock { Text = "Thread sleep:" },
                                        new TextBlock()
                                        {
                                            [!TextBlock.TextProperty] =slider.GetObservable(Slider.ValueProperty)
                                                .Select(x=>x.ToString()).ToBinding()
                                        }
                                    }
                                },
                                slider
                            }
                        },

                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Width = 300,
                        Height = 60
                    },
                    status
                }
            };
        }
    }

    public class PointerPressureTab : Decorator
    {
        public PointerPressureTab()
        {
            this[TextBlock.ForegroundProperty] = Brushes.Black;

            var status = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                FontSize = 12
            };
            Child = new Grid
            {
                Children =
                {
                    new PointerCanvas(null, status, false),
                    status
                }
            };
        }
    }

    class PointerCanvas : Control
    {
        private readonly Slider? _slider;
        private readonly TextBlock _status;
        private readonly bool _drawPoints;
        private int _events;
        private Stopwatch _stopwatch = Stopwatch.StartNew();
        private IDisposable? _statusUpdated;
        private Dictionary<int, PointerPoints> _pointers = new();
        private PointerPointProperties? _lastProperties;
        class PointerPoints
        {
            struct CanvasPoint
            {
                public IBrush Brush;
                public Point Point;
                public double Radius;
                public double Pressure;
            }

            readonly CanvasPoint[] _points = new CanvasPoint[1000];
            int _index;

            public void Render(DrawingContext context, bool drawPoints)
            {

                CanvasPoint? prev = null;
                for (var c = 0; c < _points.Length; c++)
                {
                    var i = (c + _index) % _points.Length;
                    var pt = _points[i];
                    var thickness = pt.Pressure == 0 ? 1 : (pt.Pressure / 1024) * 5;

                    if (drawPoints)
                    {
                        if (prev.HasValue && prev.Value.Brush != null && pt.Brush != null)
                            context.DrawLine(new Pen(Brushes.Black, thickness), prev.Value.Point, pt.Point);
                        if (pt.Brush != null)
                            context.DrawEllipse(pt.Brush, null, pt.Point, pt.Radius, pt.Radius);
                    }
                    else
                    {
                        if (prev.HasValue && prev.Value.Brush != null && pt.Brush != null)
                            context.DrawLine(new Pen(Brushes.Black, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), prev.Value.Point, pt.Point);
                    }
                    prev = pt;
                }

            }

            void AddPoint(Point pt, IBrush brush, double radius, float pressure)
            {
                _points[_index] = new CanvasPoint { Point = pt, Brush = brush, Radius = radius, Pressure = pressure };
                _index = (_index + 1) % _points.Length;
            }

            public void HandleEvent(PointerEventArgs e, Visual v)
            {
                e.Handled = true;
                var currentPoint = e.GetCurrentPoint(v);
                if (e.RoutedEvent == PointerPressedEvent)
                    AddPoint(currentPoint.Position, Brushes.Green, 10, currentPoint.Properties.Pressure);
                else if (e.RoutedEvent == PointerReleasedEvent)
                    AddPoint(currentPoint.Position, Brushes.Red, 10, currentPoint.Properties.Pressure);
                else
                {
                    var pts = e.GetIntermediatePoints(v);
                    for (var c = 0; c < pts.Count; c++)
                    {
                        var pt = pts[c];
                        AddPoint(pt.Position, c == pts.Count - 1 ? Brushes.Blue : Brushes.Black,
                            c == pts.Count - 1 ? 5 : 2, pt.Properties.Pressure);
                    }
                }
            }
        }

        public PointerCanvas(Slider? slider, TextBlock status, bool drawPoints)
        {
            _slider = slider;
            _status = status;
            _drawPoints = drawPoints;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _statusUpdated = DispatcherTimer.Run(() =>
            {
                if (_stopwatch.Elapsed.TotalSeconds > 1)
                {
                    _status.Text = $@"Events per second: {(_events / _stopwatch.Elapsed.TotalSeconds)}
PointerUpdateKind: {_lastProperties?.PointerUpdateKind}
IsLeftButtonPressed: {_lastProperties?.IsLeftButtonPressed}
IsRightButtonPressed: {_lastProperties?.IsRightButtonPressed}
IsMiddleButtonPressed: {_lastProperties?.IsMiddleButtonPressed}
IsXButton1Pressed: {_lastProperties?.IsXButton1Pressed}
IsXButton2Pressed: {_lastProperties?.IsXButton2Pressed}
IsBarrelButtonPressed: {_lastProperties?.IsBarrelButtonPressed}
IsEraser: {_lastProperties?.IsEraser}
IsInverted: {_lastProperties?.IsInverted}
Pressure: {_lastProperties?.Pressure}
XTilt: {_lastProperties?.XTilt}
YTilt: {_lastProperties?.YTilt}
Twist: {_lastProperties?.Twist}";
                    _stopwatch.Restart();
                    _events = 0;
                }

                return true;
            }, TimeSpan.FromMilliseconds(10));
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            _statusUpdated?.Dispose();
        }

        void HandleEvent(PointerEventArgs e)
        {
            _events++;
            if (_slider != null)
            {
                Thread.Sleep((int)_slider.Value);
            }
            InvalidateVisual();

            if (e.RoutedEvent == PointerReleasedEvent && e.Pointer.Type == PointerType.Touch)
            {
                _pointers.Remove(e.Pointer.Id);
                return;
            }
            
            var lastPointer = e.GetCurrentPoint(this);
            _lastProperties = lastPointer.Properties;

            if (e.Pointer.Type != PointerType.Pen
                || lastPointer.Properties.Pressure > 0)
            {
                if (!_pointers.TryGetValue(e.Pointer.Id, out var pt))
                    _pointers[e.Pointer.Id] = pt = new PointerPoints();
                pt.HandleEvent(e, this);
            }
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.White, Bounds);
            foreach (var pt in _pointers.Values)
                pt.Render(context, _drawPoints);
            base.Render(context);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _pointers.Clear();
                InvalidateVisual();
                return;
            }

            HandleEvent(e);
            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            HandleEvent(e);
            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            HandleEvent(e);
            base.OnPointerReleased(e);
        }
    }
}
