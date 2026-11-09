# ADR-0005: Nenhuma gravação de firmware

* **Status:** Aceita
* **Data:** 2026-11-09 (registra regra vigente desde 2026-10-27)
* **Decisores:** André Simões

## Contexto

Os seletores 1, 3, 4 e 5 da Extension Unit fazem upload e download de arquivos, executam comandos shell no Linux da câmera e conduzem a atualização de firmware ([`.docs/investigacao.md`](../../.docs/investigacao.md), §5.4 e §7.4). Um erro nesse caminho pode inutilizar a câmera, e não há hardware reserva.

## Decisão

O projeto não grava firmware nem expõe esses seletores em funções de usuário. O firmware é apenas lido e verificado (`ogcctl fw info|verify|extract`). O acesso bruto a esses seletores fica bloqueado sem `--force`.

## Consequências

* Atualizar a câmera continua exigindo o software do fabricante.
* Reverter esta decisão exige novo ADR, plano de recuperação documentado e autorização explícita do mantenedor.
