# ADR 0163 — La memoria se ve: estadísticas, apodos y la Gaceta de fin de run

Fecha: 29 sep 2026 · Estado: **aceptada**. **Decisión del revisor** (`docs/plan-diversion.md` §0): *«apodos
ganados ok. Es sencillo»*, *«la historia debería generarse al final de una run, como resumen de la partida.
Tono humor»*, *«al terminar partido crear apartado de estadísticas, también en alineación con estadísticas
acumuladas de la run»*.
**Requisitos:** RF-119, RF-122, RT-030, RT-035, RT-073
**Relacionada:** ADR 0124 (la carrera por jugador, `RunCareer`, y los créditos de rival, `RivalCredits`), ADR
0162 (el lenguaje visual de Equipo, que ya reserva un renglón para el nombre), plan Knavall F1/F2.

## Lo que había (Regla G)

`RunPlayer.Career` guarda partidos, goles, asistencias, entradas, entradas ganadas, faltas, tarjetas, lesiones
y muertes causadas, lesiones sufridas y tiempo en el campo, y `RivalCredits` quién lesionó o mató a quién. El
memorial (`RunSummary.Fallen`) existe. **Nada de eso se enseña como historia**: no hay apodos, el informe no
tiene estadísticas por jugador y el fin de run no cuenta nada. Hay un periódico de humor en el mapa
(`Game/Ui/Newspaper.cs`) con titulares de relleno.

## Decisión

1. **Apodos derivados, no guardados.** `data/nicknames/nicknames.json` (esquema propio): cada apodo con id,
   nombre es/en con humor («el Carnicero», «el Remendado», «Pies de Plomo»…), una condición sobre la carrera
   (campo, umbral) y una prioridad. El apodo de un jugador es el de mayor prioridad cuya condición cumple su
   carrera. Como la carrera sólo crece, el apodo es estable y sólo cambia a uno de más prioridad: **no hace
   falta estado nuevo ni subir el guardado**. Umbrales medidos con el censo (sección «Censo»).
2. **Se anuncia al ganarlo**: el informe post-partido dice «Grok pasa a ser *el Carnicero*» comparando la
   carrera de antes y de después.
3. **Estadísticas**: el informe post-partido gana un apartado por jugador propio (goles, asistencias,
   entradas ganadas, faltas, lesiones causadas) y la ficha de Equipo enseña las de la run y el apodo junto al
   nombre.
4. **La Gaceta**: la pantalla de fin de run pasa a ser una portada de periódico: titular según el desenlace,
   el MVP con su apodo, «el villano de la temporada» (el rival con más lesiones o muertes causadas a los
   tuyos, de `RivalCredits`) y la esquela de los caídos con su apodo y su línea de carrera. Todo el texto sale
   de plantillas localizadas con variantes elegidas de forma determinista por la semilla de la run (RT-035);
   nada escrito a mano por caso.

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Sus jugadores ganan nombre propio por lo que hacen, y la run termina contada.
2. **Qué decide.** Nada directo: es memoria. Indirectamente, a quién cuida y a quién arriesga.
3. **Qué debería decidir.** Lo mismo; el apodo no da poder (RF-127: nada acumulativo), sólo identidad.
4. **Regla.** RF-119 (informe), RF-122 (obituario y memorial, imagen compartible).
5. **Sistemas.** `/Sim`: `Sim/Run/Systems/Nicknames/` (catálogo y derivación), `Sim/Run/View/` (apodo, estadísticas
   del informe, vista de la Gaceta). `/data`: `nicknames/`, plantillas de `l10n`. `/Game`: informe, ficha de
   Equipo, fin de run.
6. **Alternativas.** Guardar el apodo en el estado: más flexible (apodos por hechos no acumulados), pero sube
   el guardado; se deja para cuando haga falta.
7. **Trade-off.** Ninguno de juego: es presentación de datos que ya existen.
8. **Estrategias.** Ninguna nueva; alimenta el apego, que es el coste emocional de la carnicería.
9. **Degeneración.** Un apodo que todos consiguen no significa nada: los umbrales se revisan con un censo
   de cuántos jugadores tienen cada apodo al final de las runs.
10. **Cómo se demuestra.** Tests de derivación (umbrales, prioridad, estabilidad), de la vista de la Gaceta
    (determinista, sin textos vacíos), capturas del informe, la ficha y la Gaceta, y un censo de apodos.

## Censo (29 sep 2026)

**Instrumento.** `dotnet run --project Balance -c Release -- --nickname-census N --seed S --out DIR`
(`Balance/NicknameCensusRunner.cs`): N runs completas con la política contextual (semilla de la run
`S·100000+i`, paralelo por índice, un catálogo por hilo), y al terminar cada una cuenta el apodo final de
cada jugador que pisó el campo, vivos y caídos. Columnas: % de runs con al menos un portador, % de jugadores,
% de runs con al menos un jugador **elegible** (cumple la condición aunque otro apodo de más prioridad se la
tape) y el máximo de la estadística. Se validó (Regla J) contra el caso sabido: el censo de una sola run
coincide con el recuento a mano sobre su plantilla final (`NicknameCensusTests`).

**Qué cuenta el censo** (enmienda 29 sep, revisión independiente): a los jugadores **de la plantilla final**
que pisaron el campo, vivos y caídos (los muertos siguen en el estado). **No** cuenta a quien se vendió o se
marchó durante la run —ya no está en el estado final—, así que el reparto por jugador infravalora a los
que pasaron por el club sin quedarse; el reparto por runs (lo que decide el criterio) casi no se ve
afectado, pero no está medido cuánto. La función que cuenta (`NicknameCensusRunner.Tally`) es pura y se
contrasta con casos de respuesta sabida (un apodo de «1 partido» sale al 100 % de las runs; un umbral
inalcanzable, al 0 %) en `NicknameCensusTests`.

**Criterio** (**provisional, sin medir**, Regla H: ninguna medición fija el 40 %; es la cifra a partir de la
cual «casi todas las runs lo tienen» deja de distinguir, puesta a ojo; el 0 % sí es una definición, un umbral
que nadie alcanza no es un apodo). Ningún apodo en más del 40 % de las runs ni en el 0 %. Umbrales de partida (ADR, «provisional, sin medir»), 300 runs, semilla 1: `keepers_bane`
(6 goles) **78,7 %**, `butcher` (2 lesiones causadas) 51,0 %, `veteran` (12 partidos) 51,0 %, `patchwork`
(2 lesiones sufridas) 44,0 %, `customs` (30 entradas ganadas) **0,0 %** (máximo alcanzado: 26); `golden_boots`
llegaba al 98 % de elegibles.

**Ajuste** (sólo umbrales, en `data/nicknames/nicknames.json`; prioridades y nombres no cambian):
`keepers_bane` 6→18 goles, `golden_boots` 3→10, `butcher` 2→6 lesiones causadas, `abattoir` 5→12,
`veteran` 12→15 partidos, `patchwork` 2→3, `delivery_boy` 3→5, `repeat_offender` 12→15, `customs` 30→20 y
`wall` 15→12. Se iteró tres veces sobre las semillas 1 y 2 (300 runs cada una): la segunda semilla destapó
`butcher` (43 %) y `golden_boots` (47,7 %) que la primera no veía, así que una sola lectura no bastaba.

**Resultado con los umbrales vigentes** (% de runs con al menos un portador final; semilla 1 / semilla 2):
abattoir 18,0 / 19,3 · keepers_bane 27,3 / 27,7 · butcher 22,7 / 27,3 · customs 2,0 / 1,3 · sieve 19,3 /
18,7 · assist_butler 26,3 / 25,0 · repeat_offender 33,7 / 34,3 · golden_boots 25,0 / 29,7 · patchwork 23,7 /
26,0 · wall 7,0 / 7,3 · collector 5,3 / 2,3 · delivery_boy 12,0 / 18,7 · grubby 36,0 / 36,7 · eternal 25,3 /
28,0 · veteran 29,3 / 34,3 · gravedigger 1,3 / 1,3. Todos entre 0 y 40 %; por jugador, el más repartido lo
lleva el 6 % de los que pisaron el campo. Los raros son raros por diseño (`gravedigger` es una muerte causada,
y la muerte es rara, ADR 0048) o están a la sombra de otros de más prioridad (`wall`, `customs`, `collector`),
pero se alcanzan. El apodo sigue sin dar poder de juego, así que el ajuste **no altera el partido** y no
necesita lote de partidos: sólo cambia qué se dice de cada jugador. Sin error típico entre semillas más allá
de las dos lecturas; si los pesos de la carrera (ADR 0124) cambian, hay que repetir el censo.

## Guardados anteriores (enmienda, 29 sep 2026, revisión independiente)

**Síntoma.** `RunController.Continue` reconstruye los sistemas desde el `DataSnapshot` congelado
(RT-061b), pero `StandardRunSystems.FromJson` exige `nicknames/nicknames.json`, `gazette/gazette.json` y
`prostheses/prostheses.json`, que un guardado de antes de este paquete no trae: la run ironman **no se podía
retomar** tras actualizar el juego, que es justo lo que la instantánea existe para impedir. Mismo esquema
(`schemaVersion` sin cambio), ficheros de datos distintos.

**Decisión.** `SnapshotCompletion` (`Sim/Run/Save/`, sin E/S: recibe los dos diccionarios) completa la
instantánea de un guardado con los ficheros de `/data` **actual** que le faltan, y **sólo** los de una lista
explícita, `SnapshotCompletion.AddableFiles`. Un fichero que el guardado ya trae no se toca; el resto de
`/data` sigue siendo el de la run. No sube la versión del esquema: el guardado no cambia de forma, y al volver
a guardar ya lleva los ficheros. `/Game` lo aplica al cargar (`RunController.Continue`).

**La lista y por qué** (criterio: catálogo **aditivo** que la run antigua nunca tuvo, que no reescribe ningún
estado guardado ni desplaza ningún flujo de RNG, RT-022):

| Fichero | Añadible porque |
|---|---|
| `nicknames/nicknames.json` | los apodos se derivan al leer de la carrera que la run ya lleva; no cambian ningún partido |
| `gazette/gazette.json` | plantillas de la portada de fin de run: sólo presentación |
| `prostheses/prostheses.json` | la run antigua no tenía ninguna instalada y una prótesis sólo entra cuando el jugador la elige en la clínica (RF-095); no toca jugadores guardados ni RNG de partidos. **Es el caso límite**: sí amplía lo que puede hacer el jugador, y se admite porque la alternativa es perder la run |

**Lo que no.** Perks, economía, mapa, rivales, pesos de IA, árbitros, objetos... **cambian reglas de la run
en curso** y no se completan jamás: si faltan, `FromJson` falla con error explícito (RT-032). Una lista
explícita y no «lo que falte» es deliberada: un catálogo nuevo debe dejar los guardados viejos sin poder
cargarse hasta que alguien decida, escribiéndolo en la lista con su motivo, que es añadible.

**Demostración.** `SnapshotCompletionTests`: un guardado sin los tres ficheros falla sin compleción
(el fallo observado) y con ella se retoma, con el resto de la instantánea idéntico y el estado igual; un
fichero presente no se sustituye; uno de reglas ausente no se completa.

## Enmienda: la revisión independiente (29 sep 2026)

Lo que la revisión encontró y cómo queda. Todo es sobre la **vista** de la memoria; salvo el primer punto,
que es un bug anterior que la Gaceta destapó.

- **Muertos que volvían a la vida** (`docs/pendientes/BR-B.md`, causa CONFIRMED): el motor lesionaba al
  objetivo que un perk letal de contacto acababa de matar al publicar la disputa, y la resolución dejaba al
  muerto «lesionado». La muerte es terminal: guarda en `ResolveInjury`, en `MatchResolution` y en
  `ApplyRivalCredits`. Los créditos y `DeathDetails` coinciden con el estado final.
- **Epitafios falsos.** La esquela dice **cómo** cayó: sacrificio del evento, matasanos, matador de un clan del
  catálogo (`epitaph.byRival`, con su nombre), matador sin crédito de catálogo —jefe o rival procedural—
  (`epitaph.byOpponent`), partido sin matador (`epitaph.noAuthor`, la única que habla del «golpe que nadie vio»)
  y causa no registrada (`epitaph.unknown`, neutra). La causa vive en `RunState.Counters` con la clave
  `deathCause:<id>` (`PlayerDeathCause`), en el mismo sitio y con la misma lógica que los `rivalCredit:` (ADR 0124):
  contabilidad de run de clave libre, **sin subir la versión del esquema**; los guardados anteriores leen
  `Unknown`. `DeathConsequences.Kill` recibe la causa sin valor por defecto.
- **El villano cuenta víctimas distintas**: quien lesionó y mató a la misma víctima suma un muerto, no dos
  hechos; lesionar tres veces al mismo suma un lesionado. Peso `3·muertos + 1·lesionados`,
  **provisional, sin medir** (Regla H): sólo dice que una muerte es peor.
- **MVP**: exige al menos un hecho (`MvpMinimumFacts = 1`: goles, asistencias, entradas ganadas, lesiones o muertes
  causadas; sólo partidos no vale, la línea enumera hechos) o la Gaceta dice `mvp.none`. Empate a puntos: más
  partidos, luego quien no es portero, luego id menor. Los destacados van **de lo más memorable a lo menos**
  (partidos en la esquela, muertes causadas, lesiones causadas, goles, asistencias, entradas ganadas) y el límite
  recorta por el final.
- **Plurales** dentro de la propia plantilla: `{deaths|# baja|# bajas}` y `{deaths|ninguna baja|# baja|# bajas}`
  (`#` es la cifra). El cargador rechaza marcadores que la vista no da y de plural mal formados; `Fill` falla en
  vez de borrar en silencio un marcador desconocido.
- **Nada desaparece sin explicación** (`/Game`): esquelas que no caben, «… y N esquelas más, en la plantilla final»;
  apodos ganados en el informe, los dos de mayor prioridad y «y N apodos más» (la vista los ordena);
  estadísticas, «y N jugadores más»; el apodo en su propia línea, sin recortar a 160 px. Límite conocido: con
  varios apodos ganados a la vez el panel de estadísticas del informe (240 px) sólo enseña unas cuatro filas de
  siete y dice cuántas faltan; agrandarlo pide reordenar la columna derecha del informe.
- **Censo**: ver «Qué cuenta el censo» arriba (sólo la plantilla final; la cifra del 40 % es provisional).
