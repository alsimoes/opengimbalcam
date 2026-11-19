namespace OpenGimbalCam.Studio.ViewModels;

public sealed class AboutViewModel
{
    public string Title => "OpenGimbalCam Studio";

    public string Version { get; } = "Versão " + AppInfo.Version;

    public string License => "Licença MIT";

    public string RepositoryUrl => "https://github.com/alsimoes/opengimbalcam";

    public string Scope => "Projeto independente e de código aberto; suporta apenas a OBSBOT Tiny original.";

    public string Trademark =>
        "Este projeto não é afiliado, autorizado, patrocinado nem endossado pela fabricante das câmeras OBSBOT "
        + "nem por qualquer outro titular das marcas citadas. OBSBOT, OBSBOT Tiny e demais nomes de produtos "
        + "são marcas de seus respectivos titulares, usadas apenas de forma nominativa. Veja TRADEMARKS.md.";

    public string ThirdParty =>
        "Componentes de terceiros: Avalonia, FlashCap, NAudio, CommunityToolkit.Mvvm, Bootstrap Icons, Inter e JetBrains Mono; "
        + "veja THIRD-PARTY-NOTICES.md.";
}
