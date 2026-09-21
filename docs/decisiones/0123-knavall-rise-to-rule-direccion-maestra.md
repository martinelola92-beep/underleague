# 0123 — Knavall: Rise to Rule — dirección maestra

Estado: **Aceptada** (21 sep 2026, **decisión del revisor**). ADR paraguas: consolida la dirección vigente
y **separa lo decidido de lo aspiracional**. No sustituye a ninguna ADR anterior; la **0122** sigue vigente
y este documento la enmarca.

**Esto no es un GDD.** No describe contenido ni sistemas al detalle: fija qué está decidido, qué es
aspiración medible, y en qué orden se trabaja. El detalle vive en `docs/requisitos.md` y en los análisis
citados.

---

# PARTE 1 — DECISIONES DE PRODUCTO

Vinculantes. Cambiarlas exige una ADR nueva.

## D1 · Nombre e identidad

El proyecto pasa a llamarse **Knavall: Rise to Rule**.

El **lenguaje del mundo es parte de la identidad del producto**, no decoración: *to knav* (herir, lesionar)
· *knaved* (herido) · *Knavall* (el torneo y el deporte). El humor alrededor de *knav someone / knav them
all* es material de diseño, no un chiste suelto.

*Nota de ejecución, no de decisión:* el renombrado es **mecánico y barato** *(MEDIDO: 1.691 apariciones en
411 ficheros — 346 `.cs` de namespace, 2 `.csproj`, 40 documentos y 23 `title` de esquema JSON, todos
cosméticos; **ninguna es un id de dato**, así que no hay riesgo de romper `/data`)*. Cabe en un commit
aislado. **No se ejecuta en esta ADR.**

## D2 · Fantasía y premisa

Autobattler roguelike de fútbol medieval-fantástico brutal. Clanes de una sola raza compiten en un torneo
**cuyo ganador obtiene el derecho a gobernar**. La fantasía del jugador es llevar un clan **desde abajo
hasta gobernar**.

Consolidado con la ADR 0122: la premisa electoral aporta el marco —justifica el reinicio de la run, el
nombre y la institución multirracial— y la estructura de run existente **ya encaja sin cambios** (3 actos,
20 partidos, 3 jefes, el del acto 3 como final).

## D3 · Qué hace que sea roguelike

No basta con que haya runs. Una run es **una secuencia de decisiones irreversibles con consecuencias
emergentes**. Los ocho rasgos que lo definen, **con su estado real** (§Parte 3):

| rasgo | estado |
|---|---|
| empiezas con un equipo limitado | **hecho** |
| cada partido modifica el estado de la run | **hecho** |
| eliges perks/builds y recursos | **hecho** |
| los jugadores pueden lesionarse y morir | **hecho** (0,81 lesiones/partido, 1,85 muertes/run) |
| eventos con decisiones y trade-offs | **hecho pero invisible** (0,38 cartas por run) |
| los rivales reaparecen y evolucionan | **reaparecen, no evolucionan, y nadie lo recuerda** |
| el mapa determina oportunidades y enfrentamientos | **hecho** |
| perder termina la run | **hecho** |
| cada run parte de un estado y combinaciones distintas | **hecho** |

**El metajuego desbloquea posibilidades, nunca sustituye la progresión de la run.**

**Principio rector:** la historia de una run la generan las decisiones y consecuencias del jugador. No hay
campaña guionizada.

## D4 · Bucle

`Preparar → Partido (~90 s, automático) → Consecuencias → Evento/decisión → Build/equipo → …→ final`

El partido **es automático y no se toca su duración** (ver A3). La profundidad vive en la preparación, la
build, la lectura del rival y las decisiones entre partidos.

## D5 · Mundo

**El mundo engancha por memoria y consecuencias, no por volumen de lore.** Queda **descartado** como vía de
producto: lore enciclopédico, árboles genealógicos, diplomacia, política jugable, quests tradicionales y
diálogo masivo.

## D6 · Espectáculo

Arquitectura vinculante: **`SIMULACIÓN → FLUJO DE EVENTOS → PRESENTATION DIRECTOR → espectáculo`**.

Esta decisión **ya está tomada y ejecutada** en la **ADR 0119**: los momentos, niveles, pausa y política de
velocidad son una vista pura de `/Sim` (`MatchMomentView`), y el ritmo vive en `/Game`
(`Game/Match/PresentationDirector.cs`). Este ADR la **ratifica**, no la reabre.

**No se presentan todos los eventos.** Se priorizan: gol, lesión, muerte, remontada, rivalidad, jugada
excepcional y momento decisivo. *Regla operativa*: un suceso se presenta si tiene **protagonista nombrado**,
**causa nombrable** o **conexión con un hecho anterior**.

Estética: retransmisión deportiva + festival medieval + cómic violento, con humor negro.

## D7 · Comentarista

**Texto contextual + voz selectiva.** La voz **no** es narración continua: reacciona en los momentos
importantes; el texto permite contenido específico de cada run.

**ES/EN desde el diseño, implementación con texto y placeholders, voces después del vertical slice.**
*Deuda ya existente que esta decisión hereda:* 13 plantillas de acontecimiento viven en `Game/Ui/UiText.cs`
**sin versión `en`**, incumpliendo la ADR 0009.

## D8 · Árbitro

Parte del espectáculo y del mundo: silbato, gestos, tarjetas y personalidad. Puede introducir modificadores
controlados. **Nunca puede ser fuente arbitraria de frustración** — queda subordinado a RF-012d, que es
regla sin excepción.

## D9 · Progresión

- **Run**: perks, builds, recursos, estado de los jugadores, bajas, decisiones, rivales y eventos.
- **Meta**: desbloquea **posibilidades** —clanes, perks, eventos, rivales, variantes—. **Nunca estadísticas
  acumulables.** El progreso es *saber más*, no *ser más fuerte*.

## D10 · Criterio de admisión de trabajo

> La pregunta deja de ser «¿qué contenido falta?» y pasa a ser
> **«¿qué sistemas hacen que una run de Knavall genere una historia que el jugador quiera contar?»**

Todo sistema, perk, evento o pieza de presentación **debe justificar su contribución a esa experiencia**.
Es criterio de admisión, no una frase de intenciones.

---

# PARTE 2 — ASPIRACIONES

**No son vinculantes y no se pueden dar por cumplidas por decreto.** Cada una lleva la señal con la que
sabremos si se cumple, porque una aspiración sin señal es una frase de marketing.

| # | aspiración | cómo sabremos |
|---|---|---|
| A1 | cada run produce una historia memorable | el revisor cuenta una anécdota sin releer el log |
| A2 | los rivales se sienten como personajes | al ver el cartel de un nodo, dice algo en voz alta |
| A3 | las consecuencias importan | una derrota cambia la alineación del partido siguiente |
| A4 | los partidos son espectáculo, no simulación visible | un clip de 12 s sin gol comunica una regla del juego |
| A5 | cada raza tiene identidad jugable reconocible | **hoy en tensión con la ADR 0092**, que cerró el abanico racial a 6,4 puntos a propósito. Si esta aspiración se mantiene, hay que decidir si se reabre esa ADR |
| A6 | el humor negro emerge de los sistemas | el chiste está en el registro institucional, no en la voz del pregonero |
| A7 | «una run más» | pregunta directa al revisor tras el slice |
| A8 | el mundo parece recordar | **requiere el perfil entre runs, que no existe** (§Parte 3) |

**A5 y A8 son las dos que hoy chocan con una decisión o con la arquitectura. Están anotadas como
aspiraciones, no como decisiones, precisamente por eso.**

---

# PARTE 3 — Corrección del estado actual

**El plan propuesto parte de un estado desactualizado y mandaría rehacer trabajo terminado.** Corrección
verificada el 21 sep 2026:

| lo que dice el plan | lo real |
|---|---|
| «~94 perks» | **102** *(LEÍDO)* |
| «Presentation Director pendiente de consolidación» | **existe**: `Game/Match/PresentationDirector.cs`, y la ADR 0119 ya separó simulación y presentación, con 17 tests y revisión independiente |
| «MatchScreen contiene lógica/debug de presentación» | **correcto, y es a propósito**: `MatchScreen` es hoy el **modo depuración** (F3). La pantalla real es `BroadcastScreen` (1.420 líneas) |
| «~26 tipos de eventos» | **correcto** — pero **14 de los 26 no llegan nunca al jugador** |
| «vertical slice de 3-5 partidos» | la run completa **ya existe y está medida**: 20 partidos, 3 actos, `runWinRate` 22-27 %, `--full-runs` con tres políticas |

**Consecuencias sobre el plan:**

- **P0 está casi hecho**, y lo que falta no son partidos: es el **rival recurrente**. Y ese cuesta **cero
  cambios de esquema** — los rivales ya reaparecen (garantizado por palomar en los actos 2 y 3), el
  emparejamiento y el resultado ya están guardados, y la identidad del jugador rival ya es estable.
- **P2 está hecho salvo un momento**: gol, falta, lesión, muerte y final ya se presentan. **La remontada
  no existe** *(LEÍDO: no hay `comeback` ni en `MatchMomentView` ni en `PresentationDirector`)*.
- **P1 tiene ya evidencia parcial medida**: las builds se diferencian (entradas ×7,5 entre extremos), la
  elección de suplente cambia el ganador el 31 % de las veces, y no pasar por el mercado hunde la victoria
  de 25,25 % a 7,83 %. **Las decisiones ya importan; lo que falla es que no se noten.**
- **A8 no tiene dónde vivir**: `Sim/Run/Save/` contiene únicamente `RunSave.cs`. No hay perfil ni
  compendio. RF-014b y RF-126 están declarados y sin implementar.

---

# PARTE 4 — Plan revisado

Mantiene la intención de P0-P6 y corrige el punto de partida. **Encaja con la ADR 0122 sin contradecirla.**

| # | qué | estado / coste |
|---|---|---|
| **P0** | **Cerrar el bucle con rival recurrente.** No hace falta construir una run pequeña: hay que hacer que la que existe recuerde. Memoria de enfrentamiento (lectura pura sobre `RunState`), el hecho «quién knaveó a quién» (contador de clave libre), y `clanId` en el esquema de rival para que un clan cruce de acto | **casi todo hecho; lo que falta es cero esquema** |
| **P1** | **Que las decisiones se noten.** No añadir perks ni eventos. Memoria del jugador propio (Fase 1A) + abrir `modifyUtility` con un perk (Fase 1B) + rasgos nuevos en `/data` | ADR 0122, acordado |
| **P2** | **Completar el director**, no rehacerlo: falta el momento de **remontada**, y sacar del silencio los eventos que no llegan (`SAVE` el primero) | pequeño |
| **P3** | **Partido como espectáculo**: cámara, público, VFX, árbitro, SFX, comentarista textual, primeras voces. Objetivo explícito: **pocos momentos memorables, no actividad constante** — 1-3 por partido | medio |
| **P-R** | **Re-skin de Knavall**: renombrado mecánico, lenguaje del mundo, comunicados institucionales, «qué se juega» en el nodo | **muy bajo, adelantable a casi cualquier punto** |
| **P4** | **Identidad del mundo**: 2-3 razas/clanes, 1 rival recurrente, 1 árbitro, 8-12 eventos buenos, perks representativos, titulares y humor negro | medio |
| **P5** | **Arte final del slice** — sujeto a la regla de fase: no se produce arte hasta cerrar el diseño de fase 2 | alto |
| **P6** | **Medir antes de escalar**, con las seis preguntas del encargo | — |

**Lo que este plan NO hace, y es deliberado:** no añade perks, no añade eventos, no añade razas y no alarga
el partido. Los cuatro están medidos como no-cuello-de-botella.

---

## Decisiones pendientes del revisor

Anotadas aparte **porque no están decididas y no deben darse por decididas**:

1. **¿La elección sustituye a las rencillas o las enmarca?** La premisa electoral, sola, deja sin apuesta
   19 de cada 20 partidos. Propuesta sobre la mesa: *la elección es la escalera, las rencillas son los
   peldaños* (`docs/analisis/premisa-electoral-y-memoria-de-mundo.md` §A).
2. **A5 contra la ADR 0092**: ¿se reabre el abanico racial o se acepta que la identidad de raza es visual
   y la jugable vive en el clan y los rasgos?
3. **¿Vampiros como raza o como clan?** Recomendación registrada: clan de los no-muertos.
4. **¿Cuándo se construye el perfil entre runs?** Recomendación registrada: al final, después de que la
   rivalidad dentro de la run demuestre que funciona.
5. **El desgaste, ¿recurso de run o de acto?** (ADR 0122 §20, sigue abierta).
6. **`kamikaze`/`iron_price`**: su coste declarado no existe en el motor, y PD-1 se apoya en ellos.

---

## Consecuencias

- Sustituye como documento de dirección a cualquier lectura informal previa del rumbo del proyecto.
  `docs/project-state.md` pasa a apuntar aquí.
- **No** reabre las ADR 0119 (director), 0120 (retransmisión), 0092 (razas) ni 0122 (memoria primero).
- El renombrado a Knavall queda **decidido y no ejecutado**: es un commit aislado, mecánico y sin riesgo
  sobre `/data`.
- La regla de fase sigue vigente: **no se produce arte hasta cerrar el diseño de la fase 2** (P5).

## Hermanos

- `docs/decisiones/0122-primero-memoria-y-atribucion-no-mas-contenido.md` — el plan de trabajo vigente.
- `docs/analisis/auditoria-identidad-generador-de-historias.md` — el diagnóstico medido.
- `docs/analisis/auditoria-conceptual-narrativa.md` — rivales, personalidad, perks, clanes.
- `docs/analisis/premisa-electoral-y-memoria-de-mundo.md` — la premisa y la memoria entre runs.
