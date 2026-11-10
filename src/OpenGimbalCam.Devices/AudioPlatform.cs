using OpenGimbalCam.Core.Audio;
using OpenGimbalCam.Devices.Windows;

namespace OpenGimbalCam.Devices;

public static class AudioPlatform
{
    /// <summary>
    /// The OBSBOT Tiny microphone, or null when it is not found or the platform has no
    /// implementation yet (Linux: Phase 2). <paramref name="reason"/> explains a null (pt-BR).
    /// </summary>
    public static IAudioCapture? CreateTinyCapture(out string? reason)
    {
        reason = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var capture = WasapiTinyCapture.TryCreate();
                if (capture is null)
                    reason = "microfone da OBSBOT Tiny não encontrado";
                return capture;
            }
            catch (Exception ex)
            {
                reason = $"falha ao abrir o microfone da OBSBOT Tiny ({ex.Message})";
                return null;
            }
        }
        reason = "captura de áudio ainda não implementada nesta plataforma";
        return null;
    }
}
