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
