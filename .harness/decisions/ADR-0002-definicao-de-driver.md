# ADR-0002: O driver aberto substitui o driver original em modo usuário

* **Status:** Aceita
* **Data:** 2026-11-09
* **Decisores:** André Simões

## Contexto

Esta versão da OBSBOT Tiny não tem mais suporte nos drivers atuais do fabricante. O objetivo do projeto é substituir o driver original da câmera por uma versão de código aberto.

A investigação ([`.docs/investigacao.md`](../../.docs/investigacao.md), §1, §3, §4 e §11) mostrou como esse driver original é feito:

* o instalador oficial não traz `.inf`, `.sys` nem `.cat`;
* vídeo e áudio são atendidos pelos drivers genéricos do sistema (`usbccgp`, `usbvideo`, `usbaudio` no Windows; `uvcvideo` no Linux);
* todo o controle específico da OBSBOT (gimbal, IA, Extension Unit, canal `0xAA`) fica na `libdev.dll`, em modo usuário.

Ou seja, o "driver" do fabricante é a `libdev.dll` e o software que a usa. Um driver de kernel próprio não acrescentaria função e traria custo alto (assinatura de driver no Windows, manutenção por versão de kernel no Linux).

## Decisão

O produto P1 "Driver" é a versão aberta do driver original: uma camada em modo usuário que substitui a `libdev.dll` (`OpenGimbalCam.Core` + `OpenGimbalCam.Devices`, com a CLI `ogcctl` como ferramenta), mais a integração com o sistema operacional necessária para o usuário comum usar a câmera. Ele se apoia nos drivers UVC/UAC genéricos do sistema, que continuam suportados. Um driver de kernel só entra no escopo por novo ADR que demonstre uma função impossível em modo usuário.

## Consequências

* "Atualizado" significa funcionar nas versões atuais do Windows 11 (e, na Fase 2, de kernels Linux em suporte), em runtime .NET com suporte.
* A distribuição do P1 é uma biblioteca e uma ferramenta, não um pacote de driver assinado.
