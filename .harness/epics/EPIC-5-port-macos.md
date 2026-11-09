# Épico 5: Port para macOS

* **Status:** Proposto, **sem previsão** (bloqueado: não há um Mac para desenvolvimento e testes, ADR-0006)
* **Produto:** P1 Driver + P2 Aplicativo · **Fase:** 3 (macOS)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Levar o driver e o `opengimbalcam-studio` ao macOS com o mesmo nível de validação do Windows e do Linux.

## Condição para iniciar

* Um Mac disponível para desenvolvimento e testes com a câmera.
* Um ADR que mova o macOS para dentro do escopo e defina as versões e arquiteturas-alvo (Apple Silicon e/ou Intel).

## Critérios de aceite

- [ ] Backend `OpenGimbalCam.Devices/MacOS`: enumeração da câmera, controles UVC e Extension Unit.
- [ ] Coluna macOS na [matriz de validação](../roadmap.md#matriz-de-validação) com ✅ ou ⚠️ aceitos.
- [ ] `opengimbalcam-studio` funcionando no macOS, com vídeo ao vivo e permissões de câmera tratadas.
- [ ] Job de macOS na CI e artefato distribuível (`osx-arm64` e/ou `osx-x64`).

## Tarefas

- [ ] [T3] Investigar o acesso a controles UVC e à Extension Unit no macOS (requisições de controle USB via IOKit/IOUSBHost, já que o AVFoundation não expõe controles UVC).
- [ ] [T2] Verificar o suporte do FlashCap ao macOS ou escolher outra fonte de vídeo.
- [ ] [T2 + HT] Avaliar assinatura e notarização do aplicativo.

## Achados

—

## Resultado

—
