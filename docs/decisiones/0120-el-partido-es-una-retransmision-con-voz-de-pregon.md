# 0120 — El partido es una retransmisión con voz de pregón

Estado: **Aceptada** (19 sep 2026, **decisión del revisor** en las fases A–D.3 de la dirección de UI). Registra
la tabla de §10 de `docs/ui/README.md`: los requisitos y ADR que la dirección contradice (RT-057: nada se
cambia en silencio). La arquitectura que la implementa es la **ADR 0119**. El razonamiento completo, las
capturas y las mediciones están en `docs/ui/README.md` y no se repiten aquí.

## Problema

La pantalla de Partido se hizo como herramienta de lectura del motor —campo 2D, log bajo el campo, fichas de
24 px de los dos equipos— y los requisitos de presentación (RF-115/116, RA-020, RA-025) se escribieron antes
de medir un partido. La fase A midió 8,6 momentos narrables por partido, 14,5 perks que saltan en el primer
segundo y un 87 % de lesiones leves propias con decisión. Con esos números el revisor fijó una dirección
—**gramática de retransmisión, materia de fiesta popular medieval, voz de pregón**— que no cabe en varios
requisitos vigentes.

## Decisión

| Vigente | Pasa a ser |
|---|---|
| **RA-025** «cultura futbolística real» | espectáculo deportivo **popular medieval** (calcio storico, Palio) con **gramática** de retransmisión: tela y heráldica, madera, papel y pregón. Se conservan el humor negro, la sangre persistente (RA-027) y los patrocinadores de parodia |
| **RA-026** sin calaveras ni gótico | se mantiene en espíritu; además, **sin estética de cómic** para los acontecimientos (se leyó infantil, D.2–D.3) |
| **RF-115 / RA-020 / RA-022** highlights ilustrados por capas | los acontecimientos se **proclaman**: sello (N1/N2), estandarte heráldico (N3), bando con lacre y acta (N4). El panel ilustrado por capas, y su cobertura mínima, quedan sin plan hasta que haya arte |
| **RA-021** estilo cómic de alto contraste | **retirado** para los acontecimientos: imprenta del XVII y heráldica (IM Fell, nunca gótica) |
| **RF-116** máximo 2 highlights por partido, por puntuación | **gramática por niveles** N0–N4 con una sola voz alta a la vez: N3 en cada gol, roja, lesión grave y turba (~2,8 por partido), N4 en muerte y final. No hay tope por partido; lo limitan la rareza del suceso y la cola de la voz alta |
| **ADR 0114** cámara de cuatro estados | **cámara táctica fija** a campo entero (ortográfica, 60°) con **gestos** (sacudida, acercamiento breve) que anuncian una clase de situación, nunca un resultado. La parte de la ADR 0114 sobre presentación fuera del partido sigue vigente |
| **ADR 0112** cartel de perk de 1 s | se mantiene, **sin carteles en el saque inicial** (P1). La visibilidad de la build (P2: el 65 % de los partidos no enseña ningún perk propio en juego) queda para `game-design-review` |
| **UI-010 / UI-011 / UI-013** la misma ficha de 24 px en todas las pantallas, tiras de los dos equipos | en el partido, **tiras de 72 px solo del equipo propio** (escudo, dorsal, nombre, puesto y raza, perks, estado por color **y** forma), que destellan con su perk. El rival se lee en el tablero. UI-010/011 siguen vigentes fuera del partido |
| **§6.3** «log bajo el campo» | el log pasa al **modo depuración** y a la crónica del informe |
| **UI-021** Equipo primero | la dirección se fijó empezando por el partido; Equipo derivará de ella y no al revés. Constancia, no cambio de regla |

Sin cambios: UI-002 (color **y** forma), UI-004 (texto mínimo; la dirección pide 20 px lógicos, ~13 px
efectivos a 1280×800), UI-005 (sobre el campo solo lo transitorio), RF-012d (el riesgo se explica antes del
partido) y la regla 10 de `CLAUDE.md`: todo se implementa con **marcadores de posición**. Las fuentes (IM Fell,
Barlow Condensed, Cinzel, licencia OFL) son provisionales.

## Consecuencias

- `docs/requisitos.md` anota cada requisito afectado con un puntero a esta ADR; el texto original no se borra.
- Las decisiones abiertas de `docs/ui/README.md` §9 (C3–C12) siguen abiertas. La implementación de la fase E
  aplica la lectura más conservadora en cada una y lo marca como *provisional*: C4, la lesión leve con
  decisión, **congela** (principio 9: las decisiones pausan).
- **RF-050**: las velocidades pasan a ser x1 / x4 «retransmisión comprimida» / x16 «ir al resultado» (C7, ya
  eran las de la pantalla actual); x1,5 y x2 no existen. x16 es el «saltar al resultado» que conserva las
  decisiones. RF-050b (x4 legible) sigue vigente y es lo que la política de x4 persigue.
