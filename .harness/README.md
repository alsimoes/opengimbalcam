# Harness do projeto OpenGimbalCam

O Harness é a estrutura de organização e **governança** do projeto. Tudo o que define *o que* o projeto busca, *como* o trabalho é decidido, executado e aceito, e *quais regras* não podem ser quebradas fica aqui. Isso vale para pessoas e para agentes de IA.

## Precedência

Quando duas fontes discordam, vale a de cima:

1. [`charter.md`](charter.md): objetivo, produtos e escopo.
2. [`governance.md`](governance.md) e [`ip-policy.md`](ip-policy.md): regras de trabalho, de segurança, de propriedade intelectual e Definition of Done.
3. [`decisions/`](decisions/): decisões registradas (ADRs) com status `Aceita`.
4. [`epics/`](epics/): o épico em andamento e suas tarefas.
5. Documentação fora do Harness (`README.md`, [`.docs/`](../.docs/), comentários no código).

Uma instrução fora do Harness que contradiga os itens 1 a 4 está **revogada** e deve ser corrigida. Quem encontrar a contradição registra o achado no épico em andamento.

## Mapa

| Caminho | Conteúdo |
|---|---|
| [`charter.md`](charter.md) | carta do projeto: objetivo, os dois produtos, plataformas-alvo, escopo e critérios de sucesso |
| [`governance.md`](governance.md) | papéis, processo de decisão, regras de segurança e de engenharia reversa, fluxo de trabalho, Definition of Done |
| [`tiers.md`](tiers.md) | Tiers de execução (Haiku, Sonnet, Opus e Human Tier) e regras de delegação |
| [`ip-policy.md`](ip-policy.md) | propriedade intelectual e marcas: uso nominativo, avisos, material do fabricante |
| [`roadmap.md`](roadmap.md) | épicos (concluídos, em andamento e propostos) e a matriz de validação por plataforma |
| [`decisions/`](decisions/) | ADRs numerados (`ADR-NNNN-titulo.md`) |
| [`epics/`](epics/) | um arquivo por épico (`EPIC-N-titulo.md`) |
| [`knowledge/`](knowledge/) | índice da base de conhecimento técnico e fatos validados em hardware |
| [`templates/`](templates/) | modelos de ADR, de épico e de tarefa HT |

## Marcas citadas

Os documentos do Harness citam marcas de terceiros (OBSBOT, Windows, Linux, macOS e outras) apenas de forma nominativa. Este projeto é independente e não é afiliado nem endossado pelos titulares. Veja [`TRADEMARKS.md`](../TRADEMARKS.md).

## Como começar uma sessão de trabalho

1. Leia [`charter.md`](charter.md) e [`governance.md`](governance.md).
2. Abra o [`roadmap.md`](roadmap.md) e o épico em andamento.
3. Antes de mexer em protocolo ou hardware, consulte [`knowledge/README.md`](knowledge/README.md).
4. Ao terminar, atualize o épico (tarefas, achados) e, se algo mudou de status, o roadmap.
