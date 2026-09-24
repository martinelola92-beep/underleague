# ADR 0145 — Represalia, y la turba sin árbitro

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloques 14 y 15)
**Paquetes**: P12 y P13
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

Las dos reglas de identidad que le faltaban al motor, y las dos por el mismo motivo: **una lesión no tenía
ninguna consecuencia social en el campo**, y **la turba era una etiqueta**.

- La auditoría de identidad lo dejó medido: `CheckEndConditions` hace cuatro cosas al entrar en la prórroga
  de gol de oro y **ningún cambio de regla**, en el 27,6 % de los partidos. Mientras tanto **RF-055d dice
  que la turba es el único tramo del partido sin árbitro**.
- Y la atribución de lesiones existe desde la **ADR 0124** (`KillerPlayerId`, quién lesionó a quién) sin
  que nadie en el campo la use para nada.

## Decisión

### 1 · La represalia es un **sumando temporal**, no una orden

Cuando a un jugador le rompen, sus compañeros **que estaban cerca** pasan a valorar más entrarle o cargarle
**a ese rival concreto** durante un rato.

Tres decisiones que la definen, y cada una responde a algo que el encargo pide expresamente:

- **Es un sumando en la misma comparación**, no un objetivo impuesto. El encargo prohíbe que sea «lesión →
  ataque automático al culpable», y la forma de cumplirlo no es poner un tope: es que el jugador siga
  decidiendo. Si entrar no era buena idea, sigue sin serlo.
- **Sólo los que estaban cerca**, no el equipo entero. Uno de diseño (`reglas locales > cambios globales de
  IA`: lo que enciende la represalia es **haberlo visto**, y eso el jugador lo entiende mirando el campo) y
  uno de honestidad: encenderla en los siete haría que una lesión cambiara el comportamiento del equipo
  entero de golpe, que es indistinguible de un script.
- **Caduca.** Una represalia que no caduca deja de ser una reacción y pasa a ser un rasgo — y los rasgos ya
  existen, se eligen y se ven en la ficha.

**No es `TackleNemesis`**, que es de un perk, no caduca y **sustituye** el objetivo de la entrada. Éste no
sustituye nada: sólo hace que ir a por ése valga más que ir a por otro.

### 2 · En la turba no hay árbitro, y eso es una regla, no un número

Durante la prórroga de gol de oro **no se señala ninguna falta y no sale ninguna tarjeta**. La jugada
sigue: el que entra se lleva su derribo —eso es física, no castigo— pero no hay falta, ni reanudación que
pare el juego, ni castigo que acumular.

**Se hace quitando al árbitro, no subiendo la tolerancia con una probabilidad.** Poner el silbato al 1 %
dejaría un 1 % de partido en el que la regla no vale, y RF-055d no dice «casi no hay árbitro».

**La geometría del campo no se toca**, que es lo que el encargo pedía comprobar antes de asumir: estrechar
el campo era una de las cuatro cosas que la prórroga prometía y ninguna investigación del código muestra
que haga falta para que la turba signifique algo. Quitar el árbitro ya la convierte en la ventana de las
builds de violencia y en el mayor riesgo de las técnicas, que es **literalmente** lo que el requisito
promete.

## Lo que queda fuera, y está anotado

- **El campo estrechado, el público invadiendo casillas y el +15 % de velocidad** que la prórroga promete
  en `docs/analisis/auditoria-identidad-generador-de-historias.md`. Son cambios de geometría y de física
  con su propia medición; el encargo pide explícitamente **no** asumir que estrechar el campo sea la
  solución, y esta ADR se queda en la regla de interacción.
- **La represalia no cambia el marcaje.** Podría hacer que el vengador *persiga* al culpable; hoy sólo hace
  que le entre con más ganas cuando ya lo tiene delante. Perseguir es cambiar a dónde va un jugador, y eso
  se nota mucho más en la estructura del equipo: merece medirse aparte.

## Valores publicados

| dato | valor | por qué ése |
|---|---|---|
| `states.GrudgeTicks` | 150 | diez segundos: lo que dura el calentón, no lo que dura el partido |
| `grudgeBonus` | 240 | del orden del bono de entrar al portador (195): inclina, no decide |
| testigo | 4 casillas | la definición de «lo vio», y lo que mantiene la regla local |

## Consecuencias

- **Una lesión ya no se olvida en el tick siguiente.** Es el primer mecanismo del motor en el que algo que
  pasó cambia lo que los jugadores quieren hacer después, que es la dirección entera de la ADR 0122
  («convertir la simulación en memoria»).
- **La turba pasa a importar de verdad** en el 27,6 % de los partidos que llegan a ella, y hay que medir
  qué le hace a las lesiones ahí dentro. Es el punto que más vigilará la fase de balance.
