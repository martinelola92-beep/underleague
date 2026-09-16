---
name: gameplay-debug
description: Investigar un síntoma de partido, un bug de comportamiento, un test de determinismo que falla, o "por qué el motor hizo X" — antes de escribir ninguna línea de arreglo. Usar ante cualquier observación de gameplay rara, propia o del revisor, y siempre antes de proponer una causa.
---

# Depurar un síntoma de gameplay

Existe porque el mismo error se ha repetido más de cinco veces en este proyecto: una hipótesis plausible
se convierte en decisión sin ejecutar el experimento barato que la habría separado de la alternativa
(`docs/analisis/auditoria-organizacion-v2.md`, §2). Esta skill es el procedimiento que lo impide. No es
opcional cuando el síntoma es de comportamiento del partido: es el camino obligatorio.

## Paso 0 — consulta antes de hipotetizar

**Memoria que no se consulta es memoria inexistente.** Antes de proponer ninguna causa:

1. `grep` en `docs/pendientes/README.md` por el sistema o el síntoma.
2. Si existe un fichero (`docs/pendientes/<ID>.md`), léelo entero. Puede tener hipótesis ya **REJECTED**
   —no las repitas sin evidencia nueva— o hermanos con la misma causa.
3. Si no existe, créalo ahora con la observación, vacío de hipótesis todavía.

## Paso 1 — enumera TODAS las hipótesis plausibles, sin fijar un número

No hay "las dos hipótesis rivales". Puede haber una, tres o cinco. Fijar un número de antemano es sesgo.

## Paso 2 — ordena por coste × poder discriminativo, no solo por coste

La pregunta no es "¿cuál es la prueba más barata?" sino **"¿qué observación barata elimina el mayor número
de hipótesis a la vez?"**. Un experimento barato que no distingue entre H1/H2/H3 no es el primero que hay
que hacer.

**Regla dura: no se modifica código mientras exista una medición de bajo coste capaz de discriminar entre
las hipótesis actuales.** Esto habría evitado los dos parches escritos y revertidos de BB-G
(`docs/pendientes/BB-G.md`): la tabla de utilidad (RT-098) era la prueba más barata Y la más directa, y fue
la tercera que se probó.

**Preferir instrumentar el mecanismo de decisión real sobre adivinar por el código de alrededor.** El
instrumental de este proyecto, de más a menos directo:

- **`SimConfig.DumpUtility = (playerId, tick)`** → `MatchReport.UtilityDump`: la tabla de utilidad completa
  de un jugador en un tick — acción, score, si está `Rejected` y por qué (RT-098). Es la respuesta directa
  a "por qué el motor eligió X en vez de Y". Empieza aquí siempre que la pregunta sea sobre una decisión
  de IA.
- **`SimConfig.Trace = true`** → `MatchResult.Trace`: posición, estado, acción y zona de cada jugador en
  cada fotograma, sin submuestrear. Empieza aquí para "¿dónde estaba/qué hacía X en el tick T?".
- **Instrumentación ad hoc con `Assert.Fail(mensaje)`**: un test que se sabe que va a fallar, usado solo
  para volcar una medición por la salida de xUnit. Bórralo o conviértelo en test permanente al terminar
  —nunca lo dejes en el árbol fallando de verdad.

**Trampa medida, dos veces**: `MatchPlayer.LeavePitch` pone la posición en `(-1,-1)`. Si mides una
distancia leyendo el tick **del** evento en vez del tick **anterior**, la medición miente y parece que algo
está lejos cuando no lo está (BB-M, BA-L). Cuando el síntoma implique una posición junto a una salida de
campo (lesión, sustitución, expulsión), lee el tick anterior al evento, no el mismo.

## Paso 3 — ejecuta, empezando por la más discriminativa

Compila y prueba siempre en Release: `dotnet build Underleague.slnx -c Release -m:1 -v q` y
`dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~X" -m:1 -v q` (ver skill `build-and-test`
para el resto de comandos — no los repitas aquí).

## Paso 4 — registra el resultado con estado epistemológico, nunca "es así y ya está"

En `docs/pendientes/<ID>.md`, cada hipótesis etiquetada:

- **REJECTED** — un experimento la contradijo. No se repite sin evidencia nueva. Si el rechazo depende de
  un sistema que puede cambiar (una ADR, un rango de balance), anota *"descartada bajo la ADR 0110"*, no
  *"falsa"* a secas — puede reabrirse si ese sistema cambia.
  - Distingue de una hipótesis **sin evidencia de activación** (mecanismo real, latente, nunca observado
    disparándose — como BB-G2): no es lo mismo que una hipótesis refutada por medición.
- **LIKELY** — consistente con lo medido, sin experimento propio que la aísle de una alternativa. No se
  actúa sobre una LIKELY como si fuera CONFIRMED sin decirlo.
- **CONFIRMED** — reproducida con un experimento que la aísla. Cita el experimento exacto (test, semilla,
  tick).

## Paso 5 — solo entonces, arregla

Si el arreglo es un cambio de **pesos** de balance (`data/ai/weights.json`, cualquier `data/economy/*`),
pasa por `balance-measure` antes de darlo por cerrado: una hipótesis de valor por vez, contra un baseline
del mismo árbol (`git stash`), mirando **todas** las métricas de diferenciación de builds — no solo la que
motivó el cambio (lección de `ChaseBall pen=50`: tres métricas independientes moviéndose juntas no son
ruido, aunque cada una por separado pudiera parecerlo).

Si el arreglo toca una regla ya fijada por una ADR, o introduce un `EffectType`/objetivo/mecánica nuevos,
pasa primero por `game-design-review` — el arreglo de un bug puede ser, sin quererlo, un cambio de diseño.

## Qué NO hace esta skill

- No propone arreglos antes del paso 4. Termina en un diagnóstico con evidencia, no en un parche.
- No declara CONFIRMED sin haber reproducido con un experimento propio.
- No lanza el lote de `/Balance` como sonda exploratoria — eso es `balance-measure`, y solo con hipótesis
  concreta.
