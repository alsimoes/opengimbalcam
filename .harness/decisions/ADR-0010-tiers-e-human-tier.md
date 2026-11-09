# ADR-0010: Tiers de execução, incluindo o Human Tier

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

O projeto é conduzido por um agente orquestrador que delega trabalho a outros agentes, como já aconteceu na pesquisa de nomes e na renomeação do repositório. Algumas tarefas, porém, só um humano pode fazer: confirmar visualmente a câmera, autorizar movimento, usar sites que exigem navegação ou cadastro (como a busca de marcas). O mantenedor já usa em outro projeto o conceito de **Human Tier (HT)**: o humano é tratado como um agente e recebe tarefas no mesmo formato.

## Decisão

Adotar a tabela de Tiers de [`tiers.md`](../tiers.md): T1 (Haiku), T2 (Sonnet), T3 (Opus, o orquestrador) e HT (mantenedor). Toda tarefa de épico leva a marcação do Tier. Tarefas HT seguem o [modelo de tarefa HT](../templates/ht-task.md) e são assíncronas.

## Consequências

* O orquestrador escolhe o menor Tier capaz e revisa o resultado de todos os Tiers, inclusive o HT.
* Tarefas humanas deixam de ficar implícitas em prosa e passam a ter guia, entregável e critério de aceite.
