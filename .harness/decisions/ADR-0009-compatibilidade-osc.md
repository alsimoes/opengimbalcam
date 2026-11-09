# ADR-0009: Compatibilidade com o protocolo OSC do OBSBOT Center, ligável e ligada por padrão

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

O OBSBOT Center, software proprietário do fabricante, expõe um servidor OSC (porta 16284, endereços `/OBSBOT/WebCam/…`). Ferramentas de terceiros (layouts TouchOSC, módulos do Bitfocus Companion, plugins de Stream Deck) controlam a câmera por esse protocolo. O projeto reimplementou do zero um servidor que entende as mesmas mensagens, a partir de análise estática ([`.docs/investigacao.md`](../../.docs/investigacao.md), §10), sem usar nem copiar código do fabricante.

Isso preserva as configurações de quem já usa essas ferramentas. Por outro lado, é a parte do projeto que mais se parece com um substituto do software do fabricante, o que pesa na premissa de não conflito ([ADR-0007](ADR-0007-premissa-tiny-original.md)). O prefixo `/OBSBOT/` é exigido pelos clientes e é uso funcional, não nome de produto ([política de PI](../ip-policy.md), item 6).

## Decisão

1. A compatibilidade com o protocolo OSC do OBSBOT Center pode ser **ligada e desligada**, e vem **ligada por padrão**.
2. CLI: `ogcctl osc serve --obsbot-compat on|off` (padrão `on`).
3. Aplicativo: caixa "Compatibilidade com o protocolo OSC do OBSBOT Center" no painel OSC, marcada por padrão, alterável com o servidor rodando.
4. Desligada, o servidor continua aberto, mas toda mensagem `/OBSBOT/WebCam/…` é registrada no log e ignorada.
5. A documentação descreve o recurso como compatibilidade para interoperar com ferramentas existentes, sem sugerir afiliação.

## Consequências

* Com a compatibilidade desligada, o servidor não tem outro protocolo para atender. Um protocolo OSC próprio (por exemplo, `/ogc/…`) fica como possibilidade futura, por novo ADR.
* Se o fabricante objetar, mudar o padrão para desligado é uma alteração de uma linha (ADR-0007, item 5).
