# Roadmap

As fases seguem o [ADR-0006](decisions/ADR-0006-windows-primeiro.md): primeiro o Windows até a 1.0.0, depois o port para Linux. macOS sem previsão.

Ordem: 1 → 2 → 3 → 4, com o 6 em paralelo antes da 1.0.0. O Épico 5 só começa quando houver um Mac. Todos os épicos seguem a premissa de atender só a OBSBOT Tiny original ([ADR-0007](decisions/ADR-0007-premissa-tiny-original.md)).

**Ação pendente do mantenedor:** a busca de marcas nas bases oficiais ([Épico 6](epics/EPIC-6-busca-de-marcas.md)), com guia passo a passo, antes da release 1.0.0.

## Fase 1: Windows 11 → 1.0.0

| Épico | Produto | Status | Resumo |
|---|---|---|---|
| [Épico 0](epics/EPIC-0-adocao-do-harness.md) | Governança | Concluído | Adotar o Harness, varrer o workspace e alinhar a documentação |
| [Épico 1](epics/EPIC-1-runtime-e-ci.md) | P1 + P2 | Concluído (2026-11-09) | Migrar para o .NET 10 (LTS) e criar CI de build e testes no Windows |
| [Épico 2](epics/EPIC-2-windows-1.0.0.md) | P1 + P2 | Proposto | Completar a validação no Windows 11, fechar as pendências técnicas e publicar a 1.0.0 |
| [Épico 6](epics/EPIC-6-busca-de-marcas.md) | Governança | Aceito (2026-11-09), em paralelo; HT: aguardando | Busca de marcas nas bases oficiais (TMview, USPTO, WIPO, INPI) com guia passo a passo; deve terminar antes da publicação da 1.0.0 |

O Épico 1 foi concluído em 2026-11-09, um dia antes do fim do suporte ao .NET 8. O Épico 6 pode correr em paralelo aos Épicos 1 e 2, porque é executado pelo mantenedor, mas a release 1.0.0 depende dele.

## Pesquisa: firmware

| Épico | Produto | Status | Resumo |
|---|---|---|---|
| [Épico 3](epics/EPIC-3-engenharia-reversa-firmware.md) | Pesquisa | Proposto (aguarda a 1.0.0) | Engenharia reversa estática do firmware para viabilizar aprimoramentos e novos usos da câmera; propõe ADR sobre modificação de firmware |

## Fase 2: Linux

| Épico | Produto | Status | Resumo |
|---|---|---|---|
| [Épico 4](epics/EPIC-4-port-linux.md) | P1 + P2 | Proposto (aguarda os Épicos 2 e 3) | Validar o driver e o aplicativo no Linux, regra udev e distribuição `linux-x64` |

## Fase 3: macOS

| Épico | Produto | Status | Resumo |
|---|---|---|---|
| [Épico 5](epics/EPIC-5-port-macos.md) | P1 + P2 | Proposto, sem previsão | Backend macOS, aplicativo e distribuição; bloqueado até haver um Mac |

## Pendências técnicas (entram no Épico 2)

* `GetGimbalPosInfo` (OSC) responde com pan/tilt UVC; o OBSBOT Center lê a atitude do gimbal (`gimbalGetAttitudeInfoR`), em que o pitch tem sinal oposto. Relatório §10.4.
* O significado dos valores dos comandos diretos (por exemplo, modos de IA) vem do nome das funções da `libdev.dll` e ainda precisa ser confirmado na Tiny. Relatório §5.5.
* Efeito não verificável por UVC: `SetHDR`, `SetAutoExposureMode`, `ToggleAILock`, `WakeSleep`, `SetAiMode`, `SetTrackingMode` foram aceitos sem erro, mas falta confirmação visual.

## Matriz de validação

Legenda: ✅ validado em hardware (data) · ⚠️ aceito sem efeito verificável · ❌ não validado · ⏸ Fase 2.

| Função | Windows 11 (Fase 1) | Linux (Fase 2) |
|---|---|---|
| Listar a câmera (VID 3564) | ✅ 2026-10-27 | ⏸ |
| Topologia UVC e localização da XU | ✅ 2026-10-27 (nó 3) | ⏸ |
| Ler controles UVC | ✅ 2026-10-27 | ⏸ |
| Escrever pan/tilt absolutos UVC | ✅ 2026-10-28 | ⏸ |
| Movimento contínuo (UVC `Pan/TiltRelative`) | ✅ 2026-10-28 | ⏸ |
| XU GET (seletores 2, 6, 7) | ✅ 2026-10-27 | ⏸ |
| XU SET seletor 6 (comandos diretos) | ⚠️ aceito (HDR, modo de IA) | ⏸ |
| Mensagem `0xAA` V1 (gimbal por posição) | ✅ 2026-10-28 | ⏸ |
| Anti-flicker, compensação de exposição, obturador, ISO | ✅ 2026-10-28 | ⏸ |
| Servidor OSC (UDP, TCP com prefixo, TCP SLIP) | ✅ 2026-10-28 | ⏸ |
| `opengimbalcam-studio`: vídeo ao vivo e gimbal | ✅ 2026-10-28 | ⏸ |
| `opengimbalcam-studio`: foto e gravação | ❌ (só testes unitários do AVI) | ⏸ |
| Firmware: info/verify/extract | ✅ (arquivo 1.2.6.2) | ⏸ |
