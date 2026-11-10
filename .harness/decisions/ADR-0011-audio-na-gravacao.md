# ADR-0011: Áudio na gravação do Studio com WASAPI (NAudio.Wasapi)

* **Status:** Proposta
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

O mantenedor decidiu, em 2026-11-09, que a gravação do `opengimbalcam-studio` precisa ter áudio na 1.0.0 ([Épico 2](../epics/EPIC-2-windows-1.0.0.md)). Hoje ela grava só vídeo MJPEG num AVI. A Tiny expõe o microfone como dispositivo UAC padrão, que o Windows atende com o driver nativo `usbaudio`. O FlashCap, usado para o vídeo, não captura áudio.

## Decisão

Capturar o microfone da Tiny por WASAPI, em modo compartilhado, com o pacote `NAudio.Wasapi` 2.2.1 (licença MIT; a 3.x marca `WasapiCapture` como obsoleta e quebra o build com `TreatWarningsAsErrors`), só no código Windows de `OpenGimbalCam.Devices`. O `Core` ganha uma abstração de captura de áudio sem dependência de plataforma. O áudio é gravado como PCM de 16 bits num segundo fluxo do mesmo AVI, e a taxa do vídeo é ajustada no fim da gravação para ficar em sincronia com o áudio. O dispositivo é escolhido pela mesma identificação da Tiny (`TinyIdentity`, [ADR-0007](ADR-0007-premissa-tiny-original.md)). Sem o microfone, a gravação continua só com vídeo, com aviso no log.

## Consequências

* Nova dependência de terceiros, restrita ao Windows. Ela não toca a câmera, só o endpoint de áudio do sistema.
* O Linux continua gravando só vídeo até a Fase 2, que vai precisar de um backend próprio (ALSA ou PipeWire).
* O AVI com PCM sem compressão é simples e toca em qualquer player, mas gera arquivos maiores. Compressão de áudio fica para depois da 1.0.0.
