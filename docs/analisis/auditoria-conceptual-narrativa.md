# Auditoría conceptual: qué narrativa puede emerger de los sistemas que ya existen

**Fecha:** 20 sep 2026. **Tipo:** revisión crítica de concepto y arquitectura, a petición del revisor.
**Estado:** diagnóstico y propuesta. **No se ha cambiado ni una línea del juego.**

Continúa `docs/analisis/auditoria-identidad-generador-de-historias.md` y la **ADR 0122**. Etiquetas de
evidencia: **MEDIDO** · **LEÍDO** (verificado en código o dato) · **DERIVADO** · **INFERIDO**, con estado
**CONFIRMED / LIKELY / SIN EVIDENCIA** para las conclusiones.

---

## 0. Tres correcciones, antes de responder nada

Las tres cambian la respuesta, así que van primero.

### 0.1 Los rivales NO se descartan. Ya reaparecen, y el juego ya guarda contra quién jugaste

El encargo parte de que el sistema funciona como `generar rival → jugar → descartar rival`, y que por eso
no se puede construir una narrativa de rivalidades. **La primera mitad es cierta; la conclusión no.**

*(CONFIRMED, verificado en código.)*

- El **emparejamiento nodo→rival ya está persistido**: `RunSave.cs:280` escribe `opponentId` por nodo,
  `:472` lo lee, y `run-save.schema.json:198` lo exige. Los **tres mapas se generan al tick cero**
  (`RunEngine.cs:106-111`) y no se descartan nunca. *El guardado ya contiene la tabla completa de con quién
  te vas a enfrentar en toda la run.*
- El **resultado ya está persistido**: `NodeHistoryEntry(NodeId, Kind, Result)`, y `RunState.FindNode`
  (`:687-700`) es el puente que une nodo → rival.
- **Los rivales ya se repiten, por construcción**: `MapGenerator.cs:597` reparte
  `opponents[opponentCursor % opponents.Count]` — cíclico, con reposición, **sin deduplicación**.
- Y se repiten **obligatoriamente**: `nodesPerAct [11,12,12]` da **6+7+7 = 20 partidos** (17 contra
  `data/rivals/` y 3 jefes), y hay **5 rivales por acto**. Con 6 partidos de liga por acto contra 5
  rivales, en los actos 2 y 3 la repetición está **garantizada por palomar, en cualquier semilla**.

**Conclusión: el juego ya tiene rivalidades. Lo que no tiene es memoria de ellas.** Hoy el jugador ve dos
veces el mismo escudo y el juego se comporta como si fuera la primera — lo cual, de paso, roza RF-012d.

**Y la identidad del jugador rival también existe ya, implícita y estable** *(CONFIRMED)*:
`RivalTeamBuilder.cs:37` asigna `OpponentFirstPlayerId + i`, donde `i` es el índice en la lista del fichero
JSON. **«Thrainn» = (fichero de rival, índice 3), siempre, en toda run y en toda semilla.** No hay que
inventar identidad: hay que dejar de tirarla.

**Lo único que falta es una línea que hoy dice lo contrario**: `MatchResolution.cs:96`,
`if (matchEvent.Team != 0 ...) continue;` — las lesiones y muertes del rival se ven pasar y se tiran.

### 0.2 Las cifras de perks del encargo están desactualizadas, y la conclusión cambia

El encargo cita «61 perks, 62,7 % `modifyProbability`, 8 de 83 efectos entrando en utility». Eso era el
catálogo de la era de 61 perks. Hoy *(MEDIDO/LEÍDO, 20 sep 2026)*:

| | entonces | hoy |
|---|---|---|
| perks | 61 | **102** |
| efectos | 83 | **128** |
| `modifyProbability` | 62,7 % | **44,5 %** (57 de 128) |
| perks con comportamiento reconocible | «unos pocos» | **30 de 102 (clase A)** |

**El catálogo está bastante mejor de lo que el encargo supone**, y por una causa identificable: las
primitivas del paquete AY y C4/C5/C7/C8 (`modifyMarkBias`, `shiftHome`, `modifyZoneShape`, `relocate`,
`setState`, `cancelEvent`, `extraAction`, `injure`) son solo 27 de 128 efectos (21 %) pero **sostienen 24
de las 30 clase A**. Varios de los ejemplos conceptuales del encargo **ya existen**: `bodyguard` (defensa
que se coloca sobre quien se acerca a su compañero), `sweeper_keeper` (portero que sale), `blood_scent`
(va derecho al rival derribado), `bloodhound` (se engancha a un rival concreto), `last_man`, `deep_run`.

**Implicación de diseño: el problema no es que falten perks de comportamiento. Es que no se comunican.**

### 0.3 Los vampiros: recomiendo NO añadir una sexta raza

*(Crítica pedida explícitamente.)* La **ADR 0092** cerró el abanico racial **a propósito**, de 21,8 a
**6,4** puntos de tasa de victoria, con sesgos de suma cero y sin sesgo racial en técnica ni velocidad.
Midió incluso que «un enano con TODOS los datos del humano mide 47,6 contra 46,8». **Hoy la raza es
identidad visual + una habilidad de entre +0,4 y +2,2 + dos perks exclusivos.**

Añadir una raza cuesta: arte y siluetas nuevas, un generador de nombres, dos perks raciales, una habilidad
racial, y **una recalibración del abanico de cinco a seis**. Y compra, mecánicamente, casi nada.

**Los vampiros deberían ser un CLAN, no una raza** — bajo `Undead`, con su heráldica, su plantilla, su
estilo de juego y su chiste burocrático propio («sancionado por morder: la sangre no figura en el
reglamento»). Eso cuesta **un fichero de datos** y da el 100 % de la identidad narrativa que buscas. La
estructura del §3 lo soporta sin contradicción: *la raza es la silueta, el clan es el personaje.*

---

## A. Premisa

**Funciona, y es mejor de lo que parece, porque es load-bearing en vez de decorativa.** La premisa explica
de golpe tres cosas que el juego **ya hace** y que hoy no tienen justificación diegética: por qué las
lesiones son legales, por qué hay un árbitro con criterio sesgado, y por qué un partido decide algo que no
es un partido.

**Qué conviene definir con precisión** (porque ya son mecánicas, y definirlas es escribirles la voz):

1. **El reglamento: qué es legal.** Es el chiste central y ya está implementado — `foulChanceBonus`,
   el criterio del árbitro, las tarjetas. Falta *enunciarlo* en registro administrativo.
2. **El criterio del árbitro.** Ya existe como sesgo acumulable (`_bias`, `ShiftBiasAgainst`), y ya señala
   el 80 % de las faltas (ADR 0090). Es decir: **el árbitro ya es falible y parcial, sistémicamente.**
3. **Las sanciones y la clínica.** El matasanos ya cobra el 40 % y puede matarte con el porcentaje a la
   vista (ADR 0099). Eso ya es humor negro; solo le falta la voz.
4. **Qué se juega un partido.** Hoy el mapa no lo dice. Es la pieza barata que más premisa compra.

**Qué conviene dejar deliberadamente ambiguo:**

- **Por qué se eligió el fútbol.** El encargo ya lo intuye y tiene razón: explicarlo mata el chiste. Que
  nadie lo recuerde *es* la broma, y además es la coartada para cualquier regla absurda futura.
- **La antigüedad exacta y la cosmología.** Basta con precedentes citados de pasada.
- **Si la Underleague es justa.** Debe sugerirse que no, sin confirmarlo nunca.

**Cómo reforzar el humor negro: ver §I.** En corto: no escribiendo chistes, sino poniendo voz de
reglamento a cosas que el motor ya hace.

---

## B. Mundo: RAZA → CLAN → EQUIPO → JUGADOR → UNDERLEAGUE

**La estructura que pides ya existe en los datos, pero aplastada.** *(LEÍDO)*

- `data/races/*.json` — 5 razas. **= RAZA.** Correcto tal cual.
- `data/clubs/*.json` — 5, uno por raza. **= el CLAN del jugador.** Correcto.
- `data/rivals/*.json` — 15, nombrados `act<N>_<raza>_<nombre>`. **Aquí está el aplastamiento**: lo que
  debería ser un CLAN está indexado por acto, así que *«Bastión de Granito» (acto 1) y «Muro de Escudos»
  (acto 2) son clanes distintos que resultan ser ambos enanos.*

**La contradicción concreta, y es la que impide tu frase objetivo.** «Joder, los Barbahierro otra vez» al
final de la run **es imposible hoy**, no porque el rival no reaparezca —reaparece—, sino porque **un clan
no puede cruzar de acto**: el catálogo se consulta por acto (`RivalCatalog.OfAct`).

**La intervención mínima, y es solo dato:** añadir un campo **`clanId`** al esquema de rival, y hacer que
los tres ficheros enanos (por ejemplo) compartan `clanId: "ironbeard"` con plantillas distintas. Entonces:

- La rivalidad se agrupa por `clanId`, no por id de fichero.
- El mismo clan puede seguirte de acto en acto **haciéndose más fuerte**, que es exactamente la progresión
  que la fantasía pide.
- **Cuesta 15 ficheros y un campo de esquema. Cero cambios de motor, cero de guardado.**

Y hay un regalo ya en el código *(CONFIRMED)*: **«élite» no es una categoría de rival, es un modificador
de nodo** — el mismo rival con **+2 niveles** (`StandardRunSystems.cs:138-140`). *El escalón del segundo
encuentro ya está construido:* la segunda vez que te cruzas con los Barbahierro pueden ser la versión
élite. «Volvieron más cabrones» es hoy un cambio de cero líneas.

**Reparto de responsabilidades, sin solapamiento:**

| capa | qué aporta | dónde vive hoy |
|---|---|---|
| **Underleague** | reglamento, árbitro, sanciones, la voz institucional | `/Sim` (árbitro), `/Game` (pregón) |
| **RAZA** | silueta, animación, sesgo de atributos (suma cero), habilidad racial, 2 perks | `data/races/` |
| **CLAN** | nombre, heráldica, plantilla, **estilo de juego**, historial, rivalidades | `data/rivals/` + `clanId` |
| **JUGADOR** | nombre propio, rasgos (= intención), perks, cuerpo | generado o `data/rivals/` |

Sin contradicciones: la raza no decide comportamiento (lo decide el rasgo), y el clan no decide estética
(la decide la raza).

---

## C. Persistencia: qué debe sobrevivir

**Principio propuesto: persiste el HECHO, no la ENTIDAD.**

No hace falta guardar equipos rivales ni sus jugadores. Hace falta guardar **qué pasó entre dos identidades
que ya son estables y deterministas**. Es más barato, no rompe el determinismo y evita el guardado
gigante.

| qué | ¿persiste hoy? | ¿debería? |
|---|---|---|
| emparejamiento nodo → rival | **sí ya** (`opponentId` en el guardado) | sí, no tocar |
| resultado del partido | **sí ya** (`NodeHistoryEntry.Result`) | sí, no tocar |
| **marcador** | no (`RunMatchSummary` es valor de retorno) | sí — vía contador libre |
| **quién lesionó a quién** | **no** (`MatchResolution.cs:96` lo descarta) | **sí — es el núcleo** |
| plantilla del rival | no | **no.** Se reconstruye de `/data`: el rival se cura entre encuentros, que es justo lo que la historia necesita («Thrainn regresa») |
| lesiones del rival entre partidos | no | **no.** Ver arriba |
| estadísticas del rival | no | no, salvo las que entren en un hecho |

**El mínimo modelo de persistencia para producir rivalidades** *(CONFIRMED por inspección; falta un test
que lo cierre)*:

1. **Memoria de enfrentamiento: CERO cambios de estado y CERO de esquema.** Una clase nueva sin estado,
   `RivalHistory`, función pura sobre `RunState`: recorrer `NodeHistory` (ya cronológico), filtrar partidos
   **excluyendo `Boss`**, resolver `FindNode(entry.NodeId)`, quedarse con `OpponentId`. Devuelve los
   encuentros previos contra un clan y su resultado. **Retrocompatible con guardados ya escritos, porque el
   dato ya estaba ahí.** Precedente idéntico ya en uso: `RunEngine.WonAt` (`:563-568`).
2. **Memoria de hechos: sin subir versión de esquema.** La convención de **contadores de clave libre** ya
   existe y el esquema la bendice literalmente (*«Clave libre para no subir de versión cada vez que entra un
   sistema»*, `run-save.schema.json:125-128`), y `counters` **no está en `required`**, así que un guardado
   antiguo sin la clave tampoco falla. Punto exacto de escritura: `MatchResolution.cs:197-199`, donde ya
   está el `WithNodeCompleted` y donde diez líneas más abajo ya se llama a `WithCounter`, con
   `node.OpponentId` en ámbito.

**Lo que NO hay que tocar: `MapGenerator`.** *Recordar y reaparecer son separables.* Reaparecer cuesta un
rediseño de mapa que consume el flujo `RngStreams.Map` de otra forma e invalida toda la referencia de
balance. **Y hoy ya reaparecen. Así que recordar es lo único que falta.**

---

## D. Rivales recurrentes

**Propuesta: no añadir un sistema de «rival importante». Derivarlo de los hechos.**

Un clan no es importante porque un campo lo diga. Es importante **cuando se ha cruzado un umbral de
hechos** que el juego ya puede leer:

- te ha ganado (ya persistido);
- le has ganado dos veces (ya persistido);
- tu jugador lesionó a uno suyo, o al revés (el hecho que hay que empezar a escribir);
- te mató a alguien (el hecho de más peso).

**Cuántos:** no hay que decidirlo — sale solo. Hay 5 clanes por acto y 17 partidos de liga, así que el
jugador se cruza con cada clan 1-2 veces por acto. La mayoría no acumulará nada. **Uno o dos acumularán
hechos, y esos son los memorables.** La selectividad es emergente, que es lo que pides.

**Cuándo reaparecen:** ya lo hacen. Con `clanId` (§B), además pueden cruzar de acto.

**Qué recuerdan:** el clan no necesita «recordar» nada como agente. Basta con que **el juego se lo recuerde
al jugador** en dos sitios, que es donde vive la rivalidad:

1. **Antes del partido**, en el nodo: *«Bastión de Granito — segunda vez. Perdiste 1-2. Tu Grok
   Rompehuesos lesionó a su Borin Yunquefirme.»*
2. **Durante el partido**, nombrando a los jugadores rivales cuando hacen algo (hoy los sellos son
   anónimos, y el rival no tiene ni tiras de identificación).

**Cómo reconocerlos inmediatamente:** heráldica + nombre + **firma de comportamiento** (§J). Los tres son
dato, no motor.

**Una ampliación opcional y barata**: si el clan que ya te ganó reaparece como nodo **élite** (+2 niveles,
mecanismo existente), la escalada narrativa sale gratis. Requiere sesgar la asignación de élite hacia un
clan con historial — y eso **sí** toca `MapGenerator`, así que va al final del roadmap, no al principio.

---

## E. Personalidad conductual

**Respuesta corta: la capa de intención ya existe, partida en dos mitades, y ninguna está al alcance de
las decisiones del jugador.** *(CONFIRMED)*

La fórmula de decisión (`Utility.cs:163-165`) es:

```
score = Base(rol, acción) × Táctica/100 × TraitMult/100 + Contexto
TraitMult = ActionMultiplier(acción) × (100 + LeaderBonus + PerkActionBonusPercent(acción)) / 100
```

- **`ActionMultiplier` viene de los RASGOS, y ya es exactamente un perfil de intención.** `Coward` lleva
  `{Tackle: 45, Dribble: 70, ChaseBall: 85, Block: 35}`; `Aggressive`, `{Tackle: 160, MarkOpponent: 120,
  Block: 175}`; `Lazy`, `{Retreat: 50, MarkOpponent: 70, OfferSupport: 80}`. **Tu «defensa agresivo» y tu
  «defensa cobarde» NO juegan igual hoy.** Pero el array es `readonly`, se rellena una vez al construir el
  jugador, **ningún perk puede tocarlo**, los rasgos **se sortean** al generar, y solo hay diez.
- **`PerkActionBonusPercent` es el canal mutable por acción y por tick**, con condición de zona ya cableada
  y recalculada cada tick. Es C1/`modifyUtility`. **Y sigue sin estar en el enum del esquema**, así que
  ningún dato puede usarlo.

**La intervención mínima es la que ya está acordada en la ADR 0122 (Fase 1B): abrir C1 al esquema.** Con
eso solo, dos de tus tres ejemplos son escribibles hoy:

| perfil | qué necesita | ¿alcanzable? |
|---|---|---|
| **Carnicero** (perseguir rival > recuperar > mantener) | `modifyUtility(Tackle, +X)` + `modifyUtility(MarkOpponent, +X)` | **sí, con C1 abierto** |
| **Cobarde** (mantener > evitar duelo > recuperar) | multiplicadores **negativos** en `Tackle`/`Block` | **ya existe como RASGO** (`Coward`). Como perk choca con la ADR 0088 |
| **Guardaespaldas** (proteger compañero > recuperar) | un predicado situacional: «compañero en peligro» | **no.** Necesita una cláusula de C2. Aproximable hoy con `bodyguard` (geometría) |

**Y de ahí sale una regla de diseño que resuelve el choque con la ADR 0088 sin tocarla:**

> **Los RASGOS describen quién es un jugador y pueden restar. Los PERKS describen qué ha aprendido y solo
> suman.**

La ADR 0088 («ningún perk es negativo») se refiere a los perks, que son **recompensa**. Un rasgo no es una
recompensa: es parte del cuerpo que te tocó, y que reste es justo lo que lo hace un personaje. **El canal
para «cobarde», «vago» o «perezoso» ya existe y es el correcto.**

**Tres intervenciones mínimas, por coste ascendente:**

1. **Cero motor:** añadir rasgos nuevos a `data/traits/traits.json` para los arquetipos que faltan
   (protector, obsesivo, insistente). Es un objeto JSON con `actionMultipliers`. **Es el cambio con mejor
   relación identidad/coste de todo este documento.**
2. **Lo ya acordado:** abrir `modifyUtility` (Fase 1B de la ADR 0122).
3. **Una sola cláusula de C2:** el vocabulario situacional completo son seis predicados; **empezar por
   uno**. Recomiendo «con o sin balón» o «compañero adyacente derribado», no los seis.

**Lo que hace falta además, y es perceptivo, no mecánico:** que el jugador **vea el rasgo**. Hoy la
diferencia existe en la simulación y no se comunica (§0.2, §G).

---

## F. Perks

**No reducir el catálogo. Confirmado por dos vías**: 102 perks es la proporción correcta de roguelite (se
ve el 60-80 % de la oferta por run y se juega el 15-25 %), y el reparto ya es 30 clase A / 28 clase B /
36 clase C / 8 clase D. *El problema no es el tamaño.*

**La taxonomía que pides, mapeada sobre lo que existe:**

| tu clase | qué es | cuántos hoy | qué hacer |
|---|---|---|---|
| **A. resultado** | cambia el desenlace de un acto concreto (anula gol, salva de morir) | ~11 | conservar; son los legendarios |
| **B. probabilidad** | mueve una cuota | **57 efectos** | **conservar**: son el tejido conectivo y el balance. No tocar |
| **C. comportamiento** | cambia qué intenta hacer el jugador | **0 vía perk** (C1 cerrado) | **abrir C1.** Es la clase que falta entera |
| **D. situaciones reconocibles** | produce un suceso que se ve | ~19 | comunicarlos (§G) |
| **E. historias** | deja consecuencia recordable | ~4 letales | la memoria de §C los multiplica sin tocarlos |

**La regla que sale del cruce primitiva × clase** *(DERIVADO, del análisis de los 102)*:

> Las primitivas que producen un **suceso** o eligen un **objetivo** dan comportamiento reconocible en
> **16 de 16** casos. Las que mueven una **cuota** solo lo dan cuando la consecuencia es irreversible.
> **Entre «subir un porcentaje» y «hacer que pase algo» no hay gradiente: hay un escalón.**

**Propuesta de reorganización, sin borrar nada:** partir el catálogo en **dos pools con reglas de posesión
distintas** (es la propuesta de `perks-design-bible.md` §2.3, que este análisis respalda):

- **Identidad** — perks de intención (clase C, los nuevos de C1). **Máximo UNO por jugador.**
- **Soporte** — todo lo demás. Libre, hasta llenar slots.

El tope de uno crea la decisión que hoy no existe: *«¿a quién le doy carácter?»* en vez de *«¿quién tiene
un slot libre?»*. Y garantiza que un jugador tenga **una** personalidad legible, no dos diluidas.

**Proporción objetivo sugerida:** no más de 25-30 perks de identidad, para que una run (7 titulares = 7
identidades) siga sorprendiendo a la tercera partida.

---

## G. Eventos y continuidad

**La unidad `PROTAGONISTA → ACCIÓN → CONSECUENCIA → MEMORIA` es correcta, y hoy se rompe en el tercer
eslabón, no en el primero.** El motor produce el protagonista y la acción; la consecuencia se calcula; **la
memoria se tira** (`MatchResolution.cs:96`).

**Qué merece presentarse, y esto es una decisión de presupuesto, no de gusto.** *(MEDIDO)* Hoy hay **4,8
presentaciones por minuto**, 7,1 s entre presentaciones y 12,7 s entre voces altas, y **14 de los 26 tipos
de evento no llegan nunca al jugador**. Tu hipótesis de **1-3 momentos memorables por partido es
compatible con ese presupuesto** y la respaldo.

**Regla propuesta para evitar el spam:** un suceso merece presentarse si cumple **al menos una** de estas
tres, y solo entonces:

1. **Tiene protagonista nombrado** (gol, lesión, muerte, roja, parada decisiva).
2. **Tiene causa nombrable** (un perk lo produjo, y se puede decir cuál).
3. **Conecta con un hecho anterior** (este jugador ya lesionó a este rival; este clan ya te ganó).

La tercera es la que no existe hoy y la que convierte tres sucesos sueltos en una historia. **Y es la más
barata, porque el dato ya está guardado.**

**Cómo conectar los acontecimientos, sin un sistema nuevo:** tres puntos de presentación, ninguno un popup.

1. **Cartel del nodo, antes del partido**: quién es, cuántas veces, qué pasó. *(Lee `RivalHistory`.)*
2. **Durante el partido**: nombrar al rival cuando hace algo, y decir *qué hizo* el perk, no solo su
   nombre.
3. **Crónica de después**: `MatchLogView` **ya produce el dato estructurado** (minuto, bando, nombres,
   marcador) **y no se usa en `ReportScreen`**.

**No hacen falta eventos nuevos.** De hecho la recomendación es la contraria: las cartas de evento están a
0,38 por run porque la política las esquiva, y subirlas compite con el mercado. La continuidad no vive en
las cartas: vive en el partido y en el nodo.

---

## H. Duración de los partidos

**No tocar la duración. Ya está medido y el problema es otro.** *(MEDIDO, 500 partidos, semilla 1.)*

El **67,8 %** de los partidos ya contiene un suceso excepcional: lesión 53 %, prórroga de turba 27,6 %,
roja 12 %, incomparecencia 4 %. `goalsPerMatch` 2,91.

**Las métricas que hay que medir antes de tocar la duración** (y las tres primeras ya están):

1. Sucesos por partido ✅ · 2. Marcadores ✅ · 3. Ritmo de presentación ✅
4. **Cuántos de esos sucesos llegan al jugador** — no medido, y es *la* pregunta.
5. **Cuántos tienen protagonista identificable en pantalla** — hoy el dorsal es el único identificador.
6. **Cuántos tienen causa atribuible** — hoy: 0,9 carteles de perk por partido, 65 % de partidos sin
   ninguno.

*Si el 68 % de los partidos ya tiene material y el jugador no lo recuerda, alargar el partido solo produce
más material que tampoco recordará.*

---

## I. Humor negro, sistémico

**Tesis: no hay que escribir el humor. Hay que ponerle voz de reglamento a lo que el motor ya hace.** El
motor ya produce la premisa entera sin saberlo:

| lo que el motor ya hace | el chiste, ya escrito por el sistema |
|---|---|
| el árbitro señala el 80 % de las faltas (ADR 0090) | el reglamento se aplica *aproximadamente* |
| el árbitro tiene un sesgo que se acumula | el criterio es parcial y **contable** |
| en la prórroga el árbitro **se va** | la institución se lava las manos en el momento decisivo |
| el matasanos cobra el 40 % y puede matarte, con el % a la vista | la sanidad es una apuesta con tarifa |
| ganar al jefe cura a toda la plantilla | la victoria tiene efectos médicos documentados |
| se pierde por incomparecencia bajo 5 jugadores (4 % de partidos) | hay un mínimo administrativo de supervivientes |

**La regla de tono, operativa:** *el mundo nunca comenta la brutalidad; la tramita.* El humor sale del
registro, no del contenido. El juego **ya lo hace bien una vez**: el bando de muerte —«…el fallecimiento de
X, delantero, al minuto 71; rematado por *Sed de médula*»— es correcto de tono y es el momento más contable
que tiene.

**Dónde meterlo, por coste:** los textos de sanción y clínica (ya existen, solo cambia el registro) · el
nombre de las faltas · el cartel del nodo · el obituario. **Cero sistemas nuevos.**

**Riesgo a vigilar, y es real:** el encargo pide no hacer una parodia constante, y la dirección visual
vigente (`docs/ui/README.md`, pregón y heráldica) ya está bien calibrada. **El humor debe vivir en el
texto institucional, nunca en la voz del narrador de los goles.** Si el pregonero hace chistes, se pierde
el peso.

---

## J. Clanes reconocibles sin lore

**Tres palancas, ninguna es texto largo:**

1. **Heráldica y nombre.** Ya hay dirección visual y generador de nombres por raza.
2. **Firma de comportamiento — y esta es la buena.** Cada fichero de rival **ya trae sus propios perks y
   rasgos**, así que **un clan ya juega de forma reconocible**: el «Bastión de Granito» ya lleva
   `iron_gate` y rasgos `Wall`, y su propia descripción dice «un muro de enanos que no se mueve». Lo que
   falta no es diseñarlo: es **que el jugador pueda notarlo**, que es el mismo problema de §G. Con §E
   (rasgos visibles) y C1 abierto, un clan puede tener una **doctrina** legible en el campo.
3. **Los hechos acumulados.** «Los Barbahierro son unos pesados» no se escribe: se gana perdiendo dos
   veces contra ellos.

**Presupuesto de texto por clan: un nombre, una línea de descripción y un lema.** Ya existen los dos
primeros en los 15 ficheros.

---

## K. Roadmap

**Encaja dentro de la ADR 0122 sin contradecirla, porque es la misma tesis —memoria— aplicada al otro lado
del campo.** Propongo añadir la memoria de rival como **Fase 1C**, y advierto de algo importante:
**es más barata que la 1A y no depende de ella.**

### Fase 1C — Memoria de rival *(cero esquema, cero estado, retrocompatible)*
1. `RivalHistory`: clase pura de lectura sobre `RunState`. Excluir `Boss` del filtro (el nodo de jefe
   consume un `opponentId` fantasma: `NodeKinds.IsMatch` incluye `Boss`).
2. Registrar el hecho en `MatchResolution.cs:197`: quién lesionó/mató a quién, vía contador de clave libre.
3. **Campo `clanId` en el esquema de rival** + los 15 ficheros, para que un clan cruce de acto.
4. **Experimento previo y barato, antes de nada**: barrer `MapGenerator.Generate` sobre N semillas y medir
   **cuántos clanes distintos ve de verdad una run**. El palomar ya garantiza repetición en los actos 2 y 3;
   falta la tasa exacta. Sin partidos, sin motor: es un bucle.

### Fase 1B (ya acordada) — Abrir `modifyUtility`
Sin cambios respecto a la ADR 0122. Con el añadido de §E: **rasgos nuevos en `/data`** como paso previo de
coste cero, para comprobar si la diferenciación de intención se nota **antes** de tocar el motor.

### Fase 2 (ya acordada) — Que el partido cuente lo que ocurre
Se le añaden dos cosas de este documento: **nombrar a los jugadores rivales** en pantalla, y el **cartel de
rivalidad en el nodo**.

### Fase 3 — Escalada
El clan con historial reaparece como élite. **Toca `MapGenerator` y por tanto el flujo `RngStreams.Map`, lo
que invalida la referencia de balance actual**: va al final, con ADR y lote.

**Lo que NO hay que hacer primero, y el encargo ya lo dice bien:** ni lore, ni nombres nuevos, ni 50
eventos, ni diálogos. Nada de esto lo pide el roadmap.

---

## La prueba, y cómo sabremos que funciona

El criterio de la ADR 0122 sigue valiendo y este documento le añade uno:

1. *«¿Me estoy acordando de mis jugadores?»* (ADR 0122)
2. **«¿Me estoy acordando de mis rivales?»** — operativamente: *si al ver el cartel de un nodo el jugador
   dice algo en voz alta, funciona.*

**Y el listón que fija el encargo es correcto y medible**: el sistema debe producir la anécdota del §15 sin
una sola escena escrita. Con lo de aquí, esa cadena concreta —Grok lesiona a Thrainn, el clan reaparece,
Thrainn vuelve, Thrainn marca— **es alcanzable, y no necesita ni un sistema nuevo de reputación, ni
relaciones, ni diplomacia**. Necesita que el juego deje de tirar tres datos que ya calcula.

---

## Riesgos y conflictos con la arquitectura actual — dicho explícitamente

1. **`MapGenerator` es intocable barato.** Cualquier control de reaparición consume `RngStreams.Map` de otra
   forma y regenera todos los mapas de todas las semillas. Separar *recordar* de *reaparecer* es lo que
   hace viable todo lo demás.
2. **`Boss` cuenta como partido** en `NodeKinds.IsMatch` y guarda un `opponentId` que nadie usa. Cualquier
   cuenta de «veces vistas» que lea el mapa dará un falso positivo si no lo excluye.
3. **Un perk «cobarde» choca con la ADR 0088.** Resuelto por la regla rasgos-restan/perks-suman (§E), sin
   tocar la ADR.
4. **Abrir C1 es un recalibrado, no un parche**, y el presupuesto está ajustado: `injuriesPerMatch` 0,81
   con techo 0,90.
5. **Una sexta raza cuesta una recalibración del abanico** y compra poco (§0.3).
6. **La turba no existe** (es una etiqueta en el 27,6 % de los partidos). Cualquier fantasía que se apoye en
   «la prórroga sin árbitro» está hoy apoyada en nada.
