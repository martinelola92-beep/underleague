# ADR 0187 — Tope de tres prótesis: a partir de ahí, una lesión grave deja al jugador lisiado

Fecha: 3 oct 2026 · Estado: **aceptada**. **Decisión del revisor** (3 oct, madrugada): *«Tope 3 prótesis, a partir de
ahí jugador lisiado»*. Sustituye el «techo estructural» (una prótesis por ranura) de la nota de `game-design-review` de
la ADR 0164, que dejaba el tope como decisión abierta.
**Requisitos:** RF-012d, RF-093, RF-095, RF-095c, RT-030. **Relacionada:** ADR 0164, ADR 0048.

## Lo que había (Regla G)

No existe ningún estado de «retirado», «inválido» ni «fuera de plantilla» (búsqueda por concepto con
`tools/existe-ya.sh`: sólo la mención en `project-state.md`). El estado físico es `Healthy / MinorInjury /
SevereInjury / Dead`; el grave no juega salvo que el jugador lo arriesgue (RF-093 vía 1) y se cura en la clínica. La
venta de un grave ya vale un cuarto (RF-114f, `PlayerSaleStatePercent`). El techo de prótesis era sólo estructural
(7 ranuras).

## Decisión

1. `RunRules.MaxProstheses = 3` (la misma cifra que el `Automaton` de RF-095c: la tercera prótesis ya era el hito).
2. **Lisiado = lesión grave con 3 prótesis** (`RunPlayer.IsCrippled`, derivado, sin campo nuevo): no tiene cura en la
   clínica (ni por pieza, ni tarifa plana, ni matasanos, ni herrero), no se puede alinear ni arriesgando (RF-093 vía 1),
   no cuenta como disponible (RF-002b), **sigue en la plantilla** (vínculos, objeto, venta al precio de un grave).
3. Con 3 prótesis el herrero no instala una cuarta (`HasFreeProsthesisSlot` es falso).
4. **Se ve antes** (RF-012d): la clínica enseña «3/3 prótesis: la próxima lesión grave lo deja lisiado» en el
   paciente a una lesión de serlo y en la mesa del herrero cuando la tirada puede instalar la tercera; la ficha lleva
   la misma línea; el informe de partido marca «queda lisiado» al caer.
5. **Esquema sin versión nueva**: al ser derivado no hay estado que guardar; un guardado anterior carga y calcula el
   lisiado al leer (test). Efecto retroactivo aceptado: un grave con 3 o más prótesis de un guardado viejo pasa a lisiado.

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Un jugador-estrella forjado que, a la próxima lesión grave, se queda en la banda para siempre.
2. **Qué decide.** Si dar la tercera prótesis (autómata, +atributos) sabiendo que desde ahí cada grave es definitivo;
   si alinear al autómata con riesgo de lesión; si venderlo antes.
3. **Qué debería decidir.** Lo mismo: el coste de concentrar prótesis es la fragilidad, no un tope invisible.
4. **Regla.** Nueva, por decisión del revisor; enmienda RF-095 (tope) y RF-093 (el grave de un jugador con tope no se
   puede arriesgar). Se anota aquí, no se inventa en silencio.
5. **Sistemas.** `/Sim`: `RunPlayer.IsCrippled`, `MedicalSystem`, `RunLineup.CanStart`, política, `CasualtyRow.Crippled`.
   `/Game`: clínica, ficha, informe. `/data`: nada.
6. **Alternativas.** (a) Nuevo `PhysicalState.Crippled`: obliga a tocar el motor de partido (`Definition.PhysicalState`),
   la economía y todos los `switch`; rechazada. (b) Campo guardado: puede desincronizarse del estado y exige subir el
   esquema; rechazada frente a lo derivado. (c) Que el cuarto implante mate: contradice «el herrero nunca mata».
7. **Trade-off.** Da la identidad de «autómata de chatarra» y quita la fiabilidad: el titular forjado deja de ser
   curable. Sin daño no anunciado (regla 11): el 3/3 se ve antes en tres sitios.
8. **Estrategias.** Repartir prótesis entre varios (legible, seguro) frente a concentrarlas en una estrella (potente y frágil).
9. **Degeneración.** Es una regla en negativo: no puede crear poder. Riesgo: el lisiado no cuenta para el mínimo de 5
   (RF-002b) y puede acelerar la derrota por falta de jugadores; la política no concentra, así que no se ha visto.
   El matasanos sobre una lesión **leve** puede empeorarla a grave; en un jugador 3/3 eso lo lisia: el matasanos ya
   enseña su riesgo (ADR 0099) y la clínica enseña el 3/3, así que queda dentro de lo anunciado.
10. **Cómo se demuestra.** `CrippledTests` (valor conocido, control, guardado anterior), captura de la clínica y la
    ficha, y `--full-runs` contra `main` (sección siguiente).

## Enmienda tras la revisión independiente (3 oct 2026)

1. **El aviso va donde se decide.** `RunEngine.LineupWarnings` gana `ProsthesisCapRisk` (un titular 3/3: «lleva 3/3 prótesis:
   una lesión grave lo deja lisiado», en el bloque «Antes de confirmar» del ojeo) y deja de avisar de «muerte» por un lisiado
   (no puede estar en el once). El botón del matasanos sobre un **leve** 3/3 dice «empeore a grave y lo deje lisiado». La carta
   de evento con una lesión grave añade a la fila de cada objetivo 3/3 «la lesión grave lo dejaría lisiado, sin cura»
   (`EventView`, plantilla `targetCrippled` es/en).
2. **Guardado anterior con 3+ prótesis y una grave: no se avisa una sola vez; se documenta.** Queda lisiado al cargar, y desde
   entonces lo dicen la clínica (lista permanente de lisiados en rojo cada vez que se abre) y la ficha («Lisiado»). No hay
   estado de «ya avisado» que guardar, que es lo que se evitó al derivarlo. Alcance: sólo un guardado de la versión 9 o
   anterior con un jugador de tres o más prótesis **y** lesión grave sin tratar; la política automática nunca llega a 3 (lote
   de abajo) y la regla es del 3 oct, así que es un caso de cola del desarrollo, no de jugadores. Coste aceptado: ese jugador
   pierde la cura sin un aviso propio al cargar.
3. Capturas por xvfb de la clínica (lisiado, aviso 3/3 y matasanos), la ficha (la lista de prótesis se parte en dos líneas con
   tres) y el ojeo (`--tour-cap`).

## Medición (3 oct 2026)

`/Balance --full-runs 300` en las semillas 1 y 1001 (900 runs por celda con las tres doctrinas; resumen de la principal),
rama contra `main` (`3128e81`, exportado con `git archive` y compilado aparte). Métrica nueva `crippledPerRun` (plantilla
final; los vendidos o muertos no cuentan).

| semilla | runWinRate (tope / main) | deathsPerRun | prosthesesPerRun | automatonsPerRun | maxProsthesesOnOnePlayer | crippledPerRun |
|---|---|---|---|---|---|---|
| 1 | 17,33 / 17,33 | 1,29 / 1,29 | 0,24 / 0,24 | 0,00 / 0,00 | 2 / 2 | 0,00 |
| 1001 | 16,00 / 16,00 | 1,28 / 1,28 | n/d | 0,00 / 0,00 | 2 / 2 | 0,00 |

**`runs.csv` byte a byte idéntico a `main` en las dos semillas** (CONFIRMED por `cmp`): nadie llega a tres prótesis con la
política automática (máximo 2, igual que en la ADR 0164), así que ninguna rama nueva se ejecuta. **Lo medido es que la
regla no se activa con esta política, no que su efecto sea nulo**: con una persona que concentre prótesis (el caso que
la ADR 0164 dejó sin representar) el efecto es el que dicen los tests de valor conocido, y no hay lote que lo mida.
Si más adelante la política concentra, `automatonsPerRun` y `crippledPerRun` son los indicadores. Sin cifra provisional
nueva: el tope (3) es la decisión del revisor y coincide con el hito del `Automaton` de RF-095c.
