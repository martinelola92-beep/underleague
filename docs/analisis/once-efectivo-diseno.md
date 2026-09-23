# Nota de diseño — el once efectivo y el hueco deliberado

*(`game-design-review`, 23 sep 2026, antes de implementar. Familia [BA-I](../pendientes/BA-I.md) +
[BB-F](../pendientes/BB-F.md) + [BC-H](../pendientes/BC-H.md). Genera ADR 0134.)*

## Por qué son un solo problema

No son tres defectos de interfaz. Los tres son el mismo concepto ausente: **nadie calcula «quién va a
jugar realmente»**. `/Sim` lo decide dentro de `RunLineup.Build` → `SelectStarters` en el momento de jugar,
y **todos los consumidores de fuera lo vuelven a derivar de `state.Lineup`**, que es la alineación
*guardada* —la intención— y no el once efectivo:

| Consumidor | Qué lee hoy | Qué debería leer |
|---|---|---|
| `RunEngine.LineupWarnings` (`RunEngine.cs:265`) | `state.Lineup` | once efectivo |
| `RunEngine.LethalRisks` (`RunEngine.cs:345`) | `state.Lineup` | once efectivo |
| «TU ONCE» del Ojeo (`ScoutScreen.cs:228`) | guardados | once efectivo |
| «TU ONCE» de Equipo (`TeamScreen.cs:625`) | guardados | once efectivo |
| Ventana de sustitución (`MatchScreen.cs:463`) | ni una cosa ni otra | once efectivo |

De ahí salen los tres síntomas. Y hay un segundo concepto ausente, que es el que convierte esto en
mecánica y no en contabilidad: **una casilla vacía no puede ser una intención**, solo una ausencia. Por eso
el relleno automático no se puede rechazar antes del partido (BC-H) ni durante (BA-I).

## Las diez preguntas

**1. ¿Qué experimenta el jugador?** Se lesionan dos titulares, vuelve al mapa y el Ojeo le dice «juegas en
inferioridad: con 5 en campo…». Pulsa «Empezar» y salen **siete**, dos de ellos gente que él no puso y cuya
ficha no ha visto. En el partido se lesiona un tercero, se abre una ventana que dice solo un nombre —no en
qué posición jugaba, no qué casilla deja hueca— y le obliga a elegir entre tres nombres igual de opacos. No
puede decir «ninguno».

**2. ¿Qué decisión toma hoy?** Antes del partido, **ninguna**: el aviso es falso (el once se rellena solo) y
no hay forma de rechazar el relleno. Durante el partido, una **elección a ciegas y forzada**. BB-F midió que
esa elección sí cambia el partido (467 pares: el marcador cambia el 70 %, el ganador el 31 %), así que hoy
es exactamente lo que el proyecto llama poder no legible: consecuencia grande, información cero.

**3. ¿Qué decisión DEBERÍA tomar?** Dos, y son la misma en dos momentos: **a quién expongo**. Antes del
partido, si el hueco lo tapa un suplente o lo dejo vacío (RF-002d). Durante el partido, si el hueco lo tapa
un suplente o lo dejo vacío. La razón para dejarlo vacío no es táctica, es de **desgaste**: el que entra
puede lesionarse y morir, y la plantilla es el recurso central de la run. Jugar 6 contra 7 a cambio de no
poner otro cuerpo en riesgo es un canje legible y muy de Underleague.

**4. ¿Qué regla representa?**
- **RF-002d** (existe, y hoy se incumple): «Se puede jugar en inferioridad numérica, con 5 o 6 jugadores,
  dejando casillas vacías… **Es una decisión legítima**… La interfaz lo advierte de forma explícita antes de
  confirmar la alineación.» Hoy la decisión es **imposible** mientras quede banquillo, y el aviso salta
  cuando no toca.
- **RF-012c / RF-012d** (se incumplen): `LethalRisks` se calcula sobre la alineación guardada, así que **el
  jugador de relleno no recibe indicador de riesgo de muerte**. Es daño no anunciado sobre alguien que el
  jugador ni siquiera sabía que iba a jugar — y rompe la **condición 3 de la ADR 0048** («se puede reducir
  el riesgo con la alineación»), que es requisito y no aspiración (regla 11 del `CLAUDE.md`).
- **UI-003** (restricción, no permiso): veinte segundos entre partidos, del mapa al saque en dos
  pulsaciones. Prohíbe la salida «pedir confirmación del once cada vez».
- **Rechazar el sustituto es mecánica nueva**: ningún RF dice que la sustitución sea obligatoria; la
  obligatoriedad es un efecto lateral de la ADR 0094, no una decisión tomada. Lo digo explícitamente.

**Corrección a la premisa de BA-I.** El revisor lo pidió porque «los no-muertos no reciben penalización por
lesión, así que igual prefiero seguir con él». Eso **no es lo que pasa**: `MatchEngine.RemoveFromPitch`
(`:528-533`) saca del campo *toda* lesión, leve o grave, sin mirar la raza; el `minorInjuryPenalty` del que
habla la ADR 0026 es −15 % de atributos **entre partidos** (`RunState.cs:312`), no dentro. «Seguir con él»
exigiría que el lesionado leve permaneciera en el campo, que es otra mecánica y mucho mayor. La decisión que
sí existe y merece existir es **no meter a nadie**. BA-I se cumple en su segunda mitad (decir la posición) y
en su espíritu (no forzar), no en su literalidad.

**5. ¿Qué sistemas intervienen?**
- `/Sim`: `RunLineup.Build`/`SelectStarters` (quién juega), `RunEngine.LineupWarnings`/`LethalRisks` (qué se
  advierte), `Substitutions.Pending`/`ResolveAutomatically` (el punto de decisión), `Lineup`/`RunState` (dónde
  vive la intención). **El cálculo del once efectivo se hace una vez en `/Sim` y se consume**; hoy el reparto
  entre `/Sim` y `/Game` es justo el fallo que RT-014 prohíbe: dos sitios que pueden contradecirse, y se
  contradicen.
- `/Game`: `ScoutScreen` (aviso y «TU ONCE»), `TeamScreen` («TU ONCE», hueco deliberado), `MatchScreen` y
  `BroadcastScreen` (ventana de sustitución). Solo pintan.
- `/data`: `data/l10n` para los textos nuevos. Ninguna tabla de balance.

**6. Alternativas consideradas.**
- *(A) Que el relleno pida confirmación cada vez.* **Rechazada: viola UI-003.** Convierte el caso normal
  —no ha pasado nada, juega el mismo once— en una pulsación extra.
- *(B) Quitar el relleno automático y que el jugador alinee a mano cuando falte gente.* Rechazada: castiga
  al jugador por una lesión que ya le castigó, y con plantilla mínima no hay elección real que tomar.
- *(C, elegida) El relleno se mantiene pero deja de ser invisible*: se calcula como **once efectivo** en
  `/Sim`, se enseña como tal en «TU ONCE» marcando al que entra de relleno, y el aviso dice la verdad
  («hueco tapado con X» contra «juegas con 6»). El hueco deliberado y el rechazo del sustituto son la
  **vía de escape explícita**, no el camino por defecto.
- Para la ventana de sustitución: *(D)* enseñar solo la posición del que sale; *(E, elegida)* posición y
  casilla del que sale, y por candidato posición, estado y **riesgo letal contra este rival**, con el que
  propondría la política por defecto marcado y una opción «que se quede el hueco».

**7. ¿Qué trade-off introduce?** El hueco deliberado da algo caro —no exponer otro cuerpo— a cambio de algo
caro: se juega 6 contra 7 el resto del partido, con la casilla vacía en la cuadrícula. No es poder gratis
porque el coste se paga **en el mismo partido** y es inmediatamente visible. La información añadida (riesgo
letal por candidato) no da poder: da **previsibilidad**, que es lo que RF-012d exige y hoy falta.

**8. ¿Cómo cambia las estrategias?** Aparece una jugada que hoy no existe: **proteger al banquillo**.
Con la clínica cobrando por pieza (ADR 0099) y la muerte solo en la entrada, aguantar 20 ticks con 6 puede
ser más barato que meter a un canterano que no volverás a poder curar. Interactúa con la inferioridad
voluntaria de RF-002d (misma decisión, otro momento), con `SubstitutionPolicy.Default` (que ahora se enseña
en vez de aplicarse sola) y con los perks de vínculo, que no cambian.

**9. ¿Puede degenerar?** Dos riesgos y sus topes:
- *Rechazar siempre para no gastar suplentes.* Tope natural: RF-002b termina la run por debajo de 5
  disponibles, y con 6 en campo se encaja más. Si la medición dijera que la política automática mejora
  rechazando, sería que el suplente medio vale menos que una casilla, y eso es un problema de balance de
  plantilla que habría que ver — se mide, no se supone.
- *El hueco deliberado como exploit de colocación* (vaciar una casilla para deformar el marcaje rival).
  Improbable con la geometría actual, pero es justo lo que `/Balance` tiene que mirar.
- **La política automática de `/Balance` no cambia**: sigue sustituyendo siempre. Así el hueco deliberado
  entra como opción del jugador sin desplazar ni una tirada de las 43 puertas — condición de aceptación.

**10. ¿Cómo se demuestra?**
- **No desplaza el partido**: las 43 puertas y `summary.csv` **idénticos** a la línea base, porque la
  política automática no usa lo nuevo. Es la misma prueba que usaron el paquete de memoria (F1) y la
  ADR 0125 D1.
- **Tests de `/Sim`**: el once efectivo coincide con el que juega el partido (contra `RunLineup.Build`);
  `LethalRisks` sobre el efectivo devuelve riesgo **para el jugador de relleno** (hoy no lo hace: test de
  regresión de RF-012c); el aviso distingue «relleno» de «inferioridad real»; rechazar el sustituto es una
  respuesta válida que no reabre el punto de decisión; el hueco deliberado sobrevive a `PruneLineup` y se
  borra tras el partido como la marca de riesgo de RF-093.
- **Determinismo**: RT-024 en verde; rechazar y volver a resolver da el mismo partido byte a byte.
- **`/Game`**: capturas por Xvfb del Ojeo con relleno, del Ojeo con inferioridad real y de la ventana de
  sustitución (skill `visual-review`), enganchadas a la secuencia de `--screenshots` para que se
  regresionen solas.

## Lo que esta nota NO decide

- Si el lesionado **leve** debería poder quedarse en el campo (la literalidad de BA-I). Es una mecánica
  mayor, toca el motor y no la abre este paquete; queda anotada en BA-I.
- Si `SubstitutionPolicy.Default` es una *buena* política. Aquí solo se **enseña** lo que elegiría.
