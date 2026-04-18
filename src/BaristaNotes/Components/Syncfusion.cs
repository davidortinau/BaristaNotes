using Syncfusion.Maui.Gauges;

namespace BaristaNotes.Components;

// ========================================
// SfRadialGauge - Main container for radial gauge
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.SfRadialGauge))]
public partial class SfRadialGauge : IEnumerable<VisualNode>
{
    private readonly List<VisualNode> _children = new();

    public new IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(VisualNode child) => _children.Add(child);

    public SfRadialGauge WithAxis(RadialAxis axis)
    {
        _children.Add(axis);
        return this;
    }

    protected override IEnumerable<VisualNode> RenderChildren()
    {
        return _children;
    }

    protected override void OnAddChild(VisualNode widget, BindableObject childControl)
    {
        if (childControl is Syncfusion.Maui.Gauges.RadialAxis axis && NativeControl != null)
        {
            NativeControl.Axes.Add(axis);
        }
        base.OnAddChild(widget, childControl);
    }

    protected override void OnRemoveChild(VisualNode widget, BindableObject childControl)
    {
        if (childControl is Syncfusion.Maui.Gauges.RadialAxis axis && NativeControl != null)
        {
            NativeControl.Axes.Remove(axis);
        }
        base.OnRemoveChild(widget, childControl);
    }
}

// ========================================
// RadialAxis - Contains scale, pointers, ranges, and annotations
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.RadialAxis))]
public partial class RadialAxis : IEnumerable<VisualNode>
{
    private readonly List<VisualNode> _children = new();

    public new IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(VisualNode child) => _children.Add(child);

    public RadialAxis WithPointers(params VisualNode[] pointers)
    {
        foreach (var pointer in pointers)
        {
            _children.Add(pointer);
        }
        return this;
    }

    public RadialAxis WithAnnotation(VisualNode annotation)
    {
        _children.Add(annotation);
        return this;
    }

    public RadialAxis WithRange(VisualNode range)
    {
        _children.Add(range);
        return this;
    }

    protected override IEnumerable<VisualNode> RenderChildren()
    {
        return _children;
    }

    protected override void OnAddChild(VisualNode widget, BindableObject childControl)
    {
        if (NativeControl == null)
        {
            base.OnAddChild(widget, childControl);
            return;
        }

        if (childControl is Syncfusion.Maui.Gauges.RadialPointer pointer)
        {
            NativeControl.Pointers.Add(pointer);
        }
        else if (childControl is Syncfusion.Maui.Gauges.RadialRange range)
        {
            NativeControl.Ranges.Add(range);
        }
        else if (childControl is Syncfusion.Maui.Gauges.GaugeAnnotation annotation)
        {
            NativeControl.Annotations.Add(annotation);
        }

        base.OnAddChild(widget, childControl);
    }

    protected override void OnRemoveChild(VisualNode widget, BindableObject childControl)
    {
        if (NativeControl == null)
        {
            base.OnRemoveChild(widget, childControl);
            return;
        }

        if (childControl is Syncfusion.Maui.Gauges.RadialPointer pointer)
        {
            NativeControl.Pointers.Remove(pointer);
        }
        else if (childControl is Syncfusion.Maui.Gauges.RadialRange range)
        {
            NativeControl.Ranges.Remove(range);
        }
        else if (childControl is Syncfusion.Maui.Gauges.GaugeAnnotation annotation)
        {
            NativeControl.Annotations.Remove(annotation);
        }

        base.OnRemoveChild(widget, childControl);
    }
}

// ========================================
// Base Pointer Classes (required for inheritance chain)
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.RadialPointer))]
public partial class RadialPointer { }

[Scaffold(typeof(Syncfusion.Maui.Gauges.MarkerPointer))]
public partial class MarkerPointer { }

// ========================================
// Concrete Pointer Types
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.ShapePointer))]
public partial class ShapePointer { }

[Scaffold(typeof(Syncfusion.Maui.Gauges.NeedlePointer))]
public partial class NeedlePointer { }

[Scaffold(typeof(Syncfusion.Maui.Gauges.RangePointer))]
public partial class RangePointer { }

[Scaffold(typeof(Syncfusion.Maui.Gauges.ContentPointer))]
public partial class ContentPointer { }

// ========================================
// Ranges
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.RadialRange))]
public partial class RadialRange { }

// ========================================
// Annotations - with content support
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.GaugeAnnotation))]
public partial class GaugeAnnotation
{
    private View? _contentView;

    public GaugeAnnotation ContentView(View view)
    {
        _contentView = view;
        return this;
    }

    protected override void OnMount()
    {
        base.OnMount();
        if (_contentView != null && NativeControl != null)
        {
            NativeControl.Content = _contentView;
        }
    }

    protected override void OnAddChild(VisualNode widget, BindableObject childControl)
    {
        if (childControl is View view && NativeControl != null)
        {
            NativeControl.Content = view;
        }

        base.OnAddChild(widget, childControl);
    }

    protected override void OnRemoveChild(VisualNode widget, BindableObject childControl)
    {
        if (NativeControl != null)
        {
            NativeControl.Content = null!;
        }
        base.OnRemoveChild(widget, childControl);
    }
}

// ========================================
// Line Styles
// ========================================
[Scaffold(typeof(Syncfusion.Maui.Gauges.RadialLineStyle))]
public partial class RadialLineStyle { }