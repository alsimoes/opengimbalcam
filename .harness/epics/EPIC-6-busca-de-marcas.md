# Épico 6: Busca de marcas registradas antes do lançamento público

* **Status:** Aceito pelo mantenedor em 2026-11-09, em paralelo ao Épico 1 por decisão dele (HT: aguardando). **Precisa estar concluído antes da release 1.0.0** (Épico 2).
* **Produto:** Governança (propriedade intelectual)
* **Abertura:** 2026-11-09 · **Conclusão:** —
* **Tier:** **HT** (Human Tier, veja [`tiers.md`](../tiers.md)). Executor: o mantenedor. As bases oficiais de marcas exigem navegação interativa e às vezes cadastro, o que um agente não consegue fazer.

## Objetivo

Confirmar, nas bases oficiais de marcas, que os nomes do projeto não colidem com marcas registradas que poderiam causar conflito. Os nomes são **OpenGimbalCam**, a sigla **OGC**, `ogcctl` e **OpenGimbalCam Studio**. Os resultados ficam registrados aqui para embasar a decisão de lançar com esses nomes ([ADR-0008](../decisions/ADR-0008-nomes-dos-produtos.md)).

> Esta busca é uma checagem de bom senso, não um parecer jurídico. Se aparecer algo preocupante, a recomendação é consultar um advogado de propriedade intelectual antes de lançar.

## Conceitos em 1 minuto

* **Marca registrada** protege um nome para certos tipos de produto ou serviço, organizados em **classes de Nice** (45 classes, numeradas). O conflito só existe, em geral, quando o nome é igual ou parecido **e** a classe ou o mercado são próximos.
* **Classes que nos interessam:**
  * **9:** software baixável, câmeras e equipamentos de vídeo. É a principal.
  * **42:** serviços de software e desenvolvimento.
  * **38:** transmissão e streaming. Secundária.
* **Status:** só importa a marca **viva** (*live*, *registered*, *pending*, "em vigor", "pedido em andamento"). Marca **morta** (*dead*, *cancelled*, *expired*, "arquivada", "extinta") não é obstáculo.

## Termos a pesquisar

| # | Termo | Por quê |
|---|---|---|
| T1 | `OPENGIMBALCAM` | nome do projeto |
| T2 | `OPEN GIMBAL CAM` | variação com espaços |
| T3 | `GIMBALCAM` | já existe um produto comercial com esse nome (Gimboo, 2017) |
| T4 | `GIMBAL CAM` | variação; existe o app "Gimbal Cam" (National Geographic/Thinkware) |
| T5 | `OGC` | sigla; marca conhecida do Open Geospatial Consortium |
| T6 | `OGCCTL` | nome da CLI |

## Guia passo a passo

Para cada base, pesquise os termos T1 a T6 e anote os resultados vivos nas classes 9, 42 e 38 na tabela de registro. Reserve cerca de 1 a 2 horas no total. Os endereços abaixo eram os vigentes em novembro de 2026; se algum mudar, procure o nome do serviço num buscador.

### 1. TMview: Europa, Brasil e dezenas de outros países numa busca só (comece por aqui)

1. Abra **https://www.tmdn.org/tmview**. Não precisa de cadastro.
2. No campo de busca, digite o termo (ex.: `opengimbalcam`) e pesquise.
3. No painel de filtros, restrinja **Nice class** a 9, 42 e 38, e **Trade mark status** a "Filed" e "Registered".
4. Para cada resultado, abra o detalhe e anote: nome, titular, país/escritório, classes, status e número.
5. Repita para os termos T2 a T6. Para `OGC`, espere muitos resultados; anote os da classe 9.

### 2. USPTO: Estados Unidos

1. Abra **https://tmsearch.uspto.gov**. Não precisa de cadastro.
2. Pesquise o termo. Na lista, use o filtro **Status = Live** e observe a coluna **Class** (IC 009, 042, 038).
3. Abra os resultados relevantes e anote o número de registro ("Reg. No." ou "Serial No."), o titular e os produtos descritos ("Goods and Services").
4. Repita para T2 a T6.

### 3. WIPO Global Brand Database: marcas internacionais (Sistema de Madri) e outros países

1. Abra **https://branddb.wipo.int**.
2. Pesquise por "Brand name" com o termo. Filtre **Nice class** 9, 42 e 38 e **Status** ativo.
3. Anote os resultados que não apareceram no TMview ou no USPTO.

### 4. INPI: Brasil (confirmação local)

1. Abra **https://busca.inpi.gov.br/pePI/**. Se pedir login, use a opção de acesso sem cadastro ("Continuar"), quando disponível. Se não houver, crie uma conta gratuita.
2. Vá em **Marcas → Pesquisa**, tipo de pesquisa **"Radical"** (acha o termo dentro de nomes maiores), e digite o termo.
3. Observe a **classe NCL** (9, 42, 38) e a **situação** (pedido ou registro em vigor).
4. Anote os resultados vivos. O TMview já cobre o INPI, mas a pesquisa por radical do INPI acha variações que a busca exata deixa passar.

### 5. Como interpretar

| Situação encontrada | Avaliação | Ação |
|---|---|---|
| Nenhum resultado vivo nas classes 9/42/38 | verde | seguir com o nome |
| Resultado vivo em classe distante (ex.: roupas, alimentos) | verde | só registrar |
| Resultado vivo em 9/42/38, mas nome diferente o bastante (ex.: "GIMBAL POD") | amarelo | registrar e avaliar com o agente; em geral seguir |
| `GIMBALCAM`/`GIMBAL CAM` vivo na classe 9 | amarelo | o nome completo "OpenGimbalCam" continua aceitável, mas é proibido usar "GimbalCam" sozinho (ADR-0008 já exige isso) |
| `OGC` vivo na classe 9 ou 42 | amarelo | manter "OGC" só informal e na CLI; nunca em nome de produto, logo ou pacote (ADR-0008) |
| `OPENGIMBALCAM` (ou quase igual) vivo em 9/42 | vermelho | não lançar; consultar advogado ou escolher outro nome por novo ADR |

### 6. Registro e encerramento

1. Preencha a tabela de resultados abaixo. Prints de tela podem ficar fora do Git; basta o link ou o número de cada registro.
2. Peça a um agente para revisar a tabela e atualizar o ADR-0008 com a conclusão.
3. Opcional, para o futuro: avaliar registrar a própria marca "OpenGimbalCam". Isso fica para uma decisão separada.

## Tarefas

- [ ] [HT] Pesquisar T1 a T6 no TMview.
- [ ] [HT] Pesquisar T1 a T6 no USPTO.
- [ ] [HT] Pesquisar T1 a T6 no WIPO Global Brand Database.
- [ ] [HT] Pesquisar T1 a T6 no INPI (pesquisa por radical).
- [ ] [HT] Preencher a tabela de resultados.
- [ ] [T3] Revisar a tabela e registrar a conclusão no ADR-0008.

## Critérios de aceite

- [ ] T1 a T6 pesquisados no TMview, no USPTO, no WIPO e no INPI, com a data da busca.
- [ ] Tabela de resultados preenchida, com avaliação verde, amarela ou vermelha por termo.
- [ ] Conclusão registrada no ADR-0008: manter os nomes ou abrir novo ADR.

## Resultados

| Data | Base | Termo | Resultado vivo relevante (nome, titular, classe, nº) | Avaliação |
|---|---|---|---|---|
| | | | | |

## Achados

* Pesquisa preliminar por agente em 2026-11-09, com fontes não oficiais: nenhum registro encontrado para "OpenGimbalCam". "GimbalCam" é um produto de 2017. "OGC" é marca do Open Geospatial Consortium, com o registro dos EUA nº 3010886 nas classes 16 e 35, segundo agregadores. Esses dados precisam ser confirmados nas bases oficiais.

## Resultado

—
