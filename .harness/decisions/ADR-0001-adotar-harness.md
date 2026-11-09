# ADR-0001: Adotar o Harness como estrutura de governança

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

As regras do projeto estavam espalhadas entre o `README.md`, o relatório de investigação e a memória de um assistente de IA, fora do repositório. Não havia definição escrita do objetivo nem lugar para decisões e planejamento.

## Decisão

Toda a governança do projeto fica na pasta `.harness/`, com a precedência definida em [`.harness/README.md`](../README.md). Documentos fora dela são informativos e não podem contradizer o Harness.

## Consequências

* O Épico 0 varre o workspace, transfere para o Harness as instruções pertinentes e corrige o que conflita.
* Assistentes de IA entram pelo `CLAUDE.md` na raiz, que aponta para o Harness.
