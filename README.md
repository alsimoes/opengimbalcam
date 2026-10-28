# oss-obsbot-driver

Uma alternativa aberta ao OBSBOT Center para controlar a **OBSBOT Tiny** (PID `FEF0`) no **Windows e no Linux**. Recursos que só existem em modelos mais novos estão fora do escopo. Ela foi construída a partir da engenharia reversa do software e do firmware oficiais.

> As câmeras OBSBOT **não usam driver de kernel próprio**. Elas são UVC padrão. O OBSBOT Center fala com a câmera em modo usuário, pela **UVC Extension Unit** `{9A1E7291-6843-4683-6D92-39BC7906EE49}`, por meio do DirectShow/Kernel Streaming. Este projeto faz o mesmo no Windows e usa V4L2 + `UVCIOC_CTRL_QUERY` (driver `uvcvideo`) no Linux.

O relatório completo, com endereços, desmontagem, hashes e o protocolo OSC, está em **[docs/investigacao.md](docs/investigacao.md)**.

## Compilar

Requer o .NET 8 SDK ou mais novo, no Windows ou no Linux.

```
dotnet build ObsbotCtl.slnx -c Release
dotnet test  ObsbotCtl.slnx -c Release
```

| Programa | Executável |
|---|---|
| CLI | `src/ObsbotCtl/bin/Release/net8.0/obsbotctl` (`.exe` no Windows) |
| Interface gráfica | `src/ObsbotStudio/bin/Release/net8.0/obsbot-studio` |

Para gerar um executável autocontido: `dotnet publish src/ObsbotStudio -c Release -r linux-x64 --self-contained`. Troque o RID conforme o alvo (`win-x64`, `linux-arm64`…).

**Linux:** o usuário precisa de acesso de leitura e escrita a `/dev/videoN`, o que normalmente significa pertencer ao grupo `video` (`sudo usermod -aG video $USER`). Diagnóstico: `obsbotctl info`.

## Interface gráfica (`obsbot-studio`)

* Escolha da câmera, com as OBSBOT selecionadas automaticamente.
* Vídeo ao vivo (via [FlashCap](https://github.com/kekyo/FlashCap): DirectShow no Windows, V4L2 no Linux; MJPEG até 1280×720). Com o vídeo aberto a câmera fica acordada: em suspensão ela aceita comandos de gimbal mas não se move.
* Foto (JPEG em Imagens/OBSBOT Studio) e gravação (AVI MJPEG em Vídeos/OBSBOT Studio), também acionáveis pelo OSC (`PCSnapshot`, `SetPCRecording`).
* Gimbal: segure as setas para mover (velocidade ao pressionar, parada ao soltar, como no OBSBOT Center), botão para centralizar e controle de velocidade.
* Zoom, campo de visão (86°/78°/65°), espelho e presets.
* Todos os controles UVC da câmera (exposição, balanço de branco, brilho, contraste…), com "Auto" onde a câmera oferece.
* Servidor OSC compatível com o OBSBOT Center (UDP/TCP), com log.

## CLI (`obsbotctl`)

```
obsbotctl list                         # câmeras de vídeo; '*' marca OBSBOT (VID 3564)
obsbotctl info                         # topologia UVC, onde está a XU e controles com faixas
obsbotctl get zoom
obsbotctl set pan -40                  # UVC padrão: pan, tilt, zoom, focus, exposure, brightness…
obsbotctl set exposure auto
obsbotctl move 80 0 --ms 300           # pan/tilt relativo pela XU, depois para
obsbotctl cmds                         # comandos diretos conhecidos (seletor 6)
obsbotctl cmd flip-horizontal 1
obsbotctl cmd ai-mode <modo> <submodo>
obsbotctl preset trigger 0             # presets do gimbal (modelos antigos, seletor 7)
obsbotctl xu-get 6                     # leitura bruta
obsbotctl xu-set 6 140101              # escrita bruta (o mesmo que flip-horizontal 1)
obsbotctl msg-read                     # caixa de mensagens 0xAA (seletor 2)
obsbotctl msg-send --sender 1 --receiver 14 --set 1 --id 0x1cd --payload 00
obsbotctl msg-decode AA000E12…
obsbotctl fw info   Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin
obsbotctl fw verify Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin
obsbotctl fw extract Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin saida/
```

Com várias câmeras conectadas, use `-d <índice>` (veja `list`).

### Servidor OSC

O comando reimplementa o "OBSBOT OSC Protocol" do OBSBOT Center (`/OBSBOT/WebCam/…`, porta 16284): as 45 rotas que se aplicam à Tiny, incluindo exposição, ISO, obturador, anti-flicker, HDR, modos de IA e posicionamento absoluto do gimbal pelo canal `0xAA`. Com ele, TouchOSC, Bitfocus Companion e plugins de Stream Deck controlam a câmera sem o OBSBOT Center:

```
obsbotctl osc serve [--udp] [--tcp] [--port 16284] [--bind 0.0.0.0] [--reply-port N] [-q]
obsbotctl osc send 127.0.0.1 /OBSBOT/WebCam/General/GetDeviceInfo 0 [--tcp] [--slip]
obsbotctl osc routes                   # endereços, o que o OBSBOT Center faz e o que está implementado
```

Feche o OBSBOT Center (ou desligue o OSC nele) antes de iniciar o servidor, porque os dois usariam a mesma porta.

## Estrutura

| Caminho | Conteúdo |
|---|---|
| `src/Obsbot.Core/` | independente de plataforma: abstrações de câmera (`ICameraPlatform`, `ICameraSession`, `IExtensionUnit`), constantes da XU, comandos diretos, quadro `0xAA` + CRC-16/USB, tabelas de IDs do protocolo V1 e tipos de produto por PID, pacote de firmware |
| `src/Obsbot.Devices/Windows/` | DirectShow (`ICreateDevEnum`, `IAMCameraControl`…) e Kernel Streaming (`IKsControl`, `IKsTopologyInfo`, `KSP_NODE`). Roda em uma thread MTA dedicada |
| `src/Obsbot.Devices/Linux/` | V4L2 (`VIDIOC_*CTRL`), `UVCIOC_CTRL_QUERY`, enumeração por sysfs e parser de descritores USB para achar o `bUnitID` da XU |
| `src/Obsbot.Osc/` | OSC 1.0 (UDP, TCP com prefixo de tamanho ou SLIP) e a ponte para o protocolo do OBSBOT Center |
| `src/ObsbotCtl/` | a CLI `obsbotctl` |
| `src/ObsbotStudio/` | a interface gráfica `obsbot-studio` (Avalonia 11) |
| `tests/Obsbot.Tests/` | testes de protocolo, comandos, firmware, OSC e descritores UVC |
| `tools/re/` | scripts Python que reproduzem a investigação: extração do Inno Setup, recorte da DLL, desmontagem, handlers OSC, layouts TouchOSC |
| `original-driver/` | arquivos originais do fabricante, **não versionados** (estão no `.gitignore`) |

## Segurança e limites

* **Nenhuma gravação de firmware.** Os seletores 1, 3, 4 e 5 da XU servem para upload de arquivos, **execução de shell no Linux da câmera** e atualização. O `xu-set` os recusa sem `--force`.
* Os formatos de byte dos comandos diretos vêm do código da `libdev.dll`. **O significado de cada valor** (por exemplo, os modos de IA) segue o nome da função de origem e ainda precisa ser confirmado modelo a modelo.
* O protocolo `0xAA` da Tiny (V1) foi recuperado e validado ao vivo movendo o gimbal por mensagem. A versão V3, dos modelos mais novos, está fora do escopo. 12 das 57 rotas OSC não têm efeito na Tiny e só ficam registradas no log: as 6 da Meet, `SetAutoFocusMode` (a Tiny tem foco fixo) e 5 com comandos que só existem na V3. O `msg-send` continua recebendo valores de fio.
* Testado ao vivo apenas com a **OBSBOT Tiny** (`VID_3564&PID_FEF0`) no Windows. **O backend Linux ainda não foi testado com a câmera.**
