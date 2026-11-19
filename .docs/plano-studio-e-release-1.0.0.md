# Plano: Studio com interface profissional e release 1.0.0 do Windows

* **Status:** aprovado pelo mantenedor em 2026-11-19. Executado pelos PRs da tabela "Sequência e pacotes de trabalho" e acompanhado no [Épico 2](../.harness/epics/EPIC-2-windows-1.0.0.md).
* **Decisões registradas em:** [ADR-0012](../.harness/decisions/ADR-0012-identidade-visual-do-studio.md), [ADR-0013](../.harness/decisions/ADR-0013-distribuicao-no-windows.md) e [ADR-0014](../.harness/decisions/ADR-0014-licenca-mit.md). Em caso de conflito, valem o Harness e os ADRs aceitos.
* As marcas citadas pertencem a seus titulares e são usadas de forma nominativa; veja [TRADEMARKS.md](../TRADEMARKS.md).

## Contexto

O `opengimbalcam-studio` (Avalonia 11.3.22, `src/OpenGimbalCam.Studio`) funciona, mas a interface ainda é um protótipo:

* três colunas de mesmo peso; o vídeo tem altura fixa de 320 px (≈520×292 efetivos) e divide a coluna central com a lista de controles UVC;
* os controles de imagem mostram o nome cru do enum em inglês (`WhiteBalance`, `PowerlineFrequency`), cada um numa linha larga; o anti-flicker aparece como slider 0–2;
* emojis e símbolos Unicode no lugar de ícones (▲ ◀ ⌂ 📷 ● ■);
* não há atalhos, estados vazios, indicador de gravação, nem ícone próprio (a janela usa o logo do Avalonia); o log geral fica preso ao painel OSC.

O mantenedor pediu aparência, usabilidade e experiência de nível comercial: ícones Bootstrap, fontes do Google, controles de imagem compactos e vídeo maior. Também pediu que o plano feche a **release 1.0.0 do Windows**, com empacotamento e publicação no GitHub. O Linux fica para a release seguinte.

Tudo pertence ao **Épico 2** (em andamento). O plano também cumpre a tarefa pendente "[T2] Botão para restaurar os controles de imagem", a correção do processo que continua vivo após fechar a janela e os critérios da 1.0.0 que dependem de agentes.

### Decisões do mantenedor (2026-11-19, na conversa)

| Tema | Decisão |
|---|---|
| Layout | vídeo no centro, deck de movimento abaixo dele, inspetor com abas à direita |
| Fontes | **Inter** no texto e **JetBrains Mono** em números, timer e log (Google Fonts, OFL 1.1) |
| Licença | **MIT**; conclui a HT-2.2 |
| Distribuição | GitHub Releases com **zip portátil + instalador** |
| Linux | release seguinte (Épico 4) |

### Decisões aprovadas com o plano (2026-11-19)

* **Tema escuro fixo**, padrão em software de vídeo porque não altera a percepção da imagem. O tema claro fica para depois.
* **Cor de destaque violeta `#6C5CE7`**: texto branco sobre ela tem contraste de 4,8:1. Não lembra nenhuma identidade do fabricante (`ip-policy.md` §4).
* **Bootstrap Icons 1.13.2** (MIT), embutidos como geometria XAML gerada por script. Nada é baixado em tempo de execução.
* **Instalador Inno Setup 6.7**, por usuário e sem admin. Os executáveis são `win-x64` autocontidos de arquivo único. **Sem assinatura de código na 1.0.0**: o aviso do SmartScreen fica documentado, e o certificado vira tarefa HT futura.
* **Avalonia continua na 11.3.22** até a 1.0.0. A migração para a 12.x fica para depois da release, o que fecha a tarefa "[T3, decisão HT] Avaliar a migração".
* **Encerramento**: o descarte passa a ser assíncrono e ganha um `Environment.Exit` de segurança.

## Regras para quem implementa

* Leia `.harness/governance.md` e `.harness/tiers.md`. Código e commits em inglês (Conventional Commits); documentação e textos da UI em pt-BR. Trabalhe em branch com PR para `master`, com os nomes da tabela abaixo.
* `TreatWarningsAsErrors` está ativo: build e testes precisam passar **sem avisos**.
* **Agentes não movem o gimbal nem escrevem na câmera.** Toda verificação em hardware é do HT (HT-2.3). Abrir o Studio com a Tiny conectada liga o vídeo, e com vídeo o gimbal fica ativo e pode se mover. Por isso agentes só executam o app com a câmera desconectada. Não altere código de protocolo ou dispositivo além do que está aqui.
* Nada imita a identidade visual do OBSBOT Center. Documento público que cita marca leva o aviso ou o link para `TRADEMARKS.md`.
* Definition of Done de cada PR: build e testes sem avisos, documentação atualizada, matriz de validação atualizada quando algo muda de estado e registro no Épico 2 do que foi feito e do que ficou pendente.
* Textos que nascem no ViewModel (status, estados do palco, avisos) ficam como constantes em `ViewModels/UiText.cs`, preparando o Épico 7 (i18n). Os rótulos dos controles ficam no `ControlCatalog`; os textos do XAML ficam no XAML.
* Relatórios de agente: até 300 palavras, com a lista de arquivos alterados e o resultado de build e testes.

## Sequência e pacotes de trabalho

```
PR1 docs (T3) ─► HT aceita ADRs ─► PR2 interface (T2) ─► revisão T3 ─► PR3 empacotamento (T2) ─► revisão T3
Épico 6 (HT, busca de marcas) ── em paralelo, já ─────────────────────────────┐
                    HT-2.3 (validação com câmera + pacote da CI) ◄────────────┘
                    PR4 release 1.0.0 ─► tag v1.0.0 (autorizada) ─► rascunho ─► HT-2.4 publica ─► fecha Épico 2
```

| PR | Branch | Tier | Conteúdo |
|---|---|---|---|
| PR1 | `docs/release-1.0.0-decisions` | T3 | ADR-0012, ADR-0013, ADR-0014, `LICENSE`, Épico 2 (tarefas, HT-2.3, HT-2.4), roadmap (seção B1) |
| PR2 | `feature/studio-pro-ui` | T2 + revisão T3 | toda a Parte A, incluindo a correção de encerramento e o projeto de testes da UI |
| PR3 | `feature/windows-packaging` | T2 + revisão T3 | Parte B, seções B3 a B7 |
| PR4 | `release/1.0.0` | T1/T2 + revisão T3 | versão 1.0.0, notas finais, matriz, resultado do Épico 2 (B9) |

---

# Parte A: Interface

## A1. Fundação visual

### A1.1 Tokens (`src/OpenGimbalCam.Studio/Styles/Tokens.axaml`, `ResourceDictionary`)

Declare cada cor como `SolidColorBrush` com a chave abaixo e use sempre `{DynamicResource …}`.

| Chave | Valor | Uso |
|---|---|---|
| `AppBg` | `#0D0F12` | fundo da janela |
| `PanelBg` | `#15181D` | barra superior, painéis, barra de status |
| `RaisedBg` | `#1D2128` | cards, campos, botões padrão |
| `HoverBg` / `PressedBg` | `#262B33` / `#2E343E` | estados de botão |
| `BorderSubtle` / `BorderStrong` | `#262B33` / `#39404B` | bordas, divisores |
| `TextPrimary` / `TextSecondary` / `TextMuted` | `#E8EAED` / `#B4BAC3` / `#8A919C` | textos (muted ≥ 4,9:1 sobre `RaisedBg`) |
| `Accent` / `AccentHover` / `AccentPressed` | `#6C5CE7` / `#7D6FF0` / `#5B4BD5` | destaque |
| `OnAccent` | `#FFFFFF` | texto sobre o destaque |
| `AccentSubtle` / `AccentText` | `#2A2550` / `#A99FF7` | fundo de item ativo; texto de destaque sobre escuro |
| `Danger` / `DangerFill` / `DangerHover` / `DangerPressed` / `DangerText` | `#EF4444` / `#DC2626` / `#E53E3E` / `#B91C1C` / `#F87171` | gravação, erro |
| `Success` / `Warning` | `#22C55E` / `#F59E0B` | pontos de status |
| `VideoBg` / `OverlayBg` | `#000000` / `#B3000000` | palco do vídeo, chips sobre o vídeo |
| `AccentGradient` | `LinearGradientBrush` 0%,0% → 100%,100%: `#7D6FF0` → `#4B3CC4` | marca do app |

Inclua também estas sobrescritas do tema Fluent. As chaves foram conferidas na DLL do Fluent 11.3.22; o tipo de cada uma deve ser o mesmo do arquivo original:

```xml
<FontFamily x:Key="ContentControlThemeFontFamily">fonts:Inter#Inter, $Default</FontFamily>
<FontFamily x:Key="MonoFont">avares://opengimbalcam-studio/Assets/Fonts#JetBrains Mono</FontFamily>
<x:Double x:Key="ControlContentThemeFontSize">13</x:Double>
<CornerRadius x:Key="ControlCornerRadius">6</CornerRadius>
<CornerRadius x:Key="OverlayCornerRadius">10</CornerRadius>
<x:Double x:Key="SliderHorizontalThumbWidth">14</x:Double>
<x:Double x:Key="SliderHorizontalThumbHeight">14</x:Double>
<CornerRadius x:Key="SliderThumbCornerRadius">7</CornerRadius>
<x:Double x:Key="SliderHorizontalHeight">24</x:Double>
<GridLength x:Key="SliderPreContentMargin">5</GridLength>   <!-- (24 − 14) / 2 -->
<GridLength x:Key="SliderPostContentMargin">5</GridLength>  <!-- sempre igual ao Pre -->
<x:Double x:Key="SliderTrackThemeHeight">3</x:Double>     <!-- confirmar o tipo -->
<x:Double x:Key="TabItemMinHeight">36</x:Double>
<SolidColorBrush x:Key="NotificationCardBackground" Color="#1D2128"/>
```

No template horizontal do Slider do Fluent, a altura é `SliderPreContentMargin` + polegar + `SliderPostContentMargin` (o padrão dá 15 + 20 + 15). Com os valores acima, o slider fica com 24 px e o polegar centralizado; Pre e Post precisam ser iguais. Confira no snapshot.

O destaque (`SystemAccentColor`) vem da paleta de A1.4. Sliders, toggles, checkboxes e a barra da aba selecionada já saem em violeta sem outras chaves. O cabeçalho das abas é ajustado por estilo (A1.5), não pela chave `TabItemHeaderFontSize`.

Escala de layout: espaços de 4, 8, 12 e 16 px; raios de 6 (controles), 8 (cards) e 10 (painéis); fontes de 11 (legenda e log), 12 (rótulos), 13 (corpo), 14 (títulos) e 15 (marca e leituras).

### A1.2 Fontes

* **Inter**: já vem no pacote `Avalonia.Fonts.Inter`, com os pesos Thin, Light, Regular, Medium, SemiBold e Bold, e é registrada por `.WithInterFont()` em `Program.cs`. Ela é a família do Google Fonts; não acrescente arquivos.
* **JetBrains Mono**: crie `tools/ui/Get-GoogleFonts.ps1`. A API CSS do Google Fonts entrega TrueType estático quando o agente de usuário é o do curl (verificado):

```powershell
param([string]$Family = 'JetBrains Mono', [int[]]$Weights = @(400, 500, 600),
      [string]$OutDir = "$PSScriptRoot/../../src/OpenGimbalCam.Studio/Assets/Fonts")
$ErrorActionPreference = 'Stop'
$q = $Family -replace ' ', '+'
$css = (Invoke-WebRequest -UseBasicParsing -UserAgent 'curl/8.0' "https://fonts.googleapis.com/css2?family=${q}:wght@$($Weights -join ';')").Content
$faces = [regex]::Matches($css, 'font-weight:\s*(\d+);\s*src:\s*url\(([^)]+)\)\s*format\(''truetype''\)')
if ($faces.Count -ne $Weights.Count) { throw "expected $($Weights.Count) TrueType faces, got $($faces.Count)" }
$names = @{ 400 = 'Regular'; 500 = 'Medium'; 600 = 'SemiBold'; 700 = 'Bold' }
New-Item -ItemType Directory -Force $OutDir | Out-Null
foreach ($m in $faces) {
  $file = Join-Path $OutDir ('{0}-{1}.ttf' -f ($Family -replace ' ', ''), $names[[int]$m.Groups[1].Value])
  Invoke-WebRequest -UseBasicParsing $m.Groups[2].Value -OutFile $file
}
```

  Versione os três `.ttf`. Baixe também os textos da licença OFL para a pasta `licenses/` na raiz do repositório: `https://raw.githubusercontent.com/google/fonts/main/ofl/jetbrainsmono/OFL.txt` vira `licenses/OFL-JetBrainsMono.txt`, e `…/ofl/inter/OFL.txt` vira `licenses/OFL-Inter.txt`.
* No csproj do Studio: `<AvaloniaResource Include="Assets\**" />`. A pasta `Assets/` guarda só fontes e `app.ico`; os `.axaml` ficam em `Styles/`, para não serem incluídos duas vezes.

### A1.3 Ícones (Bootstrap Icons 1.13.2)

**Gerador** `tools/ui/gen_icons.py`: só biblioteca padrão; execute com `python -I tools/ui/gen_icons.py`. Ele gera `src/OpenGimbalCam.Studio/Styles/Icons.axaml`. Requisitos:

1. Baixa `https://cdn.jsdelivr.net/npm/bootstrap-icons@1.13.2/icons/{nome}.svg` para cada item do dicionário `ICONS` (chave do recurso para nome do ícone).
2. Exige `viewBox="0 0 16 16"` e só elementos `<path>`, sem `transform` nem `clip-path`. Qualquer outra coisa aborta com erro e o nome do ícone.
3. **Normaliza** o atributo `d`: reescreve cada grupo de parâmetros com separadores explícitos, porque o Bootstrap usa números colados (`2.914.5.5`) e as flags de arco podem vir coladas. Tokens numéricos: `[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?`. Aridade: M/L/T=2, H/V=1, C=6, S/Q=4, A=7, Z=0. No comando A/a, os parâmetros 4 e 5 são um único caractere `0` ou `1`. Grupos repetidos reemitem a letra do comando; depois de M/m, a repetição vira L/l.
4. Regra de preenchimento por `<path>`: `F0` (EvenOdd) quando `fill-rule="evenodd"`, `F1` (NonZero) quando não.
5. **Vários `<path>` no mesmo ícone** (é o caso de 19 dos ícones da tabela, alguns misturando regras) são unidos com `CombinedGeometry GeometryCombineMode="Union"`, aninhada à esquerda, porque concatenar mudaria o resultado. Com um único path, gera `<StreamGeometry x:Key="…">F1 M…</StreamGeometry>`. Com vários:

```xml
<CombinedGeometry x:Key="IconZoomIn" GeometryCombineMode="Union">
  <CombinedGeometry.Geometry1>
    <CombinedGeometry GeometryCombineMode="Union">
      <CombinedGeometry.Geometry1><StreamGeometry>F0 M…</StreamGeometry></CombinedGeometry.Geometry1>
      <CombinedGeometry.Geometry2><StreamGeometry>F0 M…</StreamGeometry></CombinedGeometry.Geometry2>
    </CombinedGeometry>
  </CombinedGeometry.Geometry1>
  <CombinedGeometry.Geometry2><StreamGeometry>F1 M…</StreamGeometry></CombinedGeometry.Geometry2>
</CombinedGeometry>
```

6. O cabeçalho do arquivo gerado leva o comentário "Generated by tools/ui/gen_icons.py from Bootstrap Icons v1.13.2 (MIT License, Copyright (c) 2019-2024 The Bootstrap Authors). Do not edit by hand." Cada entrada é precedida pelo comentário `<!-- bi-nome -->`.

**Ícones** (todos existem na 1.13.2; conferido em 2026-11-19):

| Chave | `bi-` | Uso |
|---|---|---|
| IconCameraVideo / IconCameraVideoFill / IconCameraVideoOff | camera-video / camera-video-fill / camera-video-off | seletor e marca / ícone do app / estado vazio |
| IconArrowClockwise | arrow-clockwise | atualizar câmeras |
| IconInfoCircle / IconKeyboard | info-circle / keyboard | Sobre / atalhos |
| IconBroadcast / IconActivity / IconSliders | broadcast / activity / sliders | OSC / aba Atividade / aba Imagem |
| IconChevronUp/Down/Left/Right | chevron-up/down/left/right | direcional do gimbal |
| IconBullseye / IconArrowsMove / IconSpeedometer | bullseye / arrows-move / speedometer2 | centralizar / seção Gimbal / velocidade |
| IconZoomIn / IconZoomOut / IconAspectRatio | zoom-in / zoom-out / aspect-ratio | zoom / campo de visão |
| IconSymmetryVertical / IconBookmarkStar | symmetry-vertical / bookmark-star | espelhar / presets |
| IconCamera / IconRecordFill / IconStopFill / IconPlayFill | camera / record-fill / stop-fill / play-fill | foto / gravar / parar / iniciar servidor |
| IconGrid3x3 / IconFolder2Open / IconFullscreen / IconFullscreenExit | grid-3x3 / folder2-open / fullscreen / fullscreen-exit | grade / pastas / modo foco |
| IconArrowCounterclockwise | arrow-counterclockwise | restaurar padrão |
| IconStopwatch / IconGraphUpArrow / IconBrightnessAltHigh / IconLightbulb / IconVignette | stopwatch / graph-up-arrow / brightness-alt-high / lightbulb / vignette | obturador / ganho / contraluz / anti-flicker / íris |
| IconThermometerHalf / IconPalette / IconDropletHalf | thermometer-half / palette / droplet-half | balanço de branco / matiz / saturação |
| IconBrightnessHigh / IconCircleHalf / IconGraphUp / IconTriangleHalf | brightness-high / circle-half / graph-up / triangle-half | brilho / contraste / gama / nitidez |
| IconCrosshair / IconArrowRepeat | crosshair / arrow-repeat | foco / rotação |
| IconClipboard / IconTrash | clipboard / trash | copiar / limpar o log |
| IconCheckCircleFill / IconExclamationTriangleFill | check-circle-fill / exclamation-triangle-fill | status OK / erro |

**Controle `Glyph`** (`Controls/Glyph.cs`): `public sealed class Glyph : TemplatedControl` com `StyledProperty<Geometry?> DataProperty`. O tema dele fica em `Styles/ControlThemes.axaml`. A tela 16×16 preserva o viewBox do Bootstrap, para que todos os ícones fiquem do mesmo tamanho ótico; o `PathIcon` esticaria cada geometria pelos próprios limites. O `Foreground` é herdado, então o ícone segue a cor do botão.

```xml
<ControlTheme x:Key="{x:Type c:Glyph}" TargetType="c:Glyph">
  <Setter Property="Width" Value="16"/>
  <Setter Property="Height" Value="16"/>
  <Setter Property="Template">
    <ControlTemplate>
      <Viewbox Stretch="Uniform">
        <Canvas Width="16" Height="16">
          <Path Data="{TemplateBinding Data}" Fill="{TemplateBinding Foreground}"/>
        </Canvas>
      </Viewbox>
    </ControlTemplate>
  </Setter>
</ControlTheme>
```

**`Controls/IconKeyConverter.cs`**: um `IValueConverter` com `static Instance`. Ele recebe a chave (string) e devolve `Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var r) ? r : null`. É usado onde a chave vem do ViewModel.

### A1.4 `App.axaml`

```xml
<Application … RequestedThemeVariant="Dark">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://opengimbalcam-studio/Styles/Tokens.axaml"/>
        <ResourceInclude Source="avares://opengimbalcam-studio/Styles/Icons.axaml"/>
        <ResourceInclude Source="avares://opengimbalcam-studio/Styles/ControlThemes.axaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
  <Application.Styles>
    <FluentTheme>
      <FluentTheme.Palettes>
        <ColorPaletteResources x:Key="Dark" Accent="#FF6C5CE7" RegionColor="#FF0D0F12" ErrorText="#FFF87171"/>
      </FluentTheme.Palettes>
    </FluentTheme>
    <StyleInclude Source="avares://opengimbalcam-studio/Styles/Controls.axaml"/>
  </Application.Styles>
</Application>
```

### A1.5 Estilos (`Styles/Controls.axaml`, raiz `<Styles>`; precisa vir depois do `FluentTheme`)

Os estados do Fluent ficam no `ContentPresenter#PART_ContentPresenter` do template. Toda classe de botão precisa, portanto, de seletores próprios para `:pointerover` e `:pressed`. Em `ToggleButton`, cubra também `:checked`, `:checked:pointerover` e `:checked:pressed`; em `ListBoxItem`, `:selected`, `:selected:pointerover` e `:selected:pressed`. O Fluent define todos esses estados, e o que faltar volta à cor dele. Padrão:

```xml
<Style Selector="Button.primary">
  <Setter Property="Background" Value="{DynamicResource Accent}"/>
  <Setter Property="Foreground" Value="{DynamicResource OnAccent}"/>
  <Setter Property="BorderThickness" Value="0"/>
</Style>
<Style Selector="Button.primary:pointerover /template/ ContentPresenter#PART_ContentPresenter">
  <Setter Property="Background" Value="{DynamicResource AccentHover}"/>
  <Setter Property="Foreground" Value="{DynamicResource OnAccent}"/>
</Style>
<Style Selector="Button.primary:pressed /template/ ContentPresenter#PART_ContentPresenter">
  <Setter Property="Background" Value="{DynamicResource AccentPressed}"/>
  <Setter Property="Foreground" Value="{DynamicResource OnAccent}"/>
</Style>
```

| Seletor | Definição |
|---|---|
| `Button` (base) | `RaisedBg`, borda `BorderSubtle` 1, raio 6, padding 12,6, peso Medium; hover `HoverBg` com `BorderStrong`; pressionado `PressedBg` |
| `Button.primary` / `Button.danger` | `Accent` e `DangerFill`, nesta ordem no arquivo, para `danger` vencer quando as duas classes estão presentes |
| `Button.ghost`, `ToggleButton.ghost` | fundo transparente, sem borda; hover `HoverBg`; `:checked` com `AccentSubtle` e `AccentText` |
| `Button.icon`, `ToggleButton.icon` | 32×32, padding 0, conteúdo centralizado, transparente; hover `HoverBg`; `ToggleButton.icon:checked` com `AccentSubtle` e `AccentText`. `.mini` = 22×22 |
| `Grid.pad > Button` | 36×36, margem 2, raio 8, centralizado; `Button.home` usa `AccentSubtle` e `AccentText` |
| `Button.preset` | 36×36, `MonoFont` SemiBold |
| `Button.shutter` | 44×44, raio 22, `RaisedBg`, borda `BorderStrong` 2 |
| `Button.record` | altura 40, raio 20, padding 16,0, transparente, borda `Danger` 2; `Glyph` interno em `Danger`. `Button.record.recording`: fundo `DangerFill` e tudo em branco (hover `DangerHover`) |
| `ToggleButton.chip` | 24×20, fonte 10 Bold, raio 4, `RaisedBg`; `:checked` com `Accent` e `OnAccent` |
| `ListBox.segmented` | `RaisedBg`, borda 1, raio 8, padding 2, `ItemsPanel` = `UniformGrid Rows="1"`, sem rolagem horizontal; `ListBoxItem` com padding 8,4, MinHeight 28, raio 6, conteúdo centralizado e fonte 12; `:selected` (e `:selected:pointerover`) no `PART_ContentPresenter` com `Accent` e `OnAccent` |
| `ListBox.log` | transparente; itens com padding 4,2 e MinHeight 0 |
| `TabItem` | FontSize 13, FontWeight SemiBold, Padding 12,0; o `Glyph` do cabeçalho com Margin 0,0,6,0 |
| `Border.appbar` / `.statusbar` | `PanelBg`, borda inferior ou superior de 1 em `BorderSubtle` |
| `Border.panel` / `.card` | `PanelBg` com borda 1 e raio 10 / `RaisedBg` com raio 8 e padding 12 |
| `Border.stage` | `VideoBg`, raio 10,10,0,0, `Focusable`; `:focus-visible` com borda `Accent` 2 |
| `Border.transport` | borda superior 1, padding 12,8 |
| `Border.vdivider` | largura 1, `BorderSubtle`, margem 14,2 |
| `Border.chip` / `.recBadge` / `.pill` | `OverlayBg` com raio 6 e padding 8,4 / igual, com borda `Danger` 1 / `RaisedBg` com raio 999 e padding 10,4 |
| `Border.brandmark` | 28×28, raio 7, `AccentGradient`, `Glyph` branco |
| `Ellipse.dot` (`.ok`, `.warn`) | 8×8, `TextMuted`; `Success` / `Warning` |
| `Ellipse.recDot` | 9×9, `Danger`, animação de opacidade infinita 1 → 0,2 → 1 em 1,2 s (`Style.Animations` com `KeyFrame` 0%, 50% e 100%) |
| `TextBlock.brand` / `.brandSub` / `.title` / `.caption` | 15 SemiBold / 15 `TextSecondary` / 14 SemiBold / 11 SemiBold `TextMuted` |
| `TextBlock.muted` / `.mono` / `.value` / `.readout` / `.error` | 12 `TextMuted` / `MonoFont` 12 / mono 12 `TextSecondary` à direita com MinWidth 48 / mono 15 SemiBold à direita / `DangerText` |
| `c|Glyph.muted` | `TextMuted` |

### A1.6 Ícone do aplicativo

`tools/ui/make-icon.cs` é um app de arquivo único do .NET 10 (`dotnet run tools/ui/make-icon.cs`, a partir da raiz) com a diretiva `#:package SkiaSharp@2.88.9`. Ele gera `src/OpenGimbalCam.Studio/Assets/app.ico` (versionado):

* quadrado arredondado (raio = 22% do lado) com gradiente linear `#7D6FF0` → `#4B3CC4` e, por cima, o glyph `camera-video-fill` em branco: `SKPath.ParseSvgPathData` com o `d` da 1.13.2, `FillType = EvenOdd`, escala de 62% do lado, centralizado;
* tamanhos 16, 20, 24, 32, 40, 48, 64, 128 e 256, cada um como PNG dentro do ICO. Cabeçalho `ICONDIR` (0, 1, n) e uma `ICONDIRENTRY` de 16 bytes por imagem (largura e altura 0 para 256; planos 1; bpp 32; tamanho; deslocamento). Passe o `d` como parâmetro da função de render; nada de capturar constante local. Se o ISCC ou o Explorer recusarem as entradas PNG pequenas, grave 16 a 48 como DIB de 32 bits (BITMAPINFOHEADER com altura dobrada, BGRA de baixo para cima, máscara AND zerada) e mantenha PNG de 64 em diante.

Use o ícone no csproj do Studio (`<ApplicationIcon>Assets\app.ico</ApplicationIcon>`), no csproj da CLI (`..\OpenGimbalCam.Studio\Assets\app.ico`) e em `Icon="avares://opengimbalcam-studio/Assets/app.ico"` na `MainWindow` e na `AboutWindow`.

## A2. ViewModels

### A2.1 `ViewModels/ControlCatalog.cs` (novo, público)

`ControlGroup { Exposure, Color, Tone, Optics }`; `record ControlSpec(int Order, string Label, string IconKey, ControlGroup Group, string? Unit = null, IReadOnlyList<string>? Options = null)`.

| Controle | Rótulo | Ícone | Grupo | Extra |
|---|---|---|---|---|
| Exposure | Obturador | IconStopwatch | Exposure | valor exibido como tempo de obturador |
| Gain | Ganho | IconGraphUpArrow | Exposure | |
| BacklightCompensation | Contraluz | IconBrightnessAltHigh | Exposure | |
| PowerlineFrequency | Anti-flicker | IconLightbulb | Exposure | opções `Desligado`, `50 Hz`, `60 Hz`, `Auto` |
| Iris | Íris | IconVignette | Exposure | |
| WhiteBalance | Balanço de branco | IconThermometerHalf | Color | unidade `K` |
| Hue / Saturation | Matiz / Saturação | IconPalette / IconDropletHalf | Color | |
| Brightness / Contrast / Gamma / Sharpness | Brilho / Contraste / Gama / Nitidez | IconBrightnessHigh / IconCircleHalf / IconGraphUp / IconTriangleHalf | Tone | |
| Focus / Roll | Foco / Rotação | IconCrosshair / IconArrowRepeat | Optics | |
| outros | `ToString()` | IconSliders | Optics | |

Títulos dos grupos: `EXPOSIÇÃO`, `COR`, `TOM E DETALHE`, `ÓPTICA`. `FormatValue(control, value, range)`:

* Exposure: `FormatShutter`, com a mesma heurística de `ObsbotCamera.ExposureValue`. Se `range.Min < 0` (DirectShow), segundos = 2^valor; senão (V4L2), segundos = valor/10000. Abaixo de 1 s, mostra `1/{round(1/s)} s`; a partir daí, `{s:0.#} s`; segundos ≤ 0, o número cru.
* Unidade `K`: `{valor} K`. Demais controles: o inteiro em `InvariantCulture`.

### A2.2 `ControlViewModel` (alterar)

* Campos novos: `_range`, `_spec`; `Default = Math.Clamp(range.Default, Min, Max)`.
* `Value` notifica `DisplayValue`, `IsModified`, `SelectedOption` e `ToggleValue`; `Auto` notifica `DisplayValue` e `IsModified`.
* Novas propriedades: `Label`, `IconKey`, `Group`, `Order`, `Default`.
  * `Options`: o recorte `Skip(Min).Take(Max-Min+1)` das opções do catálogo, só quando `Min >= 0 && Max < Options.Count`; senão `null`.
  * `IsOptions`; `IsToggle = !IsOptions && Max - Min == 1`; `IsSlider = !IsOptions && !IsToggle`.
  * `SelectedOption`: `get` devolve `round(Value) - Min`; `set` ignora valores menores que 0 e faz `Value = Min + v`.
  * `ToggleValue`: `get` devolve `Value > Min`; `set` faz `Value = v ? Max : Min`.
  * `DisplayValue`: `"Auto"` se `Auto`; o rótulo da opção se `IsOptions`; `Ligado`/`Desligado` se `IsToggle`; senão `ControlCatalog.FormatValue`.
  * `IsModified = HasAuto ? !Auto : round(Value) != Default`, o mesmo critério do `Reset` (quem tem automático volta ao automático).
* `[RelayCommand] Reset()`: `Apply(Default, HasAuto ? ControlFlags.Auto : ControlFlags.Manual); Reload();`. "Padrão da câmera" = valor padrão e automático onde houver; o HT confirma o efeito.
* Remova `Name` e `ValueText`; o XAML passa a usar `Label` e `DisplayValue`.

### A2.3 Tipos novos pequenos (`ViewModels/`)

`record ControlGroupViewModel(string Title, IReadOnlyList<ControlViewModel> Items)`, `record LogEntry(string Time, string Text, bool IsError)`, `enum PreviewState { Off, Opening, Live, Failed }`, `enum StatusKind { Info, Success, Warning, Error }`, `record StudioNotice(string Title, string Message, StatusKind Kind, string? FilePath = null)`, e `static class AppInfo { Version }`, com a lógica atual do `AboutViewModel` (informational version sem o sufixo `+…`).

### A2.4 `MainViewModel` (alterar; preservar `IPcMedia` e o comportamento OSC)

* **Construtor** `MainViewModel(ICameraPlatform platform, StudioSettings? settings = null)`. Ele aplica as configurações (`PreviewEnabled`, `Speed`, campos de OSC, `InspectorTabIndex`, `ShowGrid`, câmera preferida) **antes** de `Refresh()`. Em `Refresh`, a câmera preferida (`settings.CameraPath`) tem prioridade sobre a primeira Tiny.
* **Implementa `IAsyncDisposable` em vez de `IDisposable`.** `DisposeAsync`: `StopMove()`, `await StopPreviewAsync()`, `await StopOscAsync()`, descarta a câmera e os timers. Nada de `.GetAwaiter().GetResult()`.
* **Vídeo e palco**:
  * `[ObservableProperty] PreviewState`, atualizado em `StartPreviewAsync` (Opening, depois Live ou Failed) e `StopPreviewAsync` (Off); `PreviewError` guarda a mensagem da falha.
  * `HasFrame`: implemente `partial void OnFrameChanged(Bitmap? oldValue, Bitmap? newValue)` e só notifique quando a nulidade muda (o quadro troca 30 vezes por segundo).
  * `StageTitle` e `StageDetail`:
    * sem câmera: "Nenhuma câmera conectada" / "Conecte a OBSBOT Tiny e clique em Atualizar (F5).";
    * Off: "Vídeo desligado" / "Ligue o vídeo para ver a imagem, tirar fotos e gravar. Com o vídeo ligado a câmera fica acordada e o gimbal responde.";
    * Opening: "Abrindo vídeo…";
    * Failed: "Sem vídeo" / `PreviewError`;
    * Live sem quadro: "Aguardando imagem…".
  * `CanTurnOnPreview = HasCamera && PreviewState == Off`; `[RelayCommand] TurnOnPreview()` (liga `PreviewEnabled`); `IsPreviewLive = PreviewState == Live`; `ShowGimbalHint = HasXu && !IsPreviewLive` (o gimbal só responde com vídeo).
  * Notifique `StageTitle`, `StageDetail`, `CanTurnOnPreview`, `IsPreviewLive` e `ShowGimbalHint` quando `HasCamera`, `HasXu`, `PreviewState`, `PreviewError` ou `HasFrame` mudarem (`[NotifyPropertyChangedFor]`, ou `OnPropertyChanged` nos `partial void On…Changed`).
* **Gravação**: `RecordButtonText` passa a `Gravar`/`Parar`. `partial void OnIsRecordingChanged` liga e desliga um `DispatcherTimer` de 500 ms que atualiza `RecordingElapsed` (`static string FormatElapsed(TimeSpan)` → `hh:mm:ss`). Isso cobre gravação iniciada pelo botão, por F9 e por OSC.
* **Posição**: `PanText`/`TiltText` (`"12°"`, ou `"—"`) substituem `Position`. Opcional: um `DispatcherTimer` de 250 ms chama `UpdatePosition()` enquanto `_moving` (só leitura).
* **Conexão e OSC**:
  * `IsConnectedFull = HasCamera && HasXu`, `IsConnectedUvcOnly = HasCamera && !HasXu`, `ConnectionText` = `Conectada` / `Só UVC` / `Sem câmera`;
  * `OscStatusText` = `OSC {porta}` ou `OSC desligado`; `OscStateText` = `Em execução · UDP+TCP · porta N` ou `Parado`. Guarde a porta e os transportes no momento em que o servidor inicia;
  * `OscButtonText` passa a `Iniciar servidor`/`Parar servidor`.
* **Navegação**: `[ObservableProperty] int InspectorTabIndex` (0 Imagem, 1 OSC, 2 Atividade), `bool IsFocusMode`, `bool ShowGrid`. Comandos `ShowOsc`, `ShowActivity`, `ToggleFocusMode`, `ExitFocusMode`, `ToggleGrid`, `ZoomIn`/`ZoomOut` (±10, limitado a 0–100) e `ClearLog`.
* **Imagem**: `ObservableCollection<ControlGroupViewModel> ControlGroups`, montada em `OpenCamera` (agrupa `Controls` por `Group` e ordena por `Order`) e limpa em `CloseCamera`. `[RelayCommand] RestoreImageDefaults()` chama `Reset()` em todos e define o status de sucesso "Ajustes de imagem restaurados ao padrão da câmera."
* **Status**: `[ObservableProperty] StatusKind`, mais `IsStatusError`, `IsStatusSuccess` e `IsStatusInfo`; o helper `SetStatus(text, kind)`. `Report(ex)` usa Error.
* **Avisos**: `event EventHandler<StudioNotice>? Notice` para foto salva, gravação salva e erros (sem repetir o mesmo texto de erro em menos de 3 s); `event EventHandler? SnapshotTaken`.
* **Log**: `ObservableCollection<LogEntry> Log`, com `IsError = text.StartsWith("erro", OrdinalIgnoreCase)`.
* **Pastas**: `public string PhotosFolder` / `VideosFolder`, a partir de `MediaFolder`, agora público e estático.
* `AppVersionText => "v" + AppInfo.Version`, para a barra de status.
* `FovOptions = ["86°", "78°", "65°"]`.
* `public void CaptureSettings(StudioSettings s)` copia o estado persistível.

### A2.5 Persistência (`StudioSettings.cs`)

A classe `StudioSettings` tem: `WindowWidth?`, `WindowHeight?`, `WindowX?`, `WindowY?`, `WindowMaximized`, `CameraPath?`, `PreviewEnabled = true`, `Speed = 50`, `OscPort = OscServer.DefaultPort`, `OscUdp = true`, `OscTcp`, `OscObsbotCompatibility = true`, `InspectorTab`, `ShowGrid`.

Serialize com `System.Text.Json` e geração de código: `[JsonSourceGenerationOptions(WriteIndented = true)] [JsonSerializable(typeof(StudioSettings))] internal sealed partial class StudioSettingsJsonContext : JsonSerializerContext`.

`StudioSettingsStore(string filePath)`:

* `Default` aponta para `%APPDATA%\OpenGimbalCam Studio\settings.json` (`SpecialFolder.ApplicationData`, que também resolve no Linux).
* `Load()` devolve os padrões se o arquivo faltar ou estiver corrompido (captura `IOException`, `UnauthorizedAccessException` e `JsonException`).
* `Save()` grava num `.tmp` e faz `File.Move(…, overwrite: true)`, engolindo erros de E/S.

## A3. Janela principal (`MainWindow.axaml`, reescrita)

Janela: 1280×800, mínimo 1100×700, `WindowStartupLocation="CenterScreen"`, `Background="{DynamicResource AppBg}"`, título `OpenGimbalCam Studio`, ícone `app.ico`. Mantenha `x:DataType="vm:MainViewModel"` (bindings compilados) e os nomes `UpButton`, `DownButton`, `LeftButton` e `RightButton`. Todo `DataTemplate` declara `x:DataType`.

```
Grid RowDefinitions="56,*,28"
├─ Row 0  Border.appbar   Grid ColumnDefinitions="Auto,Auto,*,Auto" Margin="16,0"
│   ├ marca: Border.brandmark(Glyph IconCameraVideoFill) + TextBlock.brand "OpenGimbalCam" + TextBlock.brandSub "Studio"
│   ├ (Margin 28,0,0,0) ComboBox x:Name="CameraBox" MinWidth=300 MaxWidth=420, item = Glyph IconCameraVideo + Name + TextBlock.mono.muted UsbId,
│   │   PlaceholderText="Nenhuma câmera encontrada"; Button.icon IconArrowClockwise (RefreshCommand, "Atualizar a lista de câmeras (F5)");
│   │   Border.pill: Ellipse.dot (Classes.ok=IsConnectedFull, Classes.warn=IsConnectedUvcOnly) + ConnectionText, ToolTip=Status
│   └ direita: Button.ghost "pill" OSC (ShowOscCommand): Ellipse.dot(Classes.ok=OscRunning) + Glyph IconBroadcast + OscStatusText;
│       Button.icon IconKeyboard com Flyout da tabela de atalhos (A4); Button.icon IconInfoCircle (Click=OnAbout)
├─ Row 1  Grid Margin="12" ColumnDefinitions="*,Auto"
│   ├ Col 0  Grid RowDefinitions="*,Auto"
│   │   ├ Border.panel ClipToBounds  Grid RowDefinitions="*,Auto"
│   │   │   ├ Border.stage x:Name="Stage" Focusable DoubleTapped=OnStageDoubleTapped → Panel:
│   │   │   │   ├ Viewbox Stretch=Uniform IsVisible=HasFrame → Panel:
│   │   │   │   │    Image Source=Frame RenderOptions.BitmapInterpolationMode="HighQuality"
│   │   │   │   │    Grid (3×3 *) IsVisible=ShowGrid IsHitTestVisible=False: 4 linhas #66FFFFFF de 2 px (terços)
│   │   │   │   ├ estado vazio (IsVisible=!HasFrame), centralizado: Glyph 44 px muted (IconCameraVideoOff), TextBlock.title StageTitle,
│   │   │   │   │    TextBlock.muted StageDetail (MaxWidth 360, centralizado), Button.primary "Ligar vídeo" (IsVisible=CanTurnOnPreview)
│   │   │   │   ├ Border.chip topo-esquerda (12) IsVisible=HasFrame: TextBlock.mono PreviewInfo
│   │   │   │   ├ Border.recBadge topo-direita (12) IsVisible=IsRecording: Ellipse.recDot + "REC" + TextBlock.mono RecordingElapsed
│   │   │   │   └ Border x:Name="FlashOverlay" Background=White Opacity=0 IsHitTestVisible=False
│   │   │   └ Border.transport  Grid ColumnDefinitions="*,Auto,*"
│   │   │       ├ ToggleSwitch PreviewEnabled (OnContent "Vídeo ligado", OffContent "Vídeo desligado", IsEnabled=HasCamera)
│   │   │       ├ centro: Button.shutter IconCamera (SnapshotCommand, IsEnabled=IsPreviewLive, "Tirar foto (F8)");
│   │   │       │   Button.record Classes.recording=IsRecording (ToggleRecordingCommand, IsEnabled=IsPreviewLive, "Gravar ou parar (F9)"):
│   │   │       │   IconRecordFill (!IsRecording) / IconStopFill (IsRecording) + RecordButtonText + mono RecordingElapsed (IsRecording)
│   │   │       └ direita: ToggleButton.icon IconGrid3x3 (ShowGrid, "Grade de composição (Ctrl+G)");
│   │   │           Button.icon IconFolder2Open com MenuFlyout "Abrir pasta de fotos" / "Abrir pasta de vídeos";
│   │   │           ToggleButton.icon IsChecked=IsFocusMode (IconFullscreen / IconFullscreenExit, "Modo foco (F11)")
│   │   └ Border.panel (Margin 0,12,0,0) IsVisible=!IsFocusMode IsEnabled=HasCamera   ← DECK
│   │       Grid Margin="16,12" ColumnDefinitions="Auto,Auto,Auto,Auto,*,Auto,Auto"
│   │       ├ 0 GIMBAL (cabeçalho: Glyph IconArrowsMove + caption + Glyph 12 IconExclamationTriangleFill em Warning, IsVisible=ShowGimbalHint,
│   │       │    tooltip "O gimbal só responde com o vídeo ligado"): Grid.pad 3×3 de 36 px, IsEnabled=HasXu;
│   │       │    ↑ ← → ↓ com IconChevron* (tooltip "Segure para mover (setas do teclado)"), centro Button.home IconBullseye (HomeCommand, "Centralizar (Home)")
│   │       ├ 1 Border.vdivider
│   │       ├ 2 POSIÇÃO (Width 150): "PAN" caption + TextBlock.readout PanText; "TILT" + TiltText;
│   │       │    linha Glyph IconSpeedometer 14 + Slider 1–100 (Speed) + TextBlock.value "{Speed:0}%"
│   │       ├ 3 Border.vdivider
│   │       ├ 4 ZOOM E ENQUADRAMENTO (MinWidth 220): Button.icon IconZoomOut (ZoomOut, "Afastar (−)") + Slider 0–100 snap 1 (Zoom)
│   │       │    + Button.icon IconZoomIn ("Aproximar (+)") + TextBlock.value "{Zoom:0}%";
│   │       │    linha IsEnabled=HasXu: ListBox.segmented FovOptions/SelectedIndex=FovIndex (tooltip "Campo de visão: 86° amplo, 78° médio, 65° fechado")
│   │       │    + ToggleButton.ghost IconSymmetryVertical "Espelhar" (Mirror)
│   │       ├ 5 Border.vdivider
│   │       └ 6 PRESETS (Glyph IconBookmarkStar) IsEnabled=HasXu: Button.preset "1" "2" "3" (TriggerPresetCommand, tooltip "Ir para o preset N (XU seletor 7)")
│   └ Col 1  Border.panel Width=320 Margin="12,0,0,0" IsVisible=!IsFocusMode → TabControl SelectedIndex=InspectorTabIndex
│       ├ TabItem cabeçalho [Glyph 14 IconSliders + "Imagem"]: DockPanel Margin 16,12,16,16
│       │    topo: TextBlock.title "Ajustes de imagem" + Button.ghost [IconArrowCounterclockwise 14 + "Restaurar tudo"] (RestoreImageDefaultsCommand, IsEnabled=HasCamera)
│       │    TextBlock.muted "Conecte uma câmera para ajustar a imagem." (IsVisible=!HasCamera)
│       │    ScrollViewer → ItemsControl ControlGroups → TextBlock.caption Title + ItemsControl Items → LINHA COMPACTA (≈44 px):
│       │      Grid ColumnDefinitions="Auto,*,Auto,30,24" Height=22:
│       │        Glyph.muted 14 (IconKey via IconKeyConverter) | Label (Margin 8,0) | TextBlock.value DisplayValue |
│       │        ToggleButton.chip "A" (Auto, IsVisible=HasAuto, "Automático") | Button.icon.mini IconArrowCounterclockwise 12 (ResetCommand, IsVisible=IsModified, "Restaurar o padrão")
│       │      Slider (IsVisible=IsSlider, Min/Max/Step, snap, Value, IsEnabled=!Auto)
│       │      ListBox.segmented (IsVisible=IsOptions, Options, SelectedIndex=SelectedOption, IsEnabled=!Auto)
│       │      ToggleSwitch (IsVisible=IsToggle, ToggleValue, OnContent/OffContent vazios, IsEnabled=!Auto)
│       ├ TabItem [IconBroadcast + "OSC"]: ScrollViewer → StackPanel Margin 16,12 Spacing 12
│       │    título "Servidor OSC" + Border.pill (Ellipse.dot ok=OscRunning + OscStateText);
│       │    TextBlock.muted: "Controle a câmera pelo TouchOSC, Bitfocus Companion ou Stream Deck com o protocolo /OBSBOT/WebCam/* do OBSBOT Center. Feche o OBSBOT Center antes: os dois usam a mesma porta.";
│       │    Border.card (IsEnabled=!OscRunning): Porta (NumericUpDown 1–65535, FormatString 0, Width 140) e Transporte (CheckBox UDP, TCP);
│       │    Border.card: "Compatibilidade com o OBSBOT Center" + texto auxiliar + ToggleSwitch OscObsbotCompatibility;
│       │    Button.primary Classes.danger=OscRunning, largura total: IconPlayFill/IconStopFill + OscButtonText (ToggleOscCommand);
│       │    Button.ghost "Ver atividade" (ShowActivityCommand)
│       └ TabItem [IconActivity + "Atividade"]: DockPanel Margin 12: título + Button.icon IconClipboard (Click=OnCopyLog, "Copiar tudo")
│            + Button.icon IconTrash (ClearLogCommand, "Limpar"); ListBox.log x:Name="ActivityList" ItemsSource=Log,
│            item = Grid Auto,*: TextBlock.mono.muted Time | TextBlock.mono Classes.error=IsError Text (TextWrapping=Wrap)
└─ Row 2  Border.statusbar  Grid ColumnDefinitions="Auto,*,Auto" Margin="12,0"
    Glyph 14 por tipo (IconCheckCircleFill em Success, IconExclamationTriangleFill em DangerText, IconInfoCircle muted) | Status (TextTrimming) |
    TextBlock.mono.muted PreviewInfo · PlatformName · AppVersionText
```

Medidas de referência do vídeo 16:9: em 1280×800, cerca de 800×450 (2,4 vezes a área atual de 520×292); em 1100×700, cerca de 620×350; maximizada em 1920×1080, cerca de 1170×660; no modo foco em 1280×800, cerca de 1120×630. Na largura mínima o deck tem cerca de 710 px úteis e as colunas somam uns 700 px. Confira tudo nos snapshots (A6).

## A4. Interações (`MainWindow.axaml.cs`)

| Tecla | Ação | Implementação |
|---|---|---|
| ← ↑ → ↓ (segurar) | move o gimbal | `OnKeyDown`/`OnKeyUp` (abaixo) |
| Home | centralizar | `OnKeyDown` |
| `+` / `−` (OemPlus, Add / OemMinus, Subtract) | zoom ±10% | `OnKeyDown` |
| F5 / F8 / F9 | atualizar / foto / gravar ou parar | `Window.KeyBindings` |
| F11 / Esc | alternar / sair do modo foco | `Window.KeyBindings` |
| Ctrl+G | grade de composição | `Window.KeyBindings` |

* **Teclado do gimbal.** Guarde `Key? _keyMove`. Em `OnKeyDown`, retorne se `e.Handled`, se há modificadores ou se o foco (`FocusManager?.GetFocusedElement()`) é `TextBox`, `NumericUpDown`, `Slider`, `ComboBox`, `ListBox` ou `ListBoxItem`. Seta com `vm.HasXu`: se `_keyMove != e.Key`, chame `vm.StartMove(direção)` e guarde a tecla (a repetição automática não reenvia). Registre a soltura com `AddHandler(KeyUpEvent, OnArrowKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true)`, para nenhum controle engolir o KeyUp e deixar o gimbal girando; nele, se `e.Key == _keyMove`, limpe e chame `StopMove()`. No `Deactivated`, faça `_keyMove = null` e `StopMove()`.
* Mantenha o "segure para mover" dos botões (`handledEventsToo: true`). `Stage.Focus()` roda em `OnOpened` e em `CameraBox.DropDownClosed`, para as setas não trocarem de câmera. Clicar no vídeo também lhe dá o foco.
* **Avisos.** Em `OnOpened`: `new WindowNotificationManager(this) { Position = NotificationPosition.BottomRight, MaxItems = 3 }`; assine `Notice`, `SnapshotTaken` e `Log.CollectionChanged`. `Notice` vira `Notification(title, message, tipo)`; com `FilePath`, o clique abre o arquivo por `TopLevel.Launcher.LaunchFileInfoAsync(new FileInfo(path))`.
* **Flash da foto.** No `SnapshotTaken`, rode `new Animation { Duration = 220 ms, FillMode = Forward }` com `Opacity` 0,6 → 0 em `FlashOverlay` (`RunAsync`).
* **Log.** Ao adicionar uma linha, `ActivityList.ScrollIntoView(Log.Count - 1)` via `Dispatcher.UIThread.Post(…, Background)`. `OnCopyLog` copia `Time Text` por linha para `Clipboard.SetTextAsync`.
* **Pastas.** `OnOpenPhotos` / `OnOpenVideos`: `Directory.CreateDirectory` e depois `Launcher.LaunchDirectoryInfoAsync`.
* **Modo foco.** `OnStageDoubleTapped` executa `ToggleFocusModeCommand`.
* **Encerramento (corrige o bug do Épico 2).**
  * `protected override async void OnClosing(WindowClosingEventArgs e)`: chame `base.OnClosing(e)`; se `_closeReady`, retorne. Senão: `e.Cancel = true`, `IsEnabled = false`, captura as configurações (VM mais tamanho, posição e estado da janela) e `SettingsStore?.Save`, `await vm.DisposeAsync()`; no `finally`, `_closeReady = true; Close()`.
  * As propriedades `Settings` e `SettingsStore` usam `init`. `RestoreWindow(settings)` aplica tamanho e maximizado antes de mostrar a janela; a posição só vale se cair dentro de `Screens.All` e, nesse caso, use `WindowStartupLocation = Manual` e `Position = new PixelPoint(x, y)`.
  * `App.axaml.cs`: crie store, settings, VM e janela; remova o `ShutdownRequested` com `vm.Dispose()`.
  * `Program.Main`: `Environment.Exit(BuildAvaloniaApp().StartWithClassicDesktopLifetime(args));`, com um comentário explicando que threads de primeiro plano de terceiros (DirectShow/FlashCap) não podem manter o processo vivo depois do descarte ordenado.

## A5. Tela "Sobre"

Largura 520, `PanelBg`, ícone do app. Conteúdo:

* marca de 48 px + "OpenGimbalCam Studio";
* "Versão X · Licença MIT";
* o texto de escopo;
* o aviso de marcas num `Border.card`;
* "Componentes de terceiros: Avalonia, FlashCap, NAudio, CommunityToolkit.Mvvm, Bootstrap Icons, Inter e JetBrains Mono; veja THIRD-PARTY-NOTICES.md.";
* botões `Button.ghost` "Repositório no GitHub" (`Launcher.LaunchUriAsync`) e `Button.primary` "Fechar".

`AboutViewModel` ganha `License` e `RepositoryUrl` e passa a usar `AppInfo.Version`.

## A6. Testes e snapshots da UI (`tests/OpenGimbalCam.Studio.Tests`, novo, no `OpenGimbalCam.slnx`)

* O csproj segue o padrão do projeto de testes atual (xunit 2.9.3, Microsoft.NET.Test.Sdk 18.11.0, xunit.runner.visualstudio 4.0.1) e acrescenta **`Avalonia.Headless.XUnit` 11.3.22**, com `ProjectReference` ao Studio. No csproj do Studio: `<InternalsVisibleTo Include="OpenGimbalCam.Studio.Tests" />`.
* `TestAppBuilder`: `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` e a classe com `public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont();`. Se `UseSkia` não resolver, referencie `Avalonia.Skia` 11.3.22.
* O `App` só cria plataforma, configurações, VM e janela dentro do `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)` que já existe. O headless não tem esse lifetime, então nenhum teste toca a câmera real: os testes montam `MainViewModel` e `MainWindow` com os fakes.
* `Fakes.cs`:
  * `FakeCameraPlatform` (lista vazia, ou uma "OBSBOT Tiny (simulada)" com VID 3564 e PID FEF0);
  * `FakeSession`, com faixas realistas: Exposure −13..−2 com Auto, WhiteBalance 2800..6500 passo 100 com Auto, PowerlineFrequency 0..2, BacklightCompensation 0..1, Pan, Tilt, Zoom e Pan/TiltRelative, Brightness, Contrast, Hue, Saturation, Sharpness, Gamma e Gain. Os valores vivem num dicionário, e `Set` os altera;
  * `FakeXu`, que lê zeros e ignora escritas.
* Testes de lógica:
  * `ControlCatalog`: todo `CameraControl` fora do gimbal (Pan, Tilt, Zoom, PanRelative e TiltRelative, a lista no topo do `MainViewModel`) tem rótulo pt-BR; obturador −7 → `1/128 s`, −2 → `1/4 s`; V4L2 333 → `1/30 s`, 20000 → `2 s`;
  * `ControlViewModel`: Reset, IsModified, opções, toggle e `DisplayValue` "Auto";
  * `MainViewModel` (`[AvaloniaFact]`): `RestoreImageDefaults`, limites do zoom, `FormatElapsed` e captura e aplicação das configurações;
  * `StudioSettingsStore`: ida e volta num diretório temporário e arquivo corrompido → padrões.
* **Snapshots** (`[AvaloniaFact]`, `PreviewEnabled = false` nas configurações para não tocar no FlashCap). Cada um faz `window.Show()`, `Dispatcher.UIThread.RunJobs()` e `window.CaptureRenderedFrame()!.Save(…)` em `AppContext.BaseDirectory/ui-snapshots/`:
  * `main-empty.png` (sem câmera, 1280×800);
  * `main-live.png` (Tiny simulada, `vm.Frame` = `WriteableBitmap` 1280×720 com gradiente, `PreviewState = Live`, `IsRecording = true`);
  * `main-min.png` (o mesmo em 1100×700);
  * `main-focus.png`;
  * `main-osc.png` e `main-activity.png`;
  * `about.png`.

  Cada teste confirma que o quadro não é nulo e tem o tamanho esperado. **O implementador abre os PNGs (ferramenta Read) a cada iteração de layout**, e o revisor T3 também.

---

# Parte B: Fechamento da release 1.0.0 (Windows)

## B1. Decisões e Harness (PR1, T3)

* **ADR-0012 Identidade visual do Studio** (Proposta): tema escuro, cor de destaque, Inter e JetBrains Mono embutidas (OFL), Bootstrap Icons como geometria gerada (MIT), ícone derivado de `camera-video-fill`, nada baixado em tempo de execução, conformidade com `ip-policy.md` §4.
* **ADR-0013 Distribuição no Windows** (Proposta): GitHub Releases; zip portátil e instalador Inno Setup por usuário; executáveis autocontidos single-file `win-x64`; Windows 11 mínimo no instalador; `SHA256SUMS.txt`; tag `vX.Y.Z` cria um **rascunho** e o mantenedor publica; sem assinatura na 1.0.0.
* **ADR-0014 Licença MIT** (Proposta; decisão do mantenedor em 2026-11-19) e `LICENSE` com o texto MIT e a linha `Copyright (c) 2026 OpenGimbalCam contributors`, igual à do `Directory.Build.props` (B3). O mantenedor pode acrescentar o próprio nome no PR.
* **Épico 2**:
  * HT-2.2 marcada como concluída (MIT);
  * tarefas novas `[T2] Interface profissional do Studio`, `[T2] Empacotamento Windows e workflow de release`, `[HT] HT-2.3` e `[HT] HT-2.4`;
  * marcar como cobertas por este plano: "restaurar controles de imagem" e "processo continua vivo";
  * registrar Avalonia 12 como adiada para depois da 1.0.0;
  * escrever as guias HT-2.3 e HT-2.4 (B8) com o modelo `templates/ht-task.md`.
* **Roadmap**: registrar a decisão do mantenedor (2026-11-19) de que o **Linux sai na release seguinte** (Épico 4, versão sugerida 1.1.0). A dependência do Épico 4 em relação ao Épico 3 deixa de existir; o Épico 3 segue como pesquisa. Acrescente à matriz, como ❌ até o HT validar:
  * "Studio: restaurar padrões de imagem";
  * "Studio: gimbal pelo teclado";
  * "Pacote win-x64 (zip e instalador) num Windows 11 limpo".
* `TRADEMARKS.md`: acrescentar Google, Google Fonts, JetBrains, JetBrains Mono, Bootstrap, GitHub e SmartScreen (na linha da Microsoft).

## B2. Pendências técnicas (T3, antes do PR4)

* **`GetGimbalPosInfo`.** Decida entre manter pan/tilt UVC e converter para a convenção do OBSBOT Center (pitch = −tilt), com base no relatório §10.4 e no ADR-0009. Recomendação: converter, por compatibilidade. Implemente com teste em `OscTests` e registre a decisão no Épico 2 (ADR se mudar o contrato OSC).
* **Critério do PID FEF0.** Conferido e marcado no PR1 (2026-11-19): `TinyIdentity` filtra a enumeração do DirectShow e do V4L2 e a captura WASAPI.
* **Itens ⚠️ (HDR, AE de rosto, modos de IA, trava, rastreamento, suspensão).** Resolvido: confirmados visualmente pelo mantenedor em 2026-11-09, na segunda rodada da HT-2.1. A parte C da HT-2.3 fica opcional.
* **Áudio na gravação.** A segunda rodada da HT-2.1 gravou sem som mesmo com a captura WASAPI. Diagnosticar (provável falha ao achar o endpoint do microfone pelo PID) e corrigir antes da HT-2.3, em que o S10 bloqueia a release.

## B3. Metadados e versão

* `Directory.Build.props`: `<Product>OpenGimbalCam</Product>`, `<Company>OpenGimbalCam</Company>`, `<Authors>OpenGimbalCam contributors</Authors>`, `<Copyright>Copyright (c) 2026 OpenGimbalCam contributors</Copyright>`, `<RepositoryUrl>https://github.com/alsimoes/opengimbalcam</RepositoryUrl>`. A versão continua 0.9.0 até o PR4.
* `<AssemblyTitle>OpenGimbalCam Studio</AssemblyTitle>` no Studio e `<AssemblyTitle>OpenGimbalCam CLI (ogcctl)</AssemblyTitle>` na CLI; é o nome que aparece no Gerenciador de Tarefas.

## B4. Avisos de terceiros

`THIRD-PARTY-NOTICES.md` (pt-BR, com os textos das licenças no original) lista tudo o que vai dentro dos executáveis:

* o runtime do .NET (MIT);
* os pacotes NuGet de runtime do Studio e da CLI, entre eles o NAudio.Wasapi ([ADR-0011](../.harness/decisions/ADR-0011-audio-na-gravacao.md));
* Bootstrap Icons (MIT), Inter e JetBrains Mono (OFL 1.1).

Para levantar a lista:

1. Faça um `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=false` de cada executável numa pasta temporária e anote as DLLs e os nativos.
2. Para cada pacote de `obj/project.assets.json` (`libraries` com `type == package`), leia `<license>` ou `<licenseUrl>` do `.nuspec` em `%USERPROFILE%\.nuget\packages\<id>\<versão>\`.
3. Copie o aviso de copyright do LICENSE de cada pacote. Textos completos ficam em `licenses/` (MIT de cada titular, Apache-2.0 do FlashCap, OFL e o que mais aparecer, como BSD do ANGLE).

## B5. Script de empacotamento (`packaging/windows/build-release.ps1`, PowerShell 7)

```powershell
param([string]$OutDir = 'artifacts', [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = @($props.Project.PropertyGroup.Version)[0]; if (-not $version) { throw 'Version not found' }
$name = "OpenGimbalCam-$version-win-x64"
$out = Join-Path $root $OutDir; $stage = Join-Path $out $name; $tmp = Join-Path $out 'publish'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage, $tmp | Out-Null
$publish = '-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true',
  '-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-p:DebugType=none','-p:DebugSymbols=false'
foreach ($proj in 'src/OpenGimbalCam.Studio/OpenGimbalCam.Studio.csproj', 'src/OpenGimbalCam.Cli/OpenGimbalCam.Cli.csproj') {
  $dest = Join-Path $tmp ([IO.Path]::GetFileNameWithoutExtension($proj))
  dotnet publish (Join-Path $root $proj) @publish -o $dest; if ($LASTEXITCODE) { throw "publish failed: $proj" }
  $extra = Get-ChildItem $dest -File | Where-Object Extension -ne '.exe'
  if ($extra) { throw "unexpected files next to the exe: $($extra.Name -join ', ')" }
  Copy-Item (Join-Path $dest '*.exe') $stage
}
'LICENSE','TRADEMARKS.md','THIRD-PARTY-NOTICES.md','README.md','packaging/windows/LEIA-ME.txt' |
  ForEach-Object { Copy-Item (Join-Path $root $_) $stage }
Copy-Item (Join-Path $root 'licenses') (Join-Path $stage 'licenses') -Recurse
$assets = @(Join-Path $out "$name.zip"); Compress-Archive -Path $stage -DestinationPath $assets[0]
if (-not $SkipInstaller) {
  $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
  if (-not (Test-Path $iscc)) { throw "Inno Setup 6 not found ($iscc); use -SkipInstaller" }
  & $iscc "/DAppVersion=$version" "/DStageDir=$stage" "/DOutputDir=$out" (Join-Path $root 'packaging/windows/OpenGimbalCam.iss')
  if ($LASTEXITCODE) { throw 'ISCC failed' }
  $assets += Join-Path $out "$name-setup.exe"
}
$assets | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $_ -Leaf) } |
  Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
Remove-Item $tmp -Recurse -Force
```

Se algum nativo legítimo não entrar no arquivo único, ajuste a verificação de arquivos extras e registre o motivo. Acrescente `artifacts/` ao `.gitignore`.

`packaging/windows/LEIA-ME.txt`, em pt-BR e em texto puro, com:

* requisitos: Windows 11 x64 e a OBSBOT Tiny original;
* como abrir o Studio (zip ou instalador) e o aviso do SmartScreen ("Mais informações → Executar assim mesmo"; os executáveis não são assinados);
* onde ficam fotos, vídeos e configurações;
* a observação sobre o OSC e o OBSBOT Center;
* uso rápido do `ogcctl list`;
* como desinstalar (apagar a pasta, ou Configurações → Aplicativos);
* licença MIT, aviso de marcas e o endereço do repositório.

## B6. Instalador (`packaging/windows/OpenGimbalCam.iss`, Inno Setup 6.7)

```ini
#ifndef AppVersion
  #error Define AppVersion (/DAppVersion=x.y.z)
#endif
#ifndef StageDir
  #error Define StageDir
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
[Setup]
AppId={{GERAR-UM-GUID-UMA-VEZ-E-NUNCA-MUDAR}
AppName=OpenGimbalCam
AppVersion={#AppVersion}
AppVerName=OpenGimbalCam {#AppVersion}
AppPublisher=OpenGimbalCam
AppPublisherURL=https://github.com/alsimoes/opengimbalcam
AppSupportURL=https://github.com/alsimoes/opengimbalcam/issues
AppUpdatesURL=https://github.com/alsimoes/opengimbalcam/releases
VersionInfoVersion={#AppVersion}.0
DefaultDirName={autopf}\OpenGimbalCam
DefaultGroupName=OpenGimbalCam
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile={#StageDir}\LICENSE
SetupIconFile=..\..\src\OpenGimbalCam.Studio\Assets\app.ico
UninstallDisplayIcon={app}\opengimbalcam-studio.exe
UninstallDisplayName=OpenGimbalCam {#AppVersion}
OutputDir={#OutputDir}
OutputBaseFilename=OpenGimbalCam-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\OpenGimbalCam Studio"; Filename: "{app}\opengimbalcam-studio.exe"
Name: "{autodesktop}\OpenGimbalCam Studio"; Filename: "{app}\opengimbalcam-studio.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\opengimbalcam-studio.exe"; Description: "{cm:LaunchProgram,OpenGimbalCam Studio}"; Flags: nowait postinstall skipifsilent
```

Gere o GUID uma única vez com `[guid]::NewGuid()`; o `{{` inicial é obrigatório. Adicionar o `ogcctl` ao PATH fica fora da 1.0.0.

## B7. Workflows

**`.github/workflows/release.yml`** (novo):

```yaml
name: Release
on:
  push:
    tags: ['v*.*.*']
  pull_request:
    branches: [master]
    paths: ['packaging/**', '.github/workflows/release.yml']
  workflow_dispatch:
permissions:
  contents: read
env:
  AVALONIA_TELEMETRY_OPTOUT: '1'
  DOTNET_CLI_TELEMETRY_OPTOUT: '1'
jobs:
  windows:
    runs-on: windows-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Check tag matches Directory.Build.props
        if: github.ref_type == 'tag'
        shell: pwsh
        run: |
          [xml]$p = Get-Content Directory.Build.props
          $v = @($p.Project.PropertyGroup.Version)[0]
          if ("v$v" -ne '${{ github.ref_name }}') { throw "tag ${{ github.ref_name }} != Version $v" }
      - run: dotnet test OpenGimbalCam.slnx -c Release
      - run: choco upgrade innosetup --version=6.7.1 --no-progress -y --allow-downgrade
      - name: Package
        shell: pwsh
        run: ./packaging/windows/build-release.ps1 -OutDir artifacts
      - uses: actions/upload-artifact@v4
        with:
          name: opengimbalcam-win-x64
          path: |
            artifacts/*.zip
            artifacts/*-setup.exe
            artifacts/SHA256SUMS.txt
      - name: Create draft release
        if: github.ref_type == 'tag'
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          $tag = '${{ github.ref_name }}'
          $files = (Get-ChildItem artifacts -File | Where-Object Name -match '\.(zip|exe)$|^SHA256SUMS').FullName
          gh release create $tag $files --draft --verify-tag --title "OpenGimbalCam $($tag.TrimStart('v'))" --notes-file ".github/release-notes/$tag.md"
```

`choco upgrade` instala a versão fixada mesmo que a imagem do runner já traga outro Inno Setup. Num PR o job só gera os artefatos; o rascunho da release só nasce de tag.

**`ci.yml`**: acrescentar o mesmo `env` de opt-out de telemetria e, depois dos testes, um `actions/upload-artifact@v4` com `if: always()`, `name: ui-snapshots`, `path: tests/OpenGimbalCam.Studio.Tests/bin/Release/net10.0/ui-snapshots/*.png` e `if-no-files-found: ignore`.

## B8. Documentação e tarefas HT

* **README**:
  * nova seção "Instalação no Windows" logo no início (link para Releases, zip ou instalador, SmartScreen, desinstalação, pastas);
  * captura de tela em `.docs/images/studio-1.0.0.png`, feita pelo HT na HT-2.3;
  * lista de recursos do Studio atualizada (atalhos, restaurar padrões, modo foco, grade, notificações);
  * "Compilar a partir do código" e a seção "Licença" (MIT).
* **Notas de versão** `.github/release-notes/v1.0.0.md`, em pt-BR:
  * resumo, destaques (driver, CLI, Studio, OSC), requisitos e downloads (zip ou setup);
  * verificação com `Get-FileHash -Algorithm SHA256`, aviso do SmartScreen;
  * limitações conhecidas (itens ⚠️, Linux na próxima release, gravação AVI MJPEG);
  * licença MIT e link para `TRADEMARKS.md`.
* **HT-2.3: validar o candidato a release** (no Épico 2, uma única sessão de cerca de 60 min, que substitui as partes pendentes da HT-2.1). Use os artefatos da execução do `release.yml` no PR3 ou o rascunho.
  * **P. Pacote:**
    1. zip extraído e Studio aberto (anote o SmartScreen);
    2. instalador em pt-BR por usuário, atalho no menu Iniciar, abertura;
    3. desinstalação pelas Configurações.
  * **S. Studio com a Tiny:**
    1. tamanho e nitidez do vídeo;
    2. direcional e setas do teclado (segurar e soltar; o gimbal para ao soltar);
    3. Home;
    4. zoom (slider, botões, `+`/`−`);
    5. campo de visão;
    6. espelhar;
    7. presets;
    8. ajustes de imagem, chips "A", restaurar por controle e "Restaurar tudo" (a imagem volta ao padrão?);
    9. foto (flash, notificação, clique abre a imagem);
    10. gravação de 10 s (REC e cronômetro; AVI com áudio sincronizado);
    11. aba OSC (iniciar e parar; log);
    12. modo foco (F11, Esc, duplo clique);
    13. janela no tamanho mínimo;
    14. **Sobre**: versão, "Licença MIT", aviso de marcas e botão do repositório;
    15. fechar durante uma gravação: o processo some do Gerenciador de Tarefas em até 3 s e o AVI toca;
    16. reabrir: tamanho da janela, câmera e aba voltam como estavam;
    17. tirar a captura de tela para o README.
  * **C.** Comandos ⚠️ B1 a B6 da HT-2.1, como estão.
  * S1, S9, S10 e S14 substituem A1 a A3 da HT-2.1.
  * **Entregável:** uma linha por item (P1 a P3, S1 a S17, B1 a B6).
* **HT-2.4: publicar a 1.0.0.** Depois do Épico 6 e da HT-2.3 sem bloqueios, com o PR4 integrado:
  1. autorize o push da tag;
  2. espere o workflow;
  3. abra o rascunho em Releases e confira os 3 arquivos e as notas;
  4. clique em "Publish release";
  5. opcional: descrição e tópicos do repositório.
* **Épico 6** (busca de marcas, HT): comece já; bloqueia a publicação.

## B9. Corte da release (PR4, T1/T2 e revisão T3) e encerramento

1. Com a HT-2.3 sem bloqueios, as correções integradas e o Épico 6 concluído:
   * `Version` 1.0.0, `AssemblyVersion` e `FileVersion` 1.0.0.0 no `Directory.Build.props`;
   * notas finais e matriz de validação atualizada com datas;
   * o "Resultado" do Épico 2 rascunhado.
2. Depois do merge, com autorização explícita do mantenedor na sessão: `git tag -a v1.0.0 -m "OpenGimbalCam 1.0.0"` em `master` e `git push origin v1.0.0`. O workflow cria o **rascunho**; a HT-2.4 publica.
3. Depois de publicar:
   * Épico 2 `Concluído` (data, resultado, pendências);
   * roadmap e matriz atualizados;
   * Épico 4 (Linux, 1.1.0) marcado como próximo, aguardando o aceite formal do mantenedor;
   * itens futuros registrados: assinatura de código (HT, exige certificado), winget, `ogcctl` no PATH, Avalonia 12, tema claro, Épico 7.

---

# Verificação

1. `dotnet build OpenGimbalCam.slnx -c Release` com 0 avisos, e `dotnet test OpenGimbalCam.slnx -c Release` (inclui `OpenGimbalCam.Studio.Tests`).
2. **Snapshots.** Abra os PNGs de `tests/OpenGimbalCam.Studio.Tests/bin/Release/net10.0/ui-snapshots/` e confirme:
   * ícones nítidos e do mesmo tamanho;
   * Inter no texto e JetBrains Mono nos números;
   * vídeo dominante;
   * deck e inspetor sem cortes em 1100×700;
   * REC visível;
   * abas legíveis.
3. **Sem câmera.** Com a Tiny desconectada (regra de segurança acima; se ela estiver ligada, peça ao HT para desconectar), abra `src/OpenGimbalCam.Studio/bin/Release/net10.0/opengimbalcam-studio.exe` e confira:
   * estado vazio;
   * F5;
   * iniciar e parar o OSC numa porta livre;
   * Atividade (copiar e limpar);
   * modo foco;
   * a janela fecha e `Get-Process opengimbalcam-studio` fica vazio em até 3 s;
   * as configurações voltam na reabertura.
4. **Pacote local.** `./packaging/windows/build-release.ps1 -SkipInstaller` (ou completo, com Inno Setup 6.7). Confira o conteúdo do zip, rode os dois `.exe` extraídos (o Studio com a câmera desconectada; `ogcctl --version`) e confira o `SHA256SUMS.txt`.
5. **CI.** A execução do `release.yml` no PR3 publica os artefatos (zip, setup e SHA256SUMS) e o `ci.yml` publica os snapshots.
6. **Hardware e publicação.** Só o HT: HT-2.3 e HT-2.4. Nenhum agente move o gimbal nem escreve na câmera.

# Fora do escopo

Empacotamento Linux (Épico 4), internacionalização (Épico 7), interface para modos de IA e HDR (pendentes de validação ⚠️), escolha de resolução e fps do vídeo, histograma, assinatura de código, winget, PATH do `ogcctl` e migração para Avalonia 12.
