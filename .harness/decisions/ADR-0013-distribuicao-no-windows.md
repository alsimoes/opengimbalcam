# ADR-0013: Distribuição no Windows

* **Status:** Aceita (2026-11-19)
* **Data:** 2026-11-19
* **Decisores:** André Simões

## Contexto

O [ADR-0004](ADR-0004-stack-dotnet-avalonia.md) prevê executáveis autocontidos por RID. O [Épico 2](../epics/EPIC-2-windows-1.0.0.md) exige um executável `win-x64` gerado pela CI, com instruções de instalação e uso, e a release 1.0.0 publicada com notas. Faltava decidir o formato. Em 2026-11-19 o mantenedor escolheu publicar no GitHub Releases um zip portátil e um instalador ([plano](../../.docs/plano-studio-e-release-1.0.0.md), Parte B).

## Decisão

Cada release do Windows publica três arquivos no GitHub Releases:

* `OpenGimbalCam-<versão>-win-x64.zip`: `opengimbalcam-studio.exe` e `ogcctl.exe` autocontidos, de arquivo único, com `LICENSE`, `TRADEMARKS.md`, `THIRD-PARTY-NOTICES.md`, `README.md`, `LEIA-ME.txt` e a pasta `licenses/`;
* `OpenGimbalCam-<versão>-win-x64-setup.exe`: instalador Inno Setup 6.7 com o mesmo conteúdo, por usuário e sem administrador, para Windows 11 x64, em pt-BR e inglês, com atalho no menu Iniciar;
* `SHA256SUMS.txt`, com o hash dos dois.

O workflow `release.yml` gera os três a partir de uma tag `vX.Y.Z` igual à versão do `Directory.Build.props` e cria um **rascunho** da release. Só o mantenedor publica. A 1.0.0 sai sem assinatura de código.

## Consequências

* Sem assinatura, o SmartScreen avisa quem baixa. O `LEIA-ME.txt`, o README e as notas de versão explicam como prosseguir. Assinar fica como tarefa futura do mantenedor, porque exige certificado.
* O Inno Setup só roda na CI; o repositório guarda apenas o script `.iss`.
* A versão tem uma única fonte, o `Directory.Build.props`. Tag diferente da versão faz o workflow falhar.
* O Linux ([Épico 4](../epics/EPIC-4-port-linux.md)) terá a própria forma de distribuição, em ADR novo ou em emenda a este.
