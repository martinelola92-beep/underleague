# BQ-A — El nodo de evento se abre sin nada que escoger

**Estado:** CERRADA (27 sep 2026)

## Observación

Revisor: *«si sale en el mapa, pero entras en el nodo y no hay nada que escoger. Ese es el problema»*.

## Hipótesis

| | hipótesis | estado |
|---|---|---|
| H1 | El mapa no genera eventos | **REJECTED**: 4,91 eventos por acto (200 semillas × 3 actos, `MapGenerator.Generate`) |
| H2 | La carta sale vacía porque `EventView.Build` devuelve null | **CONFIRMED** (abajo) |
| H3 | La build del revisor es anterior a la ADR 0100 | REJECTED por H2: el fallo está en `HEAD` |
| H4 | Hay tan pocas cartas que no se notan | No es la causa del síntoma; sigue siendo un problema de contenido (`docs/plan-diversion.md` §1) |

## Causa (CONFIRMED)

`MapScreen.OnNodePressed` trataba el evento como al entrenamiento: navegaba a `NodeScreen` **sin abrir el
nodo**, para «enseñarlo antes de entrar». Desde la ADR 0100 el evento ya no se resuelve solo: es una carta
que se elige **con el nodo abierto**, y `EventView.Build` devuelve null mientras `PendingNodeId < 0`.
`NodeScreen` no ofrece ningún botón de entrar para el evento (sólo `BuildSelfResolving` para el
entrenamiento), así que se quedaba en `ui.node.eventNone`.

- Test `EventTests.TheCardIsOnlyVisibleOnceTheEventNodeIsOpen`: vista null antes de abrir y con opciones
  después.
- Captura `--tour-event` (recorrido nuevo, `Game/Ui/Tour.cs`) con el arreglo: `evento.png` enseña «La
  vigilia de hierro» con sus dos opciones.

**BA-A** (13 sep) vio el mismo síntoma y lo cerró arreglando que no se pudiera salir, pero no llegó a la
causa: la pantalla sin opciones seguía ahí.

## Arreglo

El mapa abre el evento directamente, como la clínica (`_run.Enter` + `Nav.Route`). Abrirlo no resuelve
nada; salir sin elegir equivale a seguir camino.

## Hermanos anotados, sin arreglar

- **Texto interno visible para el jugador.** `ui.node.eventBody` termina en «(ADR 0100)», y hay al menos
  17 cadenas más de `UiText.cs` con `RF-xxx`/`ADR xxxx` a la vista (`ui.start.clubHint`,
  `ui.node.clinicBody`, `ui.end.causeBoss`…). Es un patrón, no un caso suelto.
- **Huecos declarados en la propia interfaz** que afectan al plan de diversión: `ui.scout.refereeGap` (los
  rasgos de árbitro y los sobornos no existen, todos neutros) y `ui.report.refereeGap` (las faltas no
  señaladas no se registran).
