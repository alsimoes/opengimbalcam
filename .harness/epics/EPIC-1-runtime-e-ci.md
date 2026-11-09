# Épico 1: Runtime com suporte e CI no Windows

* **Status:** Aceito pelo mantenedor em 2026-11-09. Passa a `Em andamento` após o merge do PR da adoção do Harness.
* **Produto:** P1 Driver + P2 Aplicativo · **Fase:** 1 (Windows)
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Garantir que os dois produtos sejam construídos e testados no Windows, em runtime com suporte, a cada mudança. O backend Linux continua compilando ([ADR-0006](../decisions/ADR-0006-windows-primeiro.md)), mas a CI de Linux só entra na Fase 2.

## Critérios de aceite

- [ ] `TargetFramework` em `net10.0` (LTS), com build e testes passando sem avisos.
- [ ] Dependências (Avalonia, FlashCap, CommunityToolkit.Mvvm, pacotes de teste) atualizadas para versões compatíveis.
- [ ] CI executando build e testes em `windows-latest`.
- [ ] README atualizado com o SDK exigido.

## Tarefas

- [ ] [T1] Trocar `net8.0` por `net10.0` em `Directory.Build.props` e ajustar os caminhos de saída citados no README.
- [ ] [T2] Atualizar pacotes NuGet e corrigir avisos novos do compilador.
- [ ] [T2] Criar o workflow de CI.

## Achados

* O .NET 8 sai de suporte em 2026-11-10 (ADR-0004). O SDK 10.0 já está instalado na máquina de desenvolvimento.

## Resultado

—
