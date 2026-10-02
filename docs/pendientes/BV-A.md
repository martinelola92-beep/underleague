# BV-A — Los modelos 3D no se mueven con naturalidad: «parpadeo» de animaciones por ticks

**Estado:** **Parte `/Game` implementada (3 oct 2026)**, medida antes/después con 3 semillas (tabla en
«Implementación»). Queda abierto lo que pide `/Sim` (H4 aceleración, H8 oscilación) para una ADR posterior, y la
trayectoria C1 (punto 5, no hecho). Diagnóstico del 2 oct: ocho causas CONFIRMED con el instrumento nuevo. Hermanas: [BB-K](./BB-K.md) (baile de dos compañeros; queda `FindSpace`),
[BI-C](./BI-C.md) (root motion), [BI-H](./BI-H.md) (balón anclado al hueso), [BA-K](./BA-K.md) (cortes de teletransporte).

## Observación (revisor, literal)

> «la interacción de los modelos 3D y el juego: no tienen movimientos naturales, se siente artificial, hay
> "parpadeo" de animaciones por ticks. El fútbol debe sentirse fluido, natural y divertido, si no el juego
> pierde sentido».

## Cómo pasa hoy un tick a fotogramas (leído, no supuesto)

- **Posición**: `MatchPitchView3D.Interpolate` hace `Lerp` lineal entre el tick `f` y `f+1` con `Alpha` (la fracción
  de `_carry` de `BroadcastScreen`). Continua en posición, **discontinua en velocidad** en cada frontera de tick.
- **Velocidad que ve el modelo**: `StepOf(f)·15`, el paso **de un solo tick**, constante dentro del tick. Sin
  suavizado, sin ventana.
- **Orientación**: `PlayerModel.Pose` asigna `Rotation = atan2(velocidad)` directamente. Salta en la frontera de tick.
- **Animación**: `AnimationPlayer` plano (sin `AnimationTree`). Locomoción por umbrales **sin histéresis** sobre esa
  velocidad de un tick: `≤0,15` idle, `>1,9` run, si no jog (casillas/s). Cada cambio llama a `Play(clip, 0,15 s)`,
  que **arranca el clip nuevo desde 0**. Ritmo: `SpeedScale = v / 2,8` (run) o `v / 1,4` (jog), recortado 0,6–1,8.
- **Ritmo de reproducción**: el reloj del `AnimationPlayer` es el real; ni la velocidad x4/x16, ni la cámara lenta
  (ADR 0171), ni las congelaciones del director (ADR 0173), ni la pausa manual lo tocan. Durante una congelación
  `StepOf` sigue devolviendo el paso del tick congelado.
- **Gestos**: `Once(kick)` mientras dure `Passing`/`Shooting` (5 ticks); `receive`, `header`, paradas se **retienen
  hasta que el clip termina** (`_holdingCue`).
- **Cámara**: fija encajada al campo; sólo se mueve en los gestos de tiro (acercamiento con `Ease` en tiempo real).

## Instrumento (Regla J: validado antes de creerlo)

`godot --path Game --fixed-fps 30 --resolution 1920x1200 --scene res://Scenes/CapturasRetransmision.tscn -- movimiento <carpeta>`
(`Game/Screens/BroadcastCapture.Movement.cs`), y `tools/movimiento-analisis.py <carpeta> --hojas <salida>`.

- Juega un partido **real** de la retransmisión (club `human_abattoir`, semilla 20260905, primer partido del acto 1):
  la maqueta sólo pone modelo a los humanos. `--fixed-fps 30` da el mismo `delta` a pantalla, director y
  `AnimationPlayer`, así que es determinista por lento que renderice el software.
- Registra **en `frame_post_draw`** (síncrono, mismo instante que la imagen) por fotograma y jugador: posición dibujada,
  yaw del modelo, clip, su tiempo, `SpeedScale`, velocidad recibida y la **velocidad natural del clip** (el
  desplazamiento horneado del hueso raíz antes de `PinInPlace`, a la escala del modelo). Más `traza.csv` (lo que
  escribió `/Sim`, tick a tick, partido entero) y `clips.csv`.
- **Validación con respuesta conocida**: el jugador 1 corre en línea recta 45 ticks (1247–1292, giro < 8° por tick).
  Resultado: **0 cambios de clip** (`run` todo el tramo), `Alpha` alterna 0/0,5 (30 fps sobre 15 ticks/s), el tiempo
  del clip avanza 0,0252 s por fotograma = `SpeedScale 0,7554 × 1/30` exacto, y el yaw sólo cambia en la frontera de
  tick. El instrumento mide lo que dice medir.
- Coste: ~9 min de Godot por software para 8 tramos (unos 2.700 fotogramas).

## Hipótesis y mediciones

Tramo largo = 600 ticks a x1 desde el tick 200, unos 305 segundos-jugador de modelos dibujados.

| | Hipótesis | Etiqueta | Medida |
|---|---|---|---|
| H1 | La locomoción alterna idle/jog/run según la velocidad **de cada tick**, sin histéresis | **CONFIRMED** | Dibujado: **2,35 cambios de clip/s por jugador**, de ellos **1,76/s son ida y vuelta A→B→A en ≤ 0,3 s**. Réplica sobre la traza entera: 2,86/s y 1,90/s. Reparto: idle↔jog 393, jog↔run 151, gk_idle↔jog 98, idle↔run 72 (de ~730). Hoja `hoja-giro-antes.png`: `jog t0.00 → idle → jog t0.00 → idle` cada tick |
| H1b | Cada cambio **reinicia** el clip | **CONFIRMED** | Tras cada cambio el clip nuevo sale en `t=0,00` (hojas); el ciclo de zancada nunca llega a completarse mientras parpadea. En carrera estable NO se reinicia (validación): **REJECTED** «se reinicia en cada tick» como regla general |
| H1c | Por qué la velocidad cruza los umbrales | **CONFIRMED** (traza) | El **40 %** de los ticks libres tiene velocidad 1,6–2,2 c/s, justo alrededor del umbral `run` 1,9 (base 1,965 c/s, regate al 80 %, cansancio); **13,8 %** son pasos parciales 0,01–0,13 casillas (llegar al destino, empujes de separación) que cruzan el umbral idle 0,15; **3,1 %** son ceros intercalados *paso-0-paso* |
| H2 | La rotación salta en vez de girar | **CONFIRMED** | Yaw constante dentro del tick y salto en la frontera. Cuando cambia: p50 7°, **p90 142°**, máx. 180°; el 3,8 % de los fotogramas gira > 30° de golpe. `hoja-pase-antes.png`: el #5 pasa de yaw 90 a −89 entre dos fotogramas |
| H3 | Interpolación lineal con quiebros cada 66 ms | **CONFIRMED** (mecanismo) | Dentro del tick la trayectoria no gira nunca (p50 0,01°); en las fronteras, p90 14,7° y **p99 168°**. Posición C0, velocidad discontinua |
| H4 | No hay aceleración ni frenado | **CONFIRMED** en `/Sim` | `MatchEngine.Move`/`SpeedPerTick`: paso constante de 0,131–0,159 casillas/tick desde el primer tick. Histograma de la traza bimodal: 3.480 ticks a 0 y 5.350 a 2,0–2,5 c/s, casi nada en medio |
| H5 | Los pies patinan: el ciclo no casa con la velocidad | **CONFIRMED** | A x1, cuerpo/pies **1,28** (p50; p90 1,28) corriendo: `RunReferenceSpeed = 2,8` pero el clip `run` medido avanza **2,19 c/s** con `SpeedScale` 1 (el `jog`, 1,30 c/s frente a 1,4 supuesto). A **x4**: **4,33** (p50), p90 5,1 — el ritmo no sabe de la velocidad de reproducción. Cámara lenta del destino: mismo mecanismo, **LIKELY** (no medida) |
| H6 | Corren en el sitio durante congelaciones y pausa | **CONFIRMED** | En el tramo largo a x1, el **18 %** de los fotogramas con clip de carrera tiene el cuerpo quieto (pausas breves del director). Pausa manual: `hoja-pausa-antes.png`, tick 1255 congelado y `run` avanzando de t0,04 a t0,34. En la congelación de la falta (`hoja-reanudacion-antes.png`) arranca incluso un `kick` |
| H7 | Gestos desincronizados: retenidos o truncados | **CONFIRMED** | `receive` dura **5,13 s** y se retiene entero: en pantalla 5,17 s, con el cuerpo deslizándose a > 1 c/s el **77 %** de esos fotogramas. `kick` (0,57 s) se corta a 0,33 s en 7 de 8; `tackle` (2,73 s) se ve 0,2 s |
| H8 | Trayectorias que oscilan en `/Sim` | **CONFIRMED** en la traza; causa en `/Sim` **LIKELY** | 231 inversiones > 135° a velocidad de carrera en el partido (0,33/s por jugador), y **el 52 % vuelve a invertirse en ≤ 4 ticks**: ida y vuelta. Patrón *0,100 casillas, 0, 0,100, 0…* (jugador 1, ticks 173–182): un destino que avanza 0,1 cada `decisionIntervalTicks: 2`, alcanzado en un tick y esperado el siguiente. Hermana de BB-K (`FindSpace`) |
| H9 | El modelo mira hacia donde se mueve, no al juego (sin retroceso ni lateral) | **LIKELY** | Los giros de 180° a velocidad de carrera (H2) incluyen pasos atrás que un futbolista daría de espaldas o de lado; no aislado de H8 |
| H10 | Jitter de cámara | **LIKELY REJECTED** (código) | Cámara fija; los gestos van en tiempo real con `Ease`. No se midió la transformación de la cámara |
| H11 | El balón anclado al hueso (BI-H) tiembla con los clips que parpadean | **LIKELY**, sin medida | Hereda H1: el punto del pie cambia de clip con el cuerpo |
| H12 | Ritmo de fotogramas reales (60 Hz con `delta` variable) frente a 15 ticks/s | **Sin evidencia** | El instrumento corre a fps fijos a propósito; no se midió con reloj real |

## Propuesta de arreglo (sin implementar), por impacto en la sensación

Todo lo de `/Game` se queda dentro de RT-014 y RT-020: el render sólo **lee** la traza (también los ticks vecinos, que ya
están calculados) y presenta; nada vuelve al motor.

**Sólo `/Game`:**

1. **Locomoción continua en lugar de tres clips con umbrales** (H1, H1b, H1c). Velocidad de presentación suavizada:
   derivada sobre una ventana de ticks de la traza (p. ej. `(pos[f+2] − pos[f−1]) / 3`) más un filtro en tiempo real.
   Y, o bien un `AnimationTree` con `BlendSpace1D` idle–jog–run sincronizado (no reinicia el ciclo al cruzar), o como
   mínimo histéresis (entrar en run a 2,0 y salir a 1,6; idle por debajo de 0,3 sostenido) con permanencia mínima de
   ~0,25 s. Es lo que más parpadeo quita.
2. **Ritmo del clip = velocidad dibujada / velocidad natural medida del clip × ritmo de reproducción** (H5, H6).
   Quitar las constantes 2,8/1,4 y usar la zancada del propio clip (el instrumento ya la calcula). Multiplicar por la
   velocidad x4/x16 y por la escala de la cámara lenta, y **parar** (`SpeedScale` 0 o fundir a la espera) cuando la
   reproducción está congelada o en pausa. También para los gestos.
3. **Giro con velocidad angular limitada** (H2, H9): yaw objetivo desde la velocidad suavizada, con un muelle o un tope
   de ~540–720°/s. Además, no girar ante inversiones que la traza deshace en ≤ 4 ticks (look-ahead sobre la traza ya
   calculada). Más adelante, clips de retroceso o lateral para mirar al balón al recular.
4. **Gestos acotados** (H7): `receive` recortado a su parte útil (o hecho capa de tronco), soltado en cuanto el cuerpo
   pasa de ~1 c/s; `kick` adelantado leyendo en la traza el `Passing` que viene, para que el contacto caiga en el tick en
   que sale el balón y no se corte a medias.
5. **Trayectoria C1** (H3): spline centrípeta Catmull-Rom sobre los ticks `f−1…f+2`, conservando el corte de BA-K.
   Redondea los quiebros; menos urgente si 1 y 3 ya están.

**Lo que pediría `/Sim`** (H4, H8): aceleración y frenado, y quitar las oscilaciones y el *paso-0-paso*, son
**trayectorias del partido**: cambian dónde está cada jugador, quién llega antes al balón y por tanto resultados. Hace
falta **ADR** (primitiva de motor: `game-design-review` + `architecture-review`) y `balance-measure` con las puertas.
La oscilación es la continuación natural de BB-K/ADR 0176 (histéresis del destino en `FindSpace` y en la llegada),
probablemente la más barata y la de mejor relación; la aceleración real es la más cara y **no se recomienda antes de
ver 1–4**, que tapan buena parte del síntoma sin tocar el partido.

## Implementación (3 oct 2026, sólo `/Game`)

Commits `05ee0f2` (punto 1), `50b99a8` (2), `c93ede2` (3), `8b525d5` y `945ca96` (4). El render sólo lee la traza
—también ticks futuros, ya calculados— y presenta (RT-014); nada vuelve al motor y `/Sim` no se toca.

1. **Locomoción continua** (`PlayerModel`, `MatchPitchView3D.SmoothedVelocity`): un `AnimationTree` con
   `BlendSpace1D` idle→jog→run (`Sync`), puntos en la zancada **medida** de cada clip (jog en 0,59 de la de run), y la
   velocidad de presentación promediada ±2 ticks sobre la traza (provisional) más un filtro de 0,12 s (provisional).
   Los gestos van por una `AnimationNodeTransition` encima, con fundido de 0,15 s.
2. **Ritmo**: `loco_scale = ritmo de reproducción × max(1, v / zancada de run)`, con la zancada de run **medida**
   (2,19 c/s). El ritmo de reproducción se **mide en lo que se dibuja** (`Frame + Alpha` por tick real): 0 congelada o
   en pausa, 4 a x4, 0,5 en cámara lenta. Todos los relojes del muñeco van con él.
3. **Giro**: orientación desde el desplazamiento entre `t−1` y `t+4` ticks (4 sale de la medida de H8: el 52 % de las
   inversiones se deshacen en ≤ 4 ticks), mínimo 0,15 casillas (provisional), y como mucho **720°/s** de partido
   (provisional, sin medir: media vuelta en 0,25 s). En el tick de un teletransporte se recoloca sin girar.
4. **Gestos**: el `kick` golpea a **0,20 s** del clip (medido con el perfil de los pies, `DebugFootProfile`); la vista
   busca hasta 8 ticks adelante en la traza el tick en que el jugador suelta el balón y lanza el golpeo para que el
   contacto caiga ahí. `receive` usa sólo 0,85–1,5 s del clip (sitio medido, longitud provisional). Los dos se sueltan
   si el cuerpo pasa de 1 c/s (tras el contacto + 0,1 s, o tras 0,25 s): provisional. Throw-in, penalti y balones a la
   altura del pecho quedan fuera del golpeo adelantado.
5. **Spline Catmull-Rom**: **no hecho**. H3 sigue igual (p99 del quiebro de trayectoria ~168°); con 1–4 el muñeco ya no
   gira ni cambia de paso en esos quiebros, pero el cuerpo sí dobla en la frontera de tick.

### Antes / después (3 semillas: 20260905, 20260906, 20260907; tramo largo de 600 ticks a x1 salvo donde se dice)

Las tres cifras de cada celda son las tres semillas.

| Hipótesis | Métrica | Antes | Después | Etiqueta tras el cambio |
|---|---|---|---|---|
| H1 | Idas y vueltas de clip A→B→A en ≤ 0,3 s, /s por jugador | 1,76 · 1,47 · 2,17 | **0,27 · 0,23 · 0,32** | Mejorada; no llega a «≈0» (ver abajo) |
| H1 | Cambios de clip dominante, /s por jugador | 2,35 · 2,20 · 3,00 | 0,97 · 0,96 · 1,13 | No alcanza el objetivo < 0,3 **en esta métrica** (ver abajo) |
| H1 | Oscilaciones de la mezcla ≥ 0,15, /s por jugador | (no aplica: clips discretos) | 0,79 · 0,88 · 1,01 | — |
| H1b | Clip reiniciado a t=0 al cambiar | sí, en cada cambio | **no** (`Sync`; la mezcla nunca reinicia) | REJECTED tras el cambio |
| H2 | Fotogramas con giro de yaw > 30° | 3,85 · 4,05 · 5,58 % | **0,05 · 0,02 · 0,02 %** | Arreglada |
| H2 | Giro de yaw cuando cambia, p90 / máx. | 142°/180° · 111°/180° · 114°/180° | **24°/109° · 23°/48° · 23°/78°** | Arreglada (sin 180° instantáneos) |
| H5 | Cuerpo/pies, p50 a x1 (p10–p90) | 1,28 (1,08–1,28) ×3 | **1,07 · 1,05 · 1,04** (0,98–1,9) | Dentro de ±10 % en la mediana |
| H5 | Cuerpo/pies, p50 a **x4** | 4,33 · 5,10 · 5,03 | **1,17 · 1,02 · 1,01** | Dos de tres dentro de ±10 % |
| H6 | Corriendo en el sitio, tramo largo x1 | 18,4 · 17,9 · 5,8 % | **4,5 · 3,3 · 4,0 %** | Arreglada en congelaciones; el resto es la cola del frenado |
| H6 | Corriendo en el sitio, **pausa manual** | 28,7 · 25,7 · 35,6 % | **0,2 · 0,5 · 0,3 %** | Arreglada (`hoja-pausa-despues.png`: poses idénticas, ritmo 0) |
| H7 | `receive` en pantalla, p50 | 5,17 s (deslizándose 59–77 %) | 0,07–0,27 s (deslizándose 0–20 %) | Arreglada; los 0,07 s son recepciones que enlazan con un pase al primer toque |
| H7 | `kick`: llega al contacto | 0,33 s de 0,57, cortado antes del golpe | contacto alineado con la salida del balón, soltado tras el contacto | Arreglada; **queda** deslizamiento en la carrerilla (35–56 % de sus fotogramas): el pasador se mueve en `/Sim` durante `Passing`, y el clip es en el sitio |

**Por qué el objetivo de «cambios de clip < 0,3/s» no es la medida buena después**: con un `BlendSpace1D` ya no hay
clips discretos; el instrumento etiqueta el «clip dominante» por la posición de la mezcla, y la etiqueta cambia al
rozar el punto medio jog/run (0,795) aunque la postura no cambie de forma apreciable. Mirado el detalle (semilla
20260906): de los cambios restantes, run↔jog son 219 de ~345 y son conducciones (regate al 80 % ≈ 0,73–0,80 de la
mezcla) rozando ese punto; idle↔jog son rampas monótonas de arranque y frenado (cada parada cuenta dos cambios). La
medida que describe el parpadeo de verdad es la ida y vuelta (A→B→A en ≤ 0,3 s): de ~1,8 a ~0,27/s. Si el revisor
aún lo nota, la palanca es la ventana (±2 ticks) y el filtro (0,12 s), a costa de arrancar y parar algo más tarde.

Hojas en `Game/screenshots/movimiento/`, `hoja-<momento>-antes.png` y `-despues.png` en el **mismo fotograma**
(mismo partido, mismo `n`, opción `--desde` de `tools/movimiento-analisis.py`). Comprobado a x1, x4 (tramo `x4`) y en
pausa (tramo `pausa`).

## Riesgos

- Suavizar con look-ahead hace que el cuerpo dibujado vaya hasta ~1 tick por detrás o por delante de la traza: el balón
  y los carteles leen la traza; hay que medir la separación al pie (BI-H) tras el cambio.
- Un solo partido medido (una semilla, equipo humano contra orcos en cápsula). Las cifras de traza son del partido
  entero; las dibujadas, de tramos de 3–40 s. Suficiente para el orden de magnitud, no para afinar umbrales.
