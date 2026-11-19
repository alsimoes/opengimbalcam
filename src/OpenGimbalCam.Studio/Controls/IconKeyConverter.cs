using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OpenGimbalCam.Studio.Controls;

/// <summary>Resolves an icon resource key (from a ViewModel) to its geometry.</summary>
public sealed class IconKeyConverter : IValueConverter
{
    public static IconKeyConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key && Application.Current is { } app
        && app.TryGetResource(key, app.ActualThemeVariant, out var resource)
            ? resource
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
