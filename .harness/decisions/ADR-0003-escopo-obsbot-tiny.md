# ADR-0003: Escopo restrito à OBSBOT Tiny (PID FEF0, protocolo V1)

* **Status:** Aceita; complementada pelo [ADR-0007](ADR-0007-premissa-tiny-original.md)
* **Data:** 2026-11-09 (registra decisão do mantenedor de 2026-10-28)
* **Decisores:** André Simões

## Contexto

A `libdev.dll` atende vários modelos, com dois protocolos `0xAA` (V1 e V3) escolhidos pelo `ObsbotProductType`. A única câmera disponível para testes é a OBSBOT Tiny (`USB\VID_3564&PID_FEF0`, tipo 0, V1). Em 2026-10-28 o código de outros modelos foi removido (commit `cc83c08`).

## Decisão

O projeto atende só a OBSBOT Tiny. Protocolo V3, rotas OSC de outros modelos (por exemplo `Meet/…`) e ramos por modelo ficam fora do escopo. As tabelas de outros modelos permanecem na base de conhecimento apenas como registro.

## Consequências

* Rotas OSC sem efeito na Tiny continuam registradas no log com o motivo, para compatibilidade com clientes do OBSBOT Center.
* Ampliar o suporte exige novo ADR e hardware para validação.
