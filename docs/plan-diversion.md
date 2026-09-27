# Plan de mejora de la diversión

Documento vivo, iterado con el revisor desde el 27 sep 2026. Recoge **lo acordado**, no la conversación.
Cada bloque dice qué existe ya (Regla G), qué falta y qué decisión sigue abierta.

**Tesis:** el proyecto ha invertido en que el fútbol sea creíble, y eso ya está conseguido (el revisor:
*«el realismo ahora está bien […] podemos parar por ahora»*). Lo que falta es **densidad de sorpresa,
agencia entre y durante los partidos, y memoria de lo que pasó**. Sin balance fino por ahora: se balancea
cuando todo esté más o menos listo.

**Relación con `docs/plan-evolucion-knavall.md`:** ese plan ya cubre la memoria (F1, hecha: ADR 0124), la
atribución (F2), el espectáculo con un árbitro con personalidad (F5), la turba real (F7, bloqueada por su
gate 5) y los clanes que cruzan de acto (F1.5, **no hecho**: ningún rival de `data/rivals/` lleva
`clanId`). Este plan **no lo sustituye**: lo reordena por diversión y añade lo que no tenía.

---

## 0. Decisiones del revisor (27 sep 2026)

| Tema | Decisión |
|---|---|
| Métrica guardiana de la sangre | Sí |
| Tirada del destino visible (tensión y humor) | Sí, sólo en tiradas graves o letales |
| Run más corta | Sí, menos nodos por acto. **Sin** x4 automático: lo decide el jugador |
| Consumibles | Son **el eje** de la agencia durante el partido. Gritos y provocar la turba son consumibles |
| Eventos | Prioritario: set nuevo e implementación |
| Recompensa de partido normal | **Oro + objeto común**. Nunca perk |
| Romper el juego | Permitido: combos rotos raros |
| Historia de la run | Resumen al final, en tono de humor |
| Némesis, apodos | Sí |
| Apuesta | Sí, una aleatoria por partido, se toma o se deja: **ADR 0157** |
| Muerto | Deja una reliquia más su equipo; hace falta un **cofre** en la alineación |
| Taller | Se fusiona con la clínica (el herrero) |
| Turba | Tipos distintos. Se anuncia el **tipo** («salta uno y lesiona a uno»), **no la víctima** |
| Árbitro | Visible sobre el campo, con importancia |
| Estadísticas | Apartado al final del partido y acumuladas de la run en la alineación |
| Aparcado | Clip, tutorial brutal, semilla compartible, playtest externo, balance fino |

---

## 1. Eventos

### Diagnóstico: cerrado (BQ-A)

*El revisor: «sale en el mapa, pero entras en el nodo y no hay nada que escoger».* **CONFIRMED y arreglado**
([BQ-A](pendientes/BQ-A.md)): el mapa enseñaba el evento sin abrir el nodo y, desde la ADR 0100, sin nodo
abierto no hay carta. Ahora se abre como la clínica. Captura: `--tour-event`.

### El set nuevo

Hoy hay 6 cartas y 6 efectos (`gold`, `goldShare`, `heal`, `experience`, `experienceTarget`, `injure`), en
dos familias (oro parado, carne por ventaja). Con esas primitivas no hay variedad posible: el set nuevo
necesita **primitivas nuevas**, y cada una pasa por `game-design-review`.

**Primitivas nuevas propuestas:**

| primitiva | qué hace | estado nuevo |
|---|---|---|
| `grantItem` | da un objeto de una rareza (al cofre) | no |
| `grantConsumable` | da un consumible | no |
| `grantTrait` / `removeTrait` | añade o quita un rasgo a un jugador señalado | no |
| `refereeCriterion` | el próximo partido empieza con ±N de criterio | **sí**: modificador del siguiente partido |
| `rivalWeakened` | un jugador del próximo rival empieza con lesión leve | **sí**: idem |
| `recruit` | un jugador se une a la plantilla (con hueco) | no |
| `debt` | efecto **diferido**: se cobra o se paga N nodos después | **sí**: deuda pendiente |

La ADR 0100 dejó fuera los efectos diferidos por exigir estado nuevo. Aquí se proponen porque son **donde
está la historia** («el curandero te debe un favor»). Siguen su regla: todo se ve antes de elegir.

**Cartas (16 nuevas, 22 en total).** Nombres y tono provisionales.

| familia | carta | opciones (siempre hay «seguir camino») |
|---|---|---|
| Árbitro | **La cena con el colegiado** | pagar 6 → próximo partido +25 de criterio · invitar a tu capitán → +40, pero ese árbitro te recuerda (cuenta como soborno, RF-064c) |
| Árbitro | **El árbitro lesionado** | llevarlo a tu clínica (oro) → te debe una: su próximo partido +30 · reírte → nada, y su próximo partido −20 |
| Mercado negro | **El carro del chamarilero** | comprar un objeto raro a mitad de precio pero maldito · cambiar un objeto tuyo por uno al azar de la misma rareza |
| Mercado negro | **El boticario ambulante** | 2 consumibles a elegir de 3 por oro · uno gratis de la familia sucia |
| Sobrenatural | **La bruja del córner** | un jugador gana el rasgo `Frío`, pero pierde 1 de nivel · una lesión grave se cura, pero el jugador pasa a `Descompuesto` |
| Sobrenatural | **El cementerio del club** | desenterrar la reliquia de un caído (si hay) · rezar: todos curan una lesión leve |
| Vestuario | **Pelea en el vestuario** | castigar al agresivo (pierde el rasgo `Agresivo`) · dejarlo: gana `Líder`, pero su víctima queda con lesión leve |
| Vestuario | **El veterano quiere retirarse** | dejarlo ir: se va y deja 2 niveles de experiencia repartidos entre los canteranos · convencerlo: 8 de oro |
| Afición | **Los ultras piden sangre** | prometérselo: en el próximo partido, si lesionas a un rival cobras 10; si no, −1 nivel de moral (lesión leve) a tu capitán · ignorarlos |
| Afición | **La peña del pueblo** | aceptar su chaval: canterano gratis con un rasgo raro · aceptar su dinero: 5 de oro |
| Espionaje | **El ojeador borracho** | pagarle: un jugador del próximo rival empieza con lesión leve · denunciarlo: +10 de criterio con el próximo árbitro |
| Deuda | **El prestamista** | 15 de oro ya, devuelves 25 en tres nodos (si no puedes, se lleva tu mejor objeto) |
| Deuda | **El curandero en apuros** | prestarle 5 → en el siguiente nodo de clínica, una curación gratis |
| Carne | **El foso de entrenamiento** | un jugador pelea: gana `Bruto` como estilo y queda con lesión leve |
| Carne | **Donante de órganos** (revisado) | sacrificar a un jugador (muere) para pasar **uno de sus perks** a un compañero con hueco |
| Chatarra | **El herrero borracho** | prótesis gratis a un jugador, con el resultado del taller a la vista (RF-095) |

**Reglas que se mantienen de la ADR 0100:** no hay tiradas dentro de una opción, salvo las que ya muestran
su tabla (el herrero, RF-095); quien pone el cuerpo lo elige el jugador; el texto del efecto se compone de
plantillas (RT-035).

**Donante de órganos (el revisor: «mola»)** enmienda **RF-072** (un perk asignado no se transfiere): la
única vía es la muerte elegida. Tiene un hermano que hay que mirar antes de implementarlo: la **Herencia**
(`data/economy/inheritance.json`, `InheritanceSystem`) ya pasa algo del muerto a su vinculado.

**Abierto:** si «El foso» y «Donante de órganos» chocan con las cinco condiciones de la ADR 0048 (la muerte
es rara). Una muerte **elegida** en un menú no es daño no anunciado, pero sube `deathsPerRun`.

---

## 2. Entrenamiento

### Diagnóstico

`ServiceNodeSystem.Training` da **+40 de experiencia a todos los disponibles**, sin preguntar. Es
aburrido por construcción: **no hay decisión**. Además contradice **RF-026**, que pide «experiencia
**dirigida** al jugador que el usuario elija». **CONFIRMED** leyendo el código.

### Propuesta, revisada con el revisor (27 sep 2026)

Del primer borrador, el revisor descarta la doble sesión (*«no compensa»*), el maestro (*«no mola»*), el
descanso y el ensayo de jugada, y pide algo nuevo: **«subir mucho una sola estadística de un solo
jugador»**.

La carta ofrece **tres sesiones derivadas del nodo**, siempre con la pachanga como opción segura:

| sesión | qué hace | coste |
|---|---|---|
| **Pachanga** (siempre) | todos ganan la experiencia de hoy (+40) | nada: la opción segura |
| **Especialización: <atributo>** | un jugador señalado sube **mucho** un atributo concreto (fuerza, velocidad, técnica, resistencia o correa); cantidad *provisional, sin medir* | nadie más gana experiencia |
| **Cambio de puesto** | un jugador cambia de posición | pierde un nivel |

La variedad sale de qué atributo toca en cada nodo: la especialización puede aparecer dos veces con
atributos distintos. Así el entrenamiento se decide **por el jugador**, no por la plantilla, y construye
especialistas («el orco más rápido de la liga»), que es lo que da identidad.

**Pendiente de diseño:**

- Si la especialización puede **pasar del techo** que da el nivel. Si no pasa, a nivel alto no vale nada.
- «Cambio de puesto» enmienda **RF-022b** (la posición hoy es fija). Aceptado por el revisor.
- Con menos nodos por acto, 2-3 entrenamientos por acto en vez de 5.

### Hermano anotado: los vínculos no se entienden

El revisor: *«esto no funciona bien, de hecho en la alineación no queda muy claro, no sé qué hacer con
ello»*. No es del entrenamiento: es un problema propio de los vínculos (RF-100..106) y de su lectura en la
alineación (RF-106). Queda como bloque aparte, sin diagnosticar todavía.

## 3. Némesis, con clanes cerrados

*El revisor: «¿no se supone que te vuelves a encontrar con los mismos equipos?».*

**Lo que ya existe (ADR 0124):**

- Los rivales tienen jugadores con nombre fijo (`data/rivals/*.json`).
- **Dentro de un acto** te cruzas varias veces con los mismos 5 clanes.
- El ojeo ya enseña cuántas veces te has visto y el par «quién lesionó o mató a quién» (`RivalCredits`).

**Lo que falta para que haya némesis:**

1. **Los clanes cruzan de acto (aceptado por el revisor).** Hoy `act1_orc_ironclad` («Yunque Verde») y `act2_orc_warband` son clanes
   distintos. Es la F1.5 del plan Knavall (`clanId`). Propuesta: **un clan por raza que sube de acto a acto**,
   con los mismos jugadores con nombre, subidos de nivel y con fichajes nuevos. Así los equipos son cerrados
   y reconocibles de verdad.
2. **Los rivales tienen estado.** Hoy «los rivales de datos nunca se guardan en `RunState`»
   (`RivalTeamBuilder`): si le partes la pierna a Grok, el siguiente partido está sano. Hace falta un
   registro mínimo por jugador rival (lesionado, muerto, títulos). **Sube el esquema de guardado.**
3. **El némesis es un rival que te mató a alguien**:
   - gana un título en su nombre («Grok el Matahermanos») y un nivel extra;
   - **entre actos lo ficha otro equipo «porque se ha dado a conocer»** (decisión del revisor): el némesis
     cambia de clan, y así reaparece aunque su clan original no suba contigo;
   - si lo lesionas o lo matas: pregón de **venganza**, apodo para quien lo hizo y recompensa.
   - Tope de 1-2 némesis vivos por run.
4. **Si matas a uno de los suyos**, el clan se acuerda: su próximo partido contra ti viene más agresivo. Es
   la otra dirección, y además resuelve RF-103 (vínculos negativos) a nivel de clan, sin tocar a tus
   jugadores.

**Abierto:** «el mercenario incómodo» (fichar al asesino de tu hermano) necesita un clan que venda
jugadores. Aparcado hasta tener los clanes.

---

## 4. Huecos de la base que el plan necesita

Los declara la propia interfaz (`UiText.cs`):

- **El árbitro no tiene rasgos ni hay sobornos** (`ui.scout.refereeGap`: «son de fase 3: hoy todos son
  neutros»). El árbitro con memoria de RF-061..064 **no existe todavía**: hacerlo visible (§0) empieza por
  implementarlo.
- **Las faltas no señaladas no se registran** (`ui.report.refereeGap`). La apuesta `referee_blind` de la ADR
  0157 no se puede resolver hasta que se registren.
- **Los consumibles no se pueden usar durante el partido** ([BA-H](pendientes/BA-H.md)).
- **Texto interno a la vista del jugador**: unas 18 cadenas con `RF-xxx`/`ADR xxxx` ([BQ-A](pendientes/BQ-A.md)).

## 5. Orden propuesto

1. **Eventos**: el diagnóstico está hecho (BQ-A); faltan las primitivas y el set.
2. **Entrenamiento** (§2): reutiliza la carta del evento, así que va detrás.
3. **Consumibles como eje**: antes, cerrar **BA-H** (hoy no se pueden usar durante el partido: RF-082).
   Sin eso, gritos y turba provocada no existen.
4. **Recompensa oro + objeto común** y **cofre**.
5. **Apuesta** (ADR 0157): censo de frecuencias, después implementación.
6. **Memoria visible**: estadísticas, apodos, Gaceta final. Todo lee `RunCareer` (ADR 0124).
7. **Clanes y némesis** (§3).
8. **Estadio**: árbitro visible y catálogo de turbas (F5 y F7 de Knavall).
9. **Clínica y herrero**; métrica guardiana de la sangre, que se mide en cada paso desde el 1.
