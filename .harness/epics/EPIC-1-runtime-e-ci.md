# Épico 1: Runtime com suporte e CI no Windows

* **Status:** Em andamento (desde 2026-11-09, branch `feature/dotnet10-ci`).
* **Produto:** P1 Driver + P2 Aplicativo · **Fase:** 1 (Windows)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Garantir que os dois produtos sejam construídos e testados no Windows, em runtime com suporte, a cada mudança. O backend Linux continua compilando ([ADR-0006](../decisions/ADR-0006-windows-primeiro.md)), mas a CI de Linux só entra na Fase 2.

## Critérios de aceite

- [x] `TargetFramework` em `net10.0` (LTS), com build e testes passando sem avisos (local, 2026-11-09: 0 avisos, 86 testes aprovados).
- [x] Dependências (Avalonia, FlashCap, CommunityToolkit.Mvvm, pacotes de teste) atualizadas para versões compatíveis.
- [ ] CI executando build e testes em `windows-latest` (workflow criado; falta a primeira execução verde no PR).
- [x] README atualizado com o SDK exigido.

## Tarefas

- [x] [T2] Trocar `net8.0` por `net10.0` em `Directory.Build.props`, fixar o SDK em `global.json` (10.0.403, `latestFeature`) e ajustar os caminhos de saída citados no README. Agrupada com as tarefas abaixo num só agente T2, por economia.
- [x] [T2] Atualizar pacotes NuGet e corrigir avisos novos do compilador (nenhum aviso novo).
- [x] [T2] Criar o workflow de CI (`.github/workflows/ci.yml`).
- [x] [T3] Revisar o diff.

## Achados

* O .NET 8 sai de suporte em 2026-11-10 (ADR-0004). O SDK 10.0 já está instalado na máquina de desenvolvimento.
* Pacotes: CommunityToolkit.Mvvm 8.4.0 → 8.4.2; FlashCap 1.10.0 → 1.13.0; Microsoft.NET.Test.Sdk 17.14.1 → 18.11.0; xunit.runner.visualstudio 3.0.2 → 4.0.1. xunit 2.9.3 já estava atualizado.
* Avalonia ficou em 11.3.22: compila no `net10.0`, e a 12.x é versão *major*. Migrar para a 12 é decisão à parte, candidata ao Épico 2.
* O build local inclui o backend Linux, que continua compilando. A CI de Linux só entra na Fase 2.

## Resultado

—
