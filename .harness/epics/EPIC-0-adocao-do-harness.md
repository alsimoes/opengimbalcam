# Épico 0: Adoção do Harness e alinhamento da documentação

* **Status:** Concluído
* **Produto:** Governança
* **Abertura:** 2026-11-09 · **Conclusão:** 2026-11-09

## Objetivo

Criar a estrutura `.harness/`, fazê-la assumir a governança do projeto e varrer o workspace para ajustar a documentação existente: transferir para o Harness as instruções pertinentes e descartar as que conflitam com a nova governança.

## Critérios de aceite

- [x] Objetivo do projeto definido na [carta](../charter.md), com os dois produtos (P1 Driver, P2 Aplicativo de controle).
- [x] Regras, processo de decisão e Definition of Done em [`governance.md`](../governance.md).
- [x] Decisões já tomadas registradas como ADRs.
- [x] Toda a documentação do workspace inventariada e alinhada ao Harness.
- [x] Pendências existentes transferidas para o [roadmap](../roadmap.md).
- [x] Premissa de não conflito com o fabricante registrada: só a OBSBOT Tiny original ([ADR-0007](../decisions/ADR-0007-premissa-tiny-original.md)).
- [x] Documentação revisada para identificar marcas registradas citadas sem aviso, com o aviso aplicado e uma [política de PI e marcas](../ip-policy.md) criada.

## Inventário da varredura

| Artefato | Situação encontrada | Ação |
|---|---|---|
| `README.md` | apresentava o projeto como "alternativa ao OBSBOT Center", sem o objetivo de driver + aplicativo; link quebrado para `docs/investigacao.md`; estrutura citava `original-driver/` na raiz | objetivo reescrito com os dois produtos e a definição de driver; links corrigidos; estrutura atualizada com `.harness/` e `.docs/`; seção de segurança aponta para a governança |
| `.docs/investigacao.md` | movido de `docs/` para `.docs/` pelo mantenedor (ainda não versionado); link errado para `src/ObsbotCtl/Camera/DirectCommands.cs`; seção "Pendências" própria | mantido como referência técnica; nota de precedência do Harness no topo; link corrigido para `src/Obsbot.Core/Camera/DirectCommands.cs`; pendências espelhadas no roadmap |
| `.docs/original-driver/` | binários e manual do fabricante | confirmado fora do versionamento (`.gitignore`: `original-driver/`) |
| Comentários em `ObsbotXu.cs`, `FirmwarePackage.cs`, `RemoMessage.cs`, `DirectShow.cs` | apontavam para `docs/investigacao.md`; `DirectShow.cs` citava a seção errada (4) | caminhos atualizados para `.docs/`; seção corrigida para 3 e 5 |
| `.gitignore` | não ignorava `tools/re/__pycache__/` | `__pycache__/` acrescentado |
| Memória do assistente de IA (fora do repositório) | concentrava regras de segurança, de escopo e de engenharia reversa que não estavam no repositório; apontava para `docs/investigacao.md` | regras transferidas para o Harness; a memória passa a apontar para ele |
| `CLAUDE.md` | inexistente | criado como ponteiro para o Harness, sem regras próprias |

## Revisão de marcas registradas

Reaberta em 2026-11-09 a pedido do mantenedor: revisar a documentação para identificar marcas registradas citadas sem o aviso de compromisso com a propriedade intelectual de terceiros. **Nenhum documento tinha aviso de marcas antes desta revisão.**

| Documento | Marcas encontradas | Ação |
|---|---|---|
| `README.md` | OBSBOT, OBSBOT Tiny, OBSBOT Center, Windows, DirectShow, Linux, macOS, .NET, Avalonia, FlashCap, TouchOSC, Bitfocus Companion, Stream Deck, Inno Setup | aviso de projeto independente no topo e seção "Marcas registradas e propriedade intelectual" no fim; "confirmado modelo a modelo" trocado por "confirmado na Tiny" |
| `.docs/investigacao.md` | OBSBOT (e nomes de outros produtos nas tabelas), Microsoft, Windows, DirectShow, Linux, SigmaStar, Qt, OBS Studio, TouchOSC, Tencent, Arm/Cortex, U-Boot, Modbus, ZeroMQ, Inno Setup, .NET, Avalonia | aviso de marcas e PI no topo, incluindo que informações de outros modelos são incidentais |
| Documentos do `.harness/` | OBSBOT, Windows, Linux, macOS, Apple, Mac, Intel, Arm/Cortex, SigmaStar, .NET, Avalonia, FlashCap, DirectShow | aviso centralizado em `.harness/README.md` |
| `CLAUDE.md` | nenhuma | — |
| Textos de ajuda da CLI e comentários de código | OBSBOT, Remo Tech, OBSBOT Center | sem aviso necessário no código; o aviso no aplicativo e na release ficou como critério do [Épico 2](EPIC-2-windows-1.0.0.md) |

Criados: [`TRADEMARKS.md`](../../TRADEMARKS.md) na raiz (lista de marcas e titulares, uso nominativo, material de terceiros, sem garantia, contato para titulares) e [`ip-policy.md`](../ip-policy.md) no Harness (regras para o futuro, incluindo o aviso na Definition of Done).

**Achados da revisão:**

* **A marca do fabricante está nos nomes do projeto e dos produtos** (`oss-obsbot-driver`, `obsbot-studio`, `obsbotctl`, namespaces `Obsbot.*`). É o ponto de maior risco de parecer afiliação. Registrado como [ADR-0008](../decisions/ADR-0008-nomes-dos-produtos.md). O mantenedor escolheu renomear tudo para **OpenGimbalCam**, sigla OGC (CLI `ogcctl`, app `opengimbalcam-studio`), aplicado em 2026-11-09 depois de pesquisa de conflitos de nome.
* **O código opera qualquer câmera do fabricante** (VID 3564), não só a Tiny original, o que contraria o ADR-0007. A correção é critério do Épico 2.
* **O pacote de firmware em `.docs/original-driver/` pode não ser da Tiny original:** a câmera relata firmware 1.0.6.2 e SoC `SSC336Q`, e o pacote é 1.2.6.2 para `SSC339G`, junto com um manual da "Tiny 4K". A confirmação é a primeira tarefa do [Épico 3](EPIC-3-engenharia-reversa-firmware.md).

## Instruções transferidas para o Harness

| Instrução | Origem | Destino |
|---|---|---|
| Atender só a OBSBOT Tiny (PID FEF0, V1) | README, relatório §6.3/§10.4, memória | [ADR-0003](../decisions/ADR-0003-escopo-obsbot-tiny.md), reforçado como premissa no [ADR-0007](../decisions/ADR-0007-premissa-tiny-original.md), carta |
| Não gravar firmware; seletores 1/3/4/5 bloqueados sem `--force` | README "Segurança e limites", relatório §8 | [ADR-0005](../decisions/ADR-0005-sem-gravacao-de-firmware.md), governança |
| Análise só estática; não executar binários do fabricante | relatório §2/§10, memória | governança, regras de engenharia reversa |
| Arquivos do fabricante não versionados | README "Estrutura" | governança |
| Não reproduzir o `UserLoginToken` | relatório §10.2, memória | governança, regras de segurança |
| Movimento em hardware só com autorização; câmera precisa estar transmitindo | relatório §10.4, memória | governança, regras de segurança |
| Stack .NET + Avalonia | README, código | [ADR-0004](../decisions/ADR-0004-stack-dotnet-avalonia.md) |
| Validação marcada por plataforma, sem declarar o que não foi testado | README, relatório §8/§10.4/§11 | governança (DoD), matriz de validação |
| Fatos de hardware não óbvios | memória, relatório | [base de conhecimento](../knowledge/README.md) |

## Instruções descartadas ou revogadas

* **"Alternativa ao OBSBOT Center" como objetivo**: substituída pelo objetivo formal da carta (driver + aplicativo de controle, Windows 11 e Linux). O servidor OSC compatível continua no escopo como parte do P2.
* **Ambiguidade de "driver"**: a afirmação de que a OBSBOT não tem driver de kernel não foi descartada (é fato verificado), mas deixou de conflitar com o objetivo por meio do [ADR-0002](../decisions/ADR-0002-definicao-de-driver.md).
* **"Requer o .NET 8 SDK ou mais novo"**: continua correta para o código atual, mas conflita com o objetivo de suporte atualizado, pois o .NET 8 sai de suporte em 2026-11-10. Fica revogada como meta e vira o [Épico 1](EPIC-1-runtime-e-ci.md); o README será ajustado quando o código migrar.
* **"Outros modelos OBSBOT estão fora do escopo" como simples limitação técnica**: substituída pela premissa de não conflito do ADR-0007, que exclui modelos posteriores e câmeras com suporte por princípio, não só por falta de hardware.
* **"Confirmar o significado dos valores modelo a modelo"** (README): conflitava com a premissa e passou a "confirmar na Tiny".
* Nenhuma outra instrução em conflito foi encontrada.

## Achados

* O .NET 8 sai de suporte em 2026-11-10, um dia após a abertura deste épico. Prioridade máxima do roadmap.
* O backend Linux nunca foi testado com a câmera.
* **Refinamento do mantenedor (2026-11-09, após a conclusão):** o driver aberto substitui o driver original, porque esta versão da câmera não tem mais suporte nos drivers atuais do fabricante (ADR-0002 revisto). As plataformas passam a ser atendidas em sequência: Windows até a 1.0.0, depois o port para Linux; macOS fora ([ADR-0006](../decisions/ADR-0006-windows-primeiro.md)). Roadmap reorganizado em fases: Épico 2 = 1.0.0 no Windows, Épico 3 = port para Linux.
* **Segundo refinamento (2026-11-09):** nova frente de pesquisa, engenharia reversa do firmware, como Épico 3; o port para Linux passa a ser o Épico 4; o port para macOS entra como Épico 5, sem previsão.
* A mudança de `docs/` para `.docs/` feita pelo mantenedor ainda não está versionada (`git status`: exclusão de `docs/investigacao.md` e `.docs/` não rastreado).

## Resultado

Harness criado e em vigor. Documentação existente alinhada, com avisos de marcas e premissa de não conflito. Projeto renomeado para OpenGimbalCam (ADR-0008). Épicos 1 a 5 propostos e aguardando aceite do mantenedor.
