# Épico 3: Engenharia reversa do firmware da câmera

* **Status:** Proposto (depois da 1.0.0, antes do port para Linux)
* **Produto:** Pesquisa (frente de firmware, veja a [carta](../charter.md#frente-de-pesquisa-firmware))
* **Abertura:** 2026-11-09 · **Conclusão:** —

## Objetivo

Entender o firmware da OBSBOT Tiny a ponto de permitir **aprimorar a câmera** e abrir **novas possibilidades de uso**, independentes do fabricante. O resultado é conhecimento documentado e ferramentas de análise, mais uma proposta fundamentada sobre se, e como, modificar o firmware com segurança.

## Ponto de partida

O relatório ([`.docs/investigacao.md`](../../.docs/investigacao.md), §7) já mapeou o pacote `Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin`:

* SoC **SigmaStar SSC339G** com Linux 4.9 e U-Boot 2015.01, em contêiner `AMAC` com 8 partições (`KERNEL`, `RECOVERY`, `ROOTFS.ubi`, `AIFS.ubi`, `UPDATERFS.ubi`, `UBOOT0/1`, `config`);
* firmware do MCU do gimbal (Cortex-M) e do MCU do motor de yaw;
* o fluxo de atualização do OBSBOT Center (seletores 1, 3 e 5, `bspatch`, `UPDATERFS`).

O `ogcctl fw info|verify|extract` já lê, verifica e extrai os componentes.

**Atenção à identidade do firmware.** O OBSBOT Center registrou que a câmera conectada roda o firmware 1.0.6.2 com SoC `SSC336Q` (relatório §10.2), enquanto o pacote analisado é a versão 1.2.6.2 para o SoC `SSC339G`, acompanhado de um manual da "Tiny 4K". Isso indica que **o pacote pode ser de outro modelo**. Pela premissa do [ADR-0007](../decisions/ADR-0007-premissa-tiny-original.md), a primeira tarefa do épico é confirmar qual firmware pertence à Tiny original; um firmware de outro modelo não é analisado.

## Limites (ADR-0005 e ADR-0007 continuam valendo)

* Só o firmware da **OBSBOT Tiny original** é estudado. Firmwares de outros modelos, em especial dos que ainda têm suporte do fabricante, ficam fora.
* Partições, sistemas de arquivos, modelos de IA e qualquer saída de extração **não são versionados** ([política de PI](../ip-policy.md)).

* A análise é **estática**, sobre o arquivo de firmware. Nada é gravado na câmera.
* Os seletores 1, 3, 4 e 5 não são usados, nem para leitura: o seletor 3 executa shell na câmera.
* Executar binários do firmware (por exemplo, em emulador) é execução de binário do fabricante e exige ADR aceito.
* Qualquer gravação de firmware, mesmo de teste, exige um novo ADR que substitua o ADR-0005, com plano de recuperação documentado e autorização explícita do mantenedor.

## Critérios de aceite

- [ ] Sistemas de arquivos `ROOTFS`, `AIFS` e `UPDATERFS` extraídos e documentados (estrutura, serviços, versões de componentes).
- [ ] Do lado da câmera: localizados os tratadores da Extension Unit (seletores 2, 6, 7) e do canal `0xAA`, com o espaço de comandos documentado além do que a `libdev.dll` usa.
- [ ] Pipeline de vídeo e de IA mapeado (UVC gadget, ISP, modelos em `AIFS`), com as funções ocultas ou não expostas pelo OBSBOT Center listadas.
- [ ] Firmwares dos MCUs (gimbal e yaw) mapeados: protocolo com o SoC e limites de movimento.
- [ ] Caminhos de recuperação identificados e avaliados (U-Boot, `RECOVERY`, `UPDATERFS`, UART), sem testá-los no hardware.
- [ ] Lista priorizada de aprimoramentos e novos usos viáveis, cada um classificado como "possível só pelo driver" ou "exige firmware modificado".
- [ ] ADR proposto sobre modificação de firmware (aceitar, adiar ou rejeitar), com riscos e plano de recuperação.
- [ ] Ferramentas novas em `tools/re/` e achados na [base de conhecimento](../knowledge/README.md).

## Tarefas

- [ ] [T3] Confirmar qual pacote de firmware pertence à Tiny original (SoC, versão, nome de produto) e descartar os de outros modelos.
- [ ] [T2] Extrair as partições com `ogcctl fw extract` e desmontar as imagens UBI/UBIFS com ferramenta própria ou biblioteca em venv.
- [ ] [T2] Inventariar o rootfs: binários, scripts de inicialização, módulos do kernel, configuração.
- [ ] [T3] Identificar o binário que atende a UVC/XU e desmontar os tratadores (ARM).
- [ ] [T3] Desmontar os firmwares dos MCUs (Cortex-M).
- [ ] [T3] Analisar `config`, `RECOVERY` e `UPDATERFS` em busca de caminhos de recuperação.
- [ ] [T3, aceite HT] Consolidar a lista de oportunidades e redigir o ADR.

## Achados

—

## Resultado

—
