using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGimbalCam.Osc;

namespace OpenGimbalCam.Studio;

/// <summary>User preferences persisted between runs.</summary>
public sealed class StudioSettings
{
    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public bool WindowMaximized { get; set; }

    public string? CameraPath { get; set; }

    public bool PreviewEnabled { get; set; } = true;

    public double Speed { get; set; } = 50;

    public int OscPort { get; set; } = OscServer.DefaultPort;

    public bool OscUdp { get; set; } = true;

    public bool OscTcp { get; set; }

    public bool OscObsbotCompatibility { get; set; } = true;

    public int InspectorTab { get; set; }

    public bool ShowGrid { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StudioSettings))]
internal sealed partial class StudioSettingsJsonContext : JsonSerializerContext;

public sealed class StudioSettingsStore(string filePath)
{
    public static StudioSettingsStore Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenGimbalCam Studio", "settings.json"));

    public string FilePath { get; } = filePath;

    /// <summary>The saved settings, or the defaults when the file is missing or corrupted.</summary>
    public StudioSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), StudioSettingsJsonContext.Default.StudioSettings) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fall through to the defaults.
        }
        return new();
    }

    public void Save(StudioSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, StudioSettingsJsonContext.Default.StudioSettings));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preferences are best effort.
        }
    }
}
