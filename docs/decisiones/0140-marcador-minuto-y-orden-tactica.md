# ADR 0140 — Marcador, minuto y orden táctica

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloques 6 y 7)
**Paquetes**: P6 y P7
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

Dos huecos que son el mismo hueco:

1. **La IA ignoraba el resultado por completo.** `TacticalState` salía únicamente de la posesión y la tabla
   de `data/ai/weights.json` sólo tenía entradas para esos cuatro estados. Cero referencias a diferencia de
   goles o a minuto restante en `Utility`. No existía «ir a por el empate» ni «defender la ventaja» — y por
   eso tampoco existía el **momento de remontada**, que el plan de evolución señala como el hueco real del
   espectáculo.
2. **La única decisión táctica del jugador era la alineación.** No había forma de decirle al equipo cuánto
   riesgo correr, aunque el ojeo previo sea completo y gratuito (RF-012b).

## Decisión

### 1 · La mentalidad es un **eje aparte**, no un quinto estado táctico

`TacticalState` se queda en cuatro y sigue saliendo sólo de la posesión. La mentalidad multiplica **encima**:

```
score = Base · Tactical/100 · Mentality/100 · TraitMult/100 + Context
```

`Mentality` es una tabla más en `data/ai/weights.json`, con tres filas (`Defensive`, `Neutral`,
`Offensive`) y una entrada por acción. **Es dato, no código.**

Se rechazó meterlo como estados tácticos nuevos —«InPossessionLosing», «OutOfPossessionWinning»— porque
multiplica las filas por tres y obliga a mantener a mano el producto de dos ejes independientes. La regla 17
del encargo dice lo mismo para las acciones: la complejidad sale de la interacción entre sistemas, no de
multiplicar casos.

### 2 · `Neutral` es 100 en todo, y eso hace la capa **exactamente inerte**

No es calibración: es la definición de la referencia. Y tiene una consecuencia que importa: en aritmética
entera `x · 100 / 100 == x`, así que un partido empatado con las dos órdenes neutras pasa por la capa
**sin cambiar ni un valor**. Todo lo que se mueva en un lote es atribuible a la urgencia o a la orden, nunca
a haber metido la capa. Está fijado por test.

### 3 · La urgencia mezcla, no salta

```
urgencia = min(100, |diferencia de goles| · urgencyPerGoalPercent) · (tiempo consumido en %) / 100
mentalidad efectiva = orden + (objetivo − orden) · urgencia / 100
```

- **Crece con el minuto**: el mismo 0-1 no pide lo mismo en el minuto 10 que en el 89.
- **Crece con el marcador y satura**: ir dos abajo aprieta más que ir uno; ir cuatro abajo no aprieta más
  que ir tres, porque un equipo no puede atacar «más que con todo».
- **Es una mezcla y no un salto** porque un equipo no cambia de carácter de golpe en el minuto 80: empieza
  a estirarse antes. Un umbral habría dado un motor que va como si nada y de pronto se vuelve loco.
- **El signo decide la dirección, no la cantidad**: ir ganando por uno y perdiendo por uno aprietan igual;
  lo que cambia es hacia qué mentalidad.
- En la **prórroga de turba** el tiempo reglamentario ya está consumido, así que la urgencia se queda al
  máximo que permita el marcador. Es exactamente lo que describe un gol de oro.

La fórmula vive como **función pura** (`MatchEngine.UrgencyPercent`) para poder afirmarla sin simular un
partido, que es el patrón que el repositorio ya usa con `Utility.OffsideLineColumn` y
`MatchEngine.ReachDistance`.

### 4 · La orden del jugador es estado **inicial**, y es una preferencia

`TeamSetup.Order` es `init`, como la alineación, los consumibles y las sustituciones forzadas (ADR 0094):
**todas las decisiones del jugador ocurren entre partidos**, así que no hay ninguna vía para cambiarla en
vivo y el motor no necesita una.

Y es una **preferencia, no una instrucción**: la urgencia la desplaza sin sustituirla. Un equipo al que le
quedan segundos y va perdiendo ataca aunque su orden fuera defensiva. Si la orden fuera absoluta, el jugador
podría dejar a su equipo sin reaccionar a un 0-1, que no es una decisión interesante sino una trampa.

Cuando la orden ya coincide con lo que pide el marcador —un equipo ofensivo que va perdiendo— la mezcla no
hace nada, que es justo lo que debe pasar.

## Las diez preguntas, resumidas

1. **Qué experimenta**: elige cómo quiere que juegue su equipo, y lo ve en el campo; y ve a su equipo
   estirarse cuando va perdiendo y quedan minutos.
2. **Qué decide**: arriesgar o conservar, con el rival ya conocido (RF-012b).
3. **Qué debería decidir**: eso. Hoy su única decisión táctica es dónde coloca a cada uno.
4. **Qué regla representa**: **es una regla nueva** y se declara como tal. Encaja con RF-012d porque es una
   preferencia anunciada, no un efecto oculto.
5. **Sistemas**: `/Sim` (`Utility.Choose`, `MatchSetup`), `/data` (`weights.json`), `/Game` (un selector,
   pendiente).
6. **Alternativas**: (a) sólo urgencia automática, sin orden — deja al jugador sin la decisión; (b) órdenes
   por jugador — multiplica la superficie y el encargo pide tres globales.
7. **Trade-off**: ofensivo concede espacio a la espalda; defensivo renuncia a la profundidad.
8. **Estrategias**: se combina con la plantilla y con la urgencia, que puede empujar al jugador fuera de su
   orden.
9. **Degeneración**: que «ofensivo» domine siempre. **Se vigila en la fase de balance, no aquí.**
10. **Demostración**: tests de que la capa es inerte en neutro, de que la urgencia crece con las dos cosas
    y satura, de que el motor le da a cada equipo la dirección que toca, y de que la orden **llega hasta el
    campo** (mismo partido, dos órdenes, resultados distintos).

## Valores publicados

`Neutral` es 100 por definición. Las otras dos filas son **valores de partida sin calibrar**, elegidos por
lo que cada orden *significa* y no contra ningún lote: ofensivo sube atacar el espacio, conducir, tirar, el
pase en profundidad y la presión, y baja replegar, cubrir, proteger y despejar; defensivo, al revés.
`urgencyPerGoalPercent` se publica en **60**, así que un gol de diferencia vale 60 y dos saturan.

## Consecuencias

- **Un partido con marcador ya no es el de antes**, porque la urgencia entra en cuanto alguien marca. Es el
  cambio de comportamiento más grande de todo el pass y es el que se buscaba.
- `/Game` necesita un selector de orden para que la decisión exista de cara al jugador. **No está hecho**:
  es presentación y va en su propio commit (`/Sim` y `/Game` no se mezclan).
- Los tests que construían `AiWeights` a mano necesitan la tabla nueva; se les da una neutra
  (`TestData.NeutralMentality()`), que por el §2 los deja midiendo exactamente lo que medían.
