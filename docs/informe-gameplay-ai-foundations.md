# Gameplay AI Foundations Pass — informe de implementación

**24 sep 2026.** Encargo del revisor, plan en `docs/plan-gameplay-ai-foundations.md`.
**El pass está completo y NO se ha balanceado nada**, que es lo que el encargo manda.

---

## 1. Qué se ha implementado

Nueve ADR, ocho commits, y una regla que las atraviesa todas: **una acción nueva sólo si su consecuencia
sobre el balón es propia**. De quince acciones a diecisiete.

| ADR | Qué |
|---|---|
| **0138** | Percepción compartida del equipo · **proteger** (`Shield`) · **despejar** (`Clear`) · arranque coordinado |
| **0139** | La altura parte la recogida en tres · duelo aéreo con atributos · cabezazo y segunda jugada · pase largo elevado |
| **0140** | Marcador, minuto y **orden táctica** del jugador (Defensive / Neutral / Offensive) |
| **0141** | El portero decide: tres finales de parada · el receptor de un pase importa · salir del área |
| **0142** | El cansancio como **recurso** que se gasta y se recupera |
| **0143** | El balón parado es una jugada: el sacador decide dentro de su reanudación · el área del penalti se vacía |
| **0144** | **Censo de utilidad** como instrumento del motor · `OfferSupport` pasa a ser **la descarga** |
| **0145** | **Represalia** temporal y local · **la turba sin árbitro** (RF-055d) |
| **0146** | **`modifyUtility` alcanzable** desde `/data`, con techo C10 y plantillas de descripción |

---

## 2. Qué se ha reutilizado (y no duplicado)

Lo más importante del pass es lo que **no** se construyó:

- **`UtilityContext` ya era la capa de percepción compartida** (RT-051). Se extendió en vez de montar un
  sistema paralelo.
- **La ventana de armado del pase** (`states.PassingTicks`), que no hacía nada, **es** el canal del
  arranque coordinado. Ni bus de mensajes ni cola de intenciones.
- **`EnterState(estado, ticks)`** de la ADR 0137 sirve tal cual para proteger.
- **`SetOwner` ya llamaba a `Decide`**, así que el sacador ya decidía en el tick correcto: lo que faltaba
  era filtrar sus opciones.
- **El enfriamiento por acción** del paquete U y la ADR 0129 resolvió el ping-pong del duelo aéreo.
- **La atribución de la ADR 0124** es lo que hace posible la represalia.
- **`ShotRawOffCentre`** de la ADR 0135 decide si un rechace sale por la línea o vuelve al área, sin una
  segunda tirada.
- **Los ocho pasos de fase de balón parado que el encargo describe ya existían**: sólo faltaba el de
  decisión.

---

## 3. ADRs y datos modificados

Nueve ADR nuevas (0138-0146), todas indexadas. Datos: `data/ai/weights.json` (tabla de mentalidad y
dieciocho claves de contexto), `data/sim/tuning.json` (bloques `clear`, `fatigue`, `goalkeeper`; claves de
parada, de estado y de pase; **retirada** de la rampa `movement.fatigue*`), los tres esquemas
correspondientes, y `data/l10n/*/templates.json` (plantillas de `CLEARANCE`, sección `actions` y las dos de
`modifyUtility`).

**Todos los valores publicados son de partida y están marcados como tales en su `_doc`.** Con dos
excepciones, que **no son calibración** y se explican abajo.

---

## 4. Acciones nuevas o modificadas

- **`Shield`** (nueva): conserva el balón sin avanzar y resiste con **fuerza** en vez de con técnica.
- **`Clear`** (nueva): balón alto, largo y sin destinatario, que cae **sin dueño**.
- **`OfferSupport`** (reescrita): de un punto fijo que perdía siempre a **la descarga** al compañero
  apretado.
- **`LongPass`**: se **eleva** cuando tiene el pasillo tapado. No es una acción nueva.
- **`ChaseBall`** del portero: se abre cuando este tick se le permite salir.

---

## 5. Gates cambiados

| gate | antes | ahora | por qué |
|---|---|---|---|
| receptor de un pase | **descartado** si tenía un rival dentro del radio | puntúa peor, y lo compensa su capacidad de aguantar | el pase era binario y los atributos del receptor no entraban en ningún sitio |
| `OfferSupport` | sin precondición, punto fijo | **exige compañero apretado** | no representaba ninguna situación propia |
| acciones del sacador | todas las de un portador | **las de su reanudación** | de un saque de banda no se remata |
| `ClampToArea` del portero | muro absoluto | **ensanchable** bajo condición | `GoalkeeperLeftArea` era inalcanzable por construcción |
| recogida del balón | cualquier altura | **tres casos** según la altura | la ADR 0135 le dio altura al balón y la recogida no la miraba |
| balón disputado | gana el más cercano | **tirada ponderada** por atributos y distancia | lo pedía el encargo |
| árbitro en la turba | pita el 80 % | **no hay árbitro** | RF-055d lo dice desde siempre |
| `modifyUtility` | fuera del esquema | dentro, con techo C10 | el canal existía y ningún dato podía nombrarlo |

**Los gates de `PressCarrier` y `Block` se revisaron y se dejaron como estaban**: se descartan porque casi
nunca hay poseedor y porque cargar exige estar en la jugada activa. Son correctos. *Dejarlos es una
conclusión, no una omisión.*

---

## 6. Qué sistemas interactúan ahora

Esto es lo que el encargo pedía de verdad, y es la parte que no se ve en un diff:

- la **percepción** alimenta proteger, despejar, la descarga, el pase y al portero;
- la **urgencia** mueve a todo el equipo **y** abre la salida del portero — un solo reloj, no dos;
- el **cansancio** llega a la velocidad, la puntería, los duelos, el pase, el tiro y la decisión **por un
  solo sitio**: los atributos;
- el **despeje** produce el balón aéreo que alimenta el **duelo**, que produce el **cabezazo**, que produce
  la **segunda jugada**;
- el **rechace** del portero alimenta esa misma segunda jugada desde el otro extremo;
- la **atribución de lesiones** alimenta la represalia;
- la **orden táctica** del jugador entra por el estado inicial, como la alineación.

---

## 7. Tests añadidos

**1.222 en verde**, RT-024 incluido, 241 ficheros de `/data` válidos. Nuevos:
`ShieldAndClearTests`, `AerialBallTests`, `MentalityAndUrgencyTests`, `GoalkeeperTests`, `FatigueTests`,
`RestartDecisionTests`, `DeadActionsTests`, `GrudgeAndMobTests`, `ModifyUtilityReachableTests` y
**`EmergentChainTests`**, que es el criterio de éxito del encargo escrito como test.

**Nueve tests existentes replanteados, ninguno regenerado**, y cada uno con su causa medida y escrita.
Tres patrones, y conviene que consten:

1. **Tests que fijaban una huella del flujo de RNG** (una semilla clavada para que ocurriera su
   precondición). Ahora la **buscan**: `TestPerks.SeedWithActivations`.
2. **Tests cuyo margen era menor que su propio error de muestreo** — podían ponerse rojos sin que el juego
   cambiara nada.
3. **Tests que medían una regla a través de una estadística** y dejaron de detectar nada **por haber
   acertado**: el suelo de apertura y la liberación de la barrera. Ahora afirman la regla, como funciones
   puras.

---

## 8. Limitaciones que quedan

- **Los tiros han bajado y hay que mirarlo**: `shotsPerMatch` 7,17 (semilla 1) y **5,66** (semilla 7),
  contra una banda de 7-15. Los goles bajan a 1,89 / 1,32. Es consecuencia esperada de tres cosas a la vez
  —de un córner y de un saque de banda ya no se dispara, el portero rechaza en vez de atrapar siempre, y
  el pase encuentra receptor donde antes no lo había— y es **exactamente** lo que el encargo dijo que no
  era un problema de este pass.
- **El portero recupera mucho más**: medido en la ADR 0139, de 37 a 123 en veinte partidos, y con el
  rechace habrá más balones vivos en su área. Acumulativo.
- **El centro sigue sin poder disputarse en el aire**, a propósito (ADR 0136). Candidato de balance.
- **El córner no agrupa rematadores en el área.**
- **La represalia no cambia el marcaje**, sólo las ganas de entrar.
- **El campo estrechado, el público y el +15 % de la turba** siguen sin existir: son física, no reglas.
- **`/Game` no tiene selector de orden táctica** ni pinta el estado `Shielding`. Es presentación y va en su
  propio commit (`/Sim` y `/Game` no se mezclan).

---

### Las 43 puertas, como foto y no como criterio

**39 verdes, 4 rojas** (HEAD traía 3). Se lanzan porque el encargo las admite como *smoke*, y se apuntan
aquí porque son el punto de partida de la fase siguiente — **no** porque este paquete deba dejarlas verdes:

| puerta | valor | banda |
|---|---|---|
| `coherentBuildsBeatNone_orc_violence` | 57,16 | ≥ 58 |
| `badBuildsLoseToNone_elf_brawler` | 47,37 | ≤ 45 |
| `badBuildsLoseToNone_orc_misplaced` | 46,07 | ≤ 45 |
| `badBuildsLoseToNone_elf_out_of_zone` | 46,69 | ≤ 45 |
| **curva de jefes (ADR 0033)** | `grimhold_guns` 58,1 / 68,3 / 81,4 · `eternal_crown` 36,6 | toda la escalera por debajo |

Las tres primeras ya estaban rojas antes del pass y sus fichas las documentan (BF-A, BF-B). **La curva de
jefes es nueva y es la que más dice**: con los goles por partido bajando de ~2,4 a 1,3-1,9, la economía de
la run entera se desplaza. Es el primer sitio al que debe mirar la fase de balance.

## 9. Lo que NO se ha implementado, y por qué

- **Ningún perk nuevo**: la ADR 0122 dice que el catálogo de 102 no se toca, y el encargo dice que primero
  hay que hacer que los sistemas existentes puedan modificar decisiones reales. `modifyUtility` queda
  demostrado de extremo a extremo con perks escritos en el test.
- **Ninguna fase de balón parado nueva**: los ocho pasos ya existían. Montar una máquina de estados encima
  habría duplicado lo que hay, que es lo que el punto 18 del encargo prohíbe.
- **Ningún bonificador para el centrocampista.** Medido con el censo por puesto, por mil decisiones: marca
  **133** (el defensa 16,8), pasa **7,2** (el que más de los tres), cubre 354, busca espacio 314 y repliega
  122. **Es el perfil más completo de los tres** y el único que marca mucho *y* pasa mucho. Lo que le falta
  de la lista del encargo —presionar y cambiar la orientación— es calibración, y tocarlo ahora sería el
  «bonificador arbitrario» que el propio encargo prohíbe.
- **Ningún peso tocado para resucitar una acción.** Lo único que cambió de `OfferSupport` es qué significa.

---

## 10. Decisiones de arquitectura importantes

1. **La percepción va donde ya estaba la percepción.** Se justifica porque **elimina** trabajo cuadrático
   repetido, no porque sea elegante. Verificado inerte: 500 partidos byte a byte idénticos antes de apoyar
   nada encima.
2. **La mentalidad es un eje aparte que multiplica**, no un quinto estado táctico. `Neutral` es 100 en
   todo, así que la capa es **exactamente inerte** en un partido empatado con órdenes neutras.
3. **El cansancio se aplica donde se lee el atributo.** Una regla en vez de siete, y ningún consumidor
   futuro puede olvidarse de ella.
4. **Las reglas que se pueden escribir como función se prueban como función** (`UrgencyPercent`,
   `AimApertureFactor`, `ShouldReleaseClearance`), que es el patrón que el repositorio ya usaba.
5. **Quitar al árbitro en la turba es una regla, no una probabilidad al 99 %.**

## Las dos cifras que se movieron, y que no son calibración

El encargo prohíbe el tuning. Dos valores se eligieron **midiendo**, y los dos por la misma razón: sin
ellos el sistema no tenía estado posible, no porque una frecuencia no gustara.

- **`fatigue.recoverPerTick`**: con el valor de partida, **129 de 280 jugadores acababan exactamente a
  cero** y la mediana era 6 de 1000. Medido el ciclo de trabajo real (72 %), la recuperación estaba puesta
  para equilibrar al 56 %: **no existía equilibrio**. Con 7, mediana 447 y jugadores repartidos de 0 a
  1000.
- **`goalkeeper.exitUrgencyPercent`**: estaba en 75 y la urgencia de ir **un gol por detrás con el tiempo
  cumplido** vale 60. O sea que la cadena que el propio encargo pone de ejemplo —*0-1 y quedan segundos, el
  portero sale*— **era imposible**. Lo encontró el test de integración de la cadena.

---

## Y tres defectos que el pass encontró por el camino

Ninguno era de lo que se estaba construyendo:

1. **Ping-pong del duelo aéreo**: 1.753 duelos en veinte partidos (88 por partido) porque el que cabecea
   seguía pegado al balón. → 124 con el enfriamiento.
2. **Hueco en la barrera de BB-B**: con el saque pegado a la banda, acotar al campo devolvía al rival
   **dentro** de la barrera (1,26 con la barrera en 1,30). Arreglado deslizando por la línea —y sólo hacia
   su propio lado: el primer intento cruzaba al jugador por delante del balón, 3,85 casillas en un
   fotograma—.
3. **El área del penalti se volvía a llenar**: vaciarla al abrir la reanudación no basta porque desde AW-R
   el equipo no se congela. Tres jugadores dentro en el fotograma del lanzamiento.
