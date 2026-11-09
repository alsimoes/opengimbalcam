# Governança

## Papéis

| Papel | Quem | Responsabilidade |
|---|---|---|
| Mantenedor | André Simões | define objetivo e escopo, aceita ADRs e épicos, autoriza ações em hardware |
| Agente | assistentes de IA e colaboradores | executa épicos, propõe ADRs, mantém o Harness atualizado |

Só o mantenedor muda a [carta](charter.md) ou aceita um ADR. Um agente pode **propor** (status `Proposta`), nunca aceitar.

O trabalho é distribuído por **Tiers de execução** ([`tiers.md`](tiers.md)): Haiku (T1), Sonnet (T2), Opus (T3) e o **Human Tier (HT)**, em que o mantenedor recebe tarefas do orquestrador no mesmo formato dos agentes.

## Processo de decisão

* Decisões que afetam objetivo, escopo, arquitetura, plataforma ou segurança viram um **ADR** em [`decisions/`](decisions/), a partir do [modelo](templates/adr.md).
* Um ADR aceito só muda por outro ADR que o substitua (`Substituída por ADR-NNNN`). Não se edita o conteúdo de um ADR aceito, só o status.
* Na dúvida sobre se algo é decisão, registre.

## Trabalho em épicos

* Todo trabalho relevante pertence a um épico em [`epics/`](epics/), criado a partir do [modelo](templates/epic.md) e listado no [roadmap](roadmap.md).
* Estados: `Proposto` → `Aceito` → `Em andamento` → `Concluído` (ou `Cancelado`). Só um épico fica `Em andamento` por vez, salvo decisão do mantenedor.
* O épico guarda as tarefas, os achados e o resultado. Ao concluir, atualize o roadmap e a matriz de validação.

## Regras de segurança (invioláveis)

Estas regras vieram da investigação e protegem a câmera e o usuário.

1. **Nunca gravar firmware.** Os seletores 1, 3, 4 e 5 da XU (upload, shell remoto no Linux da câmera, download, atualização) permanecem bloqueados no código. O bloqueio do `xu-set` sem `--force` não pode ser removido nem contornado sem um ADR aceito.
2. **Movimento e escrita em hardware só com autorização do mantenedor**, dada na sessão. Prefira leituras e escritas inócuas. Depois de um teste, restaure os valores originais.
3. Para o gimbal se mover, a câmera precisa estar **transmitindo vídeo**. Em suspensão ela aceita os comandos, mas não se move. Não interprete isso como falha do protocolo.
4. **Nunca copiar segredos** para o repositório, documentos ou saídas: em especial o `UserLoginToken` (JWT) que o OBSBOT Center guarda em texto puro em `%APPDATA%\OBSBOT_Center\global.ini`.

## Propriedade intelectual e premissa de não conflito

* O projeto só opera e só estuda a **OBSBOT Tiny original** ([ADR-0007](decisions/ADR-0007-premissa-tiny-original.md)). Não se trabalha com modelos posteriores nem com câmeras que ainda têm suporte do fabricante.
* Marcas, avisos e material de terceiros seguem a [política de PI e marcas](ip-policy.md). O aviso de marcas faz parte da Definition of Done de qualquer documento público.

## Regras de engenharia reversa

1. A análise de binários do fabricante é **estática**. Não execute binários do fabricante nem ferramentas baixadas; use os scripts de [`tools/re/`](../tools/re) e bibliotecas instaladas em venv próprio.
2. **Arquivos do fabricante não são versionados.** Ficam em `.docs/original-driver/`, coberto pelo `.gitignore`. Documente-os por nome, versão e SHA-256.
3. Todo fato de protocolo recuperado precisa de evidência (RVA, string, tabela ou teste ao vivo) registrada na [base de conhecimento](knowledge/README.md).
4. Fatos validados em hardware são marcados com data e plataforma. Hipóteses ficam marcadas como hipóteses.

## Engenharia

* Linguagem C#, `Nullable` ativo e `TreatWarningsAsErrors` (veja `Directory.Build.props`).
* Código independente de plataforma em `OpenGimbalCam.Core`; código de sistema em `OpenGimbalCam.Devices/{Windows,Linux}`. Nenhuma dependência de plataforma vaza para `Core`.
* Toda lógica de protocolo nova vem com teste em `tests/OpenGimbalCam.Tests`.
* Build e testes: `dotnet build OpenGimbalCam.slnx -c Release` e `dotnet test OpenGimbalCam.slnx -c Release`.

## Idioma e versionamento

* Documentação e Harness em **português do Brasil**. Identificadores, comentários de código e mensagens de commit em **inglês**.
* Commits no padrão Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:`…).
* Trabalho em branch (`feature/…`, `docs/…`) com Pull Request para `master`. Não fazer commit direto em `master`.

## Definition of Done

Uma tarefa está pronta quando:

1. Build e testes passam sem avisos.
2. A documentação afetada (README, base de conhecimento, comentários) foi atualizada, não contradiz o Harness e traz o aviso de marcas quando cita marcas de terceiros.
3. Funções que tocam o hardware estão marcadas na matriz de validação como *validadas* (com data e plataforma) ou *não validadas*. Nunca se declara validado o que não foi testado.
4. O épico registra o que foi feito e o que ficou pendente.
