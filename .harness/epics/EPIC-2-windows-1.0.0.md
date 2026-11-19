# Épico 2: Versão 1.0.0 no Windows 11

* **Status:** Em andamento (aceito pelo mantenedor em 2026-11-09).
* **Produto:** P1 Driver + P2 Aplicativo · **Fase:** 1 (Windows)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Publicar a versão 1.0.0: driver aberto e `opengimbalcam-studio` completos e validados no Windows 11, substituindo o software original da OBSBOT para a Tiny.

## Critérios de aceite

- [x] Driver, CLI, ponte OSC e aplicativo só operam a OBSBOT Tiny original (VID 3564, **PID FEF0**); qualquer outro dispositivo do fabricante é ignorado ([ADR-0007](../decisions/ADR-0007-premissa-tiny-original.md)). Conferido no código em 2026-11-19: `TinyIdentity` filtra a enumeração do DirectShow e do V4L2 e a captura WASAPI.
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
- [x] [HT] Validar o Studio (vídeo, "Sobre", foto, gravação) e confirmar visualmente os comandos ⚠️ (HDR, exposição, modos de IA, trava de IA, rastreamento, suspensão). Concluída em 2026-11-09 em duas rodadas; só o áudio da gravação falhou (veja os achados). Guia: [HT-2.1](#ht-21-validar-a-câmera-no-windows). O áudio volta a ser conferido na [HT-2.3](#ht-23-validar-o-candidato-a-release) (S10).
- [ ] [HT] Escolher a licença do projeto; o ADR e o `LICENSE` ficam com o T3. HT: concluída em 2026-11-19 (MIT). T3: [ADR-0014](../decisions/ADR-0014-licenca-mit.md) (Proposta) e `LICENSE` criados em 2026-11-19; falta o aceite do ADR. Guia: [HT-2.2](#ht-22-escolher-a-licença).
- [ ] [T3] `GetGimbalPosInfo`: decidir entre pan/tilt UVC e atitude do gimbal.
- [ ] [T3, decisão HT] Definir o formato de distribuição no Windows (zip autocontido ou instalador) por ADR. Decisão do mantenedor em 2026-11-19: zip portátil e instalador no GitHub Releases. [ADR-0013](../decisions/ADR-0013-distribuicao-no-windows.md) (Proposta) criado; falta o aceite.
- [x] [T1] Versionar os projetos (`0.x` até a release, depois `1.0.0`): `0.9.0` em `Directory.Build.props`, `ogcctl --version`. Feito pelo mesmo agente T2, por economia.
- [x] [T2] Tela "Sobre" no `opengimbalcam-studio` com o aviso de marcas. A conferência visual fica no item A1 da [HT-2.1](#ht-21-validar-a-câmera-no-windows).
- [x] [T3, decisão HT] Avaliar a migração do Avalonia 11.3 para a 12.x (vinda do Épico 1). Decisão do mantenedor em 2026-11-19, ao aprovar o [plano](../../.docs/plano-studio-e-release-1.0.0.md): a 1.0.0 sai com o Avalonia 11.3.22, e a migração fica para depois da release.
- [ ] [T2] Botão no Studio para restaurar todos os controles de imagem ao padrão da câmera (pedido do mantenedor na HT-2.1, 2026-11-09). Entra na interface profissional (plano, A2.2 e A2.4).
- [ ] [T3 desenho, T2 implementação] Gravação com áudio (obrigatória na 1.0.0, decisão do mantenedor em 2026-11-09): capturar o microfone da Tiny (UAC, WASAPI no Windows) e gravar PCM no AVI junto com o MJPEG. Implementado em 2026-11-09 ([ADR-0011](../decisions/ADR-0011-audio-na-gravacao.md), Aceita); falta validar na câmera (HT-2.3, S10).
- [ ] [T2] Corrigir: o processo `opengimbalcam-studio` continua vivo, sem janela, depois que a janela é fechada, e trava as DLLs no build seguinte. Visto em 2026-11-09. Entra na interface profissional (plano, A4, encerramento); veja a análise nos achados.
- [ ] [T2, revisão T3] Interface profissional do Studio: identidade visual, vídeo maior, controles de imagem compactos, atalhos, persistência das preferências e testes de UI com snapshots ([plano](../../.docs/plano-studio-e-release-1.0.0.md), Parte A; branch `feature/studio-pro-ui`). Depende do aceite do [ADR-0012](../decisions/ADR-0012-identidade-visual-do-studio.md).
- [ ] [T2, revisão T3] Empacotamento Windows: metadados, `THIRD-PARTY-NOTICES.md`, `build-release.ps1`, instalador Inno Setup, `release.yml` e `LEIA-ME.txt` (plano, B3 a B7; branch `feature/windows-packaging`). Depende do aceite do [ADR-0013](../decisions/ADR-0013-distribuicao-no-windows.md).
- [ ] [HT] Validar o candidato a release com a Tiny: pacote, nova interface e itens pendentes da HT-2.1. HT: aguardando (depende das duas tarefas acima). Guia: [HT-2.3](#ht-23-validar-o-candidato-a-release).
- [ ] [T1/T2, revisão T3] Corte da 1.0.0: versão, notas de versão, matriz e resultado do épico (plano, B9; branch `release/1.0.0`).
- [ ] [HT] Autorizar a tag `v1.0.0` e publicar a release. HT: aguardando (depende da HT-2.3, do Épico 6 e do corte). Guia: [HT-2.4](#ht-24-publicar-a-100).

## Achados

* **Sem arquivo LICENSE.** O repositório é público desde 2026-11-09, mas não tem licença; sem ela, ninguém pode reutilizar o código legalmente. A tela "Sobre" omite a linha de licença até isso ser decidido. Decisão do mantenedor (HT), por ADR. Encaminhado em 2026-11-19: MIT, `LICENSE` criado e [ADR-0014](../decisions/ADR-0014-licenca-mit.md) aguardando aceite.
* A filtragem por PID acontece na enumeração das duas plataformas, então a ponte OSC e o Studio herdam a restrição sem código próprio.
* **HT-2.1, primeira rodada (2026-11-09), com a build anterior ao PR #5:** os controles de imagem (matiz, exposição etc.) e os controles da câmera no Studio funcionaram. A tela "Sobre" não foi encontrada porque o PR #5, que a cria, ainda não estava no `master`. Ficam pendentes: A1 a A3 com a build nova e B1 a B6.
* O mantenedor pediu a interface em inglês como trabalho futuro: virou o [Épico 7](EPIC-7-interface-em-ingles.md).
* **HT-2.1, foto e gravação (2026-11-09, build anterior ao PR #5):** a foto funcionou (A2). A gravação gera vídeo, mas sem áudio (A3): o `MjpegAviWriter` nunca capturou áudio, então não é regressão, é função que falta.
* **HT-2.1, segunda rodada (2026-11-09, build com os PRs #5 e #6; registrada no PR #7 e trazida para cá em 2026-11-19):** A1 (tela "Sobre") validado; B1 a B6 funcionaram como esperado, e os comandos ⚠️ do seletor 6 passam a ✅ na matriz. A3: a gravação **continua sem áudio** mesmo com a captura WASAPI ([ADR-0011](../decisions/ADR-0011-audio-na-gravacao.md)); em diagnóstico. O endpoint "OBSBOT Tiny Microphone" existe, com pai `USB\VID_3564&PID_FEF0&MI_02`, mas o ID do endpoint (`SWD\MMDEVAPI\...`) não traz VID nem PID, o que sugere que a busca do microfone pelo PID não encontra o dispositivo. Bloqueia a 1.0.0 (HT-2.3, S10).
* **2026-11-19, plano da interface e da release aprovado pelo mantenedor:** [plano](../../.docs/plano-studio-e-release-1.0.0.md). Escolhas do mantenedor: vídeo no centro com o deck de movimento abaixo, Inter e JetBrains Mono, licença MIT, zip e instalador. O Linux sai na release seguinte ([Épico 4](EPIC-4-port-linux.md)).
* **Processo vivo depois de fechar (análise T3, 2026-11-19):** a thread do DirectShow (`ogc-directshow`) já é background. A causa provável é o descarte síncrono no `ShutdownRequested`: `MainViewModel.Dispose` espera `StopPreviewAsync` e `StopOscAsync` com `.GetAwaiter().GetResult()` e pode travar o encerramento. A correção do plano (A4) descarta de forma assíncrona no `Closing` e termina com `Environment.Exit`.
* **Segurança nos testes de agente:** abrir o Studio com a Tiny conectada liga o vídeo, e com vídeo o gimbal fica ativo e pode se mover. Agentes só executam o app com a câmera desconectada; os testes de UI usam câmera simulada.

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

* **Épico:** este · **Tier:** HT (decisão do mantenedor) · **Status:** concluída em 2026-11-19 (MIT; [ADR-0014](../decisions/ADR-0014-licenca-mit.md))
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

### HT-2.3: Validar o candidato a release

* **Épico:** este · **Tier:** HT · **Status:** aguardando (depende da interface profissional e do empacotamento)
* **Delegada em:** 2026-11-19 · **Bloqueia:** o corte da 1.0.0 e a coluna Windows da matriz de validação
* **Tempo estimado:** 60 min

#### Objetivo

Confirmar com a Tiny que o pacote `win-x64` instala, abre e desinstala, que a nova interface funciona e que os itens pendentes da HT-2.1 passam.

#### Por que é HT

Exige a câmera, observação visual e escrita em hardware. Executar estes passos é a autorização do mantenedor para essas escritas ([governança](../governance.md), regra de segurança 2).

#### Antes de começar

* Tiny conectada e OBSBOT Center fechado.
* O orquestrador avisa na conversa quando o candidato estiver pronto e passa o link da execução do workflow **Release** no GitHub (aba **Actions**). Nela, baixe o artefato `opengimbalcam-win-x64`: um zip com o pacote, o instalador e o `SHA256SUMS.txt`.
* Extraia o artefato numa pasta, por exemplo *Downloads\ogc-rc*.

#### Passos

**Parte P: pacote.**

1. **P1.** Extraia `OpenGimbalCam-<versão>-win-x64.zip` e abra `opengimbalcam-studio.exe`. Se o Windows mostrar o aviso do SmartScreen, clique em **Mais informações** e em **Executar assim mesmo**, e anote que o aviso apareceu. Feche o Studio.
2. **P2.** Rode `OpenGimbalCam-<versão>-win-x64-setup.exe`. O instalador deve abrir em português, sem pedir administrador. Conclua, abra o Studio pelo menu Iniciar e feche.
3. **P3.** Em **Configurações → Aplicativos**, desinstale o OpenGimbalCam e confira que o atalho sumiu. Reinstale para seguir.

**Parte S: Studio com a Tiny.** Abra o Studio instalado.

1. **S1.** O vídeo aparece grande e nítido?
2. **S2.** Segure as setas do direcional e, depois de clicar no vídeo, as setas do teclado. O gimbal se move enquanto você segura e para ao soltar?
3. **S3.** O botão central do direcional e a tecla **Home** centralizam?
4. **S4.** O zoom responde ao slider, aos botões de lupa e às teclas **+** e **−**?
5. **S5.** Os três campos de visão (86°, 78° e 65°) mudam o enquadramento?
6. **S6.** **Espelhar** inverte a imagem?
7. **S7.** Os presets 1, 2 e 3 levam a câmera às posições salvas?
8. **S8.** Na aba **Imagem**, mude o brilho e o balanço de branco, ligue e desligue o **A** (automático), use o restaurar de um controle e depois **Restaurar tudo**. A imagem volta ao padrão?
9. **S9.** Tire uma foto (botão da câmera ou **F8**). A tela pisca e aparece um aviso no canto? Clicar no aviso abre a foto?
10. **S10.** Grave uns 10 s (**F9** liga e desliga). Aparecem o selo **REC** e o cronômetro? O `.avi` em *Vídeos\OpenGimbalCam Studio* toca com o áudio sincronizado?
11. **S11.** Na aba **OSC**, inicie e pare o servidor. A aba **Atividade** registra as duas ações?
12. **S12.** **F11** e o duplo clique no vídeo entram e saem do modo foco? **Esc** sai?
13. **S13.** Reduza a janela ao tamanho mínimo. Algo fica cortado?
14. **S14.** Abra **Sobre**. Aparecem a versão, "Licença MIT", o aviso de marcas e o botão do repositório?
15. **S15.** Comece uma gravação e feche a janela. Em até 3 s o `opengimbalcam-studio` some do Gerenciador de Tarefas? O `.avi` toca?
16. **S16.** Reabra o Studio. O tamanho da janela, a câmera e a aba voltam como estavam?
17. **S17.** Com o vídeo ligado, tire uma captura de tela da janela inteira, para o README.

**Parte C (opcional): comandos da HT-2.1.** Já validados em 2026-11-09 na segunda rodada da HT-2.1; repita só para conferir com o pacote instalado. Com o vídeo ligado no Studio, inicie o servidor OSC na aba **OSC** (UDP, porta padrão). Abra um terminal na pasta de instalação, *%LOCALAPPDATA%\Programs\OpenGimbalCam*, e rode os comandos B1 a B6 da [HT-2.1](#ht-21-validar-a-câmera-no-windows), um por vez, com `.\ogcctl.exe osc send 127.0.0.1` no lugar de `...`. No fim, rode `SetAiMode 0` e `SetHDR 0`.

#### Como interpretar

"Funcionou", "nada aconteceu" e "aconteceu outra coisa" são todos resultados úteis. Falha em P1 a P3, S2, S10 ou S15 bloqueia a release; as demais podem virar pendência aceita pelo mantenedor. Os itens S1, S9, S10 e S14 substituem A1 a A3 da HT-2.1.

#### Entregável

Uma linha por item (P1 a P3, S1 a S17 e, se fizer a parte C, B1 a B6), na conversa com o orquestrador, e a captura de tela do S17. Ele atualiza a matriz de validação, os achados e o README.

#### Critério de aceite

- [ ] Todos os itens com resultado anotado.
- [ ] Nenhuma falha bloqueante em aberto.

### HT-2.4: Publicar a 1.0.0

* **Épico:** este · **Tier:** HT · **Status:** aguardando (depende da HT-2.3, do Épico 6 e do corte da 1.0.0)
* **Delegada em:** 2026-11-19 · **Bloqueia:** a conclusão deste épico
* **Tempo estimado:** 15 min

#### Objetivo

Publicar a release 1.0.0 no GitHub.

#### Por que é HT

Enviar a tag da versão e publicar a release são ações públicas que só o mantenedor autoriza ([tiers](../tiers.md)).

#### Antes de começar

HT-2.3 sem falha bloqueante, [Épico 6](EPIC-6-busca-de-marcas.md) concluído e o PR do corte da 1.0.0 integrado ao `master`.

#### Passos

1. Na conversa, autorize o agente a criar e enviar a tag `v1.0.0`, ou rode você mesmo na pasta do projeto:

   ```
   git switch master && git pull && git tag -a v1.0.0 -m "OpenGimbalCam 1.0.0" && git push origin v1.0.0
   ```

2. No GitHub, aba **Actions**, espere o workflow **Release** terminar em verde.
3. Na página **Releases**, abra o rascunho **OpenGimbalCam 1.0.0**. Confira os três arquivos (zip, instalador e `SHA256SUMS.txt`) e as notas.
4. Clique em **Publish release**.
5. Opcional: no quadro **About** da página do repositório, preencha a descrição e os tópicos.

#### Como interpretar

Se o workflow falhar, não publique: mande o link da execução na conversa. A mensagem "tag != Version" indica que o `Directory.Build.props` não está na versão 1.0.0.

#### Entregável

O link da release publicada, na conversa.

#### Critério de aceite

- [ ] Release 1.0.0 pública, com os três arquivos e as notas de versão.

## Resultado

—
