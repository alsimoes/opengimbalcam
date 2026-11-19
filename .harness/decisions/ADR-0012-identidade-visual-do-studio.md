# ADR-0012: Identidade visual do Studio

* **Status:** Proposta
* **Data:** 2026-11-19
* **Decisores:** André Simões

## Contexto

Em 2026-11-19 o mantenedor pediu que o `opengimbalcam-studio` tenha aparência, usabilidade e experiência de nível comercial, com ícones do Bootstrap, fontes do Google, controles de imagem compactos e vídeo maior ([plano](../../.docs/plano-studio-e-release-1.0.0.md)). Hoje a interface usa o tema Fluent padrão do Avalonia, símbolos Unicode no lugar de ícones e não tem ícone próprio. A [política de PI](../ip-policy.md), regra 4, proíbe logotipos, ícones, fontes, cores ou layout que imitem o fabricante ou o OBSBOT Center. O app também não deve depender de rede para desenhar a própria interface.

## Decisão

O Studio adota uma identidade visual própria, toda embutida no executável:

* tema escuro fixo, com tokens de cor próprios e destaque violeta `#6C5CE7` (texto branco sobre ele com contraste de 4,8:1);
* Inter no texto (pacote `Avalonia.Fonts.Inter`) e JetBrains Mono nos números, no cronômetro e no log, ambas do Google Fonts sob a OFL 1.1;
* Bootstrap Icons 1.13.2 (MIT), convertidos em geometria XAML durante o desenvolvimento por `tools/ui/gen_icons.py`;
* ícone do aplicativo derivado de `camera-video-fill` sobre gradiente violeta, gerado por `tools/ui/make-icon.cs`.

## Consequências

* Nada é baixado em tempo de execução. Ícone novo exige rodar o gerador e versionar o `Icons.axaml` gerado.
* As licenças das fontes e dos ícones vão para `licenses/` e para o `THIRD-PARTY-NOTICES.md`, e seguem no pacote ([ADR-0013](ADR-0013-distribuicao-no-windows.md)).
* O tema claro fica para depois da 1.0.0. Como toda cor passa pelos tokens, criá-lo é trocar um dicionário.
* Mudança visual que se aproxime da identidade do fabricante continua proibida pela política de PI.
