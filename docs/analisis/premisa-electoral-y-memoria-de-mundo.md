# La premisa electoral y la memoria de mundo — revisión crítica

**Fecha:** 20 sep 2026. **Tipo:** revisión de concepto como diseñador, no como escritor de lore.
**Estado:** propuesta y crítica. **No se ha cambiado ni una línea del juego.**

Continúa `docs/analisis/auditoria-conceptual-narrativa.md` (rivales, personalidad, perks: ahí está la base
empírica y **no se repite aquí**) y la **ADR 0122**. Etiquetas: **MEDIDO · LEÍDO · DERIVADO · INFERIDO**,
con **CONFIRMED / LIKELY / SIN EVIDENCIA**.

---

## 0. El hecho que condiciona todas las respuestas

**No existe ninguna memoria entre runs, ni el sitio donde guardarla.** *(CONFIRMED, verificado.)*

`Sim/Run/Save/` contiene **un solo fichero**: `RunSave.cs`. No hay guardado de perfil, ni de campaña, ni de
compendio. Y sin embargo:

- **RF-014b** promete que el modificador de jefe descubierto «queda registrado en el compendio y pasa a ser
  visible desde el inicio del acto **en todas las runs posteriores**». El código lo tiene preparado *dentro*
  de la run (`MapNode.BossModifierRevealed`) y comentado como «lo que se registra en el compendio del
  perfil» — **pero no hay perfil**.
- **RF-126** promete que se desbloquean perks, objetos y consumibles «que se incorporan al conjunto
  disponible en runs posteriores». Sin implementar.

**Consecuencia directa para tu encargo**: los puntos §6 (el mundo no se reinicia), §7 (world memory), §18
(campeones anteriores) y §19 (meta-progresión) **no son extensiones de algo que exista. Son un sistema de
persistencia nuevo, entero.** Es, con diferencia, la parte más cara de la nueva premisa.

Y contrasta con el hallazgo del documento anterior: **la rivalidad DENTRO de una run cuesta cero cambios de
esquema**, porque los rivales ya reaparecen y el emparejamiento y el resultado ya están guardados.

**De ahí sale la recomendación de orden que gobierna todo este documento** *(y es la afirmación más
importante que voy a hacer)*:

> **Haz funcionar primero la rivalidad dentro de una run, que es gratis. Solo si eso produce la sensación,
> construye la memoria entre runs, que es cara.**
> Si cruzarte tres veces con los Barbahierro **en la misma run** no te hace decir nada en voz alta,
> cruzártelos en la run siguiente tampoco lo hará — y habrás pagado una capa de persistencia nueva para
> descubrirlo.

---

## A. ¿Funciona mejor la premisa electoral?

**Respuesta corta: es mejor como MARCO y peor como GENERADOR DE STAKES. No son excluyentes, y combinarlas
arregla el defecto de cada una.**

### Lo que la premisa electoral resuelve, y es mucho

1. **Justifica diegéticamente el reinicio roguelike.** Esto es raro y valioso: la mayoría de los roguelites
   no lo consiguen y lo tapan con muerte y bucle temporal. «Ha empezado una elección nueva» es limpio, no
   necesita explicación mágica y **elimina la pregunta de por qué vuelvo a empezar desde abajo**.
2. **Justifica el nombre.** «Underleague» = el recorrido desde abajo. Hoy el nombre no significa nada.
3. **Justifica la institución multirracial.** Por qué un clan orco y uno elfo compiten bajo las mismas
   reglas: porque los dos son candidaturas.
4. **Justifica la escalada de dificultad.** Subir de acto deja de ser una convención de videojuego.
5. **Da nombre a la victoria.** «Gobernador» es mejor objetivo que «ganar la fase 3».

### Lo que la premisa electoral rompe, y hay que decirlo

1. **Hunde el stake del partido individual, que es donde vive el juego.** La premisa anterior —los clanes
   resuelven sus rencillas en el campo— ponía algo en juego **en cada partido**: territorio, deuda, honor.
   La electoral solo pone algo en juego **en la final**. Los otros 19 partidos de la run pasan a ser
   *clasificatorios*. **Es una regresión narrativa en 19 de 20 partidos**, y el juego se juega en esos 19.
2. **Empuja hacia lo político, que es justo lo que no quieres.** Cuanto mejor expliques la elección, más
   preguntas de gobierno invitas.
3. **Convierte al clan en candidato en vez de en residente.** Se pierde el «esto es mi casa y mi gente», que
   es la base afectiva de una plantilla que se rompe.
4. **«Gobernar» es una promesa que hay que pagar.** Si ganar da un título y nada más, la premisa se siente
   hueca la segunda vez.

### La síntesis que recomiendo

> **La elección es la escalera. Las rencillas son los peldaños.**

Cada partido de la run sigue siendo **una disputa real entre dos candidaturas** que la Underleague resuelve
—una deuda, una mina, una afrenta, un derecho de paso—, y ganar el torneo entero te hace Gobernador. Así:

- Se conserva la justificación del reinicio y el objetivo con nombre.
- **Vuelve a haber algo en juego en cada partido**, que es lo que la premisa electoral pierde sola.
- Y cuesta **un campo de dato en el nodo**: una línea de «qué se juega aquí». Es el mismo cartel de nodo
  que el documento anterior ya proponía para la rivalidad, ahora con contenido.

**Veredicto: adopta la premisa electoral, pero no sustituyas con ella la de las rencillas. Súmalas.**

---

## B. ¿Tiene sentido que se gobierne por un torneo?

**Sí, y la explicación interna correcta es procedimental, no filosófica.** No hay que justificar que sea
*justo*. Hay que justificar que **funciona**, y ahí está el chiste y la lógica a la vez:

> La democracia producía empates, bloqueos, impugnaciones y gobiernos de cuatro meses.
> El torneo produce **exactamente un ganador, siempre, en una fecha conocida**.
> **La Underleague no es justa. Es decidible.** Y eso era lo único que a la democracia le faltaba.

Es suficiente, es absurdo y es coherente. Y no hay que repetirlo: basta con que la burocracia se comporte
como si fuera obvio.

**Qué dejar deliberadamente ambiguo** (mismo criterio que el documento anterior):
- **Por qué fútbol y no otra cosa.** Nadie lo recuerda. Explicarlo mata el chiste y cierra la puerta a
  cualquier regla absurda futura.
- **Si alguna vez funcionó mejor.** Sugerirlo, no confirmarlo.
- **Cuánto gobierna de verdad el Gobernador.** Ver C.

**Dónde vive el tono:** en el comunicado institucional, nunca en la voz del pregonero que canta los goles.
El ejemplo del Ministerio Electoral de tu encargo es exactamente el registro correcto.

---

## C. Qué significa gobernar

**Regla de diseño: gobernar debe ser una consecuencia sobre el MUNDO, nunca una capa de juego.** En cuanto
el jugador administre algo, Underleague deja de ser lo que es.

**Recomendación, una sola y concreta:**

> **Ganar no te da poder. Te convierte en el rival de tu próxima candidatura.**

El clan campeón queda registrado como **gobierno en ejercicio**, y en la run siguiente aparece en el torneo
como la candidatura a batir, con la plantilla con la que ganó. Si perdiste, el que queda registrado es **el
clan que te eliminó**.

Esto es lo mejor que ofrece la nueva premisa, por tres razones:

1. **Es narrativa pura y no cuesta ni una línea de texto escrito.** «El gobierno lo tiene el clan que me
   eliminó la última vez» es una frase que el jugador construye solo.
2. **Da consecuencia a perder sin castigar.** Perder no resta nada: cambia el mundo.
3. **Es el único mecanismo de este documento capaz de producir «joder, otra vez ellos» entre partidas.**

**Lo que NO debe dar gobernar**: ni atributos, ni oro inicial, ni perks garantizados. Tu §19 tiene razón y
esta recomendación la respeta: el progreso es **saber más**, no ser más fuerte.

*Opcional, y solo si la pieza anterior funciona*: el Gobernador deja un **mandato** — una regla menor y
visible de la Underleague que rige la siguiente elección. Es el sitio natural para el humor burocrático.
**No lo implementes hasta tener lo demás**: es fácil que trivialice o que confunda.

---

## D. Cómo funciona una elección

**La mejor noticia del encargo: la estructura que pides ya existe y no hay que cambiar nada.** *(LEÍDO)*

| lo que pides | lo que hay hoy |
|---|---|
| empezar desde abajo | acto 1, 6 partidos |
| fases sucesivas | 3 actos, `nodesPerAct [11,12,12]` |
| eliminatorias | los nodos de élite (mismo rival, +2 niveles) |
| final | el jefe del acto 3 |
| ganador | fin de run victorioso |
| candidaturas rivales | 5 rivales por acto, 15 en total |

**20 partidos por run** (6+7+7), 3 de ellos jefes, 17 contra `data/rivals/`. `matchesPerFullRun` tiene banda
18-22 y está dentro.

**Lo que hay que hacer es renombrar, no rediseñar**: acto = fase electoral; jefe = la candidatura que
preside esa fase; el jefe del acto 3 = la final. **Cero cambios de motor, cero de balance.** Esto es un
argumento fuerte a favor de la premisa: encaja en la forma que el juego ya tiene medida y en banda.

**Frecuencia**: cada run es una elección. No hace falta un calendario; basta con que el comunicado la
numere, y ese número **sí** es memoria de mundo (ver F).

---

## E. Cómo funciona la run

Sin cambios respecto a lo anterior. Dónde puede desviarse: el mapa de cuatro carriles ya da **1,90 nodos
elegibles por paso** y 47 % de pasos con tipos distintos *(MEDIDO)*. Dónde puede morir: hoy el 92 % de las
derrotas históricas eran un jefe.

**Un aviso crítico sobre tu §5.** Dices que al perder «la candidatura termina». Hoy **la run no es ironman
puro** en el sentido narrativo: se pierde por eliminación. Encaja bien. Pero cuidado con reforzar la
metáfora electoral hasta el punto de hacer que perder duela más de lo que ya duele: `runWinRate` está en
**22-27 %**, o sea que **tres de cada cuatro candidaturas fracasan**. La premisa tiene que hacer que eso se
sienta normal —es una elección, se pierde— y no humillante. *La burocracia ayuda: el comunicado que
certifica tu derrota con absoluta indiferencia es más gracioso que triste.*

---

## F. Qué persiste entre runs

**Principio, y es una restricción dura que recomiendo escribir en la ADR:**

> **El perfil guarda NOMBRES y HECHOS. Nunca NÚMEROS que el jugador pueda gastar.**

### Lo mínimo que merece la pena, por orden de valor/coste

| # | qué | por qué | coste |
|---|---|---|---|
| 1 | **El gobierno en ejercicio**: clan campeón + su plantilla + número de elección | produce §C entero y el «otra vez ellos» | medio: exige serializar un equipo rival, que hoy no se serializa |
| 2 | **Modificadores de jefe descubiertos** (RF-014b) | **ya está preparado dentro de la run**, solo falta el vasija | bajo |
| 3 | **Contenido desbloqueado** (RF-126) | progresión por descubrimiento, no por poder | bajo |
| 4 | **Figuras conocidas**: un puñado de jugadores rivales con los que hay hechos | es Thrainn | medio |
| 5 | Crónica de elecciones anteriores (una línea por run) | contexto barato, sabor | muy bajo |

### Lo que NO debe persistir, y es importante

- **Nada que sume atributos, oro o perks garantizados.** Tu §19 lo dice y lo suscribo.
- **El estado físico de los rivales.** Que Thrainn vuelva **curado** es lo que la historia necesita: «Thrainn
  regresa» es el tercer acto de tu ejemplo. Si volviera cojo para siempre, el arco se rompe.
- **Relaciones, reputación, facciones, favores.** No hay un solo argumento sistémico que los justifique hoy,
  y tu encargo ya los excluye. Coincido sin reservas.

### La restricción que nadie suele poner y que hace falta: **la fama tiene que ser escasa**

Si cualquier jugador rival con un hecho entra en el perfil, a las diez runs hay doscientos nombres
«importantes» y ninguno lo es. **Recomiendo un cupo duro** —del orden de 8 a 12 figuras conocidas— con
desalojo del menos relevante. *Un mundo con doscientos personajes memorables es un mundo sin ninguno.*

### Y una advertencia de arquitectura

El perfil **no puede vivir en `/Sim`**: `/Sim` no hace E/S (RT-012). Tiene que ser el mismo patrón que
`RunSave` —`/Sim` produce y consume un documento, `/Game` lo escribe—, y **versionado desde el día uno**,
con la misma regla de que nunca se migra en silencio.

---

## G. Rivales recurrentes

El mecanismo **dentro de la run** está resuelto en el documento anterior y cuesta cero esquema: los rivales
ya reaparecen (garantizado por palomar en los actos 2 y 3), el emparejamiento y el resultado ya están
guardados, la identidad del jugador rival ya es estable (`OpponentFirstPlayerId + índice`), y lo único que
falta es dejar de descartar el hecho en `MatchResolution.cs:96`.

**Entre runs**, la capa mínima es la fila 1 y la fila 4 de la tabla de F, y **solo debe construirse después
de que la de dentro de la run demuestre que funciona.**

**Cómo asciende un rival a figura, sin un sistema de reputación:** por **umbral de hechos**, no por decisión
del diseñador. Te ganó, le ganaste dos veces, lesionó a alguien tuyo, lo lesionaste, te mató a alguien. La
selectividad que pides en tu §9 sale sola: la mayoría de los clanes no acumulan nada.

---

## H. Personalidades

Resuelto en el documento anterior, y resumo por ser la pregunta que más te preocupa: **la capa de intención
ya existe en los rasgos** —`Coward` lleva `{Tackle: 45, Block: 35}`, `Aggressive` `{Tackle: 160, Block:
175}`— pero se sortea al generar, son diez y **ningún perk puede tocarla**. El canal mutable por acción y
por tick (`modifyUtility`, C1) está construido y **no está en el esquema**.

Intervenciones por coste: **(1)** rasgos nuevos en `/data`, coste cero de motor; **(2)** abrir C1 (ya
acordado, Fase 1B de la ADR 0122); **(3)** una sola cláusula situacional.

**Lo que la premisa electoral añade aquí, y es útil:** una candidatura es pública, así que **hay motivo
diegético para que el juego te enseñe los rasgos de tus jugadores y los del rival** — hoy ni siquiera se
comunican. El «dossier de candidatura» es el sitio natural.

---

## I. Cuántos eventos

**Pocos, y no son el vehículo.** *(MEDIDO)* Las cartas de evento están hoy a **0,38 por run** porque la
política de nodos las esquiva (Evento puntúa 25 contra Mercado 90 y Clínica 100), y subirlas compite con el
mercado, que es la decisión que más gana. Con 6 cartas a esa frecuencia, un jugador ve una carta cada dos
runs: **sin reincidencia no hay reconocimiento**.

La narrativa que pides no sale de más cartas. Sale de **la recurrencia**, que ya ocurre, y de **la
atribución**, que no. Tu §8 y tu §17 apuntan al sitio correcto.

---

## J. Duración

**No la toques, y ya está medido** *(MEDIDO, 500 partidos)*. El **67,8 %** de los partidos ya contiene un
suceso excepcional: lesión 53 %, prórroga 27,6 %, roja 12 %, incomparecencia 4 %. **Tu objetivo de 1-3
momentos memorables por partido es compatible con el ritmo actual** (4,8 presentaciones por minuto) y lo
respaldo como objetivo.

El problema no es cuántas cosas pasan: es que **14 de los 26 tipos de evento no llegan nunca al jugador**.
Alargar el partido produce más material que tampoco se recordará.

Objetivos medibles que propongo: **20 partidos por run** (ya), **1-3 momentos con protagonista nombrado por
partido** (hoy: el gol y la muerte, poco más), **≥1 encuentro repetido reconocido por run** (hoy: ocurre y
no se reconoce).

---

## K. Qué adoptar de Pokémon y de los roguelites

La estructura «empiezas abajo, superas una cadena de desafíos, llegas a un campeón» ya la tienes. Lo que
merece la pena robar es más preciso:

1. **El rival con nombre que reaparece en hitos fijos y escala contigo** (Gary/Blue). Es el mecanismo
   narrativo más eficiente que existe en el género: un solo personaje, cero texto, reaparición pautada.
   Aquí se traduce en **un clan marcado por la run que te acompaña de acto en acto** — y el documento
   anterior ya identifica la pieza que falta: un **`clanId`** en el esquema de rival para que un clan pueda
   cruzar de acto. Cuesta 15 ficheros y un campo.
2. **El campeón es el jugador anterior** (Red en Oro/Plata). **Esta es la joya, y encaja con tu premisa
   como un guante**: en la elección N+1, la candidatura a batir es la que ganó la elección N — tu propio
   clan campeón, o el que te eliminó. Ver C.
3. **Los cuatro elementos = los tres jefes.** Ya están.
4. **Lo que NO hay que adoptar de Pokémon: la progresión permanente.** Tu §4 ya lo blinda y estoy de
   acuerdo; la fila 1 de F es lo único que cruza, y es un nombre, no una ventaja.

---

## Roadmap, integrado con la ADR 0122

**No propongo un plan nuevo. Propongo dónde encaja esto en el que ya está acordado.**

| fase | qué | coste | estado |
|---|---|---|---|
| **1A** | memoria del jugador propio | pequeño + un trozo de motor | ADR 0122, acordado |
| **1B** | abrir `modifyUtility` + rasgos nuevos en `/data` | pequeño | ADR 0122, acordado |
| **1C** | memoria de rival **dentro** de la run + `clanId` | **cero esquema** | propuesto, doc anterior |
| **2** | que el partido cuente lo que ocurre | pequeño, `/Game` | ADR 0122, acordado |
| **R** | **re-skin electoral**: nombres de fase, comunicados, cartel de «qué se juega» en el nodo | **muy bajo, solo texto y dato** | nuevo |
| **3** | turba, prótesis, nemesis | medio-alto | ADR 0122 |
| **4** | **perfil entre runs**: gobierno en ejercicio, compendio (RF-014b), desbloqueos (RF-126), figuras conocidas con cupo | **alto: sistema de persistencia nuevo** | nuevo, **al final** |

**La fase R puede adelantarse casi a cualquier punto** porque no toca el motor: es el cambio con mejor
relación premisa/coste de todo el documento, y permite probar el tono sin comprometer arquitectura.

**La fase 4 va al final por la razón del §0**, y lo repito porque es la recomendación que más fácil sería
ignorar: **la memoria entre runs es la parte más cara y la menos frecuente. La de dentro de la run es
gratis y ocurre veinte veces por partida. Empieza por donde el jugador va a estar mirando.**

---

## Riesgos y conflictos, dichos explícitamente

1. **No hay vasija para la memoria de mundo.** §6, §7, §18 y §19 requieren un guardado nuevo, versionado,
   fuera de `/Sim` por RT-012. Es el mayor coste oculto de la nueva premisa.
2. **RF-014b y RF-126 están declarados y sin implementar** — el mismo patrón «el texto promete lo que el
   código no hace» que ya tiene ocho casos registrados, ahora también en la capa meta.
3. **La premisa electoral, sola, vacía de stakes 19 de cada 20 partidos.** Combinarla con las rencillas no
   es adorno: es lo que la hace funcionar.
4. **Serializar un equipo rival campeón es nuevo.** Hoy los rivales se construyen de `/data` y nunca se
   guardan (`RivalTeamBuilder`: «solo existen mientras dura una llamada a `Simulator.Run`»).
5. **La fama sin cupo destruye la fama.**
6. **Una sexta raza (vampiros) sigue sin recomendarse**: la ADR 0092 cerró el abanico racial a 6,4 puntos a
   propósito. Los vampiros salen mucho más baratos y más memorables como **clan** de los no-muertos.
7. **`runWinRate` 22-27 %**: tres de cada cuatro candidaturas fracasan. El tono tiene que absorber eso.
