# ADR 0143 — El balón parado es una jugada, no «el balón en los pies y a jugar»

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloque 8)
**Paquete**: P9
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

El motor ya tenía las seis reanudaciones —saque de banda, de puerta, córner, de centro, falta y penalti—,
con su cuenta atrás, su sacador designado y su barrera de distancia (BB-B). Lo que no tenía era que la
reanudación **fuera una situación distinta**:

- **El sacador decidía con la tabla de siempre.** De un saque de banda se podía rematar; de un córner se
  podía chutar a puerta. El sacador era, a efectos de la IA, un portador cualquiera con el balón en un
  sitio raro.
- **En el penalti seguían dentro del área los doce jugadores.** RF-054 lo trata aparte de la barrera a
  propósito, pero nadie vaciaba el área, así que un penalti era un tiro con una multitud alrededor.

## Decisión

### 1 · El sacador decide **dentro de su reanudación**

No hace falta una fase nueva ni una máquina de estados aparte: **la decisión ya ocurría en el sitio
correcto**. `SetOwner` termina llamando a `Decide` —lo hace desde AW-A para que quien recibe el balón no se
quede un tick parado—, así que el sacador ya decidía en el mismo tick en que recibe el balón. Lo que
faltaba era que fuera la decisión **de esa jugada**.

Se marca al sacador en el contexto durante ese tick y la utilidad **filtra** sus acciones legales:

| reanudación | qué puede hacer | por qué |
|---|---|---|
| saque de banda | pase corto o largo | no se saca con el pie: ni tiro, ni regate, ni centro |
| córner | pase corto, pase largo o **centro** | el córner corto y el balón al área que pedía el encargo |
| saque de puerta | pase corto, pase largo o **despeje** | no se sale regateando desde la propia portería |
| saque de centro | pase corto o largo | |
| **falta** | **todo, incluido el tiro** | el tiro directo es lo que la hace una jugada y no un trámite |

**Filtrar antes de comparar, y no corregir después de elegir**, es lo que hace que la decisión sea de
verdad la de esa jugada: la utilidad compara sólo entre lo que se puede hacer.

### 2 · El área del penalti se vacía, y **se mantiene vacía**

Sólo el lanzador y el portero que lo defiende. A los demás se les saca por el **borde más cercano** del
área —el mismo criterio con el que la barrera aparta a un rival—, así que cada uno queda lo más cerca
posible de donde estaba.

Y se mantiene **toda la cuenta atrás**, no sólo al abrirla. Desde AW-R el equipo no se congela durante el
balón muerto, así que vaciar una vez y confiar deja que la gente vuelva a entrar andando: **medido, tres
jugadores dentro del área en el fotograma del lanzamiento**. Es el mismo patrón que la barrera de BB-B, y
va aparte porque RF-054 excluye el penalti de aquélla a propósito: dos reglas distintas para dos
situaciones distintas.

## Lo que NO se ha hecho, y por qué

- **No se ha construido una fase explícita con ocho pasos.** El encargo la describe como *identificar tipo
  → parar el tiempo → recolocar → respetar distancias → preparar → elegir ejecutor → ejecutar → reanudar*,
  y **el motor ya hace los ocho**: `BeginRestart` identifica y para, `ResetPositions` recoloca en el saque
  de centro, `EnforceRestartClearance` respeta distancias, `SelectTaker` elige ejecutor y `TakeRestart`
  ejecuta. Lo que faltaba era el paso de **decisión**, y es el que se ha añadido. Montar encima una
  máquina de estados nueva habría sido duplicar lo que ya existe, que es justo lo que el punto 18 del
  encargo prohíbe.
- **El córner no recoloca a nadie en el área.** Que los rematadores se agrupen en el área en un córner es
  una recolocación por tipo de reanudación —la receta (a) de `docs/referencia-motores-futbol.md` §6.2—, y
  es un cambio de posiciones que hay que medir. Queda anotado.
- **El portero no elige «corto o largo» como opción de saque**: recibe el balón y decide con la tabla, que
  desde la ADR 0141 **sí** mira a quién se la da. Partirlo en opciones explícitas no añadiría decisión, sólo
  maquinaria.

## Consecuencias

- Las reanudaciones dejan de producir disparos imposibles, así que **bajan los tiros** de banda y córner.
  Aparecerá en la medición y es el efecto buscado.
- El penalti coloca a doce jugadores cada vez que ocurre, lo que **cambia las posiciones** de todo el mundo
  en ese tramo y, con ello, el rechace y la segunda jugada posteriores.

## ENMIENDA (24 sep 2026, BC-A y BI-F): la reanudación **espera**, y no coloca a nadie de golpe

El revisor, jugando: *«cuando un jugador mete gol se queda en campo rival y el rival saca de centro con el
jugador todavía ahí. En general en todas las paradas de juego debemos dar tiempo para que los jugadores se
reposicionen de manera natural, sin teletransportes. No me importa que se alargue el tiempo de gameplay (el
reloj del partido seguiría parado)»*.

Esta ADR hizo del balón parado una jugada para **el sacador**. Le faltaba la otra mitad: para **los otros
once** seguía siendo un teletransporte. `ResetPositions` ponía a todo el mundo en su casilla de golpe, y
como la ADR anterior (BB-C) había tenido que eximir de eso al que celebra —para que la celebración no se
viera en el sitio equivocado—, el goleador se quedaba donde estaba. Medido: **el 85,7 % de los goles**, y
siempre él solo.

Los dos defectos eran el mismo: **una reanudación que coloca a la gente de golpe no puede hacer nada con el
que no se deja colocar.**

### 1 · El reloj del partido se separa del tick del motor

`_clockTick` sólo avanza con el **balón en juego**; `_tick` sigue contando siempre, porque es la secuencia
de la que cuelgan enfriamientos, traza y eventos. Sin esta separación, esperar a que la gente se coloque
costaría minutos de fútbol y **un gol en el minuto 80 acortaría el partido más que uno en el 10**. Con el
reloj parado, el partido dura lo que dice `regulationTicks` de juego real y lo que haga falta de reloj de
pared — que es exactamente lo que pasa en un campo, y lo que el revisor autorizó expresamente.

### 2 · El saque de centro devuelve al equipo **andando**, y espera a que lleguen

Sólo el saque de centro: es la única reanudación que reordena a los once. En un saque de banda o un córner
la gente sigue jugando (AW-R) y eso está bien.

**La espera es adaptativa, no una cuenta atrás fija, y eso es una decisión.** Una cuenta atrás fija habría
que dimensionarla para el peor caso —el goleador, que celebra treinta ticks y luego cruza el campo entero—
y entonces **todos** los saques de centro pagarían ese peor caso. El tope (`kickoffMaxWaitTicks`, 180 ticks)
existe sólo para que un derribado, que no anda, no congele el partido.

**Trampa medida, y costó una iteración**: fijar `TargetPoint` y confiar **no funciona** — `Decide()` lo
sobrescribe al tick siguiente con lo que diga la utilidad. La primera versión dejó **3,14** jugadores en
campo rival al sacar, **peor que el teletransporte** (1,00). Hay que caminarlos explícitamente, como ya
hacía `WalkRestartTaker` con el sacador.

| | antes | primera versión | ahora |
|---|---|---|---|
| goleador en campo rival al sacar | **85,7 %** | 39,1 % | **0,0 %** |
| jugadores del equipo que marcó en campo rival | 1,00 | 3,14 | **0,00** |

Test permanente: `Sim.Tests/Engine/KickoffFormationTests.cs`, hermano del de BB-C. Hacen falta los dos:
aquel prohíbe teletransportar al que celebra, éste exige que acabe volviendo. Con uno solo, cualquiera de
los dos arreglos rompe al otro.

### 3 · En un saque de puerta los rivales salen del área

Es la regla del fútbol y era la mitad que le faltaba a la barrera genérica de dos casillas (BB-B):
**apartarse dos casillas del balón no impide estar dentro del área esperando el rechace**, así que el
delantero que presiona se quedaba dentro y cabeceaba el saque de vuelta. Reutiliza la misma salida por el
borde más cercano que el penalti del §2 de esta ADR — un rival al que el árbitro manda salir del área sale
**por donde está**, no por donde le venga bien al motor.

## Consecuencias de la enmienda

- **El partido dura más en reloj de pared** y lo mismo en minutos de fútbol. Es lo pedido, y hay que tenerlo
  en cuenta al medir cualquier cosa por tick de motor en vez de por tick de reloj.
- **Cualquier métrica que dividiera por `_tick` ahora divide por `_clockTick`** (urgencia por minuto, fin de
  tiempo reglamentario, gol de oro). Un sitio que se dejara con `_tick` acortaría el partido en silencio.
- **Trampa que costó cuatro tests de run en rojo**: partir `ResetPositions` en dos perdió por el camino la
  guarda `!player.OnPitch`, lo que colocaba en el campo, desde el tick 0, al suplente que trae una
  sustitución programada (ADR 0094). El partido divergía entero, el que se lesionaba en el tick T dejaba de
  lesionarse y el motor rechazaba la sustitución. Detalle y las dos hipótesis equivocadas que precedieron a
  la buena, en `docs/pendientes/BC-A.md`.

### 4 · Y una métrica que había que arreglar detrás: el reparto por tercios

Parar el reloj tiene una consecuencia que no es de juego sino de **instrumento**, y hay que escribirla
(RT-057). `docs/balance.md` justificaba que `ballThirdMaxShare` contara el balón parado con estas palabras:
*«durante las reanudaciones el reloj sigue y el balón está quieto en el punto del saque; el tiempo muerto es
parte del reparto»*. **La premisa era el reloj**, y el reloj se ha parado.

Con el saque de centro esperando a que el equipo vuelva andando, el balón puede pasarse hasta 180 ticks
inmóvil en el círculo central. Medido en un lote de 2.000 partidos contra línea base propia:
`ballThirdMaxShare` **40,94 → 52,74**, fuera de banda — y sin que el juego se hubiera concentrado en ningún
tercio. La métrica estaba leyendo una **parada** como territorio.

El reparto por tercios pasa a acumular sólo con el balón en juego, que es la misma condición de
`_clockTick`: la misma decisión aplicada al mismo problema, no un parche para poner una puerta en verde.
Remedido, **ninguna métrica queda fuera de banda**.

**`PossessionTicks` no necesita la misma guarda**: `Ball.Park` pone `Owner = null` al abrir la reanudación,
así que durante la cuenta atrás el balón no tiene dueño y la posesión nunca acumuló nada. *(Este párrafo
decía lo contrario —«sigue contando los ticks en los que el sacador tiene el balón»— y era falso: la
revisión independiente lo comprobó en `Sim/Engine/Ball.cs`. Se deja el rastro porque el pendiente fantasma
que creaba es exactamente el defecto que BI-G bautiza en este mismo commit.)*

### Lo que este cambio SÍ mueve, y se deja medido sin tocar

El reloj parado significa que un partido contiene **más fútbol real** que antes, porque el tiempo muerto ya
no se descuenta de `regulationTicks`. No es un efecto secundario: es la consecuencia directa de lo que se
pidió. Medido (2.000 partidos, semilla 1, contra línea base propia):

| métrica | antes | después | |
|---|---|---|---|
| `shotsPerMatch` | 8,30 | **9,91** | IN |
| `goalsPerMatch` | 2,14 | **2,55** | INFO |
| `possessionChanges` | 22,64 | **24,81** | IN |
| **`tacklesPerMatch`** | **6,86** | **5,69** | **FUERA (suelo 6,00)** |
| ticks de motor por partido | 1.400 | ~1.700 | |

### Una métrica se sale, y NO se toca: `tacklesPerMatch`

Cae un **17 %** y se va por debajo de su suelo RT-056. No es ruido de semilla —falla en las semillas 1
(5,85) y 13 (5,78) y pasa en la 7— y **no es ninguna de las piezas que uno esperaría**. Aislado apagando
cada cambio por separado, 2.000 partidos cada uno:

| configuración | `tacklesPerMatch` |
|---|---|
| línea base, antes de todo esto | **6,86** |
| todo puesto | **5,69** |
| sin el duelo aéreo sólo-en-bajada (BI-F) | 5,50 |
| sin vaciar el área en el saque de puerta (BI-F) | 5,68 |
| **sin parar el reloj** | **4,68** |

Quitar cualquiera de las otras piezas **no lo recupera**, y parar el reloj lo **mitiga** (añade fútbol, y
con él entradas). Lo que lo causa es **la reposición andando en sí**: manteniendo el reloj constante, volver
a la formación cuesta 6,86 → 4,68.

**Y la explicación es legible, que es lo que la hace creíble**: un equipo que se recoloca de verdad en su
formación tras un gol **presiona menos** que uno al que se teletransportaba a casa y que acto seguido
derivaba hacia el balón durante la cuenta atrás. Menos gente fuera de sitio persiguiendo, menos entradas.
Es exactamente el comportamiento que se pidió, con su precio.

**No se calibra, y es deliberado**: el revisor dijo que no se balancea todavía, y mover un suelo RT-056 para
poner una puerta en verde sería justo lo que la ADR 0139 ya tuvo que retirar en este mismo trabajo. Queda
como **decisión del revisor**, con los números puestos: o se acepta un fútbol con menos entradas y se mueve
el suelo con datos (RT-057), o se decide que la reposición tiene que dejar a alguien adelantado.

### Y lo demás para la fase de balance

1. **El partido dura ~115 s de reloj de pared** (a 15/s) contra los **60-90 s** que fija
   `docs/requisitos.md`. El revisor autorizó expresamente que se alargara, así que no se toca; pero si se
   quiere devolver el partido a esa ventana, la palanca es `regulationTicks`, y bajarla es una decisión de
   requisitos, no de este arreglo.
2. **`possessionChanges` sube a 24,81** contra un techo de 28: margen, pero es la siguiente candidata si el
   fútbol sigue creciendo.
