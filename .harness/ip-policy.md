# Propriedade intelectual e marcas

O projeto se compromete a respeitar a propriedade intelectual de terceiros. Esta política vale para código, documentação, interface, nomes, commits, issues e releases.

## Marcas registradas

1. **Uso nominativo apenas.** Marcas de terceiros (OBSBOT, Windows, Linux, macOS etc.) só aparecem para identificar com o que o projeto é compatível ou o que foi analisado. Nunca para sugerir afiliação, patrocínio ou endosso.
2. **Aviso obrigatório.** Todo documento público que cite marcas de terceiros deve trazer o aviso de marcas ou um link para [`TRADEMARKS.md`](../TRADEMARKS.md). O README e os documentos de `.docs/` trazem o aviso no próprio texto; os documentos do Harness são cobertos pelo aviso em [`.harness/README.md`](README.md).
3. **Novas marcas.** Quem citar uma marca nova num documento público acrescenta-a ao `TRADEMARKS.md`.
4. **Sem identidade visual de terceiros.** Não usar logotipos, ícones, fontes, cores ou layout que imitem os do fabricante ou do OBSBOT Center.
5. **Nomes de produtos.** Os produtos do projeto não devem ter nomes que se confundam com os do fabricante. A situação atual está em avaliação no [ADR-0008](decisions/ADR-0008-nomes-dos-produtos.md).
6. **Compatibilidade de protocolo** é permitida: endereços OSC como `/OBSBOT/WebCam/…` e o GUID da Extension Unit são necessários para interoperar e não são uso de marca como nome de produto.

## Material do fabricante

1. **Nada do fabricante é redistribuído:** instaladores, binários, DLLs, firmwares, partições extraídas, modelos de IA, manuais e arquivos de configuração ficam fora do Git (`.docs/original-driver/` e qualquer saída de extração).
2. A documentação descreve o comportamento observado e cita **trechos mínimos** (endereços, poucas instruções desmontadas, strings) só quando necessários para documentar a interoperabilidade.
3. O código do projeto é escrito do zero. Não se copia código, tabela extensa ou recurso do fabricante para dentro do repositório além do necessário para interoperar (por exemplo, IDs de protocolo).
4. A engenharia reversa segue as regras de [`governance.md`](governance.md) (análise estática, sem executar binários do fabricante) e a premissa do [ADR-0007](decisions/ADR-0007-premissa-tiny-original.md).

## Contato de titulares

Um titular de direitos que entenda haver uso indevido pode abrir uma issue no repositório. O mantenedor avalia de boa-fé e, se for o caso, corrige ou remove o conteúdo.
