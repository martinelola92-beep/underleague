# Análisis comparativo de fórmulas: Brogue, DCSS, Shattered PD y CDDA

Encargo del revisor: analizar el balanceo de cuatro roguelikes de referencia y contrastarlo con el nuestro.

**Advertencia previa sobre qué transfiere.** Underleague no es un roguelike de combate por turnos: no hay puntos de vida, ni daño, ni tiempo hasta la muerte. Las fórmulas de mitigación, AC/EV y TTK **no se pueden copiar**. Lo que sí transfiere, y es lo valioso, es la **estructura matemática**: cómo se compone una resolución probabilística, cómo se controla la varianza, cómo se evita que los números se descontrolen tarde y cómo escala el poder con el nivel. Ahí las cuatro referencias tienen respuestas muy distintas y todas mejores que la nuestra en algún aspecto.

## 1. Las referencias

### Brogue — todo multiplicativo, nada lineal

```
hitProbability = accuracy × 0.987 ^ defense
```

Precisión base 100; cada punto de encantamiento **multiplica** por 1,065; cada 5,2 puntos de armadura **reducen a la mitad** la probabilidad de ser golpeado. Acierto y daño se resuelven por separado.

Lo importante no es la fórmula, es la elección: **exponencial, no aditiva**. Un punto de defensa vale un porcentaje, no una cantidad. Consecuencia: la mejora vale lo mismo en cualquier punto de la escala y **la probabilidad nunca llega a 0 ni a 1**, así que no hay saturación posible.

### DCSS — dos tiradas promediadas y trade-off explícito

El acierto compara una tirada uniforme contra el **promedio de dos aleatorios** sobre la evasión del objetivo, con un 2,5% de fallo y un 2,5% de acierto automáticos. La armadura resta una cantidad **aleatoria entre 0 y AC**.

Dos ideas: promediar dos tiradas produce una **distribución triangular** —los extremos se vuelven raros, el resultado se acerca a lo esperado— y llevar armadura pesada **sube AC y baja EV**, un trade-off estructural, no un adorno.

### Shattered Pixel Dungeon — escalado lineal y agresivo

Precisión del héroe = `nivel + 9`; armadura reduce `(2 + nivel) × tier`. Con 25 niveles, la precisión crece un **250%** a lo largo de la partida. La progresión se **siente** porque los números se mueven mucho.

### Cataclysm: DDA — todo en datos

Miles de objetos y monstruos definidos en JSON, con coberturas por parte del cuerpo. La lección es de arquitectura, no de fórmula: cuando el contenido vive en datos, el balance es un problema de tabla y no de código. **Esto ya lo hacemos.**

## 2. Diagnóstico de nuestro sistema

Nuestras resoluciones tienen todas la misma forma:

```
p = base + factor × (atributo del actor − atributo del oponente)     [ADR 0041]
```

acotada, en enteros sobre 10.000. Bases actuales: `pass` 7700 · `dribble` 7200 · `save` 5000 · `shotOnTarget` 4625 · `tackle` 2800 · `foul` 320 · `intercept` 250 · `card` 250 · `injury/injure` 40.

### Hallazgo 1 — somos aditivos donde Brogue es multiplicativo, y ya nos ha costado dos parches

Con una fórmula aditiva, **un mismo incremento vale cosas radicalmente distintas según la base**. Está medido en este proyecto: un `+5` sobre `injure` (base 40) multiplica por trece; sobre `pass` (base 7700) es un +6% relativo y encima choca con el techo.

Eso obligó a inventar la tabla de escalones por canal de la ADR 0035 —un paso vale 1 punto en `intercept` y 5 en `pass`— que **es un parche sobre un problema estructural**. Brogue no tiene ese problema porque no suma: multiplica.

### Hallazgo 2 — usamos una sola tirada uniforme donde DCSS promedia dos

`rng.Chance(p)` es una uniforme: máxima varianza posible. DCSS promedia dos tiradas y obtiene una triangular, con la mitad de desviación típica.

Consecuencia medida en nuestro proyecto: el error de una celda de la curva de puertas es de **±4 puntos** con 640 partidos, y la desviación entre plantillas generadas llegaba a **15 puntos**. Eso obliga a lotes enormes y, lo que es peor, **diluye la diferencia entre jugar bien y jugar mal**: si el ruido es de 4 puntos, una ventaja de 5 apenas se distingue. Es exactamente el problema que llevamos días persiguiendo con la separación entre doctrinas.

### Hallazgo 3 — nuestra progresión por nivel es muy plana

`budgetPerLevel = 8` sobre un presupuesto base de 250-300: del nivel 1 al 8 el jugador crece un **22%**. Shattered PD crece un 250% en su recorrido.

Es coherente con lo medido: antes de arreglarlos, los perks de acumulación valían 0,2-0,4 puntos de tasa de victoria. **La progresión dentro de la run casi no se nota**, y el revisor ha pedido explícitamente que superar la run signifique "haber hecho las cosas con cabeza".

### Hallazgo 4 — cada canal tiene su propio suelo y su propio techo

`pass` se acota a 500-9800, `save` a 5-95%, otros no se acotan explícitamente. DCSS usa **un solo par de límites** (2,5% y 97,5%) para todo. Nuestra dispersión de criterios hace que el comportamiento cerca de los extremos sea impredecible de un canal a otro.

### Hallazgo 5 — lo que sí hacemos bien

Aritmética entera y determinismo (Brogue y Crawl comparten esa disciplina); contenido en datos (CDDA); resoluciones separadas por fase, como Brogue separa acierto de daño; y un presupuesto de atributos que **es** un trade-off, en la línea del AC/EV de DCSS.

## 3. Propuesta

### P1. Los perks multiplican cuotas, no suman puntos

Sustituir el valor aditivo de `modifyProbability` por un multiplicador sobre las **cuotas** (`odds = p / (1 − p)`):

```
odds' = odds × k        p' = odds' / (1 + odds')
```

Multiplicar cuotas funciona igual de bien cerca de 0 que cerca de 1, que es justo lo que la multiplicación directa de probabilidad no consigue. Con `k = 1,3`:

| Canal | Base | Con `+1 paso` hoy | Con `×1,3` en cuotas |
|---|---|---|---|
| `intercept` | 2,50% | 3,50% (**×1,40**) | 3,23% (×1,29) |
| `tackle` | 28,00% | 31,00% (×1,11) | 33,6% (×1,20) |
| `pass` | 77,00% | 82,00% (×1,06) | 81,3% (×1,06 en p, ×1,3 en cuotas) |
| `injure` | 0,40% | 1,40% (**×3,50**) | 0,52% (×1,30) |

**Elimina la tabla de escalones de la ADR 0035 de raíz**: un perk vale lo mismo en cualquier canal por construcción, y desaparece la clase de error que ya nos ha mordido dos veces (la habilidad élfica que valía cuatro veces su presupuesto y la build "mala" que ganaba con tres copias de un perk de intercepción).

Coste: las descripciones cambian de *"+5% de probabilidad de interceptar"* a *"un 30% más de probabilidad de interceptar"*, que es honesto pero exige cuidado en el texto (`estilo-descripciones.md` ya advierte de la confusión entre puntos y proporciones).

### P2. Promediar dos tiradas en las resoluciones decisivas

Sustituir `rng.Chance(p)` por el promedio de dos tiradas en tiro, parada, entrada y regate —no en las de alta frecuencia, para no cambiar el ritmo del partido—. Reduce la desviación típica del resultado en torno a un 30% sin tocar ninguna media.

Efecto esperado, y es el que más nos interesa: **el mejor equipo gana más a menudo**, el ruido de medición baja y la ventaja de jugar bien se vuelve visible con lotes más pequeños. Es la propuesta con mejor relación entre coste y beneficio.

### P3. Curva de nivel más agresiva

`budgetPerLevel` de **8 a 14**, con los presupuestos base ajustados para conservar la relación de la ADR 0027 (un común de nivel 8 ≈ un raro de nivel 2).

### P4. Un solo suelo y un solo techo

Límites únicos de **2% y 98%** para toda probabilidad, como el 2,5% de DCSS. Predecible en todos los canales y elimina los acotados ad hoc.

## 4. Antes y después

| Parámetro | Ahora | Propuesto | Motivo |
|---|---|---|---|
| Efecto de perk sobre probabilidad | `+N puntos`, escalón por canal (1/3/5) | **`×k` sobre cuotas**, k ∈ {1,15 · 1,3 · 1,5 · 2} | Brogue: multiplicativo, sin saturación |
| Tabla de escalones (ADR 0035) | 13 canales con su paso | **se retira** | Deja de hacer falta |
| Tirada de tiro, parada, entrada, regate | 1 uniforme | **promedio de 2** | DCSS: menos varianza, más peso de la habilidad |
| Suelo/techo de probabilidad | por canal (500-9800, 5-95%…) | **2%-98% global** | DCSS: comportamiento uniforme |
| `budgetPerLevel` | 8 | **14** | Shattered PD: la progresión debe notarse |
| `budgetByRarity` común/poco común/raro | 250 / 275 / 300 | **250 / 287 / 324** | Conserva común n8 ≈ raro n2 (ADR 0027) |
| Crecimiento del nivel 1 al 8 | +22% | **+39%** | Que sobrevivir y subir de nivel importe |
| Error esperado por celda (640 partidos) | ±4 puntos | **±2,8 puntos** | Consecuencia de P2 |

## 5. Orden de aplicación y riesgos

1. **P2 primero** (dos tiradas): es el más barato, el que menos cambia el diseño y el que mejora la calidad de todas las mediciones siguientes. Hacerlo antes que nada significa que el resto se mide con menos ruido.
2. **P4** (límites únicos): trivial y elimina inconsistencias.
3. **P1** (cuotas multiplicativas): el de mayor alcance. Obliga a reescribir los valores de los 45 perks y a revalidar RT-056, la curva de puertas y las seis puertas. **No hacerlo a la vez que P3.**
4. **P3** (curva de nivel) al final, y midiendo la relación de la ADR 0027, que es la que se rompe si se hace mal.

**Riesgo transversal**: P1 y P3 tocan el equilibrio completo. Cada uno exige su propia ronda de revalidación, y hacerlos juntos haría imposible atribuir un desajuste a su causa — que es exactamente el error que ya cometimos en la fase 1 y que costó dos paquetes de trabajo desenredar.

## Fuentes

- [Brogue: combate (wiki)](https://brogue.fandom.com/wiki/Combat) · [BrogueCE](https://github.com/tmewett/BrogueCE)
- [DCSS: Armour class](http://crawl.chaosforge.org/Armour_class) · [Evasion](http://crawl.chaosforge.org/Evasion) · [crawl](https://github.com/crawl/crawl)
- [Shattered Pixel Dungeon: stats](https://pixeldungeon.fandom.com/wiki/Shattered_Pixel_Dungeon/Stats) · [repositorio](https://github.com/00-Evan/shattered-pixel-dungeon)
- [Cataclysm: DDA](https://github.com/CleverRaven/Cataclysm-DDA)
