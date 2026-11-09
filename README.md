# OpenGimbalCam

> **Projeto independente.** Não é afiliado, autorizado, patrocinado nem endossado pela fabricante das câmeras OBSBOT. OBSBOT, OBSBOT Tiny, OBSBOT Center e as demais marcas citadas pertencem a seus titulares e aparecem só para indicar compatibilidade. Veja [TRADEMARKS.md](TRADEMARKS.md).

A OBSBOT Tiny (PID `FEF0`) não tem mais suporte nos drivers atuais do fabricante. Este projeto substitui o driver original por uma versão de código aberto, construída a partir da engenharia reversa do software e do firmware oficiais. Ele entrega dois produtos:

1. **Driver:** a versão aberta do driver original, que permite usar a OBSBOT Tiny em versões atuais do Windows e do Linux (bibliotecas `OpenGimbalCam.Core` e `OpenGimbalCam.Devices`, mais a CLI `ogcctl`).
2. **Aplicativo de controle:** o `opengimbalcam-studio`, que dá suporte à OBSBOT Tiny no Windows 11 e no Linux.

**Plataformas:** o foco atual é o **Windows 11**, até a versão 1.0.0. O port para **Linux** começa depois dela. O código Linux já existe, mas ainda não é suportado nem foi testado com a câmera. O port para macOS está planejado, mas sem previsão. Depois da 1.0.0, o projeto também faz a engenharia reversa do firmware da câmera para viabilizar aprimoramentos e novos usos.

**Só a OBSBOT Tiny original.** Para não entrar em conflito com o fabricante, o projeto tem como alvo exclusivo a OBSBOT Tiny original (`USB\VID_3564&PID_FEF0`). Modelos posteriores e, principalmente, câmeras que ainda têm suporte do fabricante ficam fora do escopo ([ADR-0007](.harness/decisions/ADR-0007-premissa-tiny-original.md)).

> As câmeras OBSBOT **não usam driver de kernel próprio**. Elas são UVC padrão. O OBSBOT Center fala com a câmera em modo usuário, pela **UVC Extension Unit** `{9A1E7291-6843-4683-6D92-39BC7906EE49}`, por meio do DirectShow/Kernel Streaming. Por isso o "driver" deste projeto também roda em modo usuário: DirectShow/Kernel Streaming no Windows e V4L2 + `UVCIOC_CTRL_QUERY` (driver `uvcvideo`) no Linux. Veja o [ADR-0002](.harness/decisions/ADR-0002-definicao-de-driver.md).

**Governança:** objetivo, escopo, regras, decisões e roadmap ficam no Harness, em **[`.harness/`](.harness/README.md)**. O relatório técnico completo, com endereços, desmontagem, hashes e o protocolo OSC, está em **[.docs/investigacao.md](.docs/investigacao.md)**.

## Compilar

Requer o .NET 10 SDK (veja `global.json`), no Windows ou no Linux.

```
dotnet build OpenGimbalCam.slnx -c Release
dotnet test  OpenGimbalCam.slnx -c Release
```

| Programa | Executável |
|---|---|
| CLI | `src/OpenGimbalCam.Cli/bin/Release/net10.0/ogcctl` (`.exe` no Windows) |
| Interface gráfica | `src/OpenGimbalCam.Studio/bin/Release/net10.0/opengimbalcam-studio` |

Para gerar um executável autocontido: `dotnet publish src/OpenGimbalCam.Studio -c Release -r linux-x64 --self-contained`. Troque o RID conforme o alvo (`win-x64`, `linux-arm64`…).

**Linux:** o usuário precisa de acesso de leitura e escrita a `/dev/videoN`, o que normalmente significa pertencer ao grupo `video` (`sudo usermod -aG video $USER`). Diagnóstico: `ogcctl info`.

## Interface gráfica (`opengimbalcam-studio`)

* Escolha da câmera, com as OBSBOT selecionadas automaticamente.
* Vídeo ao vivo (via [FlashCap](https://github.com/kekyo/FlashCap): DirectShow no Windows, V4L2 no Linux; MJPEG até 1280×720). Com o vídeo aberto a câmera fica acordada: em suspensão ela aceita comandos de gimbal mas não se move.
* Foto (JPEG em Imagens/OpenGimbalCam Studio) e gravação (AVI MJPEG em Vídeos/OpenGimbalCam Studio), também acionáveis pelo OSC (`PCSnapshot`, `SetPCRecording`).
* Gimbal: segure as setas para mover (velocidade ao pressionar, parada ao soltar, como no OBSBOT Center), botão para centralizar e controle de velocidade.
* Zoom, campo de visão (86°/78°/65°), espelho e presets.
* Todos os controles UVC da câmera (exposição, balanço de branco, brilho, contraste…), com "Auto" onde a câmera oferece.
* Servidor OSC compatível com o OBSBOT Center (UDP/TCP), com log. A compatibilidade pode ser desligada no painel e vem ligada por padrão.

## CLI (`ogcctl`)

```
ogcctl list                         # câmeras de vídeo; '*' marca OBSBOT (VID 3564)
ogcctl info                         # topologia UVC, onde está a XU e controles com faixas
ogcctl get zoom
ogcctl set pan -40                  # UVC padrão: pan, tilt, zoom, focus, exposure, brightness…
ogcctl set exposure auto
ogcctl move 80 0 --ms 300           # pan/tilt relativo pela XU, depois para
ogcctl cmds                         # comandos diretos conhecidos (seletor 6)
ogcctl cmd flip-horizontal 1
ogcctl cmd ai-mode <modo> <submodo>
ogcctl preset trigger 0             # presets do gimbal (modelos antigos, seletor 7)
ogcctl xu-get 6                     # leitura bruta
ogcctl xu-set 6 140101              # escrita bruta (o mesmo que flip-horizontal 1)
ogcctl msg-read                     # caixa de mensagens 0xAA (seletor 2)
ogcctl msg-send --sender 1 --receiver 14 --set 1 --id 0x1cd --payload 00
ogcctl msg-decode AA000E12…
ogcctl fw info   Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin
ogcctl fw verify Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin
ogcctl fw extract Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin saida/
```

Com várias câmeras conectadas, use `-d <índice>` (veja `list`).

### Servidor OSC

O comando reimplementa o "OBSBOT OSC Protocol" do OBSBOT Center (`/OBSBOT/WebCam/…`, porta 16284): as 45 rotas que se aplicam à Tiny, incluindo exposição, ISO, obturador, anti-flicker, HDR, modos de IA e posicionamento absoluto do gimbal pelo canal `0xAA`. Com ele, TouchOSC, Bitfocus Companion e plugins de Stream Deck controlam a câmera sem o OBSBOT Center:

```
ogcctl osc serve [--udp] [--tcp] [--port 16284] [--bind 0.0.0.0] [--reply-port N] [--obsbot-compat on|off] [-q]
ogcctl osc send 127.0.0.1 /OBSBOT/WebCam/General/GetDeviceInfo 0 [--tcp] [--slip]
ogcctl osc routes                   # endereços, o que o OBSBOT Center faz e o que está implementado
```

Feche o OBSBOT Center (ou desligue o OSC nele) antes de iniciar o servidor, porque os dois usariam a mesma porta.

A compatibilidade com o protocolo OSC do OBSBOT Center existe só para interoperar com ferramentas que já falam esse protocolo. Ela vem ligada; com `--obsbot-compat off` (ou desmarcando a opção no app), as mensagens `/OBSBOT/WebCam/…` são ignoradas ([ADR-0009](.harness/decisions/ADR-0009-compatibilidade-osc.md)).

## Estrutura

| Caminho | Conteúdo |
|---|---|
| `src/OpenGimbalCam.Core/` | independente de plataforma: abstrações de câmera (`ICameraPlatform`, `ICameraSession`, `IExtensionUnit`), constantes da XU, comandos diretos, quadro `0xAA` + CRC-16/USB, tabelas de IDs do protocolo V1 e tipos de produto por PID, pacote de firmware |
| `src/OpenGimbalCam.Devices/Windows/` | DirectShow (`ICreateDevEnum`, `IAMCameraControl`…) e Kernel Streaming (`IKsControl`, `IKsTopologyInfo`, `KSP_NODE`). Roda em uma thread MTA dedicada |
| `src/OpenGimbalCam.Devices/Linux/` | V4L2 (`VIDIOC_*CTRL`), `UVCIOC_CTRL_QUERY`, enumeração por sysfs e parser de descritores USB para achar o `bUnitID` da XU |
| `src/OpenGimbalCam.Osc/` | OSC 1.0 (UDP, TCP com prefixo de tamanho ou SLIP) e a ponte para o protocolo do OBSBOT Center |
| `src/OpenGimbalCam.Cli/` | a CLI `ogcctl` |
| `src/OpenGimbalCam.Studio/` | a interface gráfica `opengimbalcam-studio` (Avalonia 11) |
| `tests/OpenGimbalCam.Tests/` | testes de protocolo, comandos, firmware, OSC e descritores UVC |
| `tools/re/` | scripts Python que reproduzem a investigação: extração do Inno Setup, recorte da DLL, desmontagem, handlers OSC, layouts TouchOSC |
| `.harness/` | governança: carta do projeto, regras, ADRs, épicos e roadmap |
| `.docs/` | documentação de referência (relatório da investigação) |
| `.docs/original-driver/` | arquivos originais do fabricante, **não versionados** (estão no `.gitignore`) |

## Segurança e limites

As regras completas estão em [`.harness/governance.md`](.harness/governance.md); o estado de validação por plataforma, na [matriz de validação](.harness/roadmap.md#matriz-de-validação).

* **Nenhuma gravação de firmware.** Os seletores 1, 3, 4 e 5 da XU servem para upload de arquivos, **execução de shell no Linux da câmera** e atualização. O `xu-set` os recusa sem `--force`.
* Os formatos de byte dos comandos diretos vêm do código da `libdev.dll`. **O significado de cada valor** (por exemplo, os modos de IA) segue o nome da função de origem e ainda precisa ser confirmado na Tiny.
* O protocolo `0xAA` da Tiny (V1) foi recuperado e validado ao vivo movendo o gimbal por mensagem. A versão V3, dos modelos mais novos, está fora do escopo. 12 das 57 rotas OSC não têm efeito na Tiny e só ficam registradas no log: as 6 da Meet, `SetAutoFocusMode` (a Tiny tem foco fixo) e 5 com comandos que só existem na V3. O `msg-send` continua recebendo valores de fio.
* Testado ao vivo apenas com a **OBSBOT Tiny** (`VID_3564&PID_FEF0`) no Windows. **O backend Linux ainda não foi testado com a câmera.**

## Marcas registradas e propriedade intelectual

Este projeto é independente e não tem relação com a fabricante das câmeras OBSBOT nem com os demais titulares das marcas citadas. As marcas são usadas só de forma nominativa. Nenhum binário, firmware ou manual do fabricante é redistribuído, e a engenharia reversa foi feita para fins de interoperabilidade. O software é fornecido sem garantia. Detalhes e contato para titulares de direitos em [TRADEMARKS.md](TRADEMARKS.md).
