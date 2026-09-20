# BE-A — El centrocampista nunca entra a su marcado sin balón (el comentario dice una cosa y el código otra)

Estado: **abierta, sin medir**. Encontrada el 20 sep 2026 durante la auditoría de identidad
(`docs/analisis/auditoria-identidad-generador-de-historias.md`), leyendo `Utility.cs` — **no jugando**.
Es un hallazgo de código, y otro caso del patrón que ya señala `docs/project-state.md` («el texto promete
lo que el dato o el código no hacen»).

## Síntoma

`Sim/Engine/Utility.cs:1437-1443`: el comentario documenta el caso como **«Defensa y centrocampista»** y la
guarda del código es `role is Position.Defender`. El centrocampista queda fuera.

Consecuencia *(LEÍDO, no medido)*: un centrocampista **nunca** evalúa la entrada contra el rival al que
tiene asignado como marca mientras ese rival no lleva el balón. Solo el defensa lo hace.

## Por qué importa, y con qué puede estar emparentado

La **ADR 0105** subió `MarkOpponent` del centrocampista de 240 a 300 **con el motivo declarado** de que el
centrocampista participara más en la fase sin balón. Si la entrada a su marcado está cerrada por esta
guarda, esa subida compró colocación pero no disputa: el centrocampista va al sitio y no hace nada allí.

`docs/project-state.md` deja **abierto** el papel del centrocampista: *«dispara el 6,5 % de los tiros siendo
el 43 % de los jugadores de campo»*. **HIPÓTESIS, sin comprobar:** pueden ser el mismo problema visto desde
dos lados —un puesto que está en el campo y participa poco—, o dos cosas independientes. No se ha medido.

## Qué NO se sabe todavía

- Si es un error o una decisión deliberada que el comentario no siguió. **No hay ADR que lo registre**, lo
  que inclina a error, pero no lo demuestra.
- Cuánto movería `tacklesPerMatch` corregirlo. Margen disponible: hoy **12,01 con banda 6-14** *(MEDIDO,
  lote de 500 partidos, semilla 1, 20 sep 2026)*, o sea **2 entradas de margen**. Es poco: un puesto que es
  el 43 % de los jugadores de campo empezando a disputar puede consumirlo entero.
- Si arrastra `injuriesPerMatch`, que está en **0,81 con techo 0,90** y es la métrica con menos margen del
  proyecto. Las entradas son su fuente.

## Antes de tocar nada (Regla A)

Hipótesis a discriminar, por coste de verificación × poder discriminativo:

1. **Es un error de transcripción** — el comentario es la intención. *Verificación barata:* `git log -L` sobre
   esas líneas y buscar si el `Position.Midfielder` estuvo alguna vez.
2. **Es deliberado y sin registrar** — se quitó al centrocampista por presupuesto de entradas. *Verificación:*
   buscar en las nueve auditorías de IA (`docs/auditoria-ia-jugadores-1..9.md`) y en la ADR 0110 si el caso
   se discutió.
3. **Es inocuo** — el centrocampista ya dispone de `PressCarrier`/`ChaseBall` y la entrada al marcado sin
   balón apenas se dispararía. *Verificación:* histograma de acción elegida por puesto
   (`Sim.Tests/Analysis/ActionHistogramTests.cs`, el instrumento de la Tanda 0 ya existe).

**La 3 es la que más discrimina y ya tiene instrumento: hacerla primero.** No se modifica código mientras
no se sepa si el efecto es medible.

## Hermanos

- `docs/project-state.md` — el papel abierto del centrocampista (6,5 % de los tiros / 43 % de los jugadores).
- `docs/decisiones/0105-*.md` y `0110-*.md` — las dos que tocaron los pesos del centrocampista.
- `docs/analisis/tanda-0-histograma-de-accion.md` — el instrumento de la verificación 3.
