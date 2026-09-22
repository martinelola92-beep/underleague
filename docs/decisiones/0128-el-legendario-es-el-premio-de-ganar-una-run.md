# 0128 — El legendario es el premio de ganar una run con un clan

Estado: **Propuesta — decisión del revisor, con una excepción declarada a RF-006** (22 sep 2026).
**Bloqueada** por el perfil entre runs, que no existe (gate 4 de la ADR 0123). Resuelve la ficha
`docs/pendientes/BE-E.md`.

## Lo que decide el revisor

> «Los legendarios no salen en el mercado. Se ganan cuando completas (ganas) una run por primera vez con un
> clan. La siguiente run que hagas con ese clan incluye al legendario en el equipo de inicio. Es el premio
> de ganar la run. Ya valoraremos más adelante si eso hace que volver a jugar con el mismo clan suba mucho
> el winrate, y puede que lo tengamos que limitar a dificultad "difícil", que todavía no existe.»

## Qué resuelve

La ficha **BE-E** preguntaba si que un jugador legendario no pueda aparecer nunca era un descuido o una
decisión sin registrar. **Es una decisión, y aquí queda registrada.** `GeneratedPlayers.RarityWeights` es un
record de tres campos y `Rarity.Legendary` es inalcanzable por construcción del tipo: eso pasa de ser un
hueco a ser **el comportamiento correcto**. Lo que faltaba no era el mercado: era **la vía de obtención**.

Queda pendiente aparte una incoherencia menor de dato: `playerPriceByRarity` y `playerSaleBaseByRarity`
declaran un cuarto valor para una rareza que el mercado no puede ofrecer. **El de venta sí sigue haciendo
falta** —un legendario heredado se podrá vender—; el de compra no. Se anota en BE-E.

## La excepción, dicha en voz alta

**Esto contradice dos cosas escritas, y conviene que conste como excepción deliberada y no como un
despiste:**

- **RF-006**: *«Al terminar una run se conservan todos los logros. Los logros **no son incrementales**:
  desbloquean contenido (RF-125) pero **nunca otorgan ventaja en la run siguiente**.»*
- **ADR 0123 §D9**: *«La meta desbloquea **posibilidades**, nunca estadísticas acumulables. El progreso es
  saber más, no ser más fuerte.»*

Un legendario en la plantilla inicial **es ventaja en la run siguiente**, sin ambigüedad. Tiene además 5
slots de perk contra los 4 del raro *(`Progression.PerkSlots`)*, un techo de build que **hoy es
inalcanzable** y que nunca se ha medido.

### Por qué la excepción es defendible

1. **Está acotada a un clan.** No es una ventaja global: es «este clan concreto, con el que ya ganaste».
2. **Es contenido desbloqueado tanto como poder.** Un legendario con nombre es un personaje, y encaja con
   la dirección de las ADR 0126 y 0127 (la identidad se autoriza).
3. **Da significado a ganar**, que es un problema real: con `runWinRate` 22-27 %, ganar ocurre poco y hoy
   no deja nada.
4. **El propio revisor anticipa el riesgo** y propone la mitigación (limitarlo a una dificultad alta que
   todavía no existe).

### El riesgo, que es el clásico y hay que medirlo

**Bucle de refuerzo positivo**: ganas con un clan → ese clan se vuelve más fuerte → ganas más fácil con él
→ el jugador converge a un solo clan y el resto del contenido se queda sin jugar. Es exactamente lo que
RF-006 existe para evitar.

**Qué medir, cuando exista el perfil**: `runWinRate` con y sin legendario en la plantilla inicial, con la
misma semilla y la misma doctrina. Si el salto es grande, las salidas son las que el revisor ya apunta
—restringirlo a dificultad alta— o alguna de estas: que el legendario ocupe un hueco de plantilla en vez de
ampliarla, que llegue con salario, o que sustituya a un titular en vez de sumarse.

## Dependencia dura

**Esto no se puede implementar hoy.** Requiere un **perfil entre runs** —qué clanes has ganado— y
`Sim/Run/Save/` contiene un único fichero: `RunSave.cs`. No hay perfil, ni compendio, ni campaña. Es el
gate 4 de la ADR 0123 y la fase F8 del plan de evolución, deliberadamente colocada al final.

**Consecuencia de planificación:** esta ADR **no desbloquea trabajo inmediato**. Lo que sí hace es fijar el
diseño para que, cuando se construya el perfil, ya esté decidido qué guarda y por qué — y para que nadie
«arregle» BE-E metiendo legendarios en el mercado.

## Relación con la ADR 0123 §C

La ADR 0123 propuso que ganar convirtiera a tu clan campeón en **el rival de tu próxima candidatura**
(el patrón Red de Pokémon). Esta decisión propone que ganar te dé **un legendario con ese clan**.
**No son excluyentes y se refuerzan**: el clan con el que ganaste aparece como gobierno en ejercicio
*y* te presta a su estrella la próxima vez que lo eliges. Las dos dependen del mismo perfil.

## Lo que esta ADR NO decide

- Cuántos legendarios hay por clan, ni quiénes son.
- Si el legendario es el mismo cada vez o se sortea entre varios.
- Si ganar en dificultad alta da algo distinto (esa dificultad no existe).
- Si RF-006 se reescribe o se le añade esta excepción. **Es decisión del revisor y requiere tocar
  `docs/requisitos.md`**, cosa que esta ADR no hace por su cuenta.

## Hermanos

`docs/pendientes/BE-E.md` (resuelta en su parte de diseño) · ADR 0123 §C y §D9 · ADR 0126 · ADR 0127 ·
`docs/plan-evolucion-knavall.md` F8 · RF-006, RF-125, RF-125b
