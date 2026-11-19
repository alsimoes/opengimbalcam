# Avisos de componentes de terceiros

O OpenGimbalCam (licença MIT, veja [LICENSE](LICENSE)) distribui, dentro de `opengimbalcam-studio.exe` e `ogcctl.exe`, os componentes abaixo. Cada um mantém a licença e os direitos de seus titulares. Os textos completos estão na pasta [`licenses/`](licenses/) e acompanham o zip e o instalador.

Levantamento feito em 2026-11-19 a partir do `dotnet publish -r win-x64 --self-contained` de cada executável e dos arquivos `.nuspec` de cada pacote. Refaça-o quando mudar uma dependência.

## Runtime do .NET

| Componente | Versão | Licença | Copyright |
|---|---|---|---|
| .NET Runtime (autocontido, `win-x64`) | 10.0.15 | MIT | (c) .NET Foundation and Contributors |

Textos: [MIT](licenses/MIT-dotnet-runtime.txt) e os avisos de terceiros embutidos no runtime, em [dotnet-runtime-avisos.txt](licenses/dotnet-runtime-avisos.txt).

## Pacotes NuGet de runtime

Presentes no Studio (S) e na CLI (C).

| Pacote | Versão | Em | Licença | Copyright | Texto |
|---|---|---|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Win32, Avalonia.Skia, Avalonia.Themes.Fluent, Avalonia.Remote.Protocol, Avalonia.Native, Avalonia.X11, Avalonia.FreeDesktop | 11.3.22 | S | MIT | The AvaloniaUI Project | [MIT-avalonia.txt](licenses/MIT-avalonia.txt) |
| Avalonia.Fonts.Inter (fonte Inter; veja abaixo) | 11.3.22 | S | MIT (pacote) e OFL 1.1 (fonte) | The AvaloniaUI Project | [MIT-avalonia.txt](licenses/MIT-avalonia.txt) |
| Avalonia.BuildServices | 11.3.2 | S | MIT | The AvaloniaUI Project | [MIT-avalonia.txt](licenses/MIT-avalonia.txt) |
| Avalonia.Angle.Windows.Natives (ANGLE) | 2.1.25547.20250602 | S | BSD de 3 cláusulas | The ANGLE Project Authors | [BSD-3-Clause-angle.txt](licenses/BSD-3-Clause-angle.txt) |
| SkiaSharp, SkiaSharp.NativeAssets.Win32 | 2.88.9 | S | MIT | Xamarin, Inc.; Microsoft Corporation | [MIT-skiasharp-harfbuzzsharp.txt](licenses/MIT-skiasharp-harfbuzzsharp.txt) |
| HarfBuzzSharp, HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.1 | S | MIT | Xamarin, Inc.; Microsoft Corporation | [MIT-skiasharp-harfbuzzsharp.txt](licenses/MIT-skiasharp-harfbuzzsharp.txt) |
| Skia e HarfBuzz nativos (`libSkiaSharp.dll`, `libHarfBuzzSharp.dll`) | — | S | vários (Skia BSD, HarfBuzz MIT, libjpeg-turbo, libpng, zlib, FreeType, ICU, entre outros) | os titulares de cada um | [SkiaSharp-HarfBuzzSharp-nativos-avisos.txt](licenses/SkiaSharp-HarfBuzzSharp-nativos-avisos.txt) |
| MicroCom.Runtime | 0.11.0 | S | MIT | Nikita Tsukanov | [MIT-microcom.txt](licenses/MIT-microcom.txt) |
| Tmds.DBus.Protocol | 0.21.3 | S | MIT | Alp Toker; Tom Deseyn | [MIT-tmds-dbus.txt](licenses/MIT-tmds-dbus.txt) |
| CommunityToolkit.Mvvm | 8.4.2 | S | MIT | .NET Foundation and Contributors | [MIT-communitytoolkit-mvvm.txt](licenses/MIT-communitytoolkit-mvvm.txt) |
| FlashCap, FlashCap.Core | 1.13.0 | S | Apache-2.0 | Kouji Matsui | [Apache-2.0.txt](licenses/Apache-2.0.txt) |
| NAudio.Wasapi, NAudio.Core ([ADR-0011](.harness/decisions/ADR-0011-audio-na-gravacao.md)) | 2.2.1 | S e C | MIT | Mark Heath | [MIT-naudio.txt](licenses/MIT-naudio.txt) |

O Tmds.DBus.Protocol e o Avalonia.X11/FreeDesktop/Native só são usados em Linux e macOS, mas as DLLs gerenciadas vão no pacote do Windows.

## Recursos incluídos no Studio

| Recurso | Licença | Copyright | Texto |
|---|---|---|---|
| Inter | SIL Open Font License 1.1 | The Inter Project Authors | [OFL-Inter.txt](licenses/OFL-Inter.txt) |
| JetBrains Mono | SIL Open Font License 1.1 | The JetBrains Mono Project Authors | [OFL-JetBrainsMono.txt](licenses/OFL-JetBrainsMono.txt) |
| Bootstrap Icons (geometria dos ícones e base do ícone do app) | MIT | The Bootstrap Authors | [MIT-bootstrap-icons.txt](licenses/MIT-bootstrap-icons.txt) |

As fontes e os ícones são embutidos no executável; nada é baixado em tempo de execução ([ADR-0012](.harness/decisions/ADR-0012-identidade-visual-do-studio.md)).

## Observações

* Marcas citadas neste projeto pertencem a seus titulares; veja [TRADEMARKS.md](TRADEMARKS.md).
* Ferramentas de build e de teste (xunit, Microsoft.NET.Test.Sdk, Avalonia.Headless, Inno Setup) não são distribuídas dentro dos executáveis e não constam aqui. O Inno Setup gera o instalador sem acrescentar código próprio sob licença que exija aviso.
