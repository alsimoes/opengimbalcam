using System.Globalization;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Studio.ViewModels;

public enum ControlGroup
{
    Exposure,
    Color,
    Tone,
    Optics,
}

public sealed record ControlSpec(int Order, string Label, string IconKey, ControlGroup Group, string? Unit = null, IReadOnlyList<string>? Options = null);

/// <summary>pt-BR labels, icons and grouping of the standard UVC controls shown in the "Imagem" tab.</summary>
public static class ControlCatalog
{
    private static readonly Dictionary<CameraControl, ControlSpec> Specs = new()
    {
        [CameraControl.Exposure] = new(0, "Obturador", "IconStopwatch", ControlGroup.Exposure),
        [CameraControl.Gain] = new(1, "Ganho", "IconGraphUpArrow", ControlGroup.Exposure),
        [CameraControl.BacklightCompensation] = new(2, "Contraluz", "IconBrightnessAltHigh", ControlGroup.Exposure),
        [CameraControl.PowerlineFrequency] = new(3, "Anti-flicker", "IconLightbulb", ControlGroup.Exposure,
            Options: ["Desligado", "50 Hz", "60 Hz", "Auto"]),
        [CameraControl.Iris] = new(4, "Íris", "IconVignette", ControlGroup.Exposure),
        [CameraControl.WhiteBalance] = new(0, "Balanço de branco", "IconThermometerHalf", ControlGroup.Color, "K"),
        [CameraControl.Hue] = new(1, "Matiz", "IconPalette", ControlGroup.Color),
        [CameraControl.Saturation] = new(2, "Saturação", "IconDropletHalf", ControlGroup.Color),
        [CameraControl.Brightness] = new(0, "Brilho", "IconBrightnessHigh", ControlGroup.Tone),
        [CameraControl.Contrast] = new(1, "Contraste", "IconCircleHalf", ControlGroup.Tone),
        [CameraControl.Gamma] = new(2, "Gama", "IconGraphUp", ControlGroup.Tone),
        [CameraControl.Sharpness] = new(3, "Nitidez", "IconTriangleHalf", ControlGroup.Tone),
        [CameraControl.Focus] = new(0, "Foco", "IconCrosshair", ControlGroup.Optics),
        [CameraControl.Roll] = new(1, "Rotação", "IconArrowRepeat", ControlGroup.Optics),
    };

    public static ControlSpec Get(CameraControl control) =>
        Specs.TryGetValue(control, out var spec)
            ? spec
            : new ControlSpec(100 + (int)control, control.ToString(), "IconSliders", ControlGroup.Optics);

    public static string GroupTitle(ControlGroup group) => group switch
    {
        ControlGroup.Exposure => "EXPOSIÇÃO",
        ControlGroup.Color => "COR",
        ControlGroup.Tone => "TOM E DETALHE",
        _ => "ÓPTICA",
    };

    public static string FormatValue(CameraControl control, double value, ControlRange range)
    {
        int v = (int)Math.Round(value);
        if (control == CameraControl.Exposure)
            return FormatShutter(v, range);
        string text = v.ToString(CultureInfo.InvariantCulture);
        return Get(control).Unit is { } unit ? $"{text} {unit}" : text;
    }

    /// <summary>Same heuristic as ObsbotCamera.ExposureValue: DirectShow reports log2 seconds, V4L2 100 µs units.</summary>
    public static string FormatShutter(int value, ControlRange range)
    {
        double seconds = range.Min < 0 ? Math.Pow(2, value) : value / 10_000.0;
        if (seconds <= 0)
            return value.ToString(CultureInfo.InvariantCulture);
        return seconds < 1
            ? $"1/{Math.Round(1 / seconds).ToString(CultureInfo.InvariantCulture)} s"
            : $"{seconds.ToString("0.#", CultureInfo.InvariantCulture)} s";
    }
}
