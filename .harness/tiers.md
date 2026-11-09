# Tiers de execução

O agente orquestrador (a sessão principal) não executa tudo sozinho: ele **delega cada tarefa ao Tier adequado** à complexidade e à natureza dela. Um dos Tiers é o **Human Tier (HT)**: o mantenedor é tratado como mais um agente executor e recebe tarefas no mesmo formato de delegação dos agentes de IA.

## Tabela de Tiers

| Tier | Executor | Quando usar | Exemplos neste projeto |
|---|---|---|---|
| **T1** | modelo **Haiku** | Tarefas mecânicas, bem especificadas, de baixo risco e fáceis de verificar. | renomear o repositório no GitHub; rodar build/testes e relatar; aplicar uma troca de nomes já decidida; checar links |
| **T2** | modelo **Sonnet** | Implementação e pesquisa de rotina com escopo claro; documentação. | pesquisa de conflitos de nome; implementar uma flag já desenhada; escrever testes; atualizar README |
| **T3** | modelo **Opus** (o próprio orquestrador) | Decisões técnicas, arquitetura, engenharia reversa, protocolo, segurança, redação de ADRs, revisão do trabalho dos outros Tiers. | desmontagem da `libdev.dll` e do firmware; desenho do driver; ADRs; revisão de PRs |
| **HT** | **humano** (mantenedor) | Tudo o que um agente não pode ou não deve fazer. | ver abaixo |

### O que vai obrigatoriamente para o HT

1. **Interação física ou sensorial com o hardware:** conectar a câmera, observar o vídeo, confirmar visualmente um movimento ou efeito (HDR, modos de IA).
2. **Autorizações** previstas na [governança](governance.md): movimento do gimbal, escrita em hardware, qualquer exceção a regra de segurança.
3. **Sites e sistemas interativos** que exigem navegação humana, cadastro, login ou CAPTCHA (por exemplo, as bases oficiais de marcas do [Épico 6](epics/EPIC-6-busca-de-marcas.md)).
4. **Credenciais, contas, pagamentos e assinaturas** (certificado de assinatura de código, contas em lojas, registro de marca).
5. **Decisões do mantenedor:** aceitar ADRs e épicos, mudar a carta, avaliar riscos jurídicos.
6. **Contato com terceiros:** fabricante, advogados, comunidade.

## Regras de delegação

1. **Escolha o menor Tier capaz.** Na dúvida entre dois, use o maior. Trabalho de T1/T2 que envolva risco (hardware, firmware, segurança) sobe para T3.
2. **Toda delegação usa o mesmo formato**, para agentes ou para o HT: objetivo, contexto mínimo, passos, entregável, onde registrar e critério de aceite. Para o HT, use o [modelo de tarefa HT](templates/ht-task.md).
3. **O orquestrador revisa o resultado** de qualquer Tier antes de dar a tarefa por concluída. O relato de um agente não é prova: confira o diff, a saída ou o registro.
4. **Tarefas HT são assíncronas.** O orquestrador entrega a tarefa, registra no épico como `HT: aguardando` e segue com o que não depende dela. Não presume o resultado.
5. **Tarefas HT devem ser autoexplicativas.** O mantenedor não precisa saber o contexto técnico: o guia diz o que fazer, onde clicar, o que anotar e como interpretar.
6. **Economia de tokens é prioridade do mantenedor** (veja abaixo).
7. **Marcação nos épicos:** cada tarefa leva o Tier no início, por exemplo `- [ ] [T1] Renomear…`, `- [ ] [HT] Confirmar visualmente…`.

## Economia de tokens

O custo em tokens é um critério de decisão tão importante quanto a qualidade.

1. **Menor Tier capaz, sempre.** T3 só para o que exige julgamento; trabalho mecânico vai para T1.
2. **Leitura cirúrgica:** use busca (`grep`) e trechos (offset/limit) em vez de ler arquivos inteiros; não releia o que já está no contexto.
3. **Saídas grandes ficam fora do orquestrador:** logs, varreduras amplas e pesquisas web vão para um agente que devolve só a conclusão.
4. **Relatórios curtos:** toda delegação define limite de tamanho do relatório (ex.: "até 300 palavras").
5. **Reaproveite agentes:** continue um agente que já tem o contexto (mensagem) em vez de criar outro do zero.
6. **Não delegue o trivial:** criar um agente custa contexto; uma ou duas ações simples o orquestrador faz direto.
7. **Agrupe:** junte edições num mesmo passo e tarefas HT numa mesma entrega; evite builds e testes repetidos sem mudança de código.
8. **Respostas objetivas:** comunicação com o mantenedor curta, sem repetir o que já está nos arquivos.
