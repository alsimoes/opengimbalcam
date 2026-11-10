# ADR-0012: Licença MIT

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

O repositório ficou público em 2026-11-09 sem arquivo de licença. Sem licença, ninguém pode reutilizar o código legalmente. A escolha foi pedida ao mantenedor na tarefa HT-2.2 do [Épico 2](../epics/EPIC-2-windows-1.0.0.md), com as opções MIT, Apache-2.0 e GPL-3.0/LGPL-3.0.

## Decisão

O código do OpenGimbalCam é licenciado sob a **licença MIT**, no arquivo `LICENSE` da raiz.

## Consequências

* Qualquer pessoa pode usar, modificar e redistribuir o código, inclusive em produto fechado, desde que mantenha o aviso de copyright e a licença.
* As dependências atuais (Avalonia, FlashCap, CommunityToolkit.Mvvm, NAudio) têm licenças compatíveis com a MIT.
* A licença cobre só o código do projeto. Marcas de terceiros continuam regidas pela [política de PI e marcas](../ip-policy.md) e pelo `TRADEMARKS.md`, e material do fabricante continua fora do repositório.
* A tela "Sobre" do Studio e o README passam a indicar a licença.
