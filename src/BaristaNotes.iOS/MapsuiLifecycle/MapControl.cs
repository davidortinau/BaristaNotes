// Adapted from Mapsui 5.1.0 Mapsui.UI.iOS/MapControl.cs; see LICENSE.
using CoreFoundation;
using Mapsui.Extensions;
using Mapsui.Manipulations;
using Mapsui.UI;
using SkiaSharp.Views.iOS;
using System.ComponentModel;

namespace BaristaNotes.Native.iOS.MapsuiLifecycle;

[Register("BaristaNotesLifecycleMapControl"), DesignTimeVisible(true)]
public partial class MapControl : UIView, IMapControl
{
    private SKMetalView? _metalCanvas;
    private SKCanvasView? _canvas;
    private bool _canvasInitialized;
    private readonly ManipulationTracker _manipulationTracker = new();
    private readonly object _lifetimeGate = new();
    private volatile bool _disposed;
    public static bool UseGPU { get; set; } = true;

    public MapControl(CGRect frame)
        : base(frame)
    {
        LocalConstructor();
        SharedConstructor();
    }

    [Preserve]
    public MapControl(IntPtr handle) : base(handle)
    {
        LocalConstructor();
        SharedConstructor();
    }

    public void InvalidateCanvas()
    {
        if (_disposed) return;
        RunOnUIThread(() =>
        {
            // The render loop can already have queued this callback when disposal begins.
            lock (_lifetimeGate)
            {
                if (_disposed || Handle == IntPtr.Zero) return;
                SetNeedsDisplay();
                if (_metalCanvas is { } metalCanvas && metalCanvas.Handle != IntPtr.Zero)
                    metalCanvas.SetNeedsDisplay();
            }
        });
    }

    private void InitializeCanvas()
    {
        if (!_canvasInitialized)
        {
            _canvasInitialized = true;
            if (UseGPU)
            {
                _metalCanvas?.Dispose();
                _metalCanvas = [];
            }
            else
            {
                _canvas?.Dispose();
                _canvas = [];
            }
        }
    }

    private void LocalConstructor()
    {
        InitializeCanvas();
        BackgroundColor = UIColor.White;

        if (UseGPU)
        {
            _metalCanvas!.TranslatesAutoresizingMaskIntoConstraints = false;
            _metalCanvas.MultipleTouchEnabled = true;
            _metalCanvas.PaintSurface += OnPaintSurface;
            AddSubview(_metalCanvas);

            AddConstraints(
            [
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, _metalCanvas,
                    NSLayoutAttribute.Leading, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, _metalCanvas,
                    NSLayoutAttribute.Trailing, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Top, NSLayoutRelation.Equal, _metalCanvas,
                    NSLayoutAttribute.Top, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, _metalCanvas,
                    NSLayoutAttribute.Bottom, 1.0f, 0.0f)
            ]);
        }
        else
        {
            _canvas!.TranslatesAutoresizingMaskIntoConstraints = false;
            _canvas.MultipleTouchEnabled = true;
            _canvas.PaintSurface += OnPaintSurface;
            AddSubview(_canvas);

            AddConstraints(
            [
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Leading, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Trailing, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Top, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Top, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Bottom, 1.0f, 0.0f)
            ]);
        }

        ClipsToBounds = true;
        MultipleTouchEnabled = true;
        UserInteractionEnabled = true;
        SharedOnSizeChanged(GetWidth(), GetHeight());
    }

    private void OnPaintSurface(object? sender, SKPaintMetalSurfaceEventArgs args)
    {
        if (_disposed || Handle == IntPtr.Zero) return;
        Map.RenderService.GpuContext = args.Surface.Context;
        _renderController?.Render(args.Surface.Canvas, GetPixelDensity());
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        if (_disposed || Handle == IntPtr.Zero) return;
        _renderController?.Render(args.Surface.Canvas, GetPixelDensity());
    }

    public override void TouchesBegan(NSSet touches, UIEvent? e)
    {
        if (_disposed) return;
        Catch.Exceptions(() =>
        {
            base.TouchesBegan(touches, e);
            var positions = GetScreenPositions(e, this);

            if (positions.Length == 1)
                _manipulationTracker.Restart(positions);

            if (OnPointerPressed(positions))
                return;
        });
    }

    public override void TouchesMoved(NSSet touches, UIEvent? e)
    {
        if (_disposed) return;
        Catch.Exceptions(() =>
        {
            base.TouchesMoved(touches, e);
            var positions = GetScreenPositions(e, this);

            if (OnPointerMoved(positions, false))
                return;

            _manipulationTracker.Manipulate(positions, Map.Navigator.Manipulate);
        });
    }

    public override void TouchesEnded(NSSet touches, UIEvent? e)
    {
        if (_disposed) return;
        Catch.Exceptions(() =>
        {
            base.TouchesEnded(touches, e);
            var positions = GetScreenPositions(e, this);
            OnPointerReleased(positions);
        });
    }

    private static ReadOnlySpan<ScreenPosition> GetScreenPositions(UIEvent? uiEvent, UIView uiView)
    {
        if (uiEvent is null)
            return [];
        return uiEvent?.AllTouches?.Select(t => ((UITouch)t).LocationInView(uiView))
            .Select(p => new ScreenPosition(p.X, p.Y)).ToArray() ?? [];
    }

    private static void RunOnUIThread(Action action) => DispatchQueue.MainQueue.DispatchAsync(action);

    public override CGRect Frame
    {
        get => base.Frame;
        set
        {
            if (_disposed) return;
            InitializeCanvas();
            if (UseGPU)
                _metalCanvas!.Frame = value;
            else
                _canvas!.Frame = value;

            base.Frame = value;
            SharedOnSizeChanged(GetWidth(), GetHeight());
            OnPropertyChanged();
        }
    }

    public override void LayoutMarginsDidChange()
    {
        if (_disposed) return;
        InitializeCanvas();
        if (_metalCanvas == null || _canvas == null) return;

        base.LayoutMarginsDidChange();
        SharedOnSizeChanged(GetWidth(), GetHeight());
    }

    public void OpenInBrowser(string url) => OpenUrlInBrowser(url);

    private static void OpenUrlInBrowser(string url)
    {
        Catch.TaskRun(async () =>
        {
            using var nsUrl = new NSUrl(url);
            await UIApplication.SharedApplication.OpenUrlAsync(nsUrl, new UIApplicationOpenUrlOptions());
        });
    }

    protected override void Dispose(bool disposing)
    {
        lock (_lifetimeGate)
        {
            if (_disposed) return;
            _disposed = true;

            // Stop the producer and detach the map before releasing either native view.
            SharedDispose(disposing);
            if (disposing)
            {
                if (_metalCanvas != null)
                {
                    _metalCanvas.PaintSurface -= OnPaintSurface;
                    _metalCanvas.Dispose();
                    _metalCanvas = null;
                }
                if (_canvas != null)
                {
                    _canvas.PaintSurface -= OnPaintSurface;
                    _canvas.Dispose();
                    _canvas = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    private double GetWidth()
    {
        InitializeCanvas();
        return UseGPU ? _metalCanvas!.Frame.Width : _canvas!.Frame.Width;
    }

    private double GetHeight()
    {
        InitializeCanvas();
        return UseGPU ? _metalCanvas!.Frame.Height : _canvas!.Frame.Height;
    }

    public float? GetPixelDensity()
    {
        if (_disposed) return null;
        InitializeCanvas();
        return UseGPU ? (float)_metalCanvas!.ContentScaleFactor : (float)_canvas!.ContentScaleFactor;
    }

    private static bool GetShiftPressed() => false;
}
