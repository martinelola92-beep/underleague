# 0119 — El director agrupa en `/Sim` y presenta en `/Game`

Estado: **Aceptada** (19 sep 2026). Revisión de arquitectura (skill `architecture-review`) de la fase E de la
dirección de UI (`docs/ui/README.md` §3, §4, §11). Decisión del orquestador dentro de la dirección que el
revisor acordó en las fases A–D.3; la dirección misma se registra en la **ADR 0120**.

## Problema

La dirección de UI pide un **director de presentación**: los sucesos del motor se agrupan en *momentos*, cada
momento tiene un nivel (N1–N4), un nivel decide la frase, el canal, la duración y si congela la reproducción,
y todo deja un residuo persistente. La medición de la fase A (8,6 momentos por partido; N1 3,79 · N2 0,85 · N3
2,80 · N4 1,16) se hizo con una simulación en papel en Python (`docs/ui/prototipo/medicion/director.py`), fuera
del repositorio de código.

Hay que decidir dónde vive cada parte sin que `/Game` interprete la simulación (RT-014), sin perder la
posibilidad de medir y sin mover complejidad de sitio sin eliminarla.

## Protocolo

**¿Existe ya el patrón?** Sí, dos veces. `Sim.Run.View` ya contiene vistas puras que convierten eventos en
dato estructurado para la interfaz: `MatchLogView` (el log de RF-121) y `MatchFlashView` (el cartel de perk,
ADR 0112). El agrupador tiene exactamente esa forma: eventos + traza → filas ordenadas. No se crea un
proyecto nuevo ni una capa nueva.

1. **Frontera.** El agrupador y la clasificación en niveles son una función pura de `MatchResult.Events`,
   `MatchTrace` y los puntos de sustitución: van a `Sim.Run.View`, sin tipos de Godot (RT-011) y sin reloj
   (RT-012). El **ritmo real** —duraciones en segundos, la cola de la voz alta, la caducidad, la animación—
   depende del reloj de reproducción y de la velocidad elegida: va a `/Game`, y no decide nada del partido.
2. **¿Elimina complejidad o la mueve?** La elimina en `/Game`: sin esto, `MatchScreen` tendría que
   reconocer detalles de evento (`"severe"`, `":cancelled"`, `"red"`) y decidir fusiones. Esa lógica ya
   existe en Python y está medida; en `/Game` no se podría probar (RT-084: sin tests de interfaz). En
   `Sim.Run.View` se prueba con xUnit y la medición de la fase A se convierte en una prueba estadística
   reproducible en vez de un script fuera del repo.
3. **Determinismo.** No toca el motor: RT-024 no cambia. La vista es determinista por construcción (orden
   por fotograma y, en empate, por índice de evento; nunca se itera un diccionario).
4. **Paralelismo.** La prueba estadística juega sus partidos con el patrón del arnés (`Parallel.For` por
   índice, `Catalog` por hilo, reducción en orden).
5. **Segundo orden.** (a) **Decisiones:** la sustitución vuelve a jugar el partido desde T (ADR 0094). Los
   momentos anteriores a T deben ser **idénticos** tras la re-simulación, así que un momento que contiene
   una decisión **se cierra en el tick de la decisión** y no fusiona sucesos posteriores. (b) **Residuo:**
   no lo produce el director sino el estado en el fotograma (marcador del log, estado físico de la traza).
   Así «saltar una presentación nunca salta su residuo» se cumple por construcción, y retroceder en la
   barra de tiempo funciona sin rebobinar el director. (c) **Regla de reproducción (C.2):** el momento lleva
   el **fotograma anterior al suceso** (`FreezeFrame`); el motor ya ha recolocado o retirado al jugador en el
   tick del suceso.
6. **Multiplataforma.** Nada nuevo: aritmética entera y orden explícito.

## Decisión

### En `/Sim` — `Sim.Run.View.MatchMomentView`

- `MatchMoment(Frame, FreezeFrame, LastFrame, Level, Kind, Team, LeadPlayerId, Pauses, Decision,
  EventIndices)`, ordenados por `Frame` y, en empate, por el primer índice de evento.
- **Clasificación** (tabla de `docs/ui/README.md` §4; la misma que `director.py`): falta, consumible y
  lesión anulada → N1; amarilla y lesión leve → N2; gol, roja, lesión grave, turba y árbitro que se va → N3;
  muerte y final → N4; el saque inicial → N3 sin cartel de perk; la sustitución sola → N1 (residuo).
- **Fusión**: un suceso se une al momento abierto si llega en ≤ 15 ticks (1 s, RT-020) desde el último
  suceso del momento y comparte persona (actor, objetivo o rival), o si forma una cadena fija (gol → final,
  turba → árbitro se va, muerte/lesión/roja → final). El nivel del momento es el máximo; lo encabeza el
  suceso de mayor nivel (el último en empate).
- **Pausa**: gol, muerte y final congelan a 1×; la lesión grave propia congela; **toda decisión congela a
  cualquier velocidad**.
- **Marcas de perk** (N1, canal propio): los `MatchFlash` de `MatchFlashView`, **sin los del primer segundo**
  del partido (P1: 14,5 de 19 perks saltan en el saque; son estado inicial, no acontecimiento). Una marca del
  mismo jugador dentro de un momento se marca como absorbida (subtítulo del momento, no cartel aparte).
- **Política de velocidad** (C7): una función pura dice, para un momento y una velocidad (1, 4, 16), si se
  presenta, a qué nivel y si congela. x4: solo el gol, la N4 comprimida y las decisiones; x16: solo la N4
  comprimida y las decisiones. Las duraciones en segundos **no** están aquí.

### En `/Game`

- **Director** (clase C# sin nodos): recibe los momentos y, cada fotograma de pantalla, el fotograma de la
  traza, el tiempo real transcurrido y la velocidad; devuelve qué presentaciones están activas y si la
  reproducción está congelada y en qué fotograma. Una sola voz alta (N3/N4) con cola y caducidad (1,5 s
  *provisional*); un canal de sellos (N1/N2) aparte. `Seek(frame)` descarta lo pendiente anterior a ese
  fotograma: nunca se presenta hacia atrás.
- **Pantalla de retransmisión**: pasa a ser la pantalla de Partido. La `MatchScreen` actual se queda como
  **modo depuración** (vista 2D, tick a tick, log, leyenda), accesible desde la retransmisión con una tecla y
  sobre la misma `MatchPlayback`: jugar el partido al entrar es idempotente, cambiar de modo no lo vuelve a
  jugar.
- **Tokens y Theme**: una clase estática de tokens (color, tipografía, medidas) al estilo de `Style`, y un
  `Theme` de Godot construido **en código** desde esos tokens con variaciones de tipo (proclamar, dato,
  marcador) aplicado solo a la raíz de la retransmisión. Las demás pantallas no cambian.
- **Lienzo**: la retransmisión se compone en el lienzo lógico **1920×1200** escalado al ancho de la ventana
  (×0,667 a 1280×800), que es la composición ya validada a 16:10 (`capturas/*-1280x800.jpg`). Pasar el
  proyecto a `aspect = expand` para 16:9 afecta a todas las pantallas, que están maquetadas en coordenadas
  absolutas de 1280×800; **queda fuera de esta ADR** y se hará con su propia `visual-review` de cada pantalla.

## Consecuencias

- La medición de la fase A deja de depender de un script externo: la prueba estadística de
  `MatchMomentView` fija la banda.
- Cambiar un nivel, una fusión o la política de velocidad es un cambio de `/Sim` con test, no un retoque de
  interfaz. Es deliberado: la gramática de eventos es una regla de presentación **medida**, y se mide.
- Cambiar una duración, una animación o la caducidad es un cambio de `/Game`, provisional hasta el
  prototipo animado con arte.
- `MatchFlashView` no cambia: el filtro del saque y la absorción son de `MatchMomentView`.

## Alternativas descartadas

- **El director entero en `/Game`**: la clasificación dependería de cadenas de detalle del motor dentro de
  la interfaz, y la medición no se podría repetir con pruebas (RT-084).
- **El director entero en `/Sim`, duraciones incluidas**: meter segundos reales y la cola de presentación
  en `/Sim` mezcla ritmo de pantalla con dato de partido y obliga a tocar `/Sim` para ajustar una animación.
- **Un proyecto `/Presentation` aparte**: mueve la complejidad sin eliminarla; `Sim.Run.View` ya es ese
  sitio, igual que `/Run` sigue siendo un namespace hasta que haga falta separarlo.
- **Residuo producido por el director**: rompe el retroceso y la regla de que saltar no pierde información.
