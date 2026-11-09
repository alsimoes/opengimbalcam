# Base de conhecimento

Índice do conhecimento técnico do projeto. Os documentos de referência ficam em [`.docs/`](../../.docs/); este índice diz onde está cada assunto e o que já foi validado em hardware.

## Fontes

| Documento | Conteúdo |
|---|---|
| [`.docs/investigacao.md`](../../.docs/investigacao.md) | relatório da engenharia reversa (2026-10-27/28): instalador, `libdev.dll`, Extension Unit, protocolo `0xAA`, firmware, OBSBOT Center, OSC, Linux |
| `.docs/original-driver/` | arquivos do fabricante usados na análise (**não versionados**; hashes na §2 do relatório) |
| [`tools/re/`](../../tools/re) | scripts que reproduzem a investigação (§9 do relatório) |

## Onde está cada assunto

| Assunto | Relatório | Código |
|---|---|---|
| Como o SO enxerga a câmera (sem driver de kernel) | §1, §3 | — |
| Extension Unit: descoberta, GET/SET de 60 bytes | §5.2, §5.3 | `OpenGimbalCam.Core/Camera/ObsbotXu.cs`, `OpenGimbalCam.Devices/Windows/KsExtensionUnit.cs` |
| Mapa de seletores 1–7 | §5.4 | `OpenGimbalCam.Core/Camera/ObsbotXu.cs` |
| Comandos diretos (seletor 6) | §5.5 | `OpenGimbalCam.Core/Camera/DirectCommands.cs` |
| Presets do gimbal (seletor 7) | §5.6 | — |
| Canal `0xAA`, CRC-16/USB, tabelas V1 | §6 | `OpenGimbalCam.Core/Protocol/RemoMessage.cs`, `RemoV1.cs`, `Crc16Usb.cs` |
| Pacote de firmware | §7 | `OpenGimbalCam.Core/Firmware/FirmwarePackage.cs` |
| OBSBOT Center e protocolo OSC | §10 | `OpenGimbalCam.Osc/` |
| Backend Linux (V4L2, `UVCIOC_CTRL_QUERY`) | §11 | `OpenGimbalCam.Devices/Linux/` |

## Fatos validados em hardware

Câmera: OBSBOT Tiny, `USB\VID_3564&PID_FEF0`, REV 0409. A matriz completa por plataforma está no [roadmap](../roadmap.md#matriz-de-validação).

* A XU responde no nó KS 3 (Windows).
* Em suspensão (sem vídeo aberto) a câmera aceita comandos de gimbal mas não se move.
* O comando relativo da XU (seletor 6, `09 04 …`) é aceito e **ignorado** pela Tiny. O movimento contínuo usa os controles UVC `PanRelative`/`TiltRelative`, cujo sinal de pan é o oposto do pan absoluto.
* Aumentar o pan absoluto gira a imagem para a direita (confirmado visualmente em 2026-10-28).
* O pitch do gimbal (canal `0xAA`) tem sinal oposto ao tilt UVC.
* Com exposição automática, a Tiny ignora `Gain` (ISO).

## Dicas práticas

* No Git Bash, use `MSYS_NO_PATHCONV=1` para argumentos que começam com `/OBSBOT`.
* Nesta máquina de desenvolvimento, iniciar o apphost `ogcctl.exe` pelo harness de agentes executou o programa duas vezes; `dotnet ogcctl.dll` executa uma vez.
