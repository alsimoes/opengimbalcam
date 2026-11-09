# Investigação: como o "driver" e o firmware OBSBOT funcionam no Windows

Data: 2026-10-27. Câmera usada nos testes ao vivo: **OBSBOT Tiny** (`USB\VID_3564&PID_FEF0`, REV 0409).

> **Aviso de marcas e propriedade intelectual.** Este relatório pertence a um projeto independente, sem afiliação nem endosso da fabricante das câmeras OBSBOT ou dos demais titulares citados (Microsoft, SigmaStar, Qt, OBS Studio, TouchOSC, Tencent, Arm e outros). As marcas aparecem só para identificar o que foi analisado. A análise foi estática e feita para fins de interoperabilidade com a OBSBOT Tiny original, a única câmera no escopo do projeto. Nenhum material do fabricante é redistribuído: o relatório cita só nomes de arquivos, hashes, endereços e trechos mínimos. Informações sobre outros modelos (por exemplo, a tabela de PIDs da §6.3) aparecem só porque estão nas tabelas analisadas e não são objeto do projeto. Veja [TRADEMARKS.md](../TRADEMARKS.md).
>
> **Documento de referência técnica.** Objetivo, escopo, regras e pendências do projeto são governados pelo Harness ([`.harness/`](../.harness/README.md)). O estado de validação atualizado está na [matriz de validação](../.harness/roadmap.md#matriz-de-validação) e as pendências, no [roadmap](../.harness/roadmap.md).

## 1. Resumo

1. **Não existe driver de kernel OBSBOT.** O instalador não contém `.inf`, `.sys` nem `.cat`. No Windows a câmera é um dispositivo USB composto, atendido só por drivers nativos da Microsoft: `usbccgp` (composto), `usbvideo` (UVC) e `usbaudio` (UAC).
2. O "driver" é o **OBSBOT Center**, um aplicativo em modo usuário (Qt). O controle da câmera fica na **`libdev.dll`**, que fala com a câmera por **DirectShow + Kernel Streaming**. Ela abre o filtro de captura, acha a **UVC Extension Unit** (XU) de GUID `{9A1E7291-6843-4683-6D92-39BC7906EE49}` e lê/escreve controles de **60 bytes** com `IKsControl::KsProperty`.
3. Por essa XU passam **7 seletores**: comandos diretos (seletor 6), um canal de mensagens com quadro `0xAA` e CRC-16/USB (seletor 2), presets do gimbal (7) e a máquina de atualização de firmware (1, 3, 4, 5). O seletor 3 executa comandos shell no Linux da câmera.
4. O **firmware** é um pacote com 3 componentes: (a) a imagem Linux do SoC **SigmaStar SSC339G**, em um contêiner `AMAC` com 8 partições (U-Boot, kernel 4.9, rootfs UBIFS...); (b) o firmware do MCU do gimbal; (c) o firmware do MCU do motor de yaw. Cada parte tem MD5 e CRC-32, e todos conferem.
5. Este repositório traz uma reimplementação aberta em C#/.NET 8 que repete o caminho da `libdev.dll`. Ela tem duas partes, ambas para Windows e Linux: a CLI `ogcctl` e a interface `opengimbalcam-studio` (Avalonia). A versão Windows foi validada ao vivo na câmera conectada.
6. O aplicativo **OBSBOT Center** roda em dois processos (interface Qt e um fork do OBS Studio) ligados por ZeroMQ. Ele expõe um **servidor OSC** na porta 16284, que também reimplementamos (§10).

## 2. Material analisado

| Arquivo | SHA-256 |
|---|---|
| `Obsbot_Center_OA_E_Win_x64_2.0.14.64_release.exe` | `b913e3d5541b35a1eb27fca4a73616ebc1483d91e86f8a15ac4c2f1be257a410` |
| `Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin` | `0585093683ddd0cc24f845d2fa154a3a52a70e8bd95a2dd2fa4861c272d3aef2` |
| `OBSBOT Tiny 4K User Manual_EN.pdf` | `474e03c25641ef4a88dea6970a2990b47bbed9b0cd89cb0707a25e9a55567c8a` |
| `libdev.dll` (extraída do instalador, ainda com o filtro do Inno) | `89eea38c960aaa5c3357f5fd06b559cac1c51fcf945db08ab0dbaf6e7c3ac71a` |
| `libdev.dll` (filtro desfeito, igual à instalada, §10.1) | `64adacf1a560f05a1f2463fc2e6bc2f35ee37c50762af051ca69045b5ae0376e` |

Toda a análise foi **estática**. Nenhum binário do fabricante foi executado. Os scripts usados estão em [`tools/re/`](../tools/re) (veja a seção 9).

## 3. Como o Windows enxerga a câmera (evidência ao vivo)

`Get-PnpDevice` / `Get-PnpDeviceProperty` com a câmera conectada:

| Instância | Descrição | INF | Serviço | Fornecedor |
|---|---|---|---|---|
| `USB\VID_3564&PID_FEF0\5&1F03AB54&0&1` | USB Composite Device ("OBSBOT Tiny") | `usb.inf` | `usbccgp` | Microsoft |
| `USB\VID_3564&PID_FEF0&MI_00\…` | OBSBOT Tiny Camera (classe `0E`/`03`, UVC) | `usbvideo.inf` | `usbvideo` | Microsoft |
| `USB\VID_3564&PID_FEF0&MI_02\…` | OBSBOT Tiny Audio (classe `01`, UAC) | `wdma_usb.inf` | `usbaudio` | Microsoft |

A topologia Kernel Streaming que o `usbvideo.sys` publica para a câmera (saída de `ogcctl info`):

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

A tabela completa (28 comandos, cada um com a função de origem e o endereço da chamada) está em [`DirectCommands.cs`](../src/OpenGimbalCam.Core/Camera/DirectCommands.cs) e aparece em `ogcctl cmds`. Nomes vindos dos exports, por exemplo `AiWorkModeType`, `AiTrackSubModeType` e `DevGestureParaType`, indicam o significado. **Os valores numéricos de cada modo não foram confirmados por modelo.**

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

### 6.3 Tabelas de IDs e versões do protocolo

As funções `…R` da libdev montam na pilha uma requisição com IDs **lógicos** e chamam o despachante (RVA `0x8160`):

| Offset | Campo |
|---|---|
| `+0x0` | comprimento no fio (12 + payload) |
| `+0x4` | tipo lógico (o despachante grava `r8 ^ 1`; as funções `…R` passam `r8 = 0`, logo tipo lógico 1 → tipo de fio 0) |
| `+0x8` | sequência |
| `+0xA` | remetente lógico (1 = câmera, 2 = gimbal, 3 = IA) |
| `+0xC` | destinatário lógico, sempre `0x0B` |
| `+0xE` / `+0x10` | id do comando / conjunto |
| `+0x14` | versão do protocolo do aparelho (`[dev+0x1668]`) |
| `+0x18` | transporte (`[dev+0x3C]`; 0 = UVC, que exige mensagens de até 60 bytes) |
| `+0x1C` | payload |

**Versão do protocolo.** `sub_167F10` converte o PID em `ObsbotProductType` (`[dev+0x680]`). A versão é 3 quando o tipo está na máscara `0x24D3F1C` ({2, 3, 4, 8, 9, 10, 11, 12, 13, 16, 18, 19, 22, 25}), senão 1. Nas mensagens V3 o despachante usa outro serializador (RVA `0x1001B0`) e outro finalizador (RVA `0xFF8D0`), com cabeçalho e tabelas diferentes. Como o projeto atende só à Tiny (V1), a V3 fica fora do escopo; a tabela abaixo serve de registro.

| PID | tipo | PID | tipo | PID | tipo |
|---|---|---|---|---|---|
| FEF0 (Tiny) | 0 | FEF8 | 2 | FF00 | 26 |
| FEF1 | 7 | FEF9 | 3 | FF01 | 9 |
| FEF2 | 5 | FEFA | 8 | FF02, FFFE | 18 |
| FEF3 | 6 | FEFB | 10 | FF04 | 19 |
| FEF4 | 1 | FEFC | 11 | FF06 | 25 |
| FEF6 | 4 | FEFD | 16 | F001 | 22 |
| FEF5, FEF7, FF03, FF05 | 26 | FEFE / FEFF | 13 / 12 | outros | 26 |

Os exemplos TouchOSC abrem a página da Tiny para os códigos 0, 1 e 6, os tipos dos PIDs FEF0, FEF4 e FEF3. Por isso o `DeviceInfo` envia 0, o tipo da Tiny.

**Tabelas V1** (serializador RVA `0xFFF40`, `std::map`s montados em construtores estáticos):

* dispositivo (`0x217F28`): `{0:0, 1:1, 2:2, 3:3, 4:4, 5:5, 6:6, 7:8, 8:11, 9:12, 10:13, 11:14, 12:15}`; o destinatário `0x0B` vira 14;
* conjunto (`0x217E28`): `{0:0, 1:1, 2:2, 3:3, 4:4, 5:8, 6:13}`;
* comandos por conjunto (mapa de mapas em `0x217EF8`):
  * set 1 (`0x217E58`): `{0:0x1CD, 1:0x3C4, 2:0x3C2, 3:0x3D2, 4:0x1D2}`;
  * set 2 (`0x217F08`): `{0:0x01, 2:0x04, 3:0x06, 4:0x0A, 5:0x22, 6:0x23}`;
  * set 3 (`0x217ED8`): `{0x00:0x23, 0x07:0x35, 0x08:0x36, 0x0A:0x3A, 0x0D:0x51, 0x0E:0x66, 0x0F:0x67, 0x10:0x90, 0x11:0x91, 0x13:0x92, 0x15:0x94, 0x5A:0x93, 0x64:0xEE}`;
  * set 5 (`0x217EE8`): `{0:0x01, 3:0x10, 4:0x60, 5:0x61, 6:0x64, 7:0x65, 8:0x63, 9:0x66, 10:0x02, 11:0x69, 12:0x6A, 13:0x6B}`.

O byte 3 leva `0x10 |` tipo de fio (lógico 0 → 2, 1 → 0, 2 → 1). O finalizador V1 só grava um CRC-8 no byte 8 quando o nibble alto da palavra dos bytes 1–2 é 2, o que nunca acontece em V1. Esse CRC-8 (`sub_1630F0`) usa uma tabela de polinômio `0x07` com uma entrada errada (`[0x47] = 0x0D` em vez de `0xD2`).

Comandos fora dessas tabelas, como `aiSetZoneTrackStateR` (set 3 id `0x5D`), `aiTrgZonePresetR` (`0x1D`), `aiSetControlParaR` (`0x84`) e `cameraSetWhiteBalanceSetting` (set 1 id `0x0B`), não têm id V1. A libdev não consegue enviá-los à Tiny.

[`RemoV1.cs`](../src/OpenGimbalCam.Core/Protocol/RemoV1.cs) reproduz as tabelas e as requisições usadas pelas rotas OSC:

| Função libdev | Lógico (remetente, set, id) | Fio V1 | Payload V1 |
|---|---|---|---|
| `cameraSetDevRunStatusR` | 1, 1, 2 | `0x3C2` | `[1, estado]` (1 = acordar, 3 = dormir) |
| `gimbalSetSpeedPositionR` | 2, 2, 3 | `0x006` | 6 × int16 LE de valor × 100 (truncado): velocidades roll/pitch/yaw, depois posições roll/pitch/yaw |
| `aiSetTrackingModeR` | 3, 3, `0x11` | `0x091` | `[AiVerticalTrackType]` |
| `aiSetTargetSelectR(true)` / `(false)` | 3, 3, `0x0F` / `0x0E` | `0x067` / `0x066` | `[0]` / vazio |

**Validado ao vivo na Tiny** (2026-10-28, câmera transmitindo): `SetGimMotorDegree 30 20 0` levou o gimbal de (−20°, 3°) a **(20°, 0°)**. Isso confirma, de uma vez, as tabelas de dispositivo, conjunto e comando, o cabeçalho, o CRC e o payload. O pitch do gimbal tem sinal oposto ao tilt UVC: pitch +5 resulta em tilt −4.

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

## 8. O que o `ogcctl` implementa

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
| Servidor OSC do OBSBOT Center | `ogcctl osc` / `opengimbalcam-studio` | §10 |
| Linux | V4L2 + `UVCIOC_CTRL_QUERY` | §11 |

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
python tools/re/funcmap.py OBSBOT_Main.exe "^/OBSBOT/WebCam/"   # handlers OSC (Git Bash: MSYS_NO_PATHCONV=1)
python tools/re/tosc_dump.py "OBSBOT Sample-UDP.tosc" --scripts  # mensagens dos layouts TouchOSC
ogcctl fw verify <firmware.bin>
```

## 10. O OBSBOT Center instalado e o protocolo OSC

Análise do aplicativo instalado em `C:\Program Files\OBSBOT Center` (versão 2.0.14.64, Inno Setup 6.2.2, instalado em 2026-10-27). Também aqui a análise foi **estática**, com `tools/re/funcmap.py` (mapeia as funções que referenciam uma string) e `tools/re/tosc_dump.py`. Não executamos nenhum binário do fabricante.

### 10.1 Arquivos

| Arquivo | Versão | SHA-256 |
|---|---|---|
| `bin\libdev.dll` | — | `64adacf1a560f05a1f2463fc2e6bc2f35ee37c50762af051ca69045b5ae0376e` |
| `bin\OBSBOT_Main.exe` | 2.0.14 | `407d853bb5938622d1af1c6a1059bf4257de79d072f46ea2735b53c25c1430f2` |
| `bin\OBSBOT_Center.exe` | 2.0.10 | `159096fec64cfc4f958612b0e08e2ad8b6da1b6a0fc8cf9c335c30bdb128fe79` |

A `libdev.dll` instalada é **idêntica byte a byte** à cópia que recortamos do instalador e passamos pelo `inno_unfilter.py` (§4). Isso confirma que a reversão do filtro CALL/JMP está correta. O hash `89eea38c…` da §2 é o da cópia ainda com o filtro.

### 10.2 Dois processos

* **`OBSBOT_Main.exe`** (92 MB) é a interface Qt 6.5.2 e faz todo o controle de dispositivo, via `libdev.dll` (§5). Inclui ainda:
  * o servidor OSC (liblo ligado estaticamente);
  * Bluetooth, serial, WebSocket e Wi-Fi;
  * os serviços de nuvem: `remo-api.obsbot.com`, analytics `sensors_analytics`, Tencent TRTC/XMagic.
* **`OBSBOT_Center.exe`** é o "processo de mídia", um fork do **OBS Studio**:
  * a `rm.dll` faz o papel da `libobs`, e suas mensagens de log são as do OBS ("audio settings reset", "Loading module");
  * `prev_ver 503316482` = `0x1E000002`, ou seja, OBS 30.0.2;
  * os plugins ficam em `data\rmfunc\`.
* Os dois processos conversam por **ZeroMQ** (REQ/REP e PUB/SUB) em localhost. As portas chegam pela linha de comando (`-main_rep … -main_pub … -media_rep … -media_pub … -virtual_rep … -virtual_pub …`) e ficam gravadas na seção `[Zmq]` de `%APPDATA%\OBSBOT_Center\global.ini`.
* **Câmera virtual**:
  * filtro DirectShow `{30FDC141-8E09-9817-F45B-343BD296A8D5}` ("OBSBOT Virtual Camera");
  * arquivos `data\rmfunc\indshow\rm-virtualcam-module{32,64}.dll`, registrados por `virtualcam-install.bat`;
  * aparece no `ogcctl list` sem VID/PID.
* **Firewall**: o instalador cria regras de entrada que liberam TCP e UDP em qualquer porta, nos perfis Privado e Público, para os dois executáveis.
* **Logs** em `%APPDATA%\OBSBOT_Center\centerLogs`: caminho do dispositivo, firmware "1.0.6.2" e a identificação `Obsbot_tiny_2P OA_E SSC336Q …`.
* O `global.ini` guarda **em texto puro** um token de sessão (`UserLoginToken`, um JWT). Ele não é reproduzido aqui.

### 10.3 Servidor OSC

**Configuração e registro**

* A configuração fica em `global.ini`, seção `[SoftSetting]`:
  * `OSCConnectMethod`: 0 = UDP, 1 = TCP. A função `0x2588B0` converte esses valores em `LO_UDP`/`LO_TCP` do liblo.
  * `OSCReceivePort`: padrão **16284**.
  * `OSCHostIp`.
* As respostas vão para quem enviou a mensagem.
* O construtor do servidor (RVA `0x10E590` em `OBSBOT_Main.exe`) registra **67 métodos** `/OBSBOT/WebCam/…`. A maioria tem typespec `i`. As exceções:

| Método | Typespec |
|---|---|
| `SetZoomSpeed` | `ii` |
| `SetGimMotorDegree` | `iii` / `fff` |
| `SetGimMotorDegreeEx` | `iiii` / `ifff` |
| `SetPTZValue` | `iii` |
| `SetGimWhiteBalanceShift` | `ff` |
| `SetRBGain` | `ii` |

**Endereçamento.** É igual em todos os handlers. Se a mensagem traz exatamente os argumentos de valor, ela vale para o dispositivo selecionado. Se traz um `int` a mais na frente, esse `int` é o índice do dispositivo (0..3).

**Gimbal.** Handlers: `SetGimbalUp` `0x111980`, `Down` `0x111E40`, `Left` `0x112300`, `Right` `0x1127F0`.

1. A velocidade `s` é limitada a 0..100 e convertida em `v = min(s·0,01; 1)`.
2. A direção define o eixo: Up = tilt +v, Down = tilt −v, Left = pan −v, Right = pan +v.
3. Nos tipos de produto {5, 6, 10, 13, 25}, o handler chama `cameraSetPanTiltRelative(pan, tilt)` da libdev (RVA `0x11BF0`). Essa função:
   * limita os valores a [−1, 1];
   * multiplica pelo máximo da faixa UVC `PanRelative`/`TiltRelative` e trunca;
   * envia `09 04 dir vel dir vel` no seletor 6 (§5.5).
4. Os modelos do canal `0xAA` usam `aiSetGimbalSpeedCtrlR(v·SpeedRef, 0, 0)`, com `SpeedRef = 30`.

**Outros comandos**

| Método | Efeito na libdev |
|---|---|
| `SetZoom z` | `cameraSetZoomAbsoluteR(1 + z·0,01, −1)`. `SetZoomMax` envia 2,0 e `SetZoomMin` envia 1,0 |
| `SetView` 0/1/2 | `cameraSetFovU`: `04 01 <fov>`, seletor 6 |
| `SetMirror` | `cameraSetImageFlipHorizonU`: `14 01 v` |
| `ResetGimbal` (`0x1107B0`) | `cameraSetPanTiltAbsolute(0, 0)` |
| `WakeSleep` | `cameraSetDevRunStatusR`, pelo canal `0xAA` |

**Respostas**

| Resposta | Typespec | Conteúdo |
|---|---|---|
| `ConnectedResp` | `,i` | sempre 1 |
| `DeviceInfo` | `,isisisisiii` | quatro pares (conectado, nome), índice selecionado, estado e código do produto. Nos exemplos TouchOSC, os códigos 0, 1 e 6 abrem a página da Tiny |
| `ZoomInfo` | `,ii` | zoom 0..100 e visão: 0/1/2 quando o zoom é 0/5/15, senão −1 |
| `GetGimbalPosInfoResp` | `,ii` | posição do gimbal |

O aplicativo traz dois layouts TouchOSC de exemplo em `data\ctrl\touchosc\*.tosc` (XML comprimido com zlib). O script Lua deles pede `GetGimPosInfo`, que **não existe** no binário: o método registrado é `GetGimbalPosInfo`.

### 10.4 O que foi reimplementado

O `ogcctl osc serve` e o painel OSC do `opengimbalcam-studio` implementam o mesmo protocolo em UDP e TCP. No TCP, aceitam prefixo de tamanho int32 e SLIP e detectam qual dos dois pelo primeiro byte.

Das 57 rotas `/OBSBOT/WebCam/…` mapeadas:

O projeto atende só à **OBSBOT Tiny** (PID `FEF0`, tipo de produto 0, protocolo V1). Os ramos que o OBSBOT_Main tem para outros modelos não foram reproduzidos.

* **45 estão implementadas**. O valor de cada uma segue o que o handler do OBSBOT_Main faz com ele na Tiny, conforme a tabela abaixo.
* **12 não têm efeito na Tiny** e só ficam registradas no log, com o motivo:
  * as 6 rotas `Meet/…`;
  * `SetAutoFocusMode`, porque a Tiny tem foco fixo e não expõe controle UVC de foco;
  * `SetGimWhiteBalanceShift`, `SetRBGain`, `SetZoneTrack`, `TriggerZonePreset` e `SetTrackingSpeed`, que usam comandos que só existem no protocolo `0xAA` V3 (§6.3). Para a Tiny, nem o OBSBOT Center consegue enviá-las.

`ogcctl osc routes` lista todas as rotas, com o RVA e a chamada de origem.

Rotas acrescentadas depois da análise dos handlers e das tabelas estáticas do OBSBOT_Main:

| Rota | O que o OBSBOT Center faz | Implementação |
|---|---|---|
| `SetAntiFlicker v` | só com exposição automática e `v` ≤ 2: `cameraSetAntiFlickR`, que na UVC é `PowerlineFrequency` | UVC `PowerlineFrequency` (0 = desligado, 1 = 50 Hz, 2 = 60 Hz) |
| `SetAutoExposureMode v` | só com exposição automática: `cameraSetFaceAER(0/1)` | XU 6 cmd `03` |
| `SetExposureCompensate v` | só com exposição automática: `v/10` = EV, procurado numa tabela de 19 passos de 1/3 EV (construtor RVA `0x8C600`) cujo índice vai para `cameraSetPAEEvBiasR` | UVC `BacklightCompensation` = round(3·EV) + 9 (a Tiny tem faixa 0..18, padrão 9) |
| `SetHDR v` | `cameraSetWdrR`: XU 6 `01 01 (v≠0)`; mensagem V3 para os tipos 4/11/16/22 | XU 6 cmd `01` |
| `SetISO v` | tabela `(i, i·100)`, i = 1..128 (RVA `0x8C910`), e `cameraSetMAEIsoR(i)`, que na UVC é `Gain` | UVC `Gain` = ISO/100. A Tiny ignora o valor com exposição automática |
| `SetShutterSpeed v` | `v` = denominador (1/v s), tabela de 13 valores de 6400 a 2 (RVA `0x8CDD0`), e `cameraSetExposureAbsolute(…, manual)` | exposição manual: log2(1/v) no DirectShow, 10000/v no V4L2 |
| `WakeSleep v` | `cameraSetDevRunStatusR(v == 0 ? 3 : 1)` | mensagem `0xAA`; o estado volta no `DeviceInfo` |
| `SetGimMotorDegree s p t` | (velocidade 0..90, pan ±129, pitch ±59) → `gimbalSetSpeedPositionR(0, pitch, pan, s, s, s)` | mensagem `0xAA` |
| `SetGimMotorDegreeEx id s p t` | igual, com o aparelho identificado pelo número de série e valores float | o primeiro argumento texto usa o aparelho selecionado |
| `SetPTZValue p t z` | `aiSetTargetSelectR(false)`; tempo = maior deslocamento/20 °/s (até 3 s); velocidades = deslocamento/tempo; zoom `1 + z/100` | mesmo cálculo, com a posição atual lida por UVC |
| `Tiny/SetAiMode v` | tabela de saltos `0x117164`: 0 → (0,0), 1..5 → (2, 0..4), 6 → (5,0), 7 → (4,0), 8 → (3,0), 9 → (1,0) | XU 6 cmd `16` (`ai-mode`) |
| `Tiny/SetTrackingMode v` | 1 → 0, 2 → 2, 0 → 1: `aiSetTrackingModeR` | mensagem `0xAA` |
| `Tiny/ToggleAILock v` | `aiSetTargetSelectR(v == 1)` | mensagem `0xAA` |
| `Tiny/Get*Info` | lidos da interface do OBSBOT Center; respostas `AiTrackingInfo ,i` e `PresetPositionInfo ,isisis` | estado guardado pela ponte (último valor enviado); presets com nomes genéricos |
| `PCSnapshot`, `SetPCRecording`, `SetHighfpsRecording` | feitos pelo processo de mídia `OBSBOT_Center.exe` | no `opengimbalcam-studio`: foto (JPEG) em Imagens/OpenGimbalCam Studio e gravação AVI MJPEG em Vídeos/OpenGimbalCam Studio, a partir do vídeo aberto. No `ogcctl osc serve`, que não abre vídeo, só ficam registradas no log |

**Validado ao vivo** (OBSBOT Tiny): `Connected`, `GetDeviceInfo`, `GetZoomInfo`, `GetGimbalPosInfo`, `SetGimbalUp 0` e `SetZoom 0` (estes dois sem efeito), por UDP, por TCP com prefixo de tamanho e por TCP com SLIP.

Em 2026-10-28, com a câmera transmitindo, os valores foram conferidos por leitura UVC depois de cada comando:

* `SetAntiFlicker 1` → `PowerlineFrequency` 1;
* `SetExposureCompensate 0` → `BacklightCompensation` 9;
* `SetShutterSpeed 100` → exposição −7, manual;
* `SetISO 400`, com exposição manual → `Gain` 4;
* `SetGimMotorDegree 30 20 0` → (20°, 0°);
* `SetPTZValue -10 5 0` → (−10°, −4°).

`SetHDR`, `SetAutoExposureMode`, `ToggleAILock 0`, `WakeSleep 1`, `SetAiMode 0` e `SetTrackingMode 1` foram aceitos sem erro, mas o efeito delas não pode ser lido por UVC. Depois do teste, os valores originais foram restaurados.

**Teste de movimento real** (2026-10-28, com autorização do usuário e a câmera transmitindo vídeo):

* Com a câmera **em suspensão** (sem nenhum programa usando o vídeo), ela aceita todos os comandos, mas o gimbal não se mexe e as posições lidas ficam em 0.
* Acordada, o pan absoluto UVC funciona (`set pan 40`: 19° → 36° → 39°).
* O comando relativo da XU (seletor 6, `09 04 …`) é **aceito, mas ignorado pela Tiny**. Isso bate com o binário: o OBSBOT Center só o envia aos tipos de produto {5, 6, 10, 13, 25}, e a Tiny vai pelo canal `0xAA`.
* O controle UVC padrão **CT_PANTILT_RELATIVE** (`PanRelative`/`TiltRelative`) move a Tiny:

| Valor enviado | Efeito |
|---|---|
| `PanRelative +20` | pan absoluto 39° → 27° |
| `PanRelative −20` | pan absoluto 27° → 39° |
| `TiltRelative +20` | tilt −2° → 10° |
| `TiltRelative −20` | tilt 9° → −3° |

O sinal de `PanRelative` é, portanto, o oposto do pan absoluto. O `ObsbotCamera.SetPanTiltSpeed` usa esse controle quando a câmera o oferece, invertendo o sinal do pan, e recorre à XU só quando ele não existe. Validado por OSC: `SetGimbalRight 20` aumentou o pan (8° → 30°), `Left` diminuiu, `Down` baixou o tilt e `ResetGimbal` voltou a (0, 0).

**Pendências** (acompanhadas no [roadmap do Harness](../.harness/roadmap.md))

* ~~Confirmar visualmente o sentido do pan~~: confirmado pelo usuário em 2026-10-28 com o vídeo no `opengimbalcam-studio` (▶ gira para a direita).
* ~~O código de produto 0 para a Tiny é uma aproximação~~: 0 é o `ObsbotProductType` da Tiny (§6.3).
* `GetGimbalPosInfo` responde com pan/tilt UVC. O OBSBOT Center lê a atitude do gimbal (`gimbalGetAttitudeInfoR`), em que o pitch tem sinal oposto ao tilt.

## 11. Linux

A câmera é UVC padrão, e no Linux o driver `uvcvideo` a atende. O controle fica disponível sem nenhum software da OBSBOT:

| Windows (libdev) | Linux (`ogcctl`) |
|---|---|
| `ICreateDevEnum` + `DevicePath` (VID/PID) | `/sys/class/video4linux/videoN` (só os nós com `index` 0), mais `idVendor`/`idProduct` do dispositivo USB pai |
| `IAMCameraControl` / `IAMVideoProcAmp` | `VIDIOC_QUERYCTRL` / `VIDIOC_G_CTRL` / `VIDIOC_S_CTRL`. Pan/tilt vêm em segundos de arco e são convertidos para graus. O modo automático usa controles separados (`EXPOSURE_AUTO`, `AUTO_WHITE_BALANCE`, `FOCUS_AUTO`) |
| nó KS da XU, achado com SETSUPPORT | `bUnitID` da XU, lido nos descritores USB crus (`…/descriptors`): interface VideoControl, subtipo `0x06`, GUID `{9A1E7291-…}` |
| `KsProperty(KSP_NODE)` com 60 bytes | `UVCIOC_CTRL_QUERY` (`_IOWR('u', 0x21, struct uvc_xu_control_query)`) com `UVC_GET_CUR`/`UVC_SET_CUR`. O tamanho vem de `UVC_GET_LEN`, porque o `uvcvideo` exige o tamanho exato do controle |

Diferenças de unidade:

* A exposição no V4L2 é em unidades de 100 µs; no DirectShow, em log2 de segundos.
* O máximo de `PAN_SPEED` vem sem extensão de sinal.

O backend Linux **ainda não foi testado com a câmera**. Para diagnóstico, use `ogcctl info`. Abrir `/dev/videoN` exige permissão de leitura e escrita, o que normalmente significa pertencer ao grupo `video`.
