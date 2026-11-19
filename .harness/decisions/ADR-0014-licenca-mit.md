# ADR-0014: Licença MIT

* **Status:** Aceita (2026-11-19)
* **Data:** 2026-11-19
* **Decisores:** André Simões

## Contexto

O repositório é público desde 2026-11-09 e não tinha licença, o que impede o reuso legal do código (achados do [Épico 2](../epics/EPIC-2-windows-1.0.0.md)). A [HT-2.2](../epics/EPIC-2-windows-1.0.0.md#ht-22-escolher-a-licença) pediu ao mantenedor a escolha entre MIT, Apache-2.0 e GPL-3.0 ou LGPL-3.0. Em 2026-11-19 ele escolheu a MIT.

## Decisão

O código e a documentação escritos para o projeto ficam sob a licença MIT, com o texto em `LICENSE` na raiz e a linha `Copyright (c) 2026 OpenGimbalCam contributors`. A tela "Sobre" mostra "Licença MIT", e o pacote de distribuição leva o `LICENSE` ([ADR-0013](ADR-0013-distribuicao-no-windows.md)).

## Consequências

* Qualquer pessoa pode usar, modificar e redistribuir o código, inclusive em produtos fechados, desde que mantenha o aviso de copyright e a licença.
* Contribuições entram sob a mesma licença.
* Componentes de terceiros mantêm as próprias licenças, listadas em `THIRD-PARTY-NOTICES.md`.
* A licença não concede direito sobre marcas: a [política de PI](../ip-policy.md) e o `TRADEMARKS.md` continuam valendo.
* Os trechos mínimos de terceiros citados na documentação, como nomes de funções, continuam pertencendo aos titulares. A MIT cobre só o que os autores do projeto escreveram.
