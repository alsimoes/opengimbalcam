# ADR-0008: Nomes dos produtos sem a marca do fabricante

* **Status:** Aceita (opção 1, em 2026-11-09)
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

A revisão de marcas do Épico 0 encontrou a marca "OBSBOT" nos próprios nomes do projeto e dos produtos:

* repositório `oss-obsbot-driver`;
* aplicativo `obsbot-studio` ("OBSBOT Studio", pastas `Imagens/OBSBOT Studio` e `Vídeos/OBSBOT Studio`);
* CLI `obsbotctl`, solução `ObsbotCtl.slnx`;
* namespaces e projetos `Obsbot.Core`, `Obsbot.Devices`, `Obsbot.Osc`.

Usar a marca de outra empresa no nome de um produto é o tipo de uso que mais facilmente sugere afiliação ou endosso, e contraria a premissa de não conflito ([ADR-0007](ADR-0007-premissa-tiny-original.md)). Citar a marca para descrever compatibilidade ("para a OBSBOT Tiny") é uso nominativo e continua permitido.

## Opções

1. **Renomear tudo** (repositório, produtos, executáveis, namespaces) para um nome neutro, descrevendo a compatibilidade no texto: "<nome>, driver e aplicativo abertos compatíveis com a OBSBOT Tiny".
2. **Renomear só o que o usuário vê** (repositório, aplicativo, CLI, pastas de saída) e manter os namespaces internos até um momento oportuno.
3. **Manter os nomes** e confiar no aviso de marcas.

## Recomendação

Opção 1 ou 2, antes da release 1.0.0, quando a mudança ainda é barata e nenhum usuário depende dos nomes atuais.

## Decisão

**Opção 1: renomear tudo.** Repositório, produtos, executáveis, solução, projetos e namespaces passam a usar um nome neutro, sem a marca do fabricante nem nomes de produtos dele (inclusive "Tiny"). A marca só aparece em texto, para descrever a compatibilidade.

### Nomes adotados

| Antes | Depois |
|---|---|
| repositório `oss-obsbot-driver` | `alsimoes/opengimbalcam` (renomeado no GitHub em 2026-11-09; o GitHub redireciona o endereço antigo; a pasta local continua `oss-obsbot-driver`) |
| solução `ObsbotCtl.slnx` | `OpenGimbalCam.slnx` |
| `Obsbot.Core`, `Obsbot.Devices`, `Obsbot.Osc`, `Obsbot.Tests` | `OpenGimbalCam.Core`, `.Devices`, `.Osc`, `.Tests` (projetos, pastas e namespaces) |
| CLI `obsbotctl` (projeto `ObsbotCtl`) | `ogcctl` (projeto `OpenGimbalCam.Cli`); a ajuda se identifica como "OpenGimbalCam CLI" |
| app `obsbot-studio` (projeto `ObsbotStudio`) | `opengimbalcam-studio` (projeto `OpenGimbalCam.Studio`), janela e pastas "OpenGimbalCam Studio" |
| arquivos de foto/vídeo `obsbot-<data>` | `opengimbalcam-<data>` |

**Pesquisa de nome (2026-11-09):**

* "OpenGimbalCam" por extenso não tem ocorrências conhecidas. Já "GimbalCam" sozinho é o nome de uma câmera de ação comercial (Gimboo, 2017) e se parece com o app "Gimbal Cam" (National Geographic/Thinkware). Por isso **"GimbalCam" nunca aparece sozinho** em nomes; uma primeira versão com `gimbalcamctl`/"GimbalCam Studio" foi descartada.
* A sigla **OGC** está livre no setor de câmeras, mas é marca do Open Geospatial Consortium e domina o vocabulário de software geoespacial. Regra: a sigla pode ser usada **informalmente** e no comando curto `ogcctl`; nomes que o público vê (janela, instalador, página, pacotes) usam **o nome completo**. Não usar "OGC Studio", "OGC Cam" nem o prefixo `Ogc.*` em pacotes. Na primeira menção em textos públicos, escrever "OpenGimbalCam (OGC)".
* Antes de um lançamento público, fazer a busca nas bases oficiais de marcas, conforme o guia do [Épico 6](../epics/EPIC-6-busca-de-marcas.md).

Nomes de tipos que **descrevem o dispositivo ou o protocolo do fabricante** (`ObsbotXu`, `ObsbotCamera`, `ObsbotVendorId`, `IsObsbot`, `ObsbotProductType`) foram mantidos: são uso nominativo dentro do código, não nomes de produto.

## Consequências

* A renomeação foi feita imediatamente, antes de qualquer release.
* O repositório no GitHub foi renomeado para `alsimoes/opengimbalcam` em 2026-11-09.
* Exceções necessárias para interoperar continuam: endereços OSC `/OBSBOT/WebCam/…`, o GUID da Extension Unit e constantes de VID/PID.
