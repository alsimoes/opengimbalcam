# Épico 4: Port para Linux

* **Status:** Proposto (só começa depois da 1.0.0, ADR-0006, e do Épico 3)
* **Produto:** P1 Driver + P2 Aplicativo · **Fase:** 2 (Linux)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Levar o driver e o `opengimbalcam-studio` ao Linux com o mesmo nível de validação da 1.0.0 no Windows, permitindo o uso sem privilégios especiais.

## Critérios de aceite

- [ ] Coluna Linux da [matriz de validação](../roadmap.md#matriz-de-validação) com ✅ ou ⚠️ aceitos, com data e distribuição/kernel.
- [ ] Regra udev para `VID 3564` que dê acesso a `/dev/videoN` ao usuário logado, com instruções de instalação.
- [ ] Diferenças de unidade (exposição, `PAN_SPEED`) confirmadas em hardware.
- [ ] Job de Linux na CI e artefato autocontido `linux-x64`.

## Tarefas

- [ ] [HT] Definir a máquina/distribuição de teste e registrar kernel e versão do `uvcvideo`.
- [ ] [T3] Revisar o backend existente (`OpenGimbalCam.Devices/Linux`) contra o estado da 1.0.0.
- [ ] [HT + T2] Validar `ogcctl` e `opengimbalcam-studio`, com autorização do mantenedor para movimento.
- [ ] [T2 + HT] Escrever e testar a regra udev.
- [ ] [T3, decisão HT] Definir o formato de pacote para Linux por ADR.

## Achados

—

## Resultado

—
