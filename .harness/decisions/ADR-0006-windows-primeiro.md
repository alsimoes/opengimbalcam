# ADR-0006: Windows primeiro até a 1.0.0; Linux depois; macOS fora

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

O objetivo cobre Windows e Linux, mas desenvolver e validar as duas plataformas ao mesmo tempo divide o esforço e o único hardware de teste. O código já tem um backend Linux (`OpenGimbalCam.Devices/Linux`), nunca testado com a câmera. Não há um Mac disponível.

## Decisão

1. **Fase 1:** só o Windows 11 é alvo de desenvolvimento, validação e distribuição, até a versão **1.0.0** (P1 + P2 completos no Windows, conforme a [carta](../charter.md#critérios-de-sucesso)).
2. **Fase 2:** o port para Linux começa depois da 1.0.0, em épico próprio.
3. **macOS** está fora do escopo enquanto não houver um Mac para desenvolvimento e testes. O port fica registrado como épico sem previsão.
4. As versões seguem SemVer. Antes da 1.0.0, são `0.x`.

## Consequências

* Durante a Fase 1, o código Linux existente fica onde está e deve continuar compilando, mas não recebe trabalho novo nem validação. Mudanças nas abstrações de `OpenGimbalCam.Core` não devem quebrá-lo de propósito.
* A CI da Fase 1 roda no Windows. O job de Linux entra na Fase 2.
* README e matriz de validação deixam claro que o Linux ainda não é suportado.
