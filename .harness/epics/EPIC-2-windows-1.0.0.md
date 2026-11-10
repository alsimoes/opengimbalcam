# Épico 2: Versão 1.0.0 no Windows 11

* **Status:** Em andamento (aceito pelo mantenedor em 2026-11-09).
* **Produto:** P1 Driver + P2 Aplicativo · **Fase:** 1 (Windows)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Publicar a versão 1.0.0: driver aberto e `opengimbalcam-studio` completos e validados no Windows 11, substituindo o software original da OBSBOT para a Tiny.

## Critérios de aceite

- [ ] Driver, CLI, ponte OSC e aplicativo só operam a OBSBOT Tiny original (VID 3564, **PID FEF0**); qualquer outro dispositivo do fabricante é ignorado ([ADR-0007](../decisions/ADR-0007-premissa-tiny-original.md)). Hoje o código aceita qualquer câmera com VID 3564.
- [x] Decisão do [ADR-0008](../decisions/ADR-0008-nomes-dos-produtos.md) (nomes sem a marca do fabricante) tomada e aplicada em 2026-11-09: OpenGimbalCam, repositório `alsimoes/opengimbalcam`.
- [ ] Busca de marcas nas bases oficiais concluída ([Épico 6](EPIC-6-busca-de-marcas.md)), sem resultado vermelho.
- [ ] Aplicativo e artefatos de release levam o aviso de marcas (tela "Sobre" e pacote).
- [ ] Gravação do Studio com áudio do microfone da Tiny, sincronizado com o vídeo (decisão do mantenedor, 2026-11-09).
- [ ] Coluna Windows da [matriz de validação](../roadmap.md#matriz-de-validação) sem ❌; cada ⚠️ resolvido ou aceito explicitamente pelo mantenedor.
- [ ] Pendências técnicas do roadmap resolvidas ou adiadas por decisão registrada.
- [ ] Executável autocontido `win-x64` gerado pela CI (Épico 1), com instruções de instalação e uso.
- [ ] Release 1.0.0 publicada, com notas de versão.

## Tarefas

- [x] [T2] Restringir driver, CLI, ponte OSC e app ao PID FEF0 (ADR-0007), com testes (`TinyIdentity`, 12 casos de teste; 2026-11-09).
- [ ] [HT] Validar o Studio (vídeo, "Sobre", foto, gravação) e confirmar visualmente os comandos ⚠️ (HDR, exposição, modos de IA, trava de IA, rastreamento, suspensão). HT: em andamento (primeira rodada parcial em 2026-11-09). Guia: [HT-2.1](#ht-21-validar-a-câmera-no-windows).
- [x] [HT] Escolher a licença do projeto: MIT, em 2026-11-09 ([ADR-0012](../decisions/ADR-0012-licenca-mit.md), `LICENSE`, linha na tela "Sobre"). Guia: [HT-2.2](#ht-22-escolher-a-licença).
- [ ] [T3] `GetGimbalPosInfo`: decidir entre pan/tilt UVC e atitude do gimbal.
- [ ] [T3, decisão HT] Definir o formato de distribuição no Windows (zip autocontido ou instalador) por ADR.
- [x] [T1] Versionar os projetos (`0.x` até a release, depois `1.0.0`): `0.9.0` em `Directory.Build.props`, `ogcctl --version`. Feito pelo mesmo agente T2, por economia.
- [x] [T2] Tela "Sobre" no `opengimbalcam-studio` com o aviso de marcas. A conferência visual fica no item A1 da [HT-2.1](#ht-21-validar-a-câmera-no-windows).
- [ ] [T3, decisão HT] Avaliar a migração do Avalonia 11.3 para a 12.x (vinda do Épico 1).
- [ ] [T2] Botão no Studio para restaurar todos os controles de imagem ao padrão da câmera (pedido do mantenedor na HT-2.1, 2026-11-09).
- [ ] [T3 desenho, T2 implementação] Gravação com áudio (obrigatória na 1.0.0, decisão do mantenedor em 2026-11-09): capturar o microfone da Tiny (UAC, WASAPI no Windows) e gravar PCM no AVI junto com o MJPEG. Implementado em 2026-11-09 ([ADR-0011](../decisions/ADR-0011-audio-na-gravacao.md), Aceita); falta validar na câmera (HT-2.1, A3).
- [ ] [T2] Corrigir: o processo `opengimbalcam-studio` continua vivo, sem janela, depois que a janela é fechada, e trava as DLLs no build seguinte. Suspeita: thread não-background (por exemplo, o `WorkerThread` do DirectShow ou o servidor OSC). Visto em 2026-11-09.

## Achados

* **Sem arquivo LICENSE** (resolvido em 2026-11-09): o mantenedor escolheu a MIT na HT-2.2 ([ADR-0012](../decisions/ADR-0012-licenca-mit.md)).
* A filtragem por PID acontece na enumeração das duas plataformas, então a ponte OSC e o Studio herdam a restrição sem código próprio.
* **HT-2.1, primeira rodada (2026-11-09), com a build anterior ao PR #5:** os controles de imagem (matiz, exposição etc.) e os controles da câmera no Studio funcionaram. A tela "Sobre" não foi encontrada porque o PR #5, que a cria, ainda não estava no `master`. Ficam pendentes: A1 a A3 com a build nova e B1 a B6.
* O mantenedor pediu a interface em inglês como trabalho futuro: virou o [Épico 7](EPIC-7-interface-em-ingles.md).
* **HT-2.1, foto e gravação (2026-11-09, build anterior ao PR #5):** a foto funcionou (A2). A gravação gera vídeo, mas sem áudio (A3): o `MjpegAviWriter` nunca capturou áudio, então não é regressão, é função que falta.
* **HT-2.1, segunda rodada (2026-11-09, build com os PRs #5 e #6):** A1 (tela "Sobre") validado; B1 a B6 funcionaram como esperado. A3: a gravação **continua sem áudio** mesmo com a captura WASAPI; em diagnóstico. O endpoint "OBSBOT Tiny Microphone" existe, com pai `USBVID_3564&PID_FEF0&MI_02`, mas o ID do endpoint (`SWDMMDEVAPI...`) não traz VID/PID.

## Tarefas HT

### HT-2.1: Validar a câmera no Windows

* **Épico:** este · **Tier:** HT · **Status:** em andamento (primeira rodada parcial em 2026-11-09; veja Achados)
* **Delegada em:** 2026-11-09 · **Bloqueia:** a coluna Windows da matriz de validação (1.0.0)
* **Tempo estimado:** 30 min

#### Objetivo

Confirmar com a câmera que o Studio funciona (vídeo, "Sobre", foto, gravação) e qual é o efeito visível dos comandos hoje marcados ⚠️.

#### Por que é HT

Exige observação visual e escrita em hardware. Executar estes passos é a autorização do mantenedor para essas escritas ([governança](../governance.md), regra de segurança 2).

#### Antes de começar

Tiny conectada. Na pasta do projeto, com o PR #5 já no `master`:

```
git switch master && git pull && dotnet build OpenGimbalCam.slnx -c Release
```

#### Passos

**Parte A: Studio.** Abra `src\OpenGimbalCam.Studio\bin\Release\net10.0\opengimbalcam-studio.exe`.

1. **A1.** O vídeo aparece? Clique em **Sobre** e confira se a janela mostra a versão 0.9.0 e o aviso de marcas.
2. **A2.** Clique em **📷 Foto** e abra o arquivo salvo em *Imagens\OpenGimbalCam Studio*.
3. **A3.** Clique em **Gravar**, espere uns 10 s, clique de novo e assista ao `.avi` salvo em *Vídeos\OpenGimbalCam Studio*.
4. Feche o Studio.

**Parte B: comandos ⚠️.** Abra dois terminais em `src\OpenGimbalCam.Cli\bin\Release\net10.0`.

* No terminal 1, deixe rodando `.\ogcctl.exe osc serve --udp`. Mantenha o vídeo aberto no app Câmera do Windows, porque a câmera só reage enquanto transmite.
* No terminal 2, rode um comando por vez. Nos comandos abaixo, `...` significa `.\ogcctl.exe osc send 127.0.0.1`.

| # | Comando (terminal 2) | O que observar |
|---|---|---|
| B1 | `... /OBSBOT/WebCam/General/SetHDR 1` e depois `0` | a imagem muda de contraste ou de luz? |
| B2 | `... /OBSBOT/WebCam/General/SetAutoExposureMode 1` e depois `0` | a exposição passa a priorizar o rosto? |
| B3 | `... /OBSBOT/WebCam/Tiny/SetAiMode 1`, `2`, `3` e por fim `0` | o que cada valor faz (segue o corpo, fecha no rosto, para)? |
| B4 | `... /OBSBOT/WebCam/Tiny/ToggleAILock 1` e depois `0` | o rastreamento trava num alvo e solta? |
| B5 | `... /OBSBOT/WebCam/Tiny/SetTrackingMode 0`, `1` e `2` | o comportamento do rastreamento muda? |
| B6 | `... /OBSBOT/WebCam/General/WakeSleep 0` e depois `1` | a câmera dorme (abaixa o gimbal) e acorda? |

No fim, rode `SetAiMode 0` e `SetHDR 0` para deixar a câmera como estava.

#### Como interpretar

"Funcionou", "nada aconteceu" e "aconteceu outra coisa" são todos resultados úteis. Não é preciso entender o protocolo.

#### Entregável

Uma linha por item (A1 a A3, B1 a B6), na conversa com o orquestrador. Ele atualiza a matriz de validação e os achados.

#### Critério de aceite

- [ ] Todos os itens A1 a A3 e B1 a B6 com resultado anotado.

### HT-2.2: Escolher a licença

* **Épico:** este · **Tier:** HT (decisão do mantenedor) · **Status:** concluída em 2026-11-09 (MIT)
* **Delegada em:** 2026-11-09 · **Bloqueia:** a 1.0.0 e a linha de licença na tela "Sobre"
* **Tempo estimado:** 5 min

#### Objetivo

Definir a licença de código aberto do projeto.

#### Por que é HT

É uma decisão do mantenedor com efeito jurídico ([tiers](../tiers.md), item 5).

#### Passos

1. Escolha uma das opções:
   * **MIT:** permissiva e simples, padrão no ecossistema .NET.
   * **Apache-2.0:** permissiva, com proteção explícita contra disputas de patente.
   * **GPL-3.0 ou LGPL-3.0:** quem distribuir versões modificadas precisa abrir o código delas.

#### Entregável

O nome da licença, na conversa. O T3 escreve o ADR, cria o `LICENSE` e liga a linha de licença na tela "Sobre".

#### Critério de aceite

- [ ] Licença escolhida e registrada em ADR aceito.

## Resultado

—
