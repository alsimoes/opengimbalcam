using System.Reflection;

namespace OpenGimbalCam.Studio.ViewModels;

public sealed record ControlGroupViewModel(string Title, IReadOnlyList<ControlViewModel> Items);

public sealed record LogEntry(string Time, string Text, bool IsError);

public enum PreviewState
{
    Off,
    Opening,
    Live,
    Failed,
}

public enum StatusKind
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record StudioNotice(string Title, string Message, StatusKind Kind, string? FilePath = null);

public static class AppInfo
{
    /// <summary>Informational version without the "+commit" suffix.</summary>
    public static string Version { get; } = typeof(AppInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.9.0";
}
