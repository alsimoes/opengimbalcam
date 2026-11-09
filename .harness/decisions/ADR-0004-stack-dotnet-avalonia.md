# ADR-0004: C#/.NET com Avalonia para os dois produtos

* **Status:** Aceita
* **Data:** 2026-11-09 (registra a escolha feita na implementação inicial)
* **Decisores:** André Simões

## Contexto

Os dois produtos precisam rodar no Windows 11 e no Linux a partir de um só código. A implementação existente usa .NET (`net8.0`), P/Invoke/COM para DirectShow e Kernel Streaming, `ioctl` para V4L2, Avalonia 11 para a interface e FlashCap para o vídeo.

## Decisão

Manter C#/.NET, Avalonia e FlashCap. O projeto acompanha a versão **LTS com suporte** do .NET; quando uma LTS sai de suporte, a migração para a seguinte é prioritária.

## Consequências

* O .NET 8 sai de suporte em 2026-11-10. A migração para o .NET 10 (LTS) é o Épico 1.
* Executáveis autocontidos por RID (`win-x64`, `linux-x64`) são o formato de distribuição.
