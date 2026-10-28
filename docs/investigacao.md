# Investigação: como o "driver" e o firmware OBSBOT funcionam no Windows

Data: 2026-10-27. Câmera usada nos testes ao vivo: **OBSBOT Tiny** (`USB\VID_3564&PID_FEF0`, REV 0409).

## 1. Resumo

1. **Não existe driver de kernel OBSBOT.** O instalador não contém `.inf`, `.sys` nem `.cat`. No Windows a câmera é um dispositivo USB composto, atendido só por drivers nativos da Microsoft: `usbccgp` (composto), `usbvideo` (UVC) e `usbaudio` (UAC).
2. O "driver" é o **OBSBOT Center**, um aplicativo em modo usuário (Qt). O controle da câmera fica na **`libdev.dll`**, que fala com a câmera por **DirectShow + Kernel Streaming**. Ela abre o filtro de captura, acha a **UVC Extension Unit** (XU) de GUID `{9A1E7291-6843-4683-6D92-39BC7906EE49}` e lê/escreve controles de **60 bytes** com `IKsControl::KsProperty`.
3. Por essa XU passam **7 seletores**: comandos diretos (seletor 6), um canal de mensagens com quadro `0xAA` e CRC-16/USB (seletor 2), presets do gimbal (7) e a máquina de atualização de firmware (1, 3, 4, 5). O seletor 3 executa comandos shell no Linux da câmera.
4. O **firmware** é um pacote com 3 componentes: (a) a imagem Linux do SoC **SigmaStar SSC339G**, em um contêiner `AMAC` com 8 partições (U-Boot, kernel 4.9, rootfs UBIFS...); (b) o firmware do MCU do gimbal; (c) o firmware do MCU do motor de yaw. Cada parte tem MD5 e CRC-32, e todos conferem.
5. Este repositório traz uma reimplementação aberta, `obsbotctl` (C#/.NET 8), que repete o caminho da `libdev.dll`. Ela foi validada ao vivo na câmera conectada.

## 2. Material analisado

| Arquivo | SHA-256 |
|---|---|
| `Obsbot_Center_OA_E_Win_x64_2.0.14.64_release.exe` | `b913e3d5541b35a1eb27fca4a73616ebc1483d91e86f8a15ac4c2f1be257a410` |
| `Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin` | `0585093683ddd0cc24f845d2fa154a3a52a70e8bd95a2dd2fa4861c272d3aef2` |
| `OBSBOT Tiny 4K User Manual_EN.pdf` | `474e03c25641ef4a88dea6970a2990b47bbed9b0cd89cb0707a25e9a55567c8a` |
| `libdev.dll` (extraída do instalador) | `89eea38c960aaa5c3357f5fd06b559cac1c51fcf945db08ab0dbaf6e7c3ac71a` |

Toda a análise foi **estática**. Nenhum binário do fabricante foi executado. Os scripts usados estão em [`tools/re/`](../tools/re) (veja a seção 9).

## 3. Como o Windows enxerga a câmera (evidência ao vivo)

`Get-PnpDevice` / `Get-PnpDeviceProperty` com a câmera conectada:

| Instância | Descrição | INF | Serviço | Fornecedor |
|---|---|---|---|---|
| `USB\VID_3564&PID_FEF0\5&1F03AB54&0&1` | USB Composite Device ("OBSBOT Tiny") | `usb.inf` | `usbccgp` | Microsoft |
| `USB\VID_3564&PID_FEF0&MI_00\…` | OBSBOT Tiny Camera (classe `0E`/`03`, UVC) | `usbvideo.inf` | `usbvideo` | Microsoft |
| `USB\VID_3564&PID_FEF0&MI_02\…` | OBSBOT Tiny Audio (classe `01`, UAC) | `wdma_usb.inf` | `usbaudio` | Microsoft |

A topologia Kernel Streaming que o `usbvideo.sys` publica para a câmera (saída de `obsbotctl info`):

```
nó 0: {dff229e6-…} Camera Terminal
nó 1: {dff229e5-…} Processing Unit
nó 2: {941c7ac0-c559-11d0-8a2b-00a0c9255ac1} Extension Unit
nó 3: {941c7ac0-c559-11d0-8a2b-00a0c9255ac1} Extension Unit   <- responde ao GUID OBSBOT
nó 4: {dff229e1-…} Video Streaming
```

Os controles UVC padrão já aparecem pelas interfaces nativas do Windows: Pan −130..130, Tilt −90..90, Zoom 0..100, exposição, balanço de branco e outros. Com isso **pan/tilt/zoom absolutos funcionam sem nenhum software da OBSBOT**.

## 4. O instalador OBSBOT Center 2.0.14.64

* O instalador é **Inno Setup 6.1.0 (Unicode)**. O cabeçalho `rDlPtS` fica no offset 873 864, o `setup.0` no offset 318 465 849 e os dados em **um único bloco sólido LZMA1** (318 MB comprimidos, 632 MB descomprimidos).
* O Inno aplica aos executáveis um **filtro de CALL/JMP** (endereços `E8`/`E9` relativos à posição no arquivo). É preciso desfazê-lo antes de desmontar o código (`tools/re/inno_unfilter.py`). Sem isso, as chamadas apontam para lugares errados.
* Arquivos relevantes (de `setup.0`):
  * `{app}\bin\OBSBOT_Main.exe`, `OBSBOT_Center.exe`: interface Qt 6.
  * `{app}\bin\libdev.dll`: **SDK de dispositivo**, onde está toda a comunicação com a câmera.
  * `{app}\data\libdev\rmtfu.lzma`, `bspatch.lzma`: ferramentas enviadas à câmera durante a atualização (veja a seção 7.4).
  * `{app}\data\rmfunc\indshow\rm-virtualcam-module{32,64}.dll` + `virtualcam-install.bat`: uma **câmera virtual DirectShow** ("OBSBOT Virtual Camera", `OBSBOT_VirtualCam`), registrada com `regsvr32`. É o único componente "tipo driver", e roda em modo usuário.
* Nenhuma entrada `.inf`, `.sys`, `.cat`, `pnputil` ou `DriverPackage` aparece no script do instalador.

## 5. `libdev.dll`: o transporte até a câmera

Endereços são RVAs na `libdev.dll` (base `0x180000000`), já com o filtro do Inno desfeito.

### 5.1 Imports e strings que mostram o caminho

* Imports: `SETUPAPI` (`SetupDiGetClassDevsW`, `SetupDiEnumDeviceInterfaces`…), `ole32!CoCreateInstance`. Funções `HidD_*` e `DeviceIoControl` são carregadas dinamicamente.
* GUIDs no `.rdata`: `CLSID_SystemDeviceEnum`, `CLSID_VideoInputDeviceCategory`, `IID_IKsControl`, `IID_IKsTopologyInfo`, `KSNODETYPE_DEV_SPECIFIC`, `PROPSETID_VIDCAP_CAMERACONTROL`/`VIDEOPROCAMP`, `IID_ICameraControl` `{2BA1785D-4D1B-44EF-85E8-C7F1D3F20184}` e o GUID da XU OBSBOT `{9A1E7291-6843-4683-6D92-39BC7906EE49}` (offset de arquivo `0x1DBCC8`).
* IDs de hardware: `VID_3564&PID_FEFC`, `FEFD`, `FF02`, `F001`, `FF04`, lidos junto com `PortName`. São interfaces seriais/CDC de outros modos e modelos. Há também transportes por rede (`net init`), MTP (`OBSBOT Device MTP`) e BLE (`bleConnectDevice`) para outros produtos.
* Strings de log: `uvc init %s`, `uvc_xu_init failed`, `uvcExtGet not in uvc mode`, `uvc_xu_get %s KsProperty failed. Error: %d %d`, `Data len %d > uvc max payload len %d`, `!!!extension_prop success!!!`.

### 5.2 Descoberta da Extension Unit (`uvc_xu_init`, RVA `0x168920`)

```
QueryInterface(IID_IKsTopologyInfo)
get_NumNodes                      ; vtbl+0x40
para cada nó:
  get_NodeType                    ; vtbl+0x48
  se tipo == KSNODETYPE_DEV_SPECIFIC:
    CreateNodeInstance(nó, IID_IKsControl)          ; vtbl+0x50
    KsProperty(KSP_NODE{Set=XU, Id=0, Flags=0x10000100, NodeId=nó}, data=NULL)
    se S_OK -> guarda IKsControl e NodeId          ; log "!!!extension_prop success!!!"
```

`0x10000100` = `KSPROPERTY_TYPE_TOPOLOGY | KSPROPERTY_TYPE_SETSUPPORT`.

### 5.3 Leitura e escrita (`uvcExtGet` RVA `0x85C0`, `uvcExtSet` RVA `0x87D0`)

```
000086ca  movups xmm0, [GUID 9A1E7291-6843-4683-6D92-39BC7906EE49]
000086f3  mov    [rsp+0x40], ebp            ; Property.Id   = seletor
000086fd  mov    [rsp+0x44], 0x10000001     ; Property.Flags = TOPOLOGY|GET
00008705  mov    [rsp+0x48], eax            ; NodeId
000086f7  mov    r8d, 0x20                  ; sizeof(KSP_NODE)
0000870c  mov    [rsp+0x20], 0x3c           ; DataLength = 60
00008714  call   [rax+0x18]                 ; IKsControl::KsProperty
```

`uvcExtSet` é igual, com `Flags = 0x10000002` (TOPOLOGY|SET). **Toda transferência tem 60 bytes.** Os HRESULTs `0x800701B1`, `0x80070015`, `0x8007001F` e `0x800703E3` são contados como "dispositivo sumiu".

### 5.4 Mapa de seletores (todas as 81 chamadas de `uvcExtSet` e 21 de `uvcExtGet`)

Este mapa foi gerado por `tools/re/survey.py`, que cruza o valor de `edx` em cada chamada com as strings de log próximas.

| Seletor | Uso na libdev | Exemplos de log |
|---|---|---|
| 1 | início de upload de arquivo | `transFileStart failed` |
| 2 | **canal de mensagens** (`txMsg`/`rxMsg`) | `[txMsg] UVC`, `uvcExtGet failed in rxMsg` |
| 3 | **`ugSyscall`: executa shell no aparelho** | `reboot`, `chmod +x /app/updater/bspatch`, `dd if=/dev/mtd6 …` |
| 4 | download de arquivo do aparelho | `REMO_UVC_XU_CTRL_FILE_DOWNLOAD failed` |
| 5 | **`ugUpCmd`: máquina de atualização** | `UG_UVC_UP_CMD_BSPATCH`, `…_FREE_SOURCE`, `ubi format failed` |
| 6 | **comandos diretos** (`cameraSet*U`) | `cameraSetAiModeU %d failed`… |
| 7 | presets do gimbal (modelos antigos) | `cameraTrgGimbalPresetU %d failed` |

### 5.5 Comandos diretos (seletor 6)

Os comandos diretos usam o formato `[0]=id, [1]=tamanho do payload, [2..]=payload`, completado com zeros até 60 bytes. Exemplo, `cameraSetImageFlipHorizonU` (RVA `0x2E920`):

```
0002e946  mov byte ptr [rsp+0x22], dl       ; valor
0002e94a  mov edx, 6                        ; seletor 6
0002e962  mov word ptr [rsp+0x20], 0x114    ; id 0x14, len 1
0002e969  call uvcExtSet
```

`cameraSetPanTiltRelative` (chamada a `uvcExtSet` em RVA `0x11DD4`) escreve `09 04 <dirPan> <velPan> <dirTilt> <velTilt>`. A direção é 1, 0 ou −1 (`0xFF`), e a velocidade é o módulo do valor escalado pelo máximo de `PanRelative`/`TiltRelative` do Camera Terminal. A libdev consulta esse máximo via `IID_ICameraControl` (`getRange_TiltRelative`).

A tabela completa (28 comandos, cada um com a função de origem e o endereço da chamada) está em [`DirectCommands.cs`](../src/ObsbotCtl/Camera/DirectCommands.cs) e aparece em `obsbotctl cmds`. Nomes vindos dos exports, por exemplo `AiWorkModeType`, `AiTrackSubModeType` e `DevGestureParaType`, indicam o significado. **Os valores numéricos de cada modo não foram confirmados por modelo.**

### 5.6 Presets do gimbal (seletor 7)

A libdev testa o modelo antes de escolher o caminho (função RVA `0xF550`, que compara o nome do produto):

* modelos antigos: XU seletor 7, `[0]=ação, [1]=id`, onde a ação 1 apaga (`cameraDelGimbalPresetU`) e a ação 3 dispara (`cameraTrgGimbalPresetU`);
* modelos novos: mensagem `0xAA` no seletor 2 (`aiTrgGimbalPresetR`: remetente 3, destinatário 0x0B, set 3, id 0x3D).

## 6. Canal de mensagens `0xAA` (seletor 2)

### 6.1 Envio

`txMsg` (log `Data len %d > uvc max payload len %d` em RVA `0x8402`) recusa mensagens com mais de 60 bytes, incrementa atomicamente um número de sequência, serializa (RVA `0xFFF40`), finaliza (RVA `0xFF7D0`) e chama `uvcExtSet(2, buf)`. `rxMsg` faz `uvcExtGet(2, buf)` e repassa o buffer ao parser.

### 6.2 Formato no fio

| Byte | Conteúdo |
|---|---|
| 0 | `0xAA` |
| 1–2 | comprimento total, 12 bits, big-endian |
| 3 | `0x10` (versão 1) \| tipo (2 bits) |
| 4–5 | sequência, big-endian |
| 6–7 | **CRC-16/USB**, big-endian, calculado com os bytes 6–8 zerados |
| 8 | 0 (vira CRC-8 numa outra variante de cabeçalho) |
| 9 | `destinatário << 4 \| remetente` |
| 10–11 | `set << 12 \| id` (12 bits), big-endian |
| 12… | payload |

O CRC usa as tabelas "auchCRCHi/auchCRCLo" do Modbus (RVAs `0x1CBB50`/`0x1CBA50`) e inverte o resultado. Isso é exatamente o **CRC-16/USB**. Conferi as tabelas byte a byte, e o valor de verificação `"123456789"` → `0xB4C8` bate.

### 6.3 Tabelas de IDs

A libdev guarda IDs lógicos (por exemplo remetente 3, destinatário 0x0B) e os converte em nibbles e IDs de fio com `std::map`s globais, montados em construtores estáticos. `tools/re/maps.py` recuperou parte deles:

* conjunto lógico → nibble: `{0:0, 1:1, 2:2, 3:3, 4:4, 5:8, 6:13}`;
* sub-tabelas de comandos, por exemplo `{0:0x1CD, 1:0x3C4, 2:0x3C2, 3:0x3D2, 4:0x1D2}`.

A tabela de dispositivos e várias sub-tabelas são montadas com código que o extrator ainda não segue. Por isso o `obsbotctl msg-send` recebe **valores de fio** (nibbles e id de 12 bits) e não nomes lógicos. Nas leituras ao vivo, a caixa de mensagens da Tiny estava vazia (60 bytes zero), o que é coerente com um modelo antigo que usa sobretudo o seletor 6.

## 7. O firmware `Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin`

### 7.1 Pacote externo

| Offset | Campo | Valor |
|---|---|---|
| 0x00 | magic | `98 EC 20 AB` |
| 0x04 | tamanho do cabeçalho | 0x400 |
| 0x08 | tamanho total do arquivo | 89 104 188 |
| 0x0C | nº de componentes | 3 |
| 0x10 / 0x20 | produto / região | `Obsbot_tiny_2P` / `OA_E` |
| 0x30 | versão (invertida) | `02 06 02 01` → **1.2.6.2** |
| 0x38 | MD5 de tudo após o cabeçalho | `785B89F9…3D2B517D` ✔ |
| 0x48 / 0x58 | SoC / build | `SSC339G` / `release` |
| 0xFC | CRC-32 de 0x00..0xFB | ✔ |

Cada componente ocupa 0x100 bytes a partir de 0x100. Os campos são: tipo `+0`, tamanho `+4`, offset `+8`, versão `+0x3C`, CRC-32 `+0x44`, MD5 `+0x48`, nome `+0x58` e CRC-32 da própria entrada `+0xFC`.

| # | Tipo | Versão | Tamanho | Nome | Conteúdo |
|---|---|---|---|---|---|
| 0 | 1 | 24.04.11.74 | 89 002 020 | `…_SSC339G_Camera_…` | contêiner `AMAC` (Linux) |
| 1 | 2 | 1.0.17.1 | 70 464 | `…_Gimbal_…` | MCU Cortex-M (SP `0x20004B48`, reset `0x08004119`) |
| 2 | 13 | 1.0.8.3 | 30 680 | `…_Yaw_…` | MCU Cortex-M do motor de yaw |

O produto `Obsbot_tiny_2P` aparece na tabela de nomes de firmware da libdev, ao lado de `Obsbot_tail_2P` e `Obsbot_me_1P`. Ele corresponde ao manual "Tiny 4K" que acompanha os arquivos. A câmera testada se identifica por USB apenas como "OBSBOT Tiny".

### 7.2 Contêiner `AMAC` (imagem SigmaStar)

Usa o mesmo layout de 0x100 bytes por entrada (nome em `+0x1C`, MD5 em `+0x44`), com cabeçalho de 0x900 bytes e o MD5 do conteúdo em `+0x14`. **As 8 partições conferem (MD5 e CRC das entradas):**

`AIFS.ubi` (modelos de IA, volume UBI `ai`), `config` (script shell), `KERNEL.bin` e `RECOVERY.bin` (uImage, ARM Linux, `MVX4##I6E#…KL_LX409`, ou seja Linux 4.9 para Infinity6E), `ROOTFS.ubi`, `UBOOT0/1.bin` (`…CM_UBT1501`, U-Boot 2015.01) e `UPDATERFS.ubi`.

### 7.3 O script `config`

Roda no aparelho ao fim da atualização, e só quando o rootfs ativo é `updater`. Ele regrava o ambiente do U-Boot e o mapa de partições NAND:

```
mtdparts=nand0:0xc0000@0x140000(IPL0),0x60000(IPL_CUST0),0x60000(IPL_CUST1),0x60000(UBOOT0),
0x60000(UBOOT1),0x80000(ENV0),0x400000(KERNEL),0x400000(RECOVERY),0x200000(FACTORY),
0x800000(PRIVATE),0x3000000(ROOTFS),0x2800000(AIFS),0x1200000(UPDATERFS)
bootcmd='nand read.e 0x22000000 KERNEL 0x400000; bootm 0x22000000; nand read.e 0x22000000 RECOVERY …'
loadupdater='… ubi part UPDATERFS; ubifsmount ubi0:updater; ubifsload 0x21000000 /app/updater.txt; exc 0x21000000'
```

### 7.4 Como o OBSBOT Center atualiza a câmera (pelas strings e chamadas da libdev)

1. Envia arquivos pela XU (seletores 1 e 2, `send file len = %d, pack_cnt = %d`) ou por MTP (`Dev_Firmware.bin`), conforme o modelo.
2. Executa no aparelho, pelo seletor 3 (`ugSyscall`): `lzma -d /app/updater/bspatch.lzma`, `chmod +x /app/updater/bspatch`, `dd if=/dev/mtd6 of=/app/updater/ug bs=2k count=%d`, `cd /app/updater&&./bspatch ug ug /log/ugpt` (atualização diferencial com bsdiff) e `reboot`.
3. Controla o processo pelo seletor 5 (`UG_UVC_UP_CMD_RESET_TRANS_STATE`, `…_BSPATCH`, `…_RENAME_FW`, `…_FREE_SOURCE`, `ubi format updater.ubi`).
4. Reinicia no `UPDATERFS`, que grava as partições, e o script `config` acerta o ambiente do U-Boot.

## 8. O que o `obsbotctl` implementa

| Função | Como | Evidência |
|---|---|---|
| Listar câmeras e achar VID 3564 | `ICreateDevEnum` + `IPropertyBag` (`DevicePath`) | §5.1 |
| Controles UVC padrão | `IAMCameraControl` / `IAMVideoProcAmp` | §3 |
| Achar a XU como a libdev | `IKsTopologyInfo` → `CreateNodeInstance(IKsControl)` → SETSUPPORT | §5.2 |
| GET/SET brutos de 60 bytes | `KsProperty(KSP_NODE)` com flags `0x10000001`/`0x10000002` | §5.3 |
| Comandos diretos e pan/tilt relativo | seletor 6, `[id][len][payload]` | §5.5 |
| Presets do gimbal | seletor 7 | §5.6 |
| Mensagens `0xAA` | codificador/decodificador + CRC-16/USB | §6 |
| Firmware: info/verify/extract | parser dos dois níveis de tabela | §7 |

**Validado ao vivo** (OBSBOT Tiny, PID FEF0): `list`, `info` (XU encontrada no nó 3), leitura de todos os controles UVC, `xu-get` nos seletores 2, 6 e 7, um SET inócuo no seletor 6 (pan/tilt com velocidade 0) e `set pan 0`.

**Deliberadamente não implementado:** gravar firmware. Os seletores 1, 3, 4 e 5 (upload, shell remoto, download, atualização) ficam bloqueados no `xu-set` sem `--force`. Um erro nesse caminho pode inutilizar a câmera.

## 9. Como reproduzir

Requisitos: Python 3 e `capstone` (`pip install capstone`, num venv).

```
python tools/re/inno_dump.py   <instalador.exe> <saida>        # setup.0 + bloco sólido LZMA
python tools/re/carve_pe.py    <saida>/chunk_*.bin <pe> libdev  # recorta libdev.dll
python tools/re/inno_unfilter.py <pe>/…_libdev.dll libdev_unf.dll  # desfaz filtro CALL/JMP do Inno
python tools/re/dis.py libdev_unf.dll func 85c0 120             # uvcExtGet (cria cache de xrefs)
python tools/re/survey.py libdev_unf.dll 87d0                   # todas as chamadas a uvcExtSet
python tools/re/maps.py libdev_unf.dll                          # tabelas std::map do protocolo
obsbotctl fw verify <firmware.bin>
```
