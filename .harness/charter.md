# Carta do projeto

## Objetivo

Esta versão da câmera não tem mais suporte nos drivers atuais do fabricante. O projeto **substitui o driver original da OBSBOT Tiny por uma versão de código aberto** e devolve à câmera suporte completo e atualizado no **Windows 11** e, depois, no **Linux**, sem depender do software do fabricante. O projeto entrega dois produtos:

| # | Produto | Compromisso |
|---|---|---|
| **P1** | **Driver** | Um driver atualizado que permita usar a OBSBOT Tiny em versões atuais do Windows e do Linux. |
| **P2** | **Aplicativo de controle** | Um aplicativo que dê suporte à OBSBOT Tiny e funcione no Windows 11 e no Linux. |

## Premissa de não conflito com o fabricante

O projeto **não quer entrar em conflito com a fabricante da OBSBOT Tiny**. Por isso o driver, o aplicativo de controle e o trabalho sobre firmware têm como alvo **apenas a OBSBOT Tiny original** (`USB\VID_3564&PID_FEF0`). Todos os modelos posteriores do fabricante e, principalmente, as câmeras que ainda têm suporte dele ficam fora, sem exceção ([ADR-0007](decisions/ADR-0007-premissa-tiny-original.md)). O respeito à propriedade intelectual de terceiros segue a [política de PI e marcas](ip-policy.md).

### Frente de pesquisa: firmware

Além dos dois produtos, o projeto faz a **engenharia reversa do firmware da câmera** ([Épico 3](epics/EPIC-3-engenharia-reversa-firmware.md)). O objetivo é possibilitar o aprimoramento da câmera e a liberdade de viabilizar novas possibilidades de uso. Essa frente produz conhecimento e ferramentas; mudanças no firmware da câmera continuam sujeitas ao [ADR-0005](decisions/ADR-0005-sem-gravacao-de-firmware.md).

## O que "driver" significa aqui

A investigação mostrou que a OBSBOT **não tem driver de kernel**: a câmera é UVC/UAC padrão e funciona com os drivers nativos (`usbvideo`/`usbaudio` no Windows, `uvcvideo` no Linux). O que o fabricante chama de "driver" é a `libdev.dll`, uma biblioteca em modo usuário que fala com a câmera pela UVC Extension Unit `{9A1E7291-6843-4683-6D92-39BC7906EE49}`.

Por isso, neste projeto, **o driver (P1) é a versão aberta do driver original: a camada de acesso ao dispositivo, em modo usuário**, que substitui a `libdev.dll`: descoberta da câmera, controles UVC, Extension Unit, comandos diretos e canal de mensagens `0xAA`, mais o que for preciso para o sistema operacional entregar a câmera ao usuário (por exemplo, permissões de dispositivo no Linux). Escrever um driver de kernel só entra no escopo por um novo ADR que demonstre a necessidade. Veja [ADR-0002](decisions/ADR-0002-definicao-de-driver.md).

| Produto | Componentes atuais |
|---|---|
| P1 Driver | `src/OpenGimbalCam.Core`, `src/OpenGimbalCam.Devices`, CLI `ogcctl` (`src/OpenGimbalCam.Cli`) como ferramenta de diagnóstico e automação |
| P2 Aplicativo | `src/OpenGimbalCam.Studio` (`opengimbalcam-studio`, Avalonia), `src/OpenGimbalCam.Osc` (servidor OSC compatível com o OBSBOT Center) |

## Plataformas-alvo e fases

As plataformas são atendidas **em sequência, não em paralelo** ([ADR-0006](decisions/ADR-0006-windows-primeiro.md)):

| Fase | Plataforma | Marco |
|---|---|---|
| **1** | **Windows 11**, nas versões com suporte da Microsoft, x64 | versão **1.0.0** (P1 + P2 no Windows) |
| **2** | **Linux**, distribuições com kernel em suporte e o driver `uvcvideo` (com `UVCIOC_CTRL_QUERY`), x64 | port iniciado só depois da 1.0.0 |
| **3** | **macOS** | sem previsão ([Épico 5](epics/EPIC-5-port-macos.md)); bloqueado até haver um Mac para desenvolvimento e testes |
* Outras arquiteturas (arm64) são desejáveis, não obrigatórias.
* Runtime .NET em versão com suporte do fabricante (LTS). Veja o [roadmap](roadmap.md): o .NET 8 sai de suporte em 2026-11-10.

## Escopo

**Dentro:**

* OBSBOT Tiny, `USB\VID_3564&PID_FEF0`, `ObsbotProductType` 0, protocolo `0xAA` V1 ([ADR-0003](decisions/ADR-0003-escopo-obsbot-tiny.md)).
* Controles UVC padrão, Extension Unit (seletores 2, 6 e 7), gimbal, zoom, campo de visão, imagem, modos de IA da Tiny.
* Vídeo ao vivo, foto e gravação no aplicativo.
* Servidor OSC compatível com o OBSBOT Center, nas rotas que se aplicam à Tiny.
* Leitura e verificação de pacotes de firmware (sem gravação).
* Engenharia reversa estática do firmware da Tiny (SoC, rootfs, modelos de IA, MCUs do gimbal e do yaw).

**Fora:**

* Qualquer modelo OBSBOT além da Tiny original, em especial os que ainda têm suporte do fabricante; protocolo `0xAA` V3 e rotas exclusivas de outros modelos.
* **Gravação de firmware** e qualquer uso dos seletores 1, 3, 4 e 5 da XU ([ADR-0005](decisions/ADR-0005-sem-gravacao-de-firmware.md)), até que um novo ADR, possivelmente resultado do Épico 3, decida outra coisa.
* Serviços de nuvem, conta de usuário, câmera virtual e as funções de mídia do processo `OBSBOT_Center.exe` além de foto e gravação.
* macOS, enquanto o Épico 5 estiver bloqueado.

## Critérios de sucesso

**Fase 1, versão 1.0.0 (Windows 11):**

1. **P1:** com a Tiny conectada, a camada de driver lista a câmera, lê e escreve todos os controles do escopo e move o gimbal, com cada função marcada como *validada em hardware* na coluna Windows da [matriz de validação](roadmap.md#matriz-de-validação).
2. **P2:** o `opengimbalcam-studio` é distribuído como executável `win-x64`, abre o vídeo e controla a câmera, também validado em hardware.
3. Build e testes automatizados passam no Windows, em runtime com suporte.

**Fase 2 (Linux):** os mesmos três critérios na coluna Linux, com distribuição `linux-x64`.
