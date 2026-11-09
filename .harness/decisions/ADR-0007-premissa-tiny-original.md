# ADR-0007: Premissa de não conflito: só a OBSBOT Tiny original

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões
* **Complementa:** [ADR-0003](ADR-0003-escopo-obsbot-tiny.md)

## Contexto

O projeto não quer entrar em conflito com a fabricante das câmeras OBSBOT. A razão de ser do projeto é dar uma vida útil maior a uma câmera que o fabricante deixou de suportar, não competir com os produtos que ele ainda vende e mantém. O ADR-0003 restringiu o escopo à Tiny por razões técnicas (é o único hardware de teste); esta decisão transforma essa restrição em **premissa do projeto**, com motivação de respeito ao fabricante.

## Decisão

1. **Alvo exclusivo:** o driver, o aplicativo de controle e qualquer trabalho sobre firmware têm como alvo **apenas a OBSBOT Tiny original**: `USB\VID_3564&PID_FEF0`, identificada pelo USB como "OBSBOT Tiny", `ObsbotProductType` 0.
2. **Exclusões:** ficam fora, sem exceção, todos os modelos posteriores do fabricante (incluindo outras câmeras da linha Tiny) e, **principalmente, qualquer câmera que ainda tenha suporte do fabricante**.
3. **No código:** o software só opera dispositivos que atendam ao item 1. Outros dispositivos do mesmo fabricante não são controlados, nem por caminho alternativo, nem por opção escondida.
4. **Na pesquisa:** não se faz engenharia reversa de software, firmware ou protocolo específicos de outros modelos. Informações sobre outros modelos que aparecerem incidentalmente (por exemplo, tabelas da `libdev.dll` que cobrem vários PIDs) ficam só como registro, sem trabalho adicional.
5. **Boa-fé:** se o fabricante manifestar objeção, ou voltar a dar suporte à Tiny original, o mantenedor reavalia o projeto de boa-fé.

## Consequências

* O código atual opera qualquer câmera com VID 3564 (`list`, ponte OSC). Restringir ao PID FEF0 é critério de aceite do [Épico 2](../epics/EPIC-2-windows-1.0.0.md).
* Antes de analisar um firmware (Épico 3), é preciso confirmar que ele é da Tiny original. Arquivos de outros modelos são descartados da análise.
* Ampliar o suporte a outro modelo exige novo ADR que demonstre que o modelo não tem mais suporte do fabricante, e a regra 2 continua valendo.
