# 0126 — Clanes canónicos: un nombre, un clan, un puesto. La identidad y la potencia se separan

Estado: **Propuesta — decisión del revisor sobre el qué, arquitectura por decidir sobre el cómo**
(22 sep 2026). Sigue al análisis `scratchpad/analisis-equipos-cerrados.md`. **Enmienda RF-015.**
Toca `/data` y `/Sim`, así que exige `balance-measure` (Regla D) y `independent-reviewer` (Regla E).

## Lo que pide el revisor

> «"Grimnir Cavaprofundo" debe jugar solo en un clan y ser siempre, por ejemplo, un delantero. Así es más
> reconocible y memorable. Cada raza tiene, por ahora, **2 clanes**, y en cada equipo sus jugadores son
> siempre los mismos. Eso implica crear más nombres y dejar de aleatorizar los equipos. Esto nos permitiría
> jugar en otra fase a mover un poco sus estadísticas y naturalezas para crear equipos con más
> personalidad (por ejemplo "Muro de Escudos" es más defensivo que "Reyes de Hierro", más agresivos).»

## El problema, medido

*(MEDIDO, 22 sep 2026, sobre los 15 ficheros de `data/rivals/`.)*

- **73 nombres únicos** en total.
- **50 de los 73 aparecen en más de un equipo.**
- **49 cambian de puesto entre equipos.** «Grimnir Cavaprofundo» es **defensa, portero y centrocampista**
  según el acto; «Ivarr Forjagrís» es defensa, delantero y centrocampista.

Los ficheros de una raza son una **ventana deslizante sobre una lista de nombres**, no tres clanes. Un
nombre no identifica a nadie, así que no puede haber reconocimiento — y sin reconocimiento no hay la
anécdota que la ADR 0123 persigue.

## El obstáculo que descarta la solución barata

La solución evidente sería **un fichero por clan más `Progression.LevelUp` por acto**, reutilizando el
mecanismo que ya escala los nodos élite. **No sirve**, y conviene dejar escrito por qué:

*(MEDIDO)* La curva real por acto, en suma de los cinco atributos por jugador:

| acto | nivel declarado | suma media |
|---|---|---|
| 1 | 1 (no-muertos 2) | **227-237** |
| 2 | 3 (no-muertos 4) | **299-313** |
| 3 | 5 | **358-383** |

Son **+72 por jugador** entre el acto 1 y el 2. Pero `Progression.LevelUp` suma
`(niveles) × attributesPerLevel` a **cuatro** atributos, y `attributesPerLevel` vale **2**: eso son
**+8** por jugador para el mismo salto. **Un factor de nueve.**

**Conclusión: el campo `level` de un rival es decorativo para su fuerza.** Su potencia está en los
atributos escritos a mano, que no siguen la progresión del jugador. Escalar con `LevelUp` haría a los
rivales de los actos 2 y 3 drásticamente más débiles y dispararía `runWinRate`.

## Decisión de arquitectura propuesta

> **El clan define la IDENTIDAD y la FORMA. El acto define la POTENCIA.**

- **Un fichero por clan** (10: 5 razas × 2), con diez jugadores de nombre propio, **puesto fijo**, rasgos,
  `styleTag`, perks y un **reparto de atributos de referencia**.
- **El acto aporta la potencia** mediante una tabla de escalado en `/data`, calibrada para **reproducir la
  curva medida** (227 → 299 → 365). No se inventa una curva nueva: se conserva la que ya está en banda.
- **Los diez clanes están disponibles en los tres actos.** Es lo que produce el reencuentro —«los
  Barbahierro otra vez, y más fuertes»— que es el objetivo entero. Hoy un clan no puede cruzar de acto.

Esta separación es además **el prerrequisito de la fase que el revisor anuncia**: «mover un poco sus
estadísticas para crear equipos con más personalidad» se convierte en tocar la **forma** del clan, sin
tocar su **potencia**, que es lo que mantiene el balance por acto. *Muro de Escudos y Reyes de Hierro
pueden repartir los mismos puntos de forma distinta.*

## Lo que cuesta

- **27 nombres nuevos** (de 73 a 100). Es lo más barato del paquete.
- **10 ficheros de clan** nuevos, retirando los 15 actuales.
- Una **tabla de escalado por acto** en `/data` y su aplicación en `RivalTeamBuilder`.
- `RivalCatalog.OfAct` deja de seleccionar por acto y pasa a seleccionar por clan + potencia.

## Lo que se pierde, y hay que decirlo

**Variedad de plantillas: de 15 rosters distintos a 10.** Es el intercambio que la propuesta acepta a
cambio de reconocimiento, y es deliberado. Pero **no se ha medido** cuánto pesa el eje rival en la varianza
de una run, así que el efecto sobre la sensación de repetición entre runs es **SIN EVIDENCIA**.

## Riesgos

1. **Balance.** La curva por acto está hoy escrita a mano y en banda. Cualquier escalado calculado que no
   la reproduzca mueve `runWinRate` (22-27 %) y `deathsPerRun` (1,67). *Criterio: la suma media de
   atributos por acto debe quedar dentro de ±5 de la tabla medida arriba, y las 43 puertas emparejadas.*
2. **El desnivel entre clanes.** Hoy los rivales de un acto son casi equipotentes, y eso es lo que hace que
   `runWinRate` no dependa del camino. Dar personalidad introduce **dificultad dependiente del camino**:
   sube la varianza aunque la media no se mueva, y una puerta que mide la media no lo detecta.
   *Mitigación: diferenciar en forma manteniendo constantes la suma de atributos y el recuento de perks
   dentro de cada acto* — la misma regla de suma cero que la ADR 0092 impuso a las razas.
3. **El acto 1 no tiene doctrina hoy** *(MEDIDO)*: sus cinco rivales van sin `styleTag`, con 1-2 perks y
   1-2 rasgos. Parece un olvido, y es el acto que todo el mundo juega.
4. **Enmienda RF-015**, que dice que los rivales son estáticos «por acto y división». Con clanes canónicos
   son estáticos **por clan**, y el acto pasa a ser una banda de potencia. *Esto también evita el
   multiplicador de RF-015: 5 razas × 3 actos × 5 divisiones serían 75 ficheros a mano; con esta
   separación son 10 clanes y una tabla.*

## Lo que esta ADR NO decide

- La personalidad concreta de cada clan (es la fase siguiente que el propio revisor anuncia).
- Si los clanes se reparten por el mapa de otra forma (`MapGenerator` **no se toca**: cambiarlo invalida la
  referencia de balance de todas las semillas).
- El nombre canónico de los diez clanes.

## Medición previa, antes de escribir 100 jugadores

**El censo de encuentros por clan**, que sigue sin hacerse y que esta ADR necesita: barrer
`MapGenerator.Generate` sobre N semillas recorriendo el camino de `RunPolicy.ChooseNode` y contar **cuántos
clanes distintos ve una run y cuántas veces se repite cada uno**. Sin partidos, segundos de cómputo.

Discrimina entre dos mundos: si con 10 clanes disponibles en tres actos el jugador se cruza 2-3 veces con
el mismo, la propuesta compra la rencilla entera. Si se cruza 1,1 veces, compra solo la escalada, y
entonces hay que hablar de `MapGenerator` — que es una ADR distinta y mucho más cara.

## Hermanos

`scratchpad/analisis-equipos-cerrados.md` · ADR 0123 (gates 2 y 4) · ADR 0092 (no se reabre) ·
`docs/pendientes/BE-C.md`

---

## Censo de encuentros (22 sep 2026) — MEDIDO, y desbloquea esta ADR

Instrumento: `Sim.Tests/Analysis/_RivalCensusTests.cs` (temporal, `Category=Diagnostic`), 500 semillas,
recorriendo el camino con la **política real** (`RunPolicy.ChooseNode` acoplada, no reimplementada), sin
jugar partidos, excluyendo el nodo de jefe.

| métrica | run completa | acto 1 | acto 2 | acto 3 |
|---|---|---|---|---|
| partidos de liga/élite | 16,58 | 4,85 | 5,87 | 5,85 |
| rivales distintos (media/mín/máx) | **11,46** / 8 / 14 | 3,50 / 2 / 5 | 4,04 / 2 / 5 | 3,92 / 2 / 5 |
| repeticiones del más repetido (media/máx) | **2,48** / 4 | 2,01 / 3 | 2,19 / 3 | 2,23 / 4 |
| runs con ≥1 rival repetido | **100,0 %** | 90,6 % | 98,6 % | 99,2 % |
| runs con algún rival 3+ veces | **47,2 %** | 10,8 % | 20,4 % | 23,4 % |

**El reencuentro ya existe y es universal.** No hay una sola run de las 500 que no repita rival, y casi la
mitad se cruza tres veces o más con alguno. La causa es aritmética y **dentro del acto**: 5-6 partidos
contra un conjunto de 5 ids.

### Qué cambia esto en la ADR

**Reencuadra la propuesta, y la refuerza.** Los clanes canónicos **no tienen que crear el reencuentro**:
ya ocurre en el 100 % de las runs. Lo que compran es que **sea reconocible**. Hoy el jugador se cruza dos
veces con el mismo fichero y **no puede saberlo**, porque los nombres rotan entre actos y el mismo nombre
cambia de puesto. *Toda la materia prima está ahí y se está tirando por un problema de rotulado.*

### Corrección de una corrección

La primera versión de `auditoria-conceptual-narrativa.md` §0.1 afirmaba que la repetición estaba
«garantizada por palomar en los actos 2 y 3». Se retiró el 22 sep por **falsa en su derivación** —
`nodesPerAct` son capas, no nodos. Esta medición muestra que **la conclusión sí era cierta**, por una vía
distinta: el palomar opera dentro del acto, con 5-6 partidos contra 5 ids. *La afirmación era correcta por
accidente y ahora está medida; el razonamiento que la sostenía seguía siendo inválido.*

### Límite declarado de esta medición

Mide el **catálogo de hoy** (15 equipos con ámbito de acto), **no** el pool de 10 clanes compartidos entre
los tres actos que esta ADR propone. Para responder con precisión a «2-3 veces contra 1,1» en ese mundo
haría falta repetir el censo con un conjunto sintético de 10 ids en los tres actos. **No se hizo a
propósito**: es una decisión de qué mundo simular, no de cómo medir. Queda como el siguiente paso antes de
escribir los diez ficheros.
