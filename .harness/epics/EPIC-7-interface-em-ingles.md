# Épico 7: Interface em inglês (internacionalização)

* **Status:** Proposto (trabalho futuro, pedido do mantenedor em 2026-11-09)
* **Produto:** P2 Aplicativo (e mensagens da CLI `ogcctl`)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Permitir usar o `opengimbalcam-studio` e o `ogcctl` em inglês, além do português do Brasil. O usuário escolhe o idioma, e o padrão segue o idioma do sistema.

## Critérios de aceite

- [ ] Todos os textos visíveis ao usuário (Studio, mensagens e ajuda da CLI) vêm de recursos localizáveis, sem texto fixo no código.
- [ ] Idiomas `pt-BR` e `en`; o Studio permite trocar o idioma, e a escolha fica salva entre execuções.
- [ ] Testes garantem que cada chave existe nos dois idiomas.
- [ ] O aviso de marcas da tela "Sobre" existe nos dois idiomas.

## Tarefas

- [ ] [T3] Escolher o mecanismo de localização (por exemplo, `.resx` com `IStringLocalizer`, ou recursos do Avalonia) e registrar em ADR.
- [ ] [T2] Extrair os textos do Studio e da CLI para recursos `pt-BR`.
- [ ] [T2] Traduzir para `en` e criar o seletor de idioma no Studio.
- [ ] [HT] Revisar a tradução e conferir visualmente o Studio nos dois idiomas.

## Achados

* A documentação e o Harness continuam em português do Brasil (governança). Este épico trata só da interface dos produtos.

## Resultado

—
