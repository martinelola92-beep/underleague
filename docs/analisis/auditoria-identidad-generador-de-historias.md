# Auditoría de identidad: ¿es Underleague un generador de historias?

**Fecha:** 20 sep 2026. **Tipo:** auditoría de producto, no de balance ni de calidad de código.
**Estado:** diagnóstico y propuestas. **No se ha cambiado ni una línea del juego.**

Etiquetas de evidencia, obligatorias en todo el documento: **MEDIDO** (dato experimental, propio o de un
informe del repositorio) · **LEÍDO** (está en el código o en el dato, verificado) · **DERIVADO** (se deduce
de lo anterior) · **INFERIDO** (interpretación sin prueba). Para las conclusiones se usa además el estado
epistemológico de la Regla F: **CONFIRMED** / **LIKELY** / **SIN EVIDENCIA DE ACTIVACIÓN**.

Método: seis auditorías paralelas (perks, simulación, legibilidad, eventos/mapa/economía, identidad de
jugadores, referencias externas) más un lote propio de `/Balance` de 500 partidos. Las fuentes internas
principales son `docs/analisis/builds-analisis-sistemico.md`, `docs/analisis/c1-piloto-cazagoles-diseno.md`,
`docs/analisis/catalogo-conceptual-fase-a.md` y `docs/analisis/decision-vinculos.md`; este documento se
apoya en ellas y **no repite sus mediciones**.

---

## 1. Executive summary

**Underleague ya produce los sucesos de los que están hechas las historias. Lo que no produce es
atribución.** El jugador ve *qué* pasó, casi nunca *por qué*, y casi nunca *a quién*. El cuello de botella
no está en la generación: está en la transmisión y en la memoria.

Cuatro medidas independientes lo sostienen:

1. **La oferta de sucesos es abundante.** *(MEDIDO, propio, 500 partidos, semilla 1)* El **67,8 %** de los
   partidos contiene al menos un suceso excepcional: lesión 53,0 %, prórroga de turba 27,6 %, roja 12,0 %,
   incomparecencia por plantilla agotada 4,0 %.
2. **La transmisión está rota aguas arriba.** *(LEÍDO)* **14 de los 26 `EventType`** que emite `/Sim` no
   llegan nunca al jugador — pase, regate, entrada, recuperación, intercepción y **parada** incluidos. En
   pantalla, una parada y un pase fallado son el mismo suceso: el balón cambia de dueño.
3. **La causa es invisible por construcción.** *(MEDIDO + LEÍDO)* 57 de los 128 efectos de perk son
   `modifyProbability` y 24 más son `addCounter`: el **63 %** del catálogo mueve una cuota o un contador.
   `Sim/Engine/Utility.cs` —la función que decide qué hace cada criatura— **no consulta los modificadores
   de perk**. Un perk no cambia lo que la criatura *quiere hacer*; cambia si gana la tirada que ya iba a
   hacer.
4. **No hay memoria del jugador-criatura.** *(LEÍDO, triple verificación)* El motor calcula por partido
   goles, asistencias, tiros, pases, entradas, entradas ganadas, faltas, tarjetas y minutos
   (`MatchReport.PlayerMatchStats`). `RunPlayer` **no persiste ninguna**. El generador de nombres es
   excelente; detrás del nombre no hay nada que recordar.

**El corolario que gobierna todas las propuestas de este documento: no añadir sucesos raros nuevos hasta
que los existentes se lean.** La recomendación intuitiva —«hacen falta más eventos, más perks, más
contenido»— es la que la evidencia contradice más claramente.

**La intervención de mayor relación impacto/coste es una sola y ya está identificada por tres vías
independientes:** persistir en `RunPlayer` las estadísticas que el motor ya calcula. Desbloquea el
obituario (RF-122, hoy imposible aunque se implementara: no hay datos que poner), el máximo goleador, el
historial y la mitad de la clase de historias que este documento llama «de personaje».

**La segunda es que la palanca conductual ya está construida y desconectada:** `modifyUtility` (C1) existe
en el motor, está pilotada y medida al 24 % con las siete métricas de RT-056 en verde… y **no está en el
enum de `data/schemas/perks.schema.json`**, así que ningún dato puede usarla *(LEÍDO, verificado)*.

---

## 2. La hipótesis de identidad, y la tensión que hay que resolver

La hipótesis del encargo —«Underleague debería sentirse como un generador de historias de fútbol
violento»— **no contradice el diseño existente**, pero choca de frente con una regla si se lee mal.

- **RF-012d** (regla 11, sin excepción): *«todo lo **malo** que ocurra en un partido debe haber sido
  previsible con la información disponible antes de empezarlo»*.
- **El objetivo pedido**: *«construí esto, lo dejé jugar y no sabía exactamente qué iba a pasar»*.

Leído con precisión, **RF-012d restringe el daño, no el resultado**; y restringe su *existencia anunciada*,
no su magnitud, su momento ni su reparto. De ahí la formulación que este documento propone adoptar como
criterio de diseño, porque deja intacta la regla y abre todo el espacio narrativo:

> **La previsibilidad es del riesgo; la sorpresa es del resultado.**
> El jugador debe saber antes del partido *qué puede salir mal y con qué probabilidad*.
> No debe saber *a quién le va a tocar, cuándo, ni qué hará el resto del equipo en consecuencia*.

Las cinco condiciones de la **ADR 0048** son exactamente eso: un contrato de riesgo anunciado, no de
resultado anunciado. La condición 3 («se puede reducir el riesgo con la alineación») es la que convierte
el azar en decisión, y es la que las propuestas de §17 deben reforzar, nunca debilitar.

**Consecuencia práctica**: cualquier propuesta que aumente la sorpresa debe hacerlo en el *quién*, el
*cuándo* y el *cuánto*, nunca introduciendo una clase de daño nueva sin anunciar.

---

## 3. Qué hacen los roguelikes relevantes

Informe completo con fuentes en el anexo de referencias. Aquí solo lo que condiciona decisiones.
*(Aviso de método: reddit.com estaba bloqueado para el crawler del agente; los verbatims proceden de Steam
Community, que sí era accesible. Los ejemplos de Reddit no están verificados y no se citan.)*

**Los tres patrones que importan:**

**(A) Lo compartible se reparte en tres formatos, y cada uno exige algo distinto.** *Clipeable* = cabe en
5 s sin contexto (Vampire Survivors, Risk of Rain 2). *Capturable* = cabe en una imagen con contexto (el
mazo de Slay the Spire, el marcador de Balatro). *Contable* = necesita cronología y nombres propios (Dwarf
Fortress, FTL, Blood Bowl de liga, Football Manager). **Consecuencia incómoda:** si el partido no produce
picos, la comunidad desplaza lo compartible a la fase de gestión —es literalmente lo que ocurre en
Football Manager y Backpack Battles— y el juego se recuerda como una hoja de cálculo temática.

**(B) El mecanismo probado para volver contable un efecto numérico es la resolución secuencial con
acumulado visible.** Balatro dispara cada joker de izquierda a derecha mostrando su aportación con el total
corriente actualizándose; su propio análisis de diseño lo formula como *«esta animación de 300 ms sustituye
páginas de documentación mostrando la causalidad directamente»*. Super Auto Pets resuelve un ataque cada
vez, con orden posicional fijo. **La legibilidad se compra reduciendo el número de eventos, no
acelerándolos.**

**(C) Blood Bowl ya resolvió la tensión de §2**, con cuatro mecanismos: probabilidad visible antes de
exponerse · tabla de consecuencias pública e invariable · **degradación escalonada acumulativa** (cada
lesión permanente suma a las tiradas de lesión futuras, así que la muerte casi siempre llega precedida de
avisos que el jugador ignoró) · y **un recurso escaso para negar la muerte** (el apotecario), que convierte
cada pérdida en parte una decisión de gestión previa. Los dos últimos son los que hacen que la muerte se
**acepte** en vez de sentirse arbitraria, y los dos son adoptables barato aquí.

**El contraejemplo que más importa**, porque Underleague tiene tres de sus cuatro piezas a medio construir:
FTL, Darkest Dungeon, Blood Bowl y Dwarf Fortress producen historias **sin sinergias**, con otra receta —
nombre propio · historial acumulado visible · pérdida irreversible · causa legible de esa pérdida. *Las
sinergias producen builds memorables; los nombres propios con historial producen personajes memorables. Son
ejes independientes y se pueden tener los dos.*

**Los modos de fallo documentados**, por orden de relevancia para este proyecto:
1. *Las estadísticas que el juego enseña no explican el resultado* — Football Manager lleva veinte años con
   esto. Es el riesgo número uno de Underleague (§8).
2. *Azar que decide sin poder apostar contra él* — Mechabellum en MMR alto. El azar se tolera cuando es
   **apostable**, no cuando es «compensable a la larga».
3. *Sinergias con nombres opacos sin descripción.*

---

## 4. Qué tiene Underleague actualmente

**Lo que ya funciona, y conviene no romper:**

- **La oferta de sucesos** *(MEDIDO, propio)*: 68 % de partidos con algo excepcional. §13.
- **El generador de nombres** *(LEÍDO)*: «Grok Rompehuesos», «Thrainn Barbahierro», por raza, con
  apellidos parlantes y equivalencia es/en. Es el único pilar de identidad **completamente resuelto**.
- **La diferenciación medida entre builds** *(MEDIDO, `builds-analisis-sistemico.md` §5)*: las entradas por
  partido van de 1,57 a 11,86 entre builds, un factor de 7,5. `elf_bulwark` y `orc_violence` ganan casi lo
  mismo jugando partidos **opuestos**. Es diferenciación táctica real, no reetiquetado.
- **La voz de pregón y la heráldica** *(LEÍDO)*: el gol y la muerte están bien contados. El bando de muerte
  —«…el fallecimiento de X, delantero, al minuto 71; **rematado por “Sed de médula”**»— es **el único
  momento del juego con atribución causal completa**, y no por casualidad el más contable que tiene.
- **El matasanos** (ADR 0099): el único sitio del juego con un dado que el jugador tira a sabiendas, con
  los porcentajes en el botón, que puede matar por una decisión de menú.
- **El determinismo** (RT-013/RT-021/RT-024): activo estratégico infrautilizado. §18-D.

**Lo que está construido y desconectado** (esto es lo caro de descubrir y lo barato de arreglar):

| pieza | estado | dónde |
|---|---|---|
| `modifyUtility` (C1) | motor **sí**, esquema **no** → inalcanzable desde `/data` | `Utility.cs:164`, `EffectEngine` · ausente en `perks.schema.json` |
| `PlayerMatchStats` | se calcula cada partido, **no se persiste** | `MatchReport.cs:39-53` vs `RunState.cs:122-162` |
| `MatchLogView` (crónica RF-121) | existe, **no se usa en `ReportScreen`** | `Game/` |
| `prostheses[]`, `bonds[]`, `mourning`, `bondProgress` | serializados, **cero productores** | `RunSave.cs` |
| `RunReferee.BribesReceived` | existe, **nadie lo incrementa** | `/Sim/Run` |
| `MatchReport.GoalkeeperLeftArea` | bandera que **nunca puede ser `true`** | `MatchEngine.Move():1009-1026` |
| `EventType.AERIAL_DUEL` | declarado, **nunca emitido** | `MatchEngine.cs:3372` |

---

## 5. Perks

**Catálogo: 102 perks, 128 efectos.** Clasificación por **función narrativa**, no por calidad ni por
balance *(informe completo: anexo de clasificación)*:

| clase | qué es | nº | % |
|---|---|---|---|
| **A — Story-generating** | produce comportamiento reconocible o situación memorable | 30 | 29,4 % |
| **B — Build-defining** | no da espectáculo solo, pero cambia cómo se construye y se alinea | 28 | 27,5 % |
| **C — Supporting** | valor estadístico o de consistencia | 36 | 35,3 % |
| **D — Invisible** | el impacto puede existir y es imperceptible | 8 | 7,8 % |

**El catálogo está bastante mejor de lo que su propia auditoría anterior indicaba** (la ADR 0112 midió
41 de 61 imperceptibles). La causa es identificable *(LEÍDO)*: las primitivas del paquete AY y C4/C5/C7/C8
—`modifyMarkBias`, `modifyTackleBias`, `shiftHome`, `modifyZoneShape`, `relocate`, `setState`,
`cancelEvent`, `extraAction`, `injure`— son solo **27 de 128 efectos (21 %)** pero **sostienen 24 de las 30
clase A**. Los 57 `modifyProbability` + 24 `addCounter` (63 %) son casi exactamente el mapa de la clase C.

### La regla que sale del cruce primitiva × clase *(DERIVADO, y es el hallazgo de esta sección)*

> Las primitivas que producen un **suceso** o eligen un **objetivo** dan clase A en **16 de 16** casos.
> Las que mueven una **cuota** dan A solo cuando la consecuencia es irreversible (los 4 letales).
> **Entre «subir un porcentaje» y «hacer que pase algo» no hay gradiente: hay un escalón.**

Esto responde a la pregunta §3 del encargo (no confundir profundidad con historias) con un criterio
operativo en vez de una intuición: *no hay que convertir todos los perks en comportamientos*. Hay que
saber que la clase C no escala a clase A por mucho que se le suba el número, y presupuestar el catálogo
por funciones.

### Funciones que faltan — fantasía sin ninguna clase A que la cubra

1. **La renuncia declarada.** No existe ni un perk que diga *«este jugador no hace X»*. Es la ausencia más
   grande, y es justamente la que produce personajes (§18-A).
2. **La preferencia situacional** («ante esto prefiere X sobre Y»): todo lo situacional es hoy un
   `modifyProbability` con condición.
3. **El vínculo entre criaturas concretas.** Los 7 perks de `links` son geometría de casilla-hogar, no
   relación. Nada de venganza por un muerto ni rivalidad persistente.
4. **Miedo, pánico y contagio.** Nadie huye ni deja de entrar porque acaben de matar a su compañero. *El
   juego es carnicería administrada y el efecto de la carnicería sobre los vivos es invisible durante el
   partido.*
5. **La prótesis**: pilar declarado de identidad del proyecto, sin un solo perk cuyo mecanismo sea «le
   falta algo y lo ha sustituido».
6. **El portero como personaje** y **el árbitro como sistema con el que se juega.**
7. Ninguna clase A construida sobre el **pase**, el **regate**, la **fatiga** ni la **moral**: toda la
   clase A vive en la violencia y la colocación.

### Perks donde el texto promete lo que el efecto no hace

Esta auditoría **verificó en el motor** los dos primeros; son los más graves porque sostienen una decisión
de diseño registrada (PD-1: «un perk puede penalizar si expresa identidad»).

1. **`kamikaze` — CONFIRMED, ventaja pura vendida como sacrificio.** Su `_doc` dice *«lo que cuesta: sus
   propias lesiones por encima de las de todo el equipo… con la penalización en el MISMO efecto que la
   ventaja»*. Sus dos efectos son `hardTackleBonus +15` e `injuryChanceBonus +20`, y en
   `MatchEngine.ResolveInjury:2545` el término es `tackler.InjuryChanceBonus`: **sube la probabilidad de
   lesionar a la víctima**. No existe en el motor ningún término por el que el que entra se lesione a sí
   mismo. Los dos efectos son beneficio.
2. **`iron_price` — CONFIRMED, mismo mecanismo.** Texto: *«cada rival que rompe se lo cobra su propio
   cuerpo»*. Efecto real: cada rival que rompe **lo vuelve mejor rompiendo al siguiente**. Bola de nieve
   vendida como deuda.
3. **`gathering_thirst`** — su `_doc` declara que el criterio del árbitro se le gasta en la misma
   proporción. **No hay ningún `modifyBias` en el perk.** El coste declarado no existe en el dato.
4. **`shadow`** — promete altura relativa al vinculado; hace un `shiftHome −1` absoluto y su `links` no se
   consulta nunca.
5. **`deep_run`** — promete correr a la espalda del central; medido `TerritorialBalance −4,302`: el equipo
   juega 4,3 puntos **más atrás**, y los goles no se mueven.

**Esto no es una lista de bugs sueltos: es el mismo patrón que `project-state.md` ya había encontrado cinco
veces en un día.** §15 lo trata como cuello de botella sistémico.

### Clase D, para constancia
`home_ref` (1,4 % de activación, valor −1) · `crowd_control` (6,5 %, −1) · `silky_veteran` (4,9 %, 0) ·
`wing_overlap` (9,0 %, 0) · `road_warrior` (0 %, muestra insuficiente) · **`numb`** (media habilidad racial
de los no-muertos: concede inmunidad al **luto**, RF-104, sistema sin implementar) · `elf_touch` ·
`quick_learner`.

---

## 6. Sinergias

**Respuesta a la pregunta fundamental del encargo («¿puede un jugador descubrir una interacción y sentir
que ha encontrado algo?»): hoy no, y la causa es estructural, no de catálogo.** *(DERIVADO)*

- **Dentro del mismo canal, los perks se componen como cuotas** con rendimiento decreciente y techo por
  rareza (`ProbabilityScale`, ADR 0050 P1). Acumular perks del mismo canal rinde **menos** por diseño: es
  una antisinergia estructural.
- **La única sinergia real medida del juego no es perk+perk.** Es `modifyAttribute`/`modifyLeash` → cambia
  qué acción se elige → cambia **cuántas veces se dispara todo lo demás**. Un perk de atributo multiplica
  el valor de los de probabilidad; al revés no ocurre. *Mecanismo CONFIRMED; magnitud **SIN MEDIR**, porque
  el instrumento que la mediría sigue roto (`BuildBatchRunner` no pasa el catálogo de objetos, B2).*
- **Entre rasgos sí hay multiplicación visible**: `Aggressive`×`Dirty` = ×2,0 en `Tackle`
  (`MatchPlayer.cs:105`). Es la interacción más «descubrible» que existe hoy, y **no se comunica en ningún
  sitio**.
- **Un solo efecto del catálogo toca al equipo contrario**: `free_man`, vía `MarkAvoidanceCells`.

**El diagnóstico honesto:** el juego no tiene un problema de *cantidad* de sinergias, tiene un problema de
**superficie de contacto**. Con el 63 % del catálogo moviendo cuotas en canales separados y con techo, no
hay casi nada que pueda multiplicarse. **No se arregla añadiendo perks; se arregla añadiendo efectos que
cambien el terreno en que actúan otros efectos** — que es exactamente lo que hace C1 (§18-A).

**Reserva metodológica que hay que respetar:** `perk-values.json` e `item-values.json` se miden **uno a uno
contra un espejo**, es decir, por construcción sin interacción. *Cualquier afirmación sobre sinergias medidas
hoy es inválida hasta que se arregle B2.* Esta auditoría no la hace.

---

## 7. Simulación de partidos

**Catorce acciones, y siete de ellas son el mismo gesto.** *(CONFIRMED)* `ChaseBall`, `MarkOpponent`,
`OfferSupport`, `CoverSpace`, `Retreat`, `FindSpace` y `PressCarrier` se resuelven todas como `Move()`
hacia un punto, sin estado ni evento propio. El motor no produce conductas distintas: produce **destinos**
distintos. `LongPass` y `ThroughPass` comparten estado y evento con `ShortPass`; `Block` reutiliza
`EventType.Tackle`.

**La varianza es de trayectoria, no de repertorio.** *(CONFIRMED)* La decisión es 100 % determinista
—`Utility.cs` no llama al RNG ni una vez—. Hay trece puntos de azar, **siete de ellos de posesión**, y la
posesión conmuta la tabla táctica de los dos equipos: una tirada distinta reescribe las decisiones de
catorce jugadores al tick siguiente. Hay bifurcaciones reales. Pero son **siempre las mismas trece tiradas
sobre las mismas catorce acciones**: *dos partidos cuentan la misma clase de historia con otros números.*

### La turba no existe *(CONFIRMED — hallazgo mayor)*

`docs/simulacion.md` promete, para la prórroga a gol de oro: campo estrechado, público invadiendo casillas
fijas, +15 % de velocidad, y el árbitro fuera —sin faltas ni tarjetas—. El motor hace **cuatro cosas**
(`CheckEndConditions:3102-3108`): emitir `MOB_START`, emitir `REFEREE_LEAVES`, resetear los límites de perk
y poner `_goldenGoal = true`. Un grep de `_goldenGoal|IsMob` en todo `/Sim` solo lo encuentra en el detalle
del gol, el fin de partido y la etiqueta de fase.

**El clímax declarado del partido es una etiqueta.** Y por medición propia ocurre en el **27,6 % de los
partidos**: uno de cada 3,6. Es, con diferencia, la mayor cantidad de historia por línea de código que hay
disponible en el proyecto. También invalida el sentido de RF-055d («las builds de violencia tienen ahí su
ventana natural»): no hay tal ventana.

### El ejemplo del encargo es imposible por construcción

«Mi portero abandonó la portería y ganó el partido» *(CONFIRMED como imposible)*: `Move()` acota al portero
al área **dos veces** (`:1009-1011`, `:1023-1026`) y su peso base de `Shoot` es **0**. La bandera
`MatchReport.GoalkeeperLeftArea` nunca puede ser `true`, y el rasgo `Rusher` («Sale mucho») no puede cumplir
su nombre. El perk `sweeper_keeper` mueve la correa dentro del área, que es otra cosa.

### Frecuencias de sucesos excepcionales

*(MEDIDO, propio, 500 partidos, semilla 1 — las décimas no son sólidas, los órdenes de magnitud sí)*

| suceso | % de partidos | frecuencia |
|---|---|---|
| lesión | 53,0 % | 1 de cada 1,9 |
| amarilla | 32,2 % | 1 de cada 3,1 |
| prórroga de turba | 27,6 % | 1 de cada 3,6 |
| roja | 12,0 % | 1 de cada 8,3 |
| incomparecencia (equipo bajo 5) | 4,0 % | 1 de cada 25 |
| **sin ningún suceso excepcional** | **32,2 %** | — |

Del repositorio, complementarias: penaltis ≈ 0,042 por partido (126 en 3.000, BD-A) · muertes 1,5-3 **por
run** · **córner: 0 en 60 partidos** (BB-N, sin diagnosticar).

### El techo: qué momento es imposible hoy y qué primitiva falta exactamente

| # | momento imposible | primitiva que falta |
|---|---|---|
| T1 | «se olvidó de todo y fue a por él» | **compromiso/histéresis de intención** — la utilidad se reevalúa desde cero cada 2 ticks y ninguna intención sobrevive entre decisiones |
| T2 | «cambió de idea a mitad de la acción» | **acción interrumpible** (`TryAbort`) — hoy `Shooting`/`Passing` devuelven `NoActions` durante 5 ticks |
| T3 | la turba entera | **`Pitch` parametrizable** (`Pitch.Rows` es `const`) |
| T4 | «el portero subió a rematar el córner» | excepción de zona por fase **y** arreglar la vía del córner |
| T5 | «nunca se lo perdonó» | **estado relacional persistente** `(A,B,tipo,intensidad)` en `RunState` |
| **T6** | «máximo goleador de la temporada» | **acumulador de carrera** — sumar `PlayerMatchStats` al `RunPlayer`. **La más barata y la que más historia desbloquea** |
| T7 | encararse, protestar, ayudar a levantarse | **acción dirigida sin resolución física** |
| T8 | «ese pase fue una barbaridad» | **cadena de jugada explícita** — `passChainAvgLength` medido 2,00-2,13: casi todo se resuelve en dos acciones |
| T9 | «un defensa que juega como otra cosa» | **`modifyBaseWeight`** — el peso base por posición no lo toca ningún efecto. El más potente y el más peligroso |
| T10 | «expulsión que se veía venir» | **ninguna**: el mecanismo existe (`_bias` acumula). Es presentación y calibración |

---

## 8. Legibilidad

**Veredicto: el jugador sabe QUÉ pasa mientras pasa; casi nunca POR QUÉ.** La cadena de presentación es
sólida para los sucesos de *estado* (gol, roja, lesión, muerte, turba, final) y **muda para todo lo que los
causa**.

**Los 14 `EventType` que no llegan al jugador** *(LEÍDO)*: `PLAY_START`, `PLAY_END`, `PASS_ATTEMPTED`,
`PASS_COMPLETED`, `PASS_FAILED`, `DRIBBLE_ATTEMPTED`, `DRIBBLE_WON`, `DRIBBLE_LOST`, `AERIAL_DUEL` (nunca
emitido), `TACKLE`, `RECOVERY`, `SHOT` (solo gesto de cámara, sin texto — decisión deliberada para no
delatar el resultado), `SAVE`, `SHOT_BLOCKED`.

**La ausencia de `SAVE` no está justificada en ningún documento.** Es probablemente la más costosa: una
parada es el suceso que más se celebra en el fútbol real y aquí es indistinguible de un pase fallado.

**Atribución perk → jugada: no existe** *(LEÍDO + MEDIDO)*. El cartel de perk (ADR 0112) lleva **solo el
nombre del perk, nunca el efecto**. Medido: **0,9 carteles propios por partido, y el 65 % de los partidos
no muestra ninguno**. Y el único vínculo causal presente en el código va en la dirección contraria: un
cartel absorbido por un momento se dibuja **más pequeño** — se atenúa justo la relación que habría que
subrayar. El sello «Anulado» no dice qué anuló ni quién (C13, abierta).

**Ritmo** *(MEDIDO, `docs/ui/README` §5, 11.176 partidos)*: 4,8 presentaciones por minuto a ×1, **7,1 s
entre presentaciones y 12,7 s entre voces altas**.

**Identidad en pantalla**: el dorsal es el **único** identificador en campo. Los nombres solo aparecen en
cinco sitios (gol, roja, lesión grave, muerte, bandeja); los sellos son anónimos. Y **BC-F, abierta**: al
sustituir se renumeran los dorsales, o sea que cambia a mitad de partido el único identificador visible.

**El problema inverso —el motor tenía razón y la pantalla mentía— ya tiene su caso canónico**: BB-M
(CONFIRMED), donde `LeavePitch` mandaba al lesionado a `(-1,-1)` y la lesión parecía ocurrir lejos de la
acción. Y **BB-F medida**: la elección de suplente cambia algún suceso el 96 % de las veces, el marcador el
70 % y **el ganador el 31 %** — y se presenta como un trámite.

**Post-partido**: es una tabla razonada, con buena voz y honesta (declara explícitamente no ser atribución
causal — conservar eso). Pero **no es una crónica**, y `MatchLogView` —que produce el dato estructurado con
minuto, bando, nombres y marcador— **no se usa en `ReportScreen`**. `docs/ui-partido.md` ya fija como
criterio de salida de fase 3 que «el partido se lea sin necesidad del log» y admite que hoy no se cumple.

**Hallazgo nuevo, no fichado**: las 13 plantillas de acontecimiento viven en `Game/Ui/UiText.cs` **sin
versión `en`**, lo que incumple la ADR 0009 (todo texto visible localizado desde `data/l10n/`).

---

## 9. Eventos

Seis cartas. *(LEÍDO; escala de referencia: partido = 140 xp, curar grave = 14 de oro, perk raro = 24.)*

| carta | ofrece | arriesga | ¿incierto? | ¿toca el próximo partido? | ¿contable? |
|---|---|---|---|---|---|
| `blood_oath` | 45 oro (≈2 victorias) | lesión **grave** a uno que señalas | no | **sí, fuerte** | **sí, la mejor** |
| `iron_vigil` | 260 xp a uno (≈1 nivel) | lesión leve **al mismo** | no | sí | **sí, 2ª** |
| `blood_pit` | 120 xp a titulares | lesión leve a uno que señalas | no | débil | apenas |
| `field_school` | 90 xp a titulares | −30 % del oro | no | ninguna | no |
| `guild_tithe` | cura a toda la plantilla | −40 % del oro | no | sí (es la clínica) | no |
| `smugglers_cart` | cura total + 60 xp | −50 % del oro | no | sí (ídem) | no |

**Tres de las seis son la misma carta** (familia «oro parado», nacida de la ADR 0098 para castigar el
atesoramiento, no para producir historia). **Ninguna tiene dado** —la ADR 0100 lo prohíbe—, y una carta sin
dado y con dos opciones es una operación aritmética. **Ninguna deja rastro**: los seis efectos son oro,
curación, xp o lesión; ningún estado duradero. Solo tres tienen `needsTarget`, que son las únicas donde el
jugador señala a alguien — y son exactamente las tres que la columna «contable» aprueba. *No es
coincidencia: señalar a una víctima con nombre es lo que convierte un evento en anécdota.*

**Y casi no se ven, por política y no por diseño** *(MEDIDO + LEÍDO)*: 0,38 cartas resueltas por run.
`RunPolicy.ChooseNode` puntúa **Evento 25** (40 si pobre) contra **Mercado 90 y Clínica 100**. El nodo de
evento es el peor puntuado salvo el jefe, y cada carta se paga saltándose un mercado — y saltarse mercados
es perder (7,83 % de victoria contra 25,25 %, ADR 0098). Con 6 cartas a 0,38 por run, **un jugador ve una
carta cada dos runs y nunca la misma dos veces: sin reincidencia no hay reconocimiento, y sin
reconocimiento no hay anécdota.** `BB-H` ya lo decía: «existen y casi no se ven».

*Nota sobre el encargo*: la lista de eventos seleccionados (La Apuesta, El Vestuario, Aficionado/Inversor/
Magnate, El Árbitro, El Contrabandista, El Partido Ilegal, La Caja) solo está implementada en dos
—`smugglers_cart` y `blood_pit`—. El resto sigue siendo diseño sin dato.

---

## 10. Mapa y decisiones

**El mapa de cuatro carriles sí arregló la falta de elección** *(MEDIDO)*: 1,90 nodos elegibles por paso
(antes 1,39), 37,2 % de pasos con una sola opción (antes 64,6 %), 47 % de pasos con tipos distintos (antes
17,9 %).

**Lo que no hay es estrategia de ruta.** El único eje narrable es mercado sí/no, y ese eje tiene respuesta
correcta conocida. El margen de ruta sobre número de partidos es de **3 en toda la run** (17 a 20, con
`PorousMatchLayers` fijado en 1 para que no sea más). La frase del encargo —«fui a por oro y llegué al jefe
con la plantilla rota»— **no es hoy describible**: el oro no se recoge en un carril, se recoge jugando, y
todos los caminos juegan casi lo mismo.

---

## 11. Economía: ¿funciona la carne como moneda?

**Casi nunca. Hoy es una penalización con un pequeño mercado secundario adosado.** *(DERIVADO)*

La carne **sí** se comporta como moneda en cinco sitios: el matasanos (5 de oro en vez de 14: 58 % cura /
30 % nada / **12 % un escalón peor, que en una grave es la muerte**) · las tres cartas con víctima señalada
· **alinear a un grave** (RF-093 vía 1, la más pura) · vender tocados · los perks que convierten cuerpos en
oro (`ad_machine`, `life_insurance`, `box_office`…).

**Pero los cinco tienen frecuencia medida cercana a cero** —0,03 matasanos por run, 0,38 cartas por run,
venta «casi nunca ejercitada» (CAT-I)— **frente a 1,85-1,89 muertes por run que el jugador no decide**. La
relación carne-gastada-por-decisión / carne-sufrida es de **≈1 a 5** *(INFERIDO)*.

Tres causas estructurales por encima de la frecuencia:

1. **La curación es unidireccional.** Clínica y dos cartas hacen todas oro→salud. Salud→algo solo existe en
   los cinco puntos de arriba.
2. **`nodeRewards.boss.healsRoster: true`** — ganar al jefe cura a toda la plantilla. *Esto es deliberado*:
   el `_doc` de `economy.json` dice que «cierra el ciclo de desgaste de cada acto». Pero su consecuencia es
   que **el desgaste es un recurso de acto, no de run**, y se borra tres veces por partida. `CLAUDE.md`
   abre diciendo que «el desgaste de la plantilla es el recurso central de la run». **Las dos cosas no
   pueden ser verdad a la vez, y la decisión de cuál vale es del revisor** (§20, pregunta 1).
3. **La ADR 0098 midió el contraejemplo**: la política que no compra nada tiene **la mitad de muertes**
   (1,10 vs 1,89) y llega más veterana. Hoy la forma de gastar menos carne es **no jugar a la economía**.
   Carne y oro no están enfrentados: la carne es consecuencia de construir, no alternativa a construir.

**Además, las cinco tarifas de la economía de la carne están sin medir**, y los tres ficheros lo dicen por
escrito: «calibradas a ojo», «PENDIENTES de la medición», «SIGUE SIN MEDIRSE».

---

## 12. Razas, jugadores y builds

**La raza casi no diferencia, y es una decisión tomada.** *(MEDIDO, ADR 0092)* El abanico se cerró
deliberadamente de 21,8 a **6,4** (enano 46,1 / elfo 52,5 / humano 47,3 / orco 52,1 / no-muerto 52,1), con
sesgos de suma cero y sin sesgo racial en técnica ni velocidad. Las habilidades raciales valen entre +0,4
(`roots`) y +2,2 (`elf_touch`). La propia ADR midió que «un enano con TODOS los datos del humano mide 47,6
contra 46,8». **Hoy la raza es cosmética + una habilidad pequeña + dos perks exclusivos.** El diferenciador
real medido es el **estilo individual** («los estilos pesan más que las razas», ADR 0092 §2) — y el estilo
**se sortea, no se elige**. Para el espectador sí se nota (radio de cuerpo, y `Aggressive` 25 en orcos
contra 3 en elfos cambia la utilidad de `Tackle` ×1,6).

**Lo que persiste de un jugador entre partidos, lista exacta y completa** *(LEÍDO, `RunSave.WritePlayer`)*:
id, nombre, raza, posición, rareza, nivel, experiencia, cinco atributos, rasgos, etiquetas, perks, un
objeto, estado físico, `minorInjuries`, salario, `matchesBenched` y los contadores de perk.

**Lo que NO existe en ningún sitio**: goles, partidos jugados, asistencias, entradas, paradas, faltas, a
quién lesionó, quién lo lesionó, cicatrices, apodo, historial, palmarés. La única memoria real son los 23
perks con `accumulatesAcrossMatches`, cuyos contadores topan en 3-5 y solo los tiene quien compró ese perk.

**Techo de transformación** *(DERIVADO)*: 2/3/4/5 slots por rareza, +1 objeto. Por run se ven 35-50 perks y
se adquieren **8-14** repartidos entre ~10 jugadores = **~1,2 perks nuevos por jugador y run** en 13,4
partidos. La mitad de los slots del once queda vacía. **Alcanza para “mejorado”, no para “transformado”.**
Y RF-114e asigna el perk comprado «a un jugador con slot libre»: la pregunta es *quién tiene hueco*, nunca
*a quién quiero convertir en alguien*.

**Arquetipos alcanzables hoy** *(INFERIDO, conservador)*: muro enano · carnicero orco · elfo intocable ·
rematador · conductor · portero que sale · superviviente no-muerto. Son **convención del jugador, no regla
del juego**: solo 11 de 102 perks tienen `positionOnly`, y los 35 perks `axis:identity` **no tienen** el
tope de «una identidad por jugador» que propone `perks-design-bible.md` §2.3.

### Prótesis y vínculos: los dos pilares declarados que no existen

`CLAUDE.md` define la identidad del juego como «lesiones, muertes, **prótesis y vínculos**». *(LEÍDO,
verificado por dos agentes independientes)*:

- **Prótesis: NO EXISTEN.** RF-095/095b/095c prometen taller de implantes, apuesta con oro y que «con 3
  prótesis el jugador pierde su etiqueta racial y adquiere `Autómata`, habilitando una familia de perks
  distinta». En el código hay el `record RunProsthesis`, el campo marcado «Fase 3», su serialización y su
  entrada obligatoria en el esquema de guardado. **Nada más.** En su lugar está el matasanos, que es buena
  mecánica pero **no deja nada en el cuerpo**: quien sobrevive a cinco sale idéntico a quien no fue nunca.
- **Vínculos: NO EXISTEN, y ya hay una decisión escrita de retirarlos.** Existen `BondKind`, `RunBond`,
  `Bonds`, `Mourning`, `BondProgress` y **dos consumidores que leen un cero permanente**. Cero productores.
  `docs/analisis/decision-vinculos.md` (15 sep 2026) ya lo midió y recomienda **retirar RF-100..106 del
  alcance de lanzamiento**.
- **Falso positivo a vigilar**: los «vínculos» que la ficha de jugador muestra son los **vínculos de
  alineación** (ADR 0021, geometría de casilla adyacente). Mirando la pantalla parece que RF-100..106 está
  implementado. Lo mismo pasa con el perk `inheritance`: hereda **el de al lado**, no el amigo.
- **Y `numb` promete lo imposible**: la habilidad racial de los no-muertos concede inmunidad al luto, que
  no existe; y como RT-035 genera las descripciones desde el efecto, **la ficha de todo no-muerto promete
  al jugador una inmunidad a algo que no puede ocurrir**.

**La muerte**: el jugador pasa a `Dead` y sigue en el roster; su objeto vuelve al inventario (ADR 0048
cond. 4), sus perks no; vale 0 de oro; `RunEndScreen` lo lista con nombre, raza y nivel, **una línea**.
**RF-122 (obituario con estadísticas y memorial) no está implementado — y aunque se implementara mañana, no
hay estadísticas que poner.**

---

## 13. Story Potential

Aplicación cualitativa de la métrica del encargo. «Alto» = un jugador podría querer contarlo.

| sistema | Story Potential | por qué |
|---|---|---|
| Muerte de un jugador | **Alto** | único momento con atribución causal completa; 1,85/run |
| Incomparecencia por plantilla agotada | **Alto** | 4 % de partidos; es la tesis del juego hecha resultado |
| Matasanos | **Alto** | dado tirado a sabiendas, con porcentaje a la vista, que puede matar |
| `blood_oath` / `iron_vigil` | **Alto** | víctima con nombre elegida por el jugador |
| Prórroga de turba | **Alto en potencia, nulo hoy** | 27,6 % de partidos; el motor no cambia ninguna regla |
| Roja | **Medio** | 12 % de partidos; el mecanismo de acumulación existe y no se telegrafía |
| Perks clase A de suceso | **Medio** | pasan cosas, pero el cartel no dice qué hizo el perk |
| Builds | **Medio** | diferenciación medida real (×7,5 en entradas), invisible durante el partido |
| Razas | **Bajo** | abanico 6,4 por decisión de balance |
| Perks clase C (63 % del catálogo) | **Bajo por construcción** | mueven una cuota; §5 |
| Mapa / ruta | **Bajo** | no hay estrategia de ruta describible |
| Objetos | **Bajo** | 30 de 34 sin contrapartida: elegir es ordenar por valor |

---

## 14. Spectator Legibility — el test de los 10-15 segundos

*(INFERIDO a partir de datos MEDIDOS de ritmo; no verificable del todo sin ejecutar y grabar.)*

Un clip de 12 s contiene ~1,5 presentaciones y ~1 voz alta. Lo más probable es **un sello pequeño y nada
más, o nada**.

- **Clip sin momento (~40-45 % de los clips posibles)**: catorce cápsulas numeradas, un balón, un marcador
  de madera. El espectador entiende «es fútbol, son criaturas, van 1-0». **Ni una regla del juego.** Los
  tres pases fallados, la entrada y la parada que hubo no se ven.
- **Clip con gol**: lo mejor que tiene. Punch-in, sacudida, congelado de 2 s y estandarte con nombre
  propio. Comunica identidad, acontecimiento y tono.
- **Clip con muerte (raro)**: punch-in, sangre persistente, bando con lacre **y la causa nombrada**. Es el
  único que comunica de qué va realmente el juego.

**Veredicto: pasa el test solo si el clip lleva gol o muerte.** En el caso medio comunica «autobattler de
fútbol con monstruos y estética de fiesta medieval» y **cero carnicería administrada**.

---

## 15. Cuellos de botella

Los seis que la evidencia sostiene, ordenados por cuánto limitan el potencial narrativo.

**CB-1 · No hay memoria del jugador-criatura.** El motor calcula las estadísticas y la run las tira.
Sin historial no hay personaje, y sin personaje la mitad de las historias posibles (§3, contraejemplo) no
pueden existir. Bloquea RF-122. *(CONFIRMED por tres vías.)*

**CB-2 · No hay atribución causal.** 14 de 26 eventos no se presentan; el cartel de perk dice el nombre y
no el efecto; 0,9 carteles por partido y 65 % de partidos sin ninguno. **Es el riesgo Football Manager**:
una simulación correcta que el jugador no puede interpretar se siente como azar. *(CONFIRMED.)*

**CB-3 · Los perks no cambian conducta.** 63 % del catálogo mueve cuotas y contadores; `Utility.cs` es
ciego a ellos; entre cuota y suceso hay un escalón, no un gradiente. La palanca que lo arregla está
construida y **desconectada del esquema**. *(CONFIRMED.)*

**CB-4 · Lo declarado y lo implementado divergen sistemáticamente.** El `project-state.md` ya había
registrado cinco casos en un día; esta auditoría añade, como mínimo, nueve más: la turba · el portero que
sale (`GoalkeeperLeftArea` imposible, `Rusher` sin sentido) · `numb`/luto · prótesis · vínculos ·
`AERIAL_DUEL` · `MatchLogView` sin conectar · plantillas sin `en` · `BribesReceived` · y los cinco perks de
§5, con `kamikaze` e `iron_price` verificados como **ventaja pura vendida como sacrificio**.
**Esto ya no son incidencias sueltas: es un cuello de botella de proceso**, y merece una pasada sistemática
—como el propio `project-state.md` sugiere— y no quince arreglos. *(CONFIRMED.)*

**CB-5 · La carne no es moneda.** 1 a 5 entre gastada por decisión y sufrida; las cinco vías de gasto con
frecuencia ≈0; `healsRoster` borra el desgaste tres veces por run. *(DERIVADO.)*

**CB-6 · Los momentos existen y no se ven porque no hay foco ni pasos.** 7 de 14 acciones son el mismo
gesto, 7,1 s entre presentaciones, y ningún mecanismo elige qué mirar. *(CONFIRMED.)*

**Lo que NO es cuello de botella, contra la intuición**: la frecuencia de sucesos dramáticos (§7), el
tamaño del catálogo de perks (102 es proporción correcta de roguelite: se ve el 60-80 %, se juega el
15-25 %), y la diferenciación entre builds (medida y real).

---

## 16. Quick Wins

Diez cambios pequeños, con el sistema actual. **Ninguno está ejecutado**; y por las Reglas B/C/D/E de
`CLAUDE.md`, cada uno necesita su skill antes de implementarse (se indica cuál).

| # | cambio | resuelve | coste | skill previa |
|---|---|---|---|---|
| **QW-1** | **Persistir `PlayerMatchStats` en `RunPlayer`** (goles, partidos, entradas, lesiones causadas, muertes causadas) y mostrarlo en la ficha | CB-1 | pequeño: el mecanismo existe para `Counters`; sube versión de guardado | `architecture-review` + Regla E |
| **QW-2** | **Añadir `modifyUtility` al enum de `perks.schema.json`** + soporte de cargador, y publicar **un** perk que lo use (Cazagoles, 24 % ya medido) | CB-3 | pequeño: motor y medición ya pagados | `game-design-review`, `perk-authoring`, Regla E |
| **QW-3** | **El cartel de perk dice qué hizo, no cómo se llama** — y deja de encogerse cuando lo absorbe un momento | CB-2 | pequeño, solo `/Game` | `visual-review` |
| **QW-4** | **Presentar `SAVE`** (y decidir explícitamente sobre `TACKLE`/`RECOVERY`) | CB-2, CB-6 | pequeño, solo `/Game` | `visual-review` |
| **QW-5** | **Conectar `MatchLogView` a `ReportScreen`**: la crónica existe y no se usa | CB-2 | pequeño, solo `/Game` | `visual-review` |
| **QW-6** | **Arreglar `kamikaze`, `iron_price` y `gathering_thirst`**: o el efecto cumple el texto, o el texto deja de prometerlo | CB-4 | pequeño si se corrige el dato; medio si se implementa el coste propio | `game-design-review` |
| **QW-7** | **Resolver `numb`/RF-104**: implementar el luto o retirarlo del efecto, porque RT-035 lo está prometiendo en la ficha de todo no-muerto | CB-4 | pequeño | `game-design-review` |
| **QW-8** | **BC-F: dejar de renumerar los dorsales al sustituir** — es el único identificador en campo | CB-2 | pequeño | ya fichada |
| **QW-9** | **`no_dying`: `per: match` → run**, o arreglar `LimitScope.Run`, que nunca se reinicia | CB-4 | pequeño | `game-design-review` |
| **QW-10** | **Semilla de run compartible y visible** — el determinismo ya está garantizado (RT-013/024); convierte una anécdota en algo reproducible por otro | §3, patrón Balatro | pequeño | `architecture-review` |

**QW-1 y QW-2 son los dos que recomiendo hacer primero, y en ese orden.** QW-1 porque desbloquea una clase
entera de historias con código que ya existe; QW-2 porque el coste de motor y de medición está pagado y hoy
está tirado.

---

## 17. Cambios medianos

Amplían un sistema existente. Cada uno con el riesgo de balance explícito, porque el presupuesto está
ajustado: *(MEDIDO, propio)* `injuriesPerMatch` **0,81 con techo 0,90** y `tacklesPerMatch` 12,01 con techo
14. **Ninguna propuesta que añada violencia cabe sin recalibrar.**

**CM-1 · La turba, de verdad.** Implementar lo que `docs/simulacion.md` ya decidió: campo estrechado,
casillas de público fijas y anunciadas, +15 % de velocidad, sin criterio de árbitro. *Problema:* CB-4 y
CB-6. *Comportamiento esperado:* el 27,6 % de los partidos tiene un tercer acto con reglas distintas y
anunciadas. *Riesgo:* alto en balance — toca `injuriesPerMatch`, que no tiene margen; requiere `Pitch`
parametrizable (`Pitch.Rows` es `const`). *Potencial de historia:* el más alto del documento por línea de
código. *Dependencia:* T3.

**CM-2 · Degradación escalonada acumulativa** (patrón Blood Bowl, §3-C). Cada lesión no tratada sube la
probabilidad de la siguiente **y se ve en la ficha**. *Problema:* CB-5 y la aceptabilidad de la muerte.
*Por qué encaja:* refuerza la condición 3 de la ADR 0048 (se puede reducir el riesgo) y hace que la muerte
llegue precedida de avisos que el jugador ignoró — es **más** previsible, no menos, así que refuerza
RF-012d en vez de tensionarla. *Riesgo:* directo sobre `deathsPerRun`, que está en banda; se mide junto con
CM-3 o no se mide.

**CM-3 · La prótesis como lo que deja el matasanos.** RF-095 ya está escrito y el `record` existe.
*Problema:* CB-4 y CB-1 — es lo que convierte «sobreviví a la clínica» en algo que queda en el cuerpo.
*Riesgo:* toca la economía de la clínica, que las ADR 0099-0100 acaban de rehacer y lleva dos lotes en
banda.

**CM-4 · Nombrar los pasos del partido** (patrón Balatro/Super Auto Pets, §3-B). Elegir explícitamente qué
sucesos son «los pasos», darles nombre, peso y aire, y reducir el resto. *Problema:* CB-6. *Riesgo:* solo
de presentación, pero cambia la sensación del producto entero; es decisión de dirección de UI.

**CM-5 · Nemesis persistente** (T5): estado relacional `(A,B,tipo,intensidad)` en `RunState`, sembrado al
arrancar el partido. *Problema:* la función 3 que falta en §5 y CB-1. *Nota:* es el hueco que dejan los
vínculos al retirarse, resuelto por la vía que el juego **sí** sabe producir —violencia— en vez de por
amistad. *Riesgo:* medio; toca `/Sim` y el guardado.

**CM-6 · Subir el peso del nodo de evento en `RunPolicy.ChooseNode`** y/o dar a las cartas efecto diferido
sobre el partido siguiente (familia 4 de la ADR 0100, ya diseñada y fuera de alcance). *Problema:* §9 —
0,38 cartas por run. *Riesgo:* bajo en motor, medio en economía.

---

## 18. Grandes hipótesis de diseño

No son tareas. Son direcciones, cada una con la evidencia de este documento que la justifica.

### Hipótesis A — Los perks deben cambiar la intención, no la cuota

*Evidencia:* 63 % del catálogo mueve cuotas; `Utility.cs` es ciego a ellas; las primitivas de suceso y de
objetivo dan clase A en 16 de 16 casos y las de cuota casi nunca; `modifyUtility` está construido, medido y
desconectado; el catálogo conceptual estima que **desbloquea 42 de 76** candidatos de diseño.
*Lo que afirma:* que el escalón entre «sube un porcentaje» y «hace que pase algo» es la diferencia entre un
catálogo de 102 sumas y un catálogo de 102 fantasías.
*Lo que hay que vigilar:* es **un recalibrado, no un parche** — un ×1,6 a `Tackle` cambia cuántas entradas
hay, y las entradas son la fuente de `injuriesPerMatch`, que no tiene margen. Requiere techos por acción y
por puesto en el cargador (C10) para que un ×3 a `Shoot` en un portero sea un error de datos y no una
anécdota.

### Hipótesis B — La criatura tiene que ser un personaje, no un vector

*Evidencia:* el generador de nombres es excelente y detrás no hay nada; no se persiste ni un hecho; RF-122
es imposible aunque se implemente; los cuatro contraejemplos de §3 (FTL, Darkest Dungeon, Blood Bowl, Dwarf
Fortress) producen historias sin sinergias con exactamente la receta que aquí falta.
*Lo que afirma:* que **este es un eje independiente del de las builds**, más barato, y probablemente el que
más se parece a la identidad declarada del juego («carnicería administrada» es una frase sobre cuerpos con
nombre, no sobre porcentajes).
*Piezas:* QW-1 (historial) → CM-3 (prótesis) → CM-5 (nemesis) → obituario RF-122.

### Hipótesis C — El partido debe estar hecho de pasos, no de flujo

*Evidencia:* 7 de 14 acciones son el mismo gesto; 14 de 26 eventos no se presentan; 7,1 s entre
presentaciones; el test de los 10 s solo lo pasan el gol y la muerte; y el único modelo demostrado para
hacer legible una simulación sin control es la resolución secuencial con acumulado visible.
*Lo que afirma:* que la legibilidad se compra **reduciendo y nombrando** eventos, no acelerándolos, y que
la alineación debe ser la build expression principal porque es el único verbo que el jugador ejerce sobre
el partido.
*Tensión declarada:* choca con RT-020 (ticks fijos) solo en apariencia — es presentación, y la ADR 0119 ya
estableció que el ritmo vive en `/Game` y los momentos son una vista pura de `/Sim`.

### Hipótesis D — El determinismo es una característica de producto, no solo de ingeniería

*Evidencia:* RT-013/RT-021/RT-024 garantizan reproducción exacta; Mechabellum tiene replay determinista y
su comunidad lleva años pidiendo línea de tiempo clicable y rebobinado, que es justo lo que aquí se puede
dar; Balatro convirtió la semilla compartible en el motor de su comunidad.
*Lo que afirma:* que Underleague puede ofrecer «mira mi partida, aquí está la semilla» — anécdota
convertida en experimento verificable — con un coste casi nulo comparado con cualquier mecánica nueva.

### Hipótesis E — La carne tiene que poder gastarse, no solo perderse

*Evidencia:* relación 1 a 5; las cinco vías de gasto con frecuencia ≈0; la política que no compra tiene la
mitad de muertes; `healsRoster` borra el desgaste tres veces por run.
*Lo que afirma:* que mientras la carne sea consecuencia de construir y no **alternativa** a construir, el
recurso central declarado del juego no es una decisión.
*Requisito previo:* medir las cinco tarifas, que los propios ficheros piden por escrito.

---

## 19. Roadmap sugerido por impacto y dependencia

**Tanda I — desbloquear lo que ya está pagado** (sin motor nuevo)
QW-1 (historial) · QW-2 (`modifyUtility` en el esquema + un perk) · QW-5 (crónica) · QW-8 (dorsales).
*Criterio de salida:* la ficha de un jugador cuenta algo que no estaba en su ficha al empezar la run, y
existe al menos un perk en `/data` que cambia una decisión del motor.

**Tanda II — cerrar la divergencia declarado/implementado** (CB-4, pasada sistemática)
QW-6, QW-7, QW-9, más un barrido de los casos de §4 y §15. *Criterio de salida:* ningún texto visible por
el jugador promete un efecto que el motor no produce. **Esta tanda no añade nada al juego y es la que más
protege la confianza del jugador** (riesgo Football Manager).

**Tanda III — atribución** (CB-2)
QW-3, QW-4, CM-4. *Criterio de salida:* el test de los 10 s se pasa sin gol y sin muerte.

**Tanda IV — la carne y la muerte** (CB-5, CM-2 + CM-3 juntos, nunca por separado)
Requiere antes medir las cinco tarifas y resolver la pregunta 1 de §20.

**Tanda V — la turba** (CM-1)
Deliberadamente al final pese a su alto potencial: es la que más presupuesto de balance consume y la única
que necesita `Pitch` parametrizable.

*Nota de proceso:* toda tanda que toque `/Sim`, `/data` o una ADR necesita `independent-reviewer` antes de
cerrarse (Regla E), y las que tocan comportamiento cuantificable necesitan `balance-measure` con línea base
real (Regla D).

---

## 20. Preguntas abiertas — decisiones que no son mías

1. **¿El desgaste es un recurso de run o de acto?** `CLAUDE.md` dice run; `economy.json` implementa acto
   (`healsRoster` en el jefe, deliberado y documentado). Las dos no pueden ser verdad. De la respuesta
   dependen CM-2, CM-3 y la hipótesis E. **Es la pregunta más importante del documento.**
2. **¿Se retiran formalmente los vínculos (RF-100..106)?** `decision-vinculos.md` ya lo recomendó el 15 sep
   y no se ha ejecutado. Mientras tanto, `CLAUDE.md` sigue declarándolos identidad del juego y `numb`
   sigue prometiendo inmunidad a un sistema inexistente. Si se retiran, CM-5 (nemesis) es el sustituto
   natural.
3. **¿Cuál es el presupuesto de `injuriesPerMatch`?** Está en 0,81 con techo 0,90. Varias propuestas
   valiosas (turba real, degradación acumulativa, perks conductuales de violencia) compiten por esos 0,09.
   O se sube la banda con una ADR, o se decide qué propuesta se lo queda.
4. **¿`kamikaze` e `iron_price` deben penalizar de verdad?** Si sí, hace falta una capacidad de motor que
   hoy no existe (que el que entra se lesione a sí mismo) y eso es C4 real. Si no, hay que reescribir la
   intención declarada y revisar PD-1, que se apoya en ellos.
5. **¿Cuántas cartas de evento debe ver una run?** Hoy 0,38. Subirlo significa competir con el mercado, y
   el mercado está medido como la decisión que más gana. Es un intercambio explícito, no un ajuste.

---

## Anexos y trabajo derivado

- Informes completos de las seis auditorías y la medición propia: fuera del repositorio, en el scratchpad
  de la sesión (`ref-externas.md`, `perks-clasificacion.md` con las 102 filas, `sim-emergencia.md`,
  `legibilidad.md`, `eventos-mapa-economia.md`, `identidad-jugadores.md`, `medicion-sucesos.md`).
- **Hallazgo de código sin fichar, detectado de paso** y anotado en `docs/pendientes/BE-A.md`:
  `Sim/Engine/Utility.cs:1437-1443` documenta «Defensa **y centrocampista**» y el código dice
  `role is Position.Defender`.
- Datos obsoletos encontrados en documentos vigentes: el 0 % de activación de `steamroller` (el perk se
  rehízo en BB-Q y no se ha vuelto a medir) y el AVISO de `first_touch_school` en su `_doc` (el dato actual
  sí declara `requiresPerks`/`blocksPerks`).
