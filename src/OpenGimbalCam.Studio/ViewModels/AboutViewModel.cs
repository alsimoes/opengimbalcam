using System.Reflection;

namespace OpenGimbalCam.Studio.ViewModels;

public sealed class AboutViewModel
{
    public string Title => "OpenGimbalCam Studio";

    public string Version { get; } = "Versão " + (typeof(AboutViewModel).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.9.0");

    public string Scope => "Projeto independente e de código aberto; suporta apenas a OBSBOT Tiny original.";

    public string License => "Licença MIT. Veja o arquivo LICENSE.";

    public string Trademark =>
        "Este projeto não é afiliado, autorizado, patrocinado nem endossado pela fabricante das câmeras OBSBOT "
        + "nem por qualquer outro titular das marcas citadas. OBSBOT, OBSBOT Tiny e demais nomes de produtos "
        + "são marcas de seus respectivos titulares, usadas apenas de forma nominativa. Veja TRADEMARKS.md.";
}
