# Plan de evolución de Knavall — del sistema que funciona a la run que se recuerda

**Fecha:** 21 sep 2026. Deriva de la **ADR 0123** (dirección maestra) y la **ADR 0122** (memoria y
atribución primero). **No rediseña nada y no rehace nada.**

Toda la evidencia citada procede de los análisis ya hechos y **no se repite aquí**:
`docs/analisis/auditoria-identidad-generador-de-historias.md` (el diagnóstico medido),
`auditoria-conceptual-narrativa.md` (rivales, personalidad, perks),
`premisa-electoral-y-memoria-de-mundo.md` (memoria entre runs),
`docs/plan-fase-memoria-y-atribucion.md` (el plan de detalle de F1).

---

# 1. Diagnóstico — los cinco cuellos de botella reales

Ordenados por cuánto impiden la experiencia de la ADR 0123, no por facilidad.

### CB-A · La causa no llega al jugador
**Tipo: presentación + percepción.** *(MEDIDO / LEÍDO)*

**14 de los 26 `EventType` no se presentan nunca** —pase, regate, entrada, recuperación, intercepción y
**parada** incluidos—, así que en pantalla una parada y un pase fallado son el mismo suceso. El cartel de
perk lleva **el nombre del perk, nunca el efecto**, y sale **0,9 veces por partido** con el **65 % de los
partidos sin ninguno**.

**Por qué es el primero:** las decisiones **ya tienen impacto medido** —builds ×7,5 en entradas, el
suplente cambia el ganador el 31 %, evitar el mercado hunde la victoria de 25,25 % a 7,83 %—. *El problema
no es que las decisiones no importen: es que el jugador no puede saber que importaron.* Mientras esto no se
arregle, **cualquier sistema nuevo será igual de invisible**, así que todo lo demás rinde menos.

### CB-B · Nada recuerda nada
**Tipo: sistema.** *(LEÍDO, triple verificación)*

Tres huecos de distinto precio, y conviene no confundirlos:

- **Jugador propio**: el motor calcula `PlayerMatchStats` (goles, asistencias, entradas, faltas, minutos) y
  `RunPlayer` **no persiste ninguna**. Además **no atribuye** lesiones ni muertes causadas: `Kill(victim,
  detail)` no recibe al matador. *Coste: pequeño + un trozo de motor.*
- **Rival dentro de la run**: los rivales **ya reaparecen** (garantizado por palomar en los actos 2 y 3), el
  emparejamiento (`opponentId`) y el resultado ya están guardados, y la identidad del jugador rival ya es
  estable. Lo único que falta es dejar de tirar el hecho en `MatchResolution.cs:96`. *Coste: **cero cambios
  de esquema**.*
- **Entre runs**: `Sim/Run/Save/` contiene un único fichero. No hay perfil. *Coste: sistema de persistencia
  nuevo.*

### CB-C · Los perks no cambian la intención
**Tipo: sistema. NO es contenido.** *(MEDIDO / LEÍDO)*

63 % de los efectos mueve una cuota o un contador. La capa de intención **existe en dos mitades y ninguna
está al alcance de una decisión del jugador**: los rasgos llevan multiplicadores por acción (`Coward`
`{Tackle: 45, Block: 35}`) pero **se sortean y ningún perk puede tocarlos**; y `modifyUtility` —el canal
mutable por acción y por tick, ya pilotado y medido al 24 % con RT-056 en verde— **no está en el enum del
esquema**, así que ningún dato puede usarlo.

*No se resuelve con más perks. 102 es la proporción correcta y 30 ya producen comportamiento reconocible.*

### CB-D · Lo declarado y lo implementado divergen
**Tipo: sistema + confianza.** *(CONFIRMED, nueve casos verificados)*

La turba no existe (cuatro líneas y ningún cambio de regla, en el 27,6 % de los partidos) · el portero
**no puede** salir del área (`GoalkeeperLeftArea` nunca puede ser `true`; el rasgo `Rusher` no puede cumplir
su nombre) · `kamikaze` e `iron_price` son ventaja pura vendida como sacrificio · `numb` concede inmunidad
a un sistema inexistente **y RT-035 lo imprime en la ficha de todo no-muerto** · prótesis y vínculos no
existen · `AERIAL_DUEL` nunca se emite · `MatchLogView` no se usa · RF-014b y RF-126 sin implementar.

**Es el riesgo Football Manager**: una simulación correcta que promete lo que no cumple se percibe como
rota. Y envenena en silencio: no falla ningún test.

### CB-E · Decisiones de producto abiertas
**Tipo: decisión.** Las seis de la ADR 0123. **Bloquean fases concretas y no se resuelven aquí** (§3).

---

# 2. Roadmap por fases

Ordenado por **impacto / coste**. Las fases con la misma letra de coste pueden solaparse.

---

## F1 · Memoria — que el juego sepa lo que ha pasado
**Impacto: muy alto · Coste: bajo** · *(ADR 0122 Fase 1A + 1C)*

**Objetivo:** que al final de una run existan hechos atribuidos a nombres propios, propios y rivales.

**Reutiliza:** `PlayerMatchStats` entero (ya calculado) · `opponentId` en el guardado (ya persistido) ·
`NodeHistoryEntry.Result` (ya persistido) · `RunState.FindNode` (el puente, ya existe) · la convención de
**contadores de clave libre**, que el esquema bendice explícitamente para no subir de versión · la
identidad estable del jugador rival (`OpponentFirstPlayerId + índice`).

**Cambios:**
1. Historial de carrera en `RunPlayer` (registro nuevo, **no** reutilizar `Counters`: es rehén de las reglas
   de perk). Sube `CurrentSchemaVersion` 4 → 5.
2. `PlayerMatchStats` gana `InjuriesCaused` y `DeathsCaused`; `Kill` gana un matador opcional —su única
   llamada está dentro de `ResolveInjury`, que ya conoce al `tackler`—.
3. `RivalHistory`: clase **pura de lectura** sobre `RunState`, sin estado ni esquema. **Excluir `Boss`**
   del filtro: el nodo de jefe consume un `opponentId` fantasma.
4. Registrar «quién knaveó a quién» en `MatchResolution.cs:197`, vía contador de clave libre.
5. **`clanId` en el esquema de rival** + los 15 ficheros, para que un clan cruce de acto.
6. **Superficie mínima obligatoria**: la ficha de jugador y el cartel del nodo muestran lo anterior.
   *Regla dura de esta fase: no se construye memoria que nadie vea.*

**Dependencias:** ninguna. **Riesgo:** medio-bajo, y concreto — tocar `Kill` puede desplazar el consumo de
RNG. *Mitigación: F1 no debe mover ninguna métrica.*

**Terminado cuando:** (a) las **43 puertas idénticas** a la línea base; (b) tras una run, el guardado
contiene al menos un hecho por jugador superviviente que no era derivable al empezar; (c) el cartel de un
nodo repetido dice contra quién, cuántas veces y con qué resultado.

---

## F2 · Atribución — que el jugador pueda ver la causa
**Impacto: muy alto · Coste: bajo-medio** · *(ADR 0122 Fase 2)*

**Objetivo:** que un partido se explique solo, y que la memoria de F1 se note.

**Reutiliza:** el `PresentationDirector` **tal cual** (no se toca su arquitectura, ADR 0119) · los canales
de presentación existentes (sellos, estandartes, bandos, punch-in, sacudida) · `MatchLogView`, que **ya
produce la crónica estructurada y no se usa** · las plantillas de `UiText.cs`.

**Cambios:**
1. **Presentar `SAVE`.** Es el suceso más caro de ocultar: en fútbol una parada es un acontecimiento. Y
   decidir explícitamente sobre `TACKLE` y `RECOVERY`.
2. **El cartel de perk dice qué hizo**, no cómo se llama — y deja de encogerse cuando lo absorbe un momento
   (hoy se atenúa justo la relación que habría que subrayar).
3. **Nombrar a los jugadores rivales** en pantalla. Hoy los sellos son anónimos y el rival no tiene ni
   tiras de identificación.
4. **Conectar `MatchLogView` a `ReportScreen`**: la crónica prometida existe y no está enchufada.
5. **Dorsales estables** al sustituir (BC-F): hoy se renumeran, y el dorsal es el único identificador en
   campo.
6. **Cartel de rivalidad en el nodo**, alimentado por `RivalHistory`.

**Dependencias:** F1 para los puntos 3 y 6. Los puntos 1, 2, 4 y 5 son independientes y **pueden empezar
ya**.

**Riesgo:** bajo. Solo `/Game`, salvo el punto 2, que necesita que el efecto sea nombrable.

**Terminado cuando:** un clip de 12 s **sin gol y sin muerte** comunica al menos una regla del juego; y, en
el informe de después, el jugador puede señalar qué jugada decidió el partido y por qué.

---

## F3 · Intención — que dos jugadores del mismo puesto no jueguen igual
**Impacto: alto · Coste: medio** · *(ADR 0122 Fase 1B)*

**Objetivo:** que una build cambie **qué intenta hacer** un jugador, no solo si gana la tirada.

**Reutiliza:** `modifyUtility` ya implementado en el motor y **medido al 24 % con las siete métricas de
RT-056 `IN` en dos semillas** · `Pitch.ZoneOf` para la condición de tercio · el histograma de acción de la
Tanda 0, **ya calibrado** · los `actionMultipliers` de rasgo, que ya son perfiles de intención.

**Cambios, por coste ascendente:**
1. **Rasgos nuevos en `data/traits/traits.json`** para los arquetipos que faltan (protector, obsesivo,
   insistente). **Coste cero de motor.** Se hace primero, porque comprueba si la diferenciación de
   intención se nota **antes** de tocar nada.
2. `modifyUtility` en el enum del esquema + `utilityAction`/`utilityZone` + soporte de `PerkLoader`.
3. **Techos por acción y por puesto en el cargador (C10)**: un ×3 a `Shoot` en un portero tiene que ser un
   **error de datos**, no una anécdota (RT-032).
4. **Un** perk que lo use. Uno.
5. Saldar la deuda del piloto: `independent-reviewer` sobre C1 (tocó `/Sim` y nunca pasó) y **RT-024 con el
   perk realmente equipado** (solo se verificó con C1 inerte).
6. **Hacer visibles los rasgos** en la ficha y en el dossier del rival.

**Regla de diseño que esto fija, y que evita reabrir la ADR 0088:** *los rasgos describen quién es un
jugador y pueden restar; los perks describen qué ha aprendido y solo suman.*

**Dependencias:** F2.6 para que se note. **Riesgo: alto, y es el único alto de la primera mitad** — es un
recalibrado, no un parche, y el presupuesto está ajustado: `injuriesPerMatch` **0,81 con techo 0,90**.

**Terminado cuando:** el histograma de acción da **L1 claramente distinto de cero con exposición completa
verificada** (un L1 = 0 con poca activación no prueba nada), y las siete métricas obligatorias siguen `IN`.

---

## F4 · Verdad — que lo que el juego dice sea lo que el juego hace
**Impacto: alto (defensivo) · Coste: bajo pero disperso**

**Objetivo:** eliminar el riesgo Football Manager antes de invitar a nadie a jugar.

**Reutiliza:** nada nuevo. Es una pasada sistemática, no quince arreglos sueltos.

**Cambios:** retirar o implementar `numb`/RF-104 · corregir `gathering_thirst`, `shadow`, `no_dying`
(`per: match` contra un diseño que pide run, con `LimitScope.Run` que nunca se reinicia) · decidir sobre
`kamikaze`/`iron_price` (**gate**, §3) · retirar formalmente vínculos y prótesis del alcance o programarlos
· `AERIAL_DUEL` · las 13 plantillas sin versión `en` (incumple ADR 0009) · el texto del portero que sale.

**Dependencias:** ninguna, salvo el gate de `kamikaze`. **Riesgo:** bajo por cambio; medio en agregado si
toca `/data` de perks — cada uno necesita su comprobación.

**Terminado cuando:** ningún texto visible por el jugador promete un efecto que el motor no produce, y
queda una lista cerrada —no vacía— de lo que se retira a propósito, con su ficha.

---

## F5 · Espectáculo — pocos momentos, no actividad constante
**Impacto: alto · Coste: medio-alto**

**Objetivo:** 1-3 momentos memorables por partido, con protagonista nombrado.

**Reutiliza:** el director y sus niveles de momento · la dirección de UI ya acordada (pregón, heráldica) ·
los gestos de cámara existentes.

**Cambios:** el **momento de remontada**, que es el único hueco real de la lista de la ADR 0123 *(LEÍDO: no
existe ni en `MatchMomentView` ni en el director)* · gestos de cámara · público · árbitro con personalidad
—subordinado a RF-012d— · SFX · **comentarista textual**, con voz después del slice.

**Dependencias:** F2. **Riesgo:** medio. El de verdad es de *ritmo*: hoy hay 4,8 presentaciones por minuto
y 7,1 s entre ellas; **añadir sin quitar destruye la jerarquía**.

**Terminado cuando:** 1-3 momentos con protagonista nombrado por partido, medido sobre una muestra, **sin
que suba el total de presentaciones**.

---

## F6 · Re-skin de Knavall
**Impacto: medio-alto en identidad · Coste: muy bajo · Adelantable a casi cualquier punto**

Renombrado mecánico **en su propio commit aislado** (1.691 apariciones, 411 ficheros; las 23 de `/data` son
títulos de esquema, ningún id) · lenguaje del mundo (*to knav*, *knaved*) · comunicados institucionales ·
**«qué se juega aquí» en el nodo**.

**Dependencias:** el punto 4 depende del **gate 1** (elección contra rencillas). Los otros tres, de nada.
**Riesgo:** muy bajo. **Terminado cuando:** el juego se llama Knavall en todas partes y el nodo dice qué
está en juego.

---

## F7 · Turba real
**Impacto: alto · Coste: alto en balance · GATED**

El clímax declarado ocurre en el **27,6 %** de los partidos y hoy es una etiqueta. Requiere `Pitch`
parametrizable (`Pitch.Rows` es `const`) y **consume el presupuesto de `injuriesPerMatch`**, que no lo
tiene. **Bloqueada por el gate 5.**

---

## F8 · Memoria de mundo
**Impacto: alto si F1 funciona · Coste: sistema de persistencia nuevo · GATED**

Perfil entre runs: gobierno en ejercicio, compendio (RF-014b, ya preparado dentro de la run), desbloqueos
(RF-126), figuras conocidas **con cupo duro**. No puede vivir en `/Sim` (RT-012): mismo patrón que
`RunSave`, versionado desde el día uno.

**Va la última a propósito.** Es la parte más cara y la menos frecuente: si cruzarte tres veces con un clan
**dentro** de una run no produce la sensación, cruzártelo en la siguiente tampoco, y habrás pagado una capa
de persistencia para averiguarlo.

---

# 3. Dependencias y gates

**No resuelvo ninguna de las seis. Indico qué bloquea cada una.**

| # | decisión abierta (ADR 0123) | bloquea |
|---|---|---|
| 1 | ¿la elección **sustituye** a las rencillas o las **enmarca**? | **F6.4** (qué dice el nodo que está en juego). No bloquea F1-F5 |
| 2 | A5 contra la **ADR 0092**: ¿identidad jugable por raza, o visual? | **el alcance de F3.1** (si los rasgos nuevos se reparten por raza) y cualquier trabajo de identidad racial |
| 3 | ¿vampiros raza o clan? | nada hoy. Bloquearía cualquier fase de contenido nuevo de raza |
| 4 | ¿cuándo se construye el perfil entre runs? | **F8 entera** |
| 5 | ¿el desgaste es recurso de **run** o de **acto**? (`healsRoster`) | **F7** y cualquier trabajo sobre la carne como moneda |
| 6 | `kamikaze`/`iron_price`: ¿deben penalizar de verdad? | **una parte de F4**. Si sí, hace falta una capacidad de motor que no existe (que el que entra se lesione) y eso es C4 real |

**Ninguna bloquea F1 ni F2**, que son las dos de mayor impacto. Ese es el argumento para empezar por ahí.

---

# 4. Plan de implementación — qué toca cada fase

Nivel suficiente para que otro agente lo convierta en ADR, spec o tarea. Cada fase que toque `/Sim`,
`/data` o una ADR necesita `independent-reviewer` (Regla E); las que tocan comportamiento cuantificable,
`balance-measure` (Regla D); las mecánicas nuevas, `game-design-review` (Regla B) **antes** de implementar.

| | F1 memoria | F2 atribución | F3 intención | F4 verdad | F5 espectáculo | F6 re-skin |
|---|---|---|---|---|---|---|
| **Simulación** | `PlayerMatchStats` +2 campos; firma de `Kill` | — | `PerkLoader`, techos C10 | varias rutas | momento de remontada | — |
| **Run** | `RunPlayer` historial; `RivalHistory`; `MatchResolution` | — | — | `LimitScope.Run` | — | — |
| **Save** | `CurrentSchemaVersion` 4→5; contadores libres | — | — | — | — | — |
| **PresentationDirector** | — | nuevos momentos y niveles | — | — | ritmo, gestos | — |
| **BroadcastScreen** | — | `SAVE`, cartel con efecto, nombres, dorsales | rasgos visibles | — | cámara, público, SFX | — |
| **UI** | ficha de jugador, cartel de nodo | `ReportScreen` + `MatchLogView` | ficha y dossier | textos | — | nombres, comunicados |
| **Audio/comentario** | — | — | — | — | comentarista textual; voz después | — |
| **Datos/config** | `clanId` + 15 ficheros de rival | plantillas `es`/`en` | `traits.json`, esquema de perks, 1 perk | varios `/data` | — | títulos de esquema |
| **Tests** | round-trip de guardado; **43 puertas idénticas**; RT-024 | — | histograma de acción; RT-024 con perk equipado; RT-056 | validación de `/data` | muestreo de momentos | compilación |

**Delegable con especificación cerrada** (`opencode-worker`): esquemas JSON y su validación, tests de
round-trip, los 15 ficheros de `clanId`, las plantillas `en`, el renombrado mecánico.
**No delegable:** la frontera del historial en `RunPlayer`, la firma de `Kill`, la lectura del histograma,
y cualquier decisión de los seis gates.

---

# 5. Validación

Dos clases de señal, y conviene no confundirlas: **las automáticas pueden desmentir, no confirmar**; las
de partida son las que deciden.

### Señales automáticas (puertas, no opiniones)

| qué | instrumento | umbral |
|---|---|---|
| F1 no rompe nada | las 43 puertas | **idénticas** a la línea base |
| el historial existe | round-trip del guardado | ≥1 hecho no derivable por superviviente |
| F3 cambia la intención | histograma de acción contra control emparejado | L1 ≠ 0 **con exposición verificada** |
| F3 no rompe el partido | RT-056 | las siete obligatorias `IN` |
| el ritmo no se degrada | presentaciones por minuto | **no sube** respecto a 4,8 |
| determinismo | RT-024 | verde, **con el perk equipado** |

### Las siete preguntas del encargo, con su prueba

1. **¿Entiende por qué una decisión cambió el resultado?** — Prueba: tras un partido, el revisor señala la
   jugada que lo decidió y el porqué, **sin abrir el log**. *Hoy fallaría: 14 de 26 eventos no se presentan.*
2. **¿Recuerda rivales y acontecimientos?** — Prueba: al aparecer el cartel de un nodo repetido, **dice algo
   en voz alta**. Es la señal de A2 de la ADR 0123, y es binaria.
3. **¿Las consecuencias se acumulan?** — Prueba: una derrota cambia la alineación del partido siguiente.
   *Instrumento barato ya disponible: la elección de suplente ya cambia el ganador el 31 %; basta con
   comprobar que el jugador lo sabe cuando elige.*
4. **¿El partido tiene momentos memorables?** — Prueba: un clip de 12 s **sin gol ni muerte** comunica una
   regla. *Hoy solo la pasan el gol y la muerte.*
5. **¿Entiende quién es su clan?** — Prueba: describe su equipo en una frase que no sea su raza.
   **Atención: esta es la que hoy está en tensión con la ADR 0092** y puede ser la que obligue a abrir el
   gate 2.
6. **¿Una derrota invita a otra run?** — Prueba: pregunta directa tras perder. Con `runWinRate` 22-27 %,
   **tres de cada cuatro candidaturas fracasan**: es la señal que más veces se va a muestrear y el tono
   tiene que absorberla.
7. **¿El mundo parece reaccionar?** — **No se puede probar hasta F8**, y F8 está gated. *Decirlo es parte
   de la validación: no se debe declarar cumplida A8 con nada de F1-F7.*

### El listón final, y es el de la ADR 0123

> El sistema debe producir una anécdota contable **sin que se haya escrito una escena para ella**.

Caso de prueba concreto, ya validado como **alcanzable** con F1+F2+F3 y **sin sistemas nuevos**: un defensa
con intención de carnicero knavea a un delantero rival con nombre; el clan reaparece; el delantero vuelve;
marca. Las cuatro piezas que hacen falta —identidad estable del rival, reaparición, hecho atribuido y
presentación con nombre— **existen o cuestan cero esquema**, salvo la intención, que es F3.

---

## Lo que este plan NO hace, y es deliberado

No crea un vertical slice (la run completa existe y está medida) · no crea un `PresentationDirector` (ya
existe) · **no añade perks, eventos, razas ni duración de partido**: los cuatro están medidos como
no-cuellos-de-botella · no reabre la ADR 0092 ni la 0119 · no resuelve ninguno de los seis gates.
