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
- [ ] Coluna Windows da [matriz de validação](../roadmap.md#matriz-de-validação) sem ❌; cada ⚠️ resolvido ou aceito explicitamente pelo mantenedor.
- [ ] Pendências técnicas do roadmap resolvidas ou adiadas por decisão registrada.
- [ ] Executável autocontido `win-x64` gerado pela CI (Épico 1), com instruções de instalação e uso.
- [ ] Release 1.0.0 publicada, com notas de versão.

## Tarefas

- [ ] [T2] Restringir driver, CLI, ponte OSC e app ao PID FEF0 (ADR-0007), com testes.
- [ ] [HT + T2] Validar foto e gravação do `opengimbalcam-studio` com a câmera.
- [ ] [HT] Confirmar visualmente os comandos ⚠️ (HDR, modo de exposição, modos de IA, trava de IA, suspensão).
- [ ] [T3] `GetGimbalPosInfo`: decidir entre pan/tilt UVC e atitude do gimbal.
- [ ] [T3, decisão HT] Definir o formato de distribuição no Windows (zip autocontido ou instalador) por ADR.
- [ ] [T1] Versionar os projetos (`0.x` até a release, depois `1.0.0`).
- [ ] [T2] Tela "Sobre" no `opengimbalcam-studio` com o aviso de marcas.
- [ ] [T3, decisão HT] Avaliar a migração do Avalonia 11.3 para a 12.x (vinda do Épico 1).

## Achados

—

## Resultado

—
