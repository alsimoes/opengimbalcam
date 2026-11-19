namespace OpenGimbalCam.Studio.ViewModels;

/// <summary>pt-BR texts that originate in view models; kept together to prepare i18n (Epic 7).</summary>
internal static class UiText
{
    public const string NoCameraTitle = "Nenhuma câmera conectada";
    public const string NoCameraDetail = "Conecte a OBSBOT Tiny e clique em Atualizar (F5).";
    public const string PreviewOffTitle = "Vídeo desligado";
    public const string PreviewOffDetail = "Ligue o vídeo para ver a imagem, tirar fotos e gravar. Com o vídeo ligado a câmera fica acordada e o gimbal responde.";
    public const string PreviewOpening = "Abrindo vídeo…";
    public const string PreviewFailedTitle = "Sem vídeo";
    public const string PreviewWaiting = "Aguardando imagem…";
    public const string NoPosition = "—";
    public const string Connected = "Conectada";
    public const string UvcOnly = "Só UVC";
    public const string Disconnected = "Sem câmera";
    public const string OscStopped = "Parado";
    public const string OscOff = "OSC desligado";
    public const string OscStart = "Iniciar servidor";
    public const string OscStop = "Parar servidor";
    public const string Record = "Gravar";
    public const string StopRecord = "Parar";
    public const string ChooseTransport = "Escolha UDP e/ou TCP.";
    public const string OpenVideoForPhoto = "Abra o vídeo para tirar uma foto.";
    public const string OpenVideoForRecording = "Abra o vídeo para gravar.";
    public const string PhotoSaved = "Foto salva.";
    public const string PhotoSavedTitle = "Foto salva";
    public const string RecordingSavedTitle = "Gravação salva";
    public const string ErrorTitle = "Erro";
    public const string ImageRestored = "Ajustes de imagem restaurados ao padrão da câmera.";

    public static string NoCameraFound(string platform) => $"Nenhuma câmera encontrada ({platform}).";

    public static string OscStatus(int port) => $"OSC {port}";

    public static string OscState(string transports, int port) => $"Em execução · {transports} · porta {port}";

    public static string PortInUse(int port) => $"Porta {port} em uso. O OBSBOT Center está aberto com OSC ligado?";

    public static string CameraWithXu(string name, string location) => $"{name}: XU OBSBOT em {location}.";

    public static string CameraUvcOnly(string name) => $"{name}: sem a Extension Unit OBSBOT; só controles UVC padrão.";
}
