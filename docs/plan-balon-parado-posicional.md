# El balón parado como fase posicional — nota de diseño (game-design-review)

**Fecha**: 24 sep 2026 · **Estado**: nota de diseño, **sin implementar** · **Encargo del revisor**
**Skill**: `game-design-review` (primitiva de motor modificada, Regla B de `CLAUDE.md`)

## De dónde sale

El revisor, tras ver la caída de `tacklesPerMatch` (6,86 → 5,69, fuera del suelo 6,00):

> *«analiza cómo se posiciona un equipo en un saque de centro, saque de banda, córner y falta en el fútbol
> real. Debes poder simular eso en el juego. De mientras el reloj del partido está parado hasta que se
> resuelvan las posiciones. Es lo más natural.»*

Y que el punto 3 —energía y enfriamientos congelados durante la espera— va con esto.

---

## 1 · Cómo se coloca un equipo de verdad

La escala importa, así que primero la conversión. El campo son **16 columnas × 7 filas**. Contra un campo
real de ~105 × 68 m: **1 columna ≈ 6,6 m**, **1 fila ≈ 9,7 m**. La cuadrícula es **anisótropa**, así que un
radio medido «en casillas» no es un círculo.

- **9,15 m** (la distancia reglamentaria de barrera) ≈ **1,4 columnas** ≈ **0,94 filas**.
- **2 m** (la distancia en un saque de banda) ≈ **0,3 columnas**.

> **Primer hallazgo, y es de dato**: `restart.restartClearanceCells` vale **2,0 casillas** y se aplica igual
> a las cinco reanudaciones. Eso son **13 m en horizontal y 19 m en vertical** — más que la barrera real en
> los dos ejes, y **seis veces** la distancia real de un saque de banda. La barrera genérica no representa
> ninguna regla concreta del fútbol.

### Saque de centro

Ambos equipos **enteros en su propio campo**. El que no saca, además, **fuera del círculo central**. El que
saca pone **dos jugadores sobre el balón** (uno toca, otro recibe) y el resto en forma, con la línea
defensiva **adelantada hacia el medio campo** para comprimir — nadie está en su casilla de reposo.

*En la cuadrícula*: punto central columna 8, fila 3. Campo propio = columnas < 8 / > 8. El círculo ≈ 1,4
columnas de radio.

**Es casi lo que ya hacemos**, con una diferencia que resulta ser justo la que importa: mandamos a todos a
su `HomeCenter`, que para un delantero es su sitio de **reposo, atrás**. En un saque de centro real el
delantero está **en la línea de medio campo**. Ésa es, muy probablemente, una parte de la caída de entradas.

### Saque de banda

**Aquí no se recoloca nadie, y esto es lo más importante de todo el análisis.** El fútbol no reorganiza un
equipo para un saque de banda: el sacador pisa fuera, dos o tres compañeros ofrecen apoyo corto a menos de
10 m, el rival los marca, y **los demás mantienen la forma que traían**. El bloque defensivo se escora hacia
esa banda y el atacante sube unos metros. Nada más. Y la distancia del rival son **2 m**, no 9,15.

*En la cuadrícula*: un reajuste **local**, de una o dos casillas, alrededor del punto de saque.

### Córner

La jugada más estructurada después del saque de centro. En once: 4-6 atacantes al área. **En fútbol 7, con
6 de campo, son 3-4**, con papeles distintos:

- un rematador **al primer palo**,
- un **objetivo central** —el más fuerte y más grande—,
- un **segundo palo**,
- y **uno fuera del área**, para el rechace: la «segunda jugada».

El que defiende mete a casi todos: uno o dos **en los palos**, marcas sobre cada atacante, y **deja un
jugador arriba** como salida del contragolpe. Los rivales, a 9,15 m del córner hasta que se saca.

*En la cuadrícula*: el área son **2 columnas × 4 filas = 8 casillas**. Meter 3-4 atacantes + 4 defensas +
portero es **saturarla**. Funciona, pero va a estar llenísima, y con el empuje entre cuerpos (ADR 0020) eso
significa **más contacto** — que en este juego no es un efecto secundario, es el tema.

> **Y aquí está el mejor argumento del cambio entero**: un córner a un área llena es **exactamente** la
> situación para la que se construyó el duelo aéreo de la **ADR 0139** (Fuerza + radio corporal) y el centro
> de la **ADR 0136**. Hoy esa mecánica existe y casi nunca ocurre donde debería. Esto no añade una mecánica:
> **hace que una que ya pagamos ocurra en su sitio.**

### Falta

No hay una colocación de falta: hay **tres**, y las decide la distancia y el ángulo a portería.

1. **Lejos / campo propio**: se saca rápido, no hay barrera. Los dos equipos solo se reajustan — el que
   ataca sube, el que defiende baja.
2. **A distancia de tiro, centrada**: **barrera** a 9,15 m en la línea balón-portería (en once son 3-5
   jugadores; **en fútbol 7, dos**), el portero tapa el lado libre, el resto marca en el área y los
   atacantes se colocan para el rechace.
3. **Escorada, a distancia de centro**: funciona **como un córner**. Cuerpos al área, barrera de uno o
   ninguno.

---

## 2 · La conclusión de diseño: **no son la misma cosa**

Tratar las cuatro igual sería el error. El fútbol distingue dos familias:

| | **Balón parado de verdad** | **Reanudación en juego** |
|---|---|---|
| cuáles | saque de centro, córner, falta de tiro | saque de banda, falta lejana, saque de puerta |
| ¿se recoloca el equipo? | **sí, entero y con papeles** | **no**, ajuste local |
| cuántas por partido (medido) | ~**5-6** | ~**17** |

### DECISIÓN DEL REVISOR (24 sep 2026), y corrige la mitad de esta sección

> *«Quiero que córner y puerta paren el reloj para dar tiempo a asimilar que ha pasado algo y que los
> jugadores se reposicionen. Con banda se para pero mucho menos tiempo (viendo el gameplay hay veces que no
> te enteras que pasa).»*

La distinción de arriba **sigue siendo válida para la COLOCACIÓN** —el saque de banda no recoloca a nadie,
eso es fútbol— pero **no** para el reloj. Y el motivo del revisor no es el mío, que era fidelidad: es
**legibilidad**, y es mejor argumento.

**Una reanudación es también el momento en que el espectador se entera de lo que acaba de pasar.** Si el
balón sale y vuelve a entrar en un segundo, el jugador no ha visto por qué salió. Eso conecta con un
principio que ya está escrito en `CLAUDE.md`: *feedback simple > cinemáticas complejas — el jugador no
necesita una escena, necesita que nada desaparezca sin explicación*. El saque de puerta entra en la lista
por esto, no por colocación: tras una parada o un tiro fuera hay **algo que entender**.

Así que el eje no es «para o no para», sino **cuánto**, y son dos ejes distintos que hay que separar:

| reanudación | ¿recoloca? | pausa | por qué |
|---|---|---|---|
| saque de centro | **equipo entero** | **larga** | acaba de haber un gol: lo más que hay que asimilar |
| córner | **equipo entero, con papeles** | **larga** | la jugada más estructurada que existe |
| saque de puerta | no (sólo vaciar el área) | **larga** | *legibilidad*: hay que ver por qué salió el balón |
| falta de tiro | barrera + área | **media** | la barrera tiene que verse subir |
| saque de banda | **no** (ajuste local) | **corta, pero no cero** | que se entienda que salió, sin frenar el partido |
| falta lejana | no | **corta** | se saca rápido, como en el fútbol |

---

## 3 · Lo que choca con los requisitos — **decisión del revisor**

Esto hay que decirlo antes que nada, porque `docs/requisitos.md` es la fuente de verdad y lo que se pide la
contradice en tres puntos:

- **RF-053**: *«Las reanudaciones (banda, córner, saque de puerta) son **instantáneas**, con una animación
  superpuesta de 1 segundo que **no** detiene el reloj.»* — La instrucción dice lo contrario para el córner.
- **RF-054**: *«**Solo** detienen el partido: penalti y tarjeta roja. Son los únicos puntos de pausa
  dramática.»* — Añadir paradas de balón parado rompe la exclusividad, que estaba puesta a propósito.
- **RF-050**: *«Un partido dura entre **60 y 90 segundos** a velocidad x1.»* — Ya se incumple hoy (~115 s
  tras parar el reloj) y esto lo empuja más.

**No son rangos de balance, son requisitos funcionales**, así que no los toco por mi cuenta. La propuesta es
enmendarlos con una ADR (RT-057: nunca un cambio silencioso), y el argumento a favor es bueno: RF-053 y
RF-054 se escribieron cuando el balón parado **no era una jugada**, y la ADR 0143 ya empezó a moverlos al
hacer que el sacador decidiera dentro de su reanudación. Pero es enmienda de requisito, y la firma el
revisor.

---

## 4 · Presupuesto de tiempo — la palanca que decide si esto cabe

Medido: ~**20 reanudaciones** por partido de más de 15 ticks, ~**445 ticks parados** (26,3 % del partido),
~1.700 ticks de motor ≈ **115 s** de reloj de pared.

| alcance | ticks muertos estimados | partido |
|---|---|---|
| hoy | ~445 | ~115 s |
| **lo decidido por el revisor** (pausa graduada, §2) | ~**830** | ~**135 s** |
| todas con pausa larga, sin graduar | ~1.200 | ~153 s |

**La pausa graduada cabe**, y cabe justo porque está graduada: el saque de banda es la reanudación **más
frecuente** (~10 por partido) y darle una pausa corta en vez de larga es lo que salva el presupuesto.

> **Y el que hay que vigilar es el saque de puerta**, no el córner: son ~7 por partido contra ~2, así que
> con pausa larga se lleva **~315 ticks**, la partida más cara de la cuenta. Si el presupuesto se pasa, ése
> es el primero que hay que recortar — y su pausa es la única de las tres largas que **no** necesita tiempo
> para recolocar a nadie, sólo para que se entienda.

---

## 5 · Las diez preguntas

**1. ¿Qué experimenta el jugador?** Hoy un córner, una banda y una falta se ven **iguales**: una pausa y un
pase. Con el cambio cada uno tiene **forma reconocible** — el área se llena en un córner, sube una barrera
en una falta de tiro, una banda no interrumpe nada. Ve fútbol en vez de una lista de reanudaciones.

**2. ¿Qué decisión toma el jugador con esto?** **Ninguna nueva, hoy.** Es comportamiento observable, no un
modificador invisible —o sea, el lado bueno del principio— pero hay que decirlo claro: es espectáculo y
legibilidad, no una decisión.

**3. ¿Cuál debería tomar?** La natural es **cuántos subes al córner**: comprometer gente arriba es más gol y
más contragolpe, y en este juego además **más contacto sobre tu grandullón**. Eso sí es la «carnicería
administrada». **No entra aquí**: sería una mecánica de órdenes con su propia ADR, y colarla de rondón en un
arreglo de posicionamiento es justo lo que la Regla B prohíbe. Se anota como continuación natural.

**4. ¿Qué regla representa?** Las reanudaciones existen (RF-052, RF-053, RF-054). **Las colocaciones por
tipo NO existen como requisito: las estoy proponiendo yo**, y lo digo explícitamente. La barrera de 9,15 m,
los 2 m del saque de banda y el círculo central son reglas del fútbol, no de `docs/requisitos.md`.

**5. ¿Qué sistemas?** `/Sim` (`MatchEngine`, la fase de reanudación) y **`/data`** para las plantillas de
colocación — por RT-031 son datos, no código, igual que perks y objetos. `/Game` sólo consume eventos
(RT-014). **Sin riesgo de contradicción** entre `/Sim` y `/Game`: la colocación la decide el motor entera.

**6. ¿Alternativas?** Tres, y la elegida es la B:
 - **A — las cuatro con fase completa.** Lo pedido al pie de la letra. Rechazada: contradice el fútbol en el
   saque de banda y cuesta ~153 s por partido.
 - **B — dos familias** (§2). Más fiel y la mitad de coste.
 - **C — no tocar las reanudaciones y arreglar las entradas sólo por BI-D** (conducción). Más barata, pero
   no da la legibilidad ni pone el duelo aéreo en su sitio.

**7. ¿Qué trade-off?** **Tiempo de reloj de pared**, que es un recurso real del jugador (RF-050), y
**cansancio más barato**: cada tick parado es recuperación gratis — ver §6.

**8. ¿Cómo cambia las estrategias?** Un córner a un área llena revaloriza **Fuerza y radio corporal**
(duelo aéreo, ADR 0139) y por tanto a los Brutos; y el jugador que dejas fuera del área para el rechace
conecta con `OfferSupport` / la descarga (ADR 0144). Refuerza identidades que ya existen en vez de crear un
bonus nuevo.

**9. ¿Puede degenerar?** Tres riesgos concretos:
 - **El cansancio deja de ser un recurso** (§6). Es el más serio.
 - **Lesiones al alza** por saturar 8 casillas de área en cada córner. Temáticamente correcto, pero
   `injuriesPerMatch` tiene banda RT-056 y hay que medirlo.
 - **El córner como granja**: si colocar bien lo hace muy rentable, la IA puede preferir forzar córners.
   Medible con `cornersPerMatch` y la tasa de gol desde córner.

**10. ¿Cómo se demuestra?** Tests posicionales **en el fotograma en que se resuelve** cada reanudación (en
un córner, N atacantes dentro del área y uno fuera; en una falta de tiro, barrera entre balón y portería a
la distancia correcta; en un saque de centro, los dos equipos en su campo y el que no saca fuera del
círculo); `tacklesPerMatch` de vuelta a banda, que es lo que motivó todo; presupuesto de reloj de pared
medido; y capturas por `visual-review`, porque la mitad del valor es que **se vea**.

---

## 6 · El punto 3 del revisor: energía y enfriamientos

Hoy la rama del saque de centro hace `continue` antes de `UpdatePlayer`, así que durante la espera **ni se
recupera energía ni bajan los enfriamientos** (entrada, bloqueo, aéreo, rencor). Lo señaló la revisión
independiente y está mal.

**El principio que lo resuelve, y que además explica por qué hay dos relojes:**

> **Lo físico sigue el reloj de pared. Lo que es disputa sigue el reloj del partido.**

Un jugador **sí** recupera el resuello durante una parada —por eso los equipos pierden tiempo—, y un
enfriamiento de entrada es físico. Los dos deben correr con el tick del motor.

**Pero tiene un precio que hay que poner sobre la mesa**: cada tick parado es recuperación gratis. Ya hoy,
con el 26,3 % del partido parado, el cansancio es ~26 % más barato por minuto de fútbol que antes — y estas
fases lo abaratan más. Si la **ADR 0142** quiere que el cansancio sea un recurso gestionable, esto lo
erosiona. Tres salidas, **ninguna decidida aquí**:

- **(a)** recuperación en balón parado a **tasa reducida** (`deadBallRecoveryPercent`): el jugador recupera,
  pero la parada no regala un partido entero de descanso. Es la que más me convence.
- **(b)** enfriamientos con el tick del motor (son físicos) y **energía con el reloj del partido** (es
  recurso). Coherente, pero rompe el principio de arriba por la mitad.
- **(c)** nada, y aceptar que el cansancio pesa menos.

---

## 7 · Lo que NO entra en este paquete

- **Órdenes de balón parado** («cuántos suben al córner»): pregunta 3, su propia ADR.
- **Tocar el peso de `Tackle`**: es el instrumento romo, y taparía la causa.
- **Cambiar `restartClearanceCells`** más allá de darle un valor por tipo de reanudación: que la barrera
  genérica no represente ninguna regla real (§1) merece su propia medición.
- **BI-D / la conducción**, aprobada aparte por el revisor: va en su propio hito, y conviene medirla
  **después** de esto para no confundir dos causas sobre la misma métrica — que es el error que BI-F ya
  cometió con sus tres piezas a la vez.
