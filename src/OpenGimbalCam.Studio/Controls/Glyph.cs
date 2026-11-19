using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace OpenGimbalCam.Studio.Controls;

/// <summary>A 16x16 vector icon (Bootstrap Icons geometry) painted with the inherited Foreground.</summary>
public sealed class Glyph : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Glyph, Geometry?>(nameof(Data));

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
