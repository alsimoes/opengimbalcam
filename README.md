# oss-obsbot-driver

Uma alternativa aberta ao OBSBOT Center para controlar câmeras OBSBOT no Windows, baseada na engenharia reversa do software e do firmware oficiais.

> As câmeras OBSBOT **não usam driver de kernel próprio**. O Windows as atende com `usbvideo.sys` (UVC) e `usbaudio.sys` (UAC). O OBSBOT Center fala com a câmera em modo usuário, pela **UVC Extension Unit** `{9A1E7291-6843-4683-6D92-39BC7906EE49}`, por meio do DirectShow/Kernel Streaming. O `obsbotctl` faz o mesmo.

O relatório completo, com endereços, desmontagem e verificação de hashes, está em **[docs/investigacao.md](docs/investigacao.md)**.

## Compilar

Requer o .NET 8 SDK (ou mais novo) no Windows.

```
dotnet build ObsbotCtl.slnx -c Release
dotnet test  ObsbotCtl.slnx -c Release
```

O executável fica em `src/ObsbotCtl/bin/Release/net8.0-windows/obsbotctl.exe`.

## Uso

```
obsbotctl list                         # câmeras DirectShow; '*' marca OBSBOT (VID 3564)
obsbotctl info                         # topologia KS, nó da XU e controles UVC com faixas
obsbotctl get zoom
obsbotctl set pan -40                  # UVC padrão: pan, tilt, zoom, focus, exposure, brightness…
obsbotctl set exposure auto
obsbotctl move 80 0 --ms 300           # pan/tilt relativo pela XU, depois para
obsbotctl cmds                         # comandos diretos conhecidos (seletor 6)
obsbotctl cmd flip-horizontal 1
obsbotctl cmd ai-mode <modo> <submodo>
obsbotctl preset trigger 0             # presets do gimbal (modelos antigos, seletor 7)
obsbotctl xu-get 6                     # leitura bruta de 60 bytes
obsbotctl xu-set 6 140101              # escrita bruta (o mesmo que flip-horizontal 1)
obsbotctl msg-read                     # caixa de mensagens 0xAA (seletor 2)
obsbotctl msg-send --sender 1 --receiver 14 --set 1 --id 0x1cd --payload 00
obsbotctl msg-decode AA000E12…
obsbotctl fw info   Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin
obsbotctl fw verify Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin
obsbotctl fw extract Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin saida\
```

Com várias câmeras conectadas, use `-d <índice>` (veja `list`).

## Estrutura

| Caminho | Conteúdo |
|---|---|
| `src/ObsbotCtl/Interop/` | interfaces COM do DirectShow (`ICreateDevEnum`, `IAMCameraControl`…) e do Kernel Streaming (`IKsControl`, `IKsTopologyInfo`, `KSP_NODE`) |
| `src/ObsbotCtl/Camera/` | enumeração, sessão, Extension Unit (`uvcExtGet`/`uvcExtSet`), tabela de comandos diretos |
| `src/ObsbotCtl/Protocol/` | quadro `0xAA` e CRC-16/USB do canal de mensagens |
| `src/ObsbotCtl/Firmware/` | parser, verificador e extrator do pacote de firmware e do contêiner `AMAC` |
| `tests/ObsbotCtl.Tests/` | testes de protocolo, comandos e firmware (pacote sintético + o real, se estiver em `original-driver/`) |
| `tools/re/` | scripts Python que reproduzem a investigação: extração do Inno Setup, recorte da DLL, desmontagem e tabelas |
| `original-driver/` | arquivos originais do fabricante, **não versionados** (estão no `.gitignore`) |

## Segurança e limites

* **Nenhuma gravação de firmware.** Os seletores 1, 3, 4 e 5 da XU servem para upload de arquivos, **execução de shell no Linux da câmera** e atualização. Por isso o `xu-set` os recusa sem `--force`.
* Os formatos de byte dos comandos diretos vêm do código da `libdev.dll`. **O significado de cada valor** (por exemplo, os modos de IA) segue o nome da função de origem, mas ainda precisa ser confirmado modelo a modelo.
* As tabelas que convertem IDs lógicos em IDs de fio no protocolo `0xAA` foram recuperadas só em parte. Por isso o `msg-send` trabalha com valores de fio.
* Testado ao vivo apenas com a **OBSBOT Tiny** (`VID_3564&PID_FEF0`).
