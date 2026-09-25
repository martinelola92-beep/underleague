# Biblia de diseño de perks: 57 fantasías, no 57 multiplicadores

**Premisa del encargo, adoptada sin discusión** (corrección del revisor a la auditoría
`perks-auditoria-diseno.md`, cuya conclusión de catálogo —37 ficheros— queda **rechazada**):

> No necesitamos menos perks. Necesitamos menos perks **redundantes** y más **comportamientos distintos**.

> `ModifyUtility` probablemente debe existir, pero **no debe convertirse en el lenguaje de diseño**. Debe ser
> el mecanismo técnico debajo de un lenguaje mucho más expresivo:
> `PERK → INTENCIÓN → MODIFICACIÓN DE PRIORIDADES → UTILITY`, nunca `PERK → +37 Shoot / −14 Pass`.

Este documento **no modifica nada del repositorio**. Es el único fichero escrito.

## Etiquetas

- **MEDIDO** — leído directamente del código o de `/data`, o calculado con aritmética exacta sobre ellos.
- **DERIVADO** — conclusión encadenada sobre algo medido.
- **HIPÓTESIS** — interpretación de diseño sin validar; se marca y se dice cómo se mediría.

No se ha lanzado ningún lote de `/Balance`: todo lo cuantitativo de la Parte 1 sale de aritmética entera
sobre `data/ai/weights.json` y `Sim/Engine/Utility.cs`, que es exacta y no necesita muestra. Lo que sí
necesita medición está marcado como tal y tiene experimento asignado en la Parte 4.

---

## Correspondencia con el encargo

| encargo | aquí |
|---|---|
| Parte 1.A — el sub-100 en tres niveles | **Parte 1** (§1.0-1.5) |
| Parte 1.B — el techo | **Parte 2** |
| Parte 1.C — el espacio de fantasías | **Parte 3** |
| Parte 2 — 70-80 candidatos | **Parte 4** (80) |
| Parte 3 — filtrado a ~55-60 | **Parte 5** (57) |
| Parte 4 — hoja de ruta de motor | **Parte 6** |
| *corrección del revisor, 25 sep 2026 — los dos registros del lenguaje* | **§1.6 (reescrito)** · §1.3 · §3.1 · §5.3 · §6.2 (C17-C18) · §6.4 · §6.5 (decisión 6) |

---

# PARTE 1 — ARQUITECTURA DE DISEÑO

## 1.0 El hecho que gobierna toda la parte 1: la palanca no es el porcentaje

**MEDIDO** (`Sim/Engine/Utility.cs:159-161`):

```csharp
int traitMultiplier = p.ActionMultiplier(action) * (100 + p.LeaderBonusPercent) / 100;
int score = (baseWeight * tactical / 100 * traitMultiplier / 100) + eval.Context;
...
if (!found || score > bestScore) { ... }   // gana el mayor: comparación pura
```

Tres consecuencias, y las tres importan más que el signo del multiplicador:

1. **El contexto NO se multiplica.** El multiplicador solo escala `Base × Táctico/100`. El brazo de palanca
   real de un multiplicador `m` sobre la acción A para el rol R en el estado táctico S es
   **`Δ = Base(R,A) × Tac(S,A)/100 × (m−100)/100`**. *(MEDIDO)*
2. **`Base × Tac` varía hasta 60× dentro de un mismo estado**, así que el **mismo porcentaje hace cosas
   incomparables** según dónde se aplique. Un `+40 %` sobre `FindSpace` de un delantero en posesión mueve
   **+386 puntos**; el mismo `+40 %` sobre su `Retreat` mueve **+34**. *(MEDIDO, tabla §1.1)*
3. **El motor ya tiene un segundo multiplicador mutable en esa misma línea**: `LeaderBonusPercent`, un entero
   por jugador que `MatchEngine.RecomputeLeaderBonuses` escribe en tiempo de partido y que `Utility` compone
   con `_actionMultipliers`. *(MEDIDO, `MatchEngine.cs:434`.)* **DERIVADO:** el canal de intención no es una
   invención arquitectónica; es ese mismo hueco, ampliado de escalar a vector de 14.

**DERIVADO — y es la razón por la que el revisor tiene razón en lo del lenguaje.** Si el lenguaje de diseño
fuera el porcentaje, el diseñador tendría que conocer de memoria la tabla `Base × Tac` de 4 roles × 4 estados
× 14 acciones para saber si su `×1,4` hace algo o nada. Eso no es un lenguaje de diseño: es una hoja de
cálculo. El porcentaje es **unidad de máquina**; la unidad de diseño tiene que ser la **intención**
(§1.5).

## 1.1 La tabla que decide qué nivel necesita sub-100

`Base(rol,acción) × Táctico(estado,acción)/100`, normalizado al líder de cada conjunto de acciones legales
(`StateMachine.LegalActions`: 9 acciones sin balón, 5 con balón). **MEDIDO**, aritmética exacta sobre
`data/ai/weights.json`.

| rol · estado · conjunto | líder | resto, en % del líder |
|---|---|---|
| Delantero · posesión · sin balón | **FindSpace 966** | Chase 24 · Support 24 · Block 10 · Retreat 8 · Cover 7 · Mark 3 · Tackle 3 · Press 2 |
| Delantero · sin posesión · sin balón | **ChaseBall 500** | Press 46 · Tackle 42 · Mark 36 · Cover 33 · Retreat 32 · FindSpace 13 · Support 8 · Block 3 |
| Delantero · con balón | ShortPass 420 | **Shoot 91 · Dribble 71** · Through 35 · LongPass 32 |
| Medio · posesión · sin balón | **FindSpace 798** | Chase 31 · Support 26 · Cover 19 · Retreat 16 · Mark 11 · Block 9 · Tackle 6 · Press 3 |
| Medio · sin posesión · sin balón | ChaseBall 525 | **Mark 85 · Cover 69 · Tackle 65 · Press 56 · Retreat 48** · FindSpace 10 · Support 6 · Block 2 |
| Medio · con balón | ShortPass 560 | **LongPass 44 · Dribble 42 · Shoot 42 · Through 32** |
| Defensa · sin posesión · sin balón | Mark 600 | **Cover 98 · Chase 79 · Tackle 70 · Retreat 61** · Press 41 · FindSpace 5 |
| Defensa · posesión · sin balón | FindSpace 420 | **Cover 60 · Chase 54 · Retreat 45** · Mark 28 · Support 28 · Block 15 · Tackle 15 |
| Defensa · con balón | ShortPass 500 | LongPass 34 · Shoot 30 · Dribble 28 · Through 18 |
| Portero · sin posesión · sin balón | Cover 700 | **Retreat 65 · Chase 53 · Tackle 38** · el resto 0 |
| Portero · con balón | ShortPass 600 | LongPass 53 · Dribble 8 |

**Tres bandas, y son la clave de todo lo que sigue** *(DERIVADO de la tabla)*:

- **CONTENDIENTE (≥ 55 % del líder).** Basta subir de `+20 %` a `+80 %` para que gane. **No hace falta ningún
  sub-100.** Aquí vive **todo el juego con balón** (el pentágono Pase/Tiro/Regate/Profundidad/Largo está entre
  el 32 % y el 100 % para medio y delantero) y **toda la fase defensiva** de defensa y medio (cinco acciones
  entre el 48 % y el 98 %).
- **ALCANZABLE (35-55 %).** Hace falta de `+80 %` a `+185 %`. Se puede, pero roza el techo razonable y
  deforma la conducta en las demás situaciones donde esa acción se evalúa.
- **DOMINADA (< 35 %).** Hace falta `m ≥ 285`, y en los casos extremos `m ≥ 800`. Aquí hay exactamente
  **cuatro casillas** en todo el juego, y son siempre la misma pareja: **`FindSpace` en posesión**
  (delantero 4,0× sobre el segundo, medio 3,2×) y **`ChaseBall` sin posesión** (delantero 2,2×). *(MEDIDO)*

## 1.2 Los tres niveles, resueltos

### NIVEL 1 — PRIORIDAD: «esta acción le resulta más deseable»

Una sola acción sube; nada baja. Requiere que la acción esté en banda CONTENDIENTE en el estado donde el
perk quiere que ocurra.

**No necesita sub-100. Nunca.** *(DERIVADO, §1.1.)* La exclusión que produce sale gratis, exactamente como
dice el revisor: subir `Shoot` de un delantero de 385 a 540 (`m=140`) le hace **tirar en vez de pasar** sin
declarar ninguna reducción, porque `ShortPass` no ha bajado: ha dejado de ser el mayor.

**Ejemplo con los números reales** *(MEDIDO+DERIVADO)*: delantero con balón, en rango de tiro.
`ShortPass = 420 + 220 (receptor libre) = 640`. `Shoot = 385 + 388 (en rango) − 50/casilla = ~620` a 6
casillas. Diferencia: 20 puntos. `Shoot ×130` añade `385×0,30 = +116` y el tiro gana **con holgura**. El
perk «Cazagoles» está implementado con un único entero positivo.

### NIVEL 2 — PREFERENCIA: «ante esta situación, prefiere X sobre Y»

Dos acciones del **mismo conjunto legal**, ambas CONTENDIENTES, y una **situación** que enciende la
preferencia.

**Tampoco necesita sub-100** *(DERIVADO)*, y la razón es la que el revisor identificó: la utilidad es una
comparación pura, así que «prefiere X sobre Y» **es aritméticamente idéntico** a «sube X», siempre que X e Y
compitan en el mismo `argmax`. Lo que hace de esto un nivel distinto **no es el signo, es la condición**: el
Nivel 2 exige que el motor sepa, en el tick de la decisión, en qué situación está el jugador — y eso **hoy no
existe** (§4, capacidad C2).

**Este es el hallazgo estructural de la Parte 1.** Lo que separa la preferencia de la prioridad no es un
número negativo: es una **capacidad de motor ausente**. La discusión sobre la ADR 0088 ha estado ocupando el
sitio de la discusión que importa, que es la del **vocabulario de situaciones**.

### NIVEL 3 — EXCLUSIÓN: «varias prioridades a la vez, con renuncia deliberada»

Se divide limpiamente en dos, y solo una mitad necesita sub-100.

**3a — Renuncia espacial: NO necesita sub-100.** La zona de acción es una **precondición dura**: `Utility`
descarta la acción entera cuando el destino cae fuera del límite exterior (`eval.OutsideOuterLimit`), antes
de comparar nada. *(MEDIDO.)* `modifyLeash` y un desplazamiento de la casilla-hogar producen renuncia
**sin tocar ningún multiplicador**: el delantero que no vuelve no es un delantero con `ChaseBall ×0,4`, es un
delantero cuya zona no llega a su propio tercio. Es más legible, más previsible en la pantalla de Equipo, y
usa el efecto que la auditoría ya señaló como el mejor del sistema.

**3b — Renuncia de intención: SÍ necesita sub-100, y no hay sustituto.** Cuando lo que se abandona es el
**líder de su estado** por ≥ 2×, subir al sustituto exige `m` entre 285 y 800. Eso hace dos daños, ambos
DERIVADOS de §1.1:
1. **Rompe cualquier techo sano.** Un `FindSpace ×724` en un delantero (lo que haría falta para que se
   quedara arriba sin tocar `ChaseBall`) es un valor que ninguna tabla de validación debería aceptar.
2. **Se derrama.** El multiplicador de acción es **global al jugador**: ese `×7` a `FindSpace` también actúa
   en posesión, donde `FindSpace` ya lidera con 966, y convierte al delantero en una estatua.
   Bajar `ChaseBall` a 40 mueve **−300 puntos** y solo donde estorba.

**Conclusión: el sub-100 es cirugía, no castigo.** Y su necesidad se reduce a **cuatro casillas de la tabla**
(§1.1, banda DOMINADA), que son las que producen las fantasías de personalidad más memorables del género: el
delantero que no defiende, el ancla que no sube, el centrocampista que no se ofrece.

## 1.3 Reparto del catálogo por nivel

Contado sobre los 57 perks del catálogo final (Parte 5); los que no modifican intención (oficio puro, regla
rota, contador) no llevan nivel.

| | nº | % del catálogo | % de los perks de intención |
|---|---|---|---|
| **Nivel 1 — Prioridad** | 20 | 35 % | 45 % |
| **Nivel 2 — Preferencia** | 16 | 28 % | 36 % |
| **Nivel 3a — Exclusión espacial** (leash/hogar, sin sub-100) | 3 | 5 % | 7 % |
| **Nivel 3b — Exclusión de intención** (**sub-100 obligatorio**) | 5 | 9 % | 11 % |
| Sin nivel: oficio, regla, contador | 13 | 23 % | — |

**Los cinco del 3b, nombrados** *(la lista completa de perks que pedirían romper la ADR 0088)*: **No vuelve**
(delantero que no repliega), **Ancla** (defensa que no sale de su tercio), **Cobarde con ojo** (no entra,
lee), **Torre** (no busca hueco, espera el balón largo) y **Carnicero ciego** (persigue al hombre, no al
balón). **Cinco de cincuenta y siete: el 9 %.** *(DERIVADO)*

**La fila «sin nivel» no es un residuo: es el registro B.** Esos 13 perks —oficio, regla rota, contador— son
los que **no modifican intención**, y por tanto los únicos que pueden pertenecer al registro de excepción
(§1.6.B). Que sean 13 de 57 (23 %) y la cuota de `ruleBreaker` sea el 10 % **no es una contradicción**: la
fila mezcla los tres, y sólo la parte «regla rota» cuenta para la cuota. **DERIVADO: hace falta separar la
fila en tres al cerrar el catálogo**, o la cuota de RF-069 seguirá sin poder comprobarse — que es
exactamente el fallo que §1.6 mide en el catálogo de hoy.

## 1.4 El argumento de la dilución

**HIPÓTESIS DEL REVISOR**, enunciada aquí en su forma fuerte y luego corregida:

> Si todo sube, el segundo perk de un jugador levanta otra acción y encoge la identidad que creó el primero,
> así que el sub-100 sería **normalización**, no castigo.

**Lo que la aritmética dice, y es más interesante que la hipótesis** *(DERIVADO de la fórmula)*:

1. **En el límite, subir todo por igual no es neutro: es anti-situacional.** `argmax(k·x_i)` es invariante
   bajo escala común, pero aquí el contexto **no** escala: `score = k·(Base×Tac) + Context`. Subir todas las
   acciones un `k` común **encoge el peso relativo del contexto**, que es el único término que sabe dónde
   está el balón, si el receptor está libre o si el rival está a una casilla. **Un jugador con muchos perks
   al alza no se vuelve genérico: se vuelve más esclavo de su rol y más ciego a la jugada.** Es peor que la
   dilución que teme el revisor, y va en la misma dirección de su conclusión.
2. **Dos perks al alza sobre acciones distintas no borran la primera identidad: añaden un segundo pico.**
   Con `Shoot ×140` y `Tackle ×140`, el delantero dispara más *y* entra más. No es un personaje, es una
   estadística mejor — que es exactamente el diagnóstico de la auditoría, repetido a nivel de jugador en vez
   de a nivel de catálogo.

**Cómo se mediría** *(instrumento que hoy no existe; coste bajo)*: **histograma de acción elegida**. `Utility`
ya sabe volcar la tabla de puntuaciones por tick (RT-098); basta contar la acción **ganadora** por jugador y
partido. Dos métricas sobre ese histograma, normalizado:

- **Distancia de identidad** `d` = distancia L1 entre el histograma del portador y el de su **control
  emparejado** (el instrumento de la ADR 0087 ya construye ese control).
- **Entropía** `H` del histograma del portador.

Predicciones falsables:
- *Dilución:* `d(2 perks al alza) < 2·d(1 perk)` **y** `H` no baja al añadir perks. Si se cumple, el revisor
  tiene razón y el sub-100 es normalización.
- *Anti-situacionalidad:* `H` **baja** y `d` crece al añadir perks al alza, pero el histograma converge hacia
  la forma del **rol** y no hacia la del perk. Si se cumple, el problema es peor y el sub-100 es necesario
  por una razón distinta (recuperar el peso del contexto).
- *Nulo:* `d(2) ≈ 2·d(1)`, `H` estable. Entonces la dilución no existe y el sub-100 se justifica solo por
  §1.2-3b (cuatro casillas), no por normalización.

Un solo lote con tres brazos (0/1/2 perks al alza) sobre la build de referencia lo resuelve.

## 1.5 Recomendación: cuándo se permite el sub-100

**Se permite, acotado a cinco condiciones acumulativas.** *(Propuesta; exige ADR y decisión del revisor,
RT-057.)*

1. **Nunca solo.** El sub-100 solo existe **dentro del mismo perk que sube otra cosa**, y la descripción
   generada dice las dos mitades en una frase. No existe el perk que solo baja.
2. **Mismo conjunto legal.** Lo que baja y lo que sube pertenecen al mismo conjunto de acciones
   (`WithBallActions` con `WithBallActions`, `WithoutBall` con `WithoutBall`). Así el equipo **no pierde una
   capacidad: la reubica.** Es lo que separa «identidad» de «malus».
3. **Suelo 40 y lista negra.** Nunca por debajo de `m=40`, y nunca sobre la acción que sostiene una garantía
   dura: `Retreat` del portero, `ChaseBall` del perseguidor designado (AW-S), `Tackle` de nadie que sea el
   único en zona. Validado al cargar, error explícito (RT-032), igual que la rareza acota las cuotas.
4. **`positionOnly` obligatorio.** Un perk de Nivel 3b declara el puesto. Es la respuesta directa a la
   preocupación de la ADR 0088 —«un perk mal puesto es un perk que no tiene efecto»—: el 3b **no puede
   colocarse mal**, porque el cargador no deja.
5. **Previsible antes del partido.** Su condición se resuelve en `LineupPerkPreview` o es geométrica y fija.
   Nada de proximidad en el tick ni de etiquetas del rival.

**Por qué esto no contradice la ADR 0088** *(argumento, no hecho)*: la ADR 0088 prohíbe que **una rama
`else`** castigue, es decir, que **fallar la condición** tenga coste. Un Nivel 3b **no tiene rama else**:
está siempre encendido y su renuncia es el precio anunciado de una ventaja anunciada, no la penalización por
haber colocado mal. Son dos cosas distintas y la ADR 0088 solo habla de la primera. **Pero es una lectura, y
la decisión es del revisor.**

## 1.6 El lenguaje de intención: dos registros, no uno

Requisito del revisor: `ModifyUtility` no puede ser el lenguaje de diseño. Propuesta concreta, y es la pieza
de arquitectura más importante de este documento.

**Corrección del revisor (25 sep 2026), posterior al primer borrador de esta sección y motivo de su
reescritura:**

> Los perks comunes modifican **cómo juega la máquina**. Los perks excepcionales pueden hacer que la máquina
> **haga cosas que el fútbol normal no puede hacer**.

**No es una regla nueva: es RF-069 leída entera** *(MEDIDO, `docs/requisitos.md`)*. La distribución objetivo
del catálogo tiene tres filas y la tercera dice *«Rompe-reglas · 10 % · anulan o invierten una regla del
simulador»*. El borrador anterior de este epígrafe proponía **un** lenguaje —`prefers`, `refuses`, `commits`,
`stretches`— y los cuatro verbos son de conducta: cubren la primera fila y la segunda y **no tienen forma de
escribir la tercera**. El lenguaje no la prohibía; simplemente no sabía decirla, que en un vocabulario
cerrado es lo mismo. Corregirlo ahora cuesta un epígrafe; corregirlo después de escribir las 57 fichas
cuesta las 57 fichas.

**El hueco, medido en el catálogo de hoy** *(MEDIDO, censo de `data/perks/`, 102 ficheros)*: 13 perks están
etiquetados `ruleBreaker`, y detrás de la etiqueta hay

- **4 que anulan un evento** —`hand_of_god`, `iron_gate`, `mob_instigator`, `no_dying`—: rompen una regla de
  verdad;
- **4 que matan** (los cuatro letales): rompen una regla, pero por una **bandera** del perk, no por su efecto;
- **2 inmunidades** y **1 que encarece el marcaje rival** (`free_man`, el único efecto del sistema que
  escribe sobre el emparejamiento del equipo **contrario**);
- **y 2 que no rompen nada: `blood_tithe` y `loan` son `modifyProbability` y `addCounter`.**

Además **6 de los 13 se disparan en `MATCH_START`**. **DERIVADO: el grado más alto de la taxonomía es hoy
medio ficticio** —dos de sus trece miembros son un número con etiqueta de excepción— **y casi la mitad
ocurre antes de que empiece el partido**, donde por definición no hay regla de fútbol que romper. Un
lenguaje con un solo registro no habría corregido eso: lo habría formalizado.

### 1.6.A — Registro de conducta: *cómo juega la máquina*

**Ámbito: el 90 % del catálogo** (`filler` + `conditional`, RF-069). **La propiedad que lo define: todo lo
que este registro produce, el fútbol ya lo podía producir.** Un jugador dispara antes, marca a otro, no
repliega — son partidos legales con una distribución rara. Es exactamente lo que debe ser el grosor de una
build, y es lo que hace que dos plantillas jueguen distinto.

**En `/data` el diseñador no escribe porcentajes. Escribe verbos de intención con un vocabulario cerrado:**

```jsonc
// lo que NO se escribe nunca
{ "type": "modifyUtility", "action": "Shoot", "value": 137 }

// lo que se escribe
{ "intent": "prefers",  "action": "Shoot", "over": "ShortPass", "when": "inShootingRange" }
{ "intent": "commits",  "action": "MarkOpponent", "target": "mostTechnical" }
{ "intent": "refuses",  "action": "ChaseBall", "when": "outsideOpposingThird" }   // Nivel 3b
{ "intent": "stretches","toward": "opposingGoal", "cells": 2 }                     // Nivel 3a, modifyLeash
```

**El cargador compila la intención a enteros**, usando la tabla `Base × Tac` del rol del portador y del
estado que la situación implica:

- `prefers(A over B, when S)` → el `m` mínimo que pone A por encima de B en `(rol, S)`, más un margen fijo
  de seguridad. El diseñador pide **un resultado**; el cargador calcula **el número**.
- `refuses(A, when S)` → `m` bajo con el suelo de §1.5, y **solo** si A está en banda DOMINADA en `(rol, S)`;
  si A es contendiente, el cargador **rechaza el dato** y exige `prefers` (RT-032: error explícito).
- `commits(A, target)` → selección de objetivo (capacidad C5/C7), no multiplicador.
- `stretches` → `modifyLeash` + desplazamiento de hogar, que ya existen.

**Qué compra esto** *(DERIVADO)*:

1. **El lenguaje de diseño deja de ser aritmético** y RT-035 puede por fin generar una frase de conducta
   —«prefiere el disparo al pase en cuanto ve portería»— en vez de «multiplica por 1,37 su…». Hoy la
   plantilla **obliga** a que el texto sea aritmética; con la intención como dato, deja de obligarlo.
2. **El balance se hace una vez, en la tabla de compilación**, no perk a perk. Recalibrar `weights.json`
   recalcula todos los perks solo; hoy recalibrar obliga a revisar 61 ficheros a mano.
3. **`/Sim` no se entera.** Sigue leyendo un `int[14]` por jugador. Determinismo intacto: aritmética entera,
   sin E/S, compilación una sola vez al cargar (el mismo sitio donde ya se compilan las condiciones NCalc).
4. **El cargador puede rechazar lo imposible.** Un `prefers(Shoot over ShortPass)` en un portero da
   `Base(GK,Shoot)=0`: incompilable, error explícito. Hoy nada impide un `×3` a `Shoot` en un portero.

### 1.6.B — Registro de excepción: *lo que el fútbol no puede hacer*

**Ámbito: el 10 %** (`ruleBreaker`, RF-069). **La propiedad que lo define, y es el criterio de admisión: el
suceso que produce no tiene ninguna secuencia de decisiones legales que lo genere.** Si el mismo resultado
se alcanza subiendo una prioridad, **no es registro B**: es registro A mal escrito, y el cargador debe
rechazarlo. Es la misma prueba que §5.1 aplica al catálogo —comportamiento observable, no resultado
agregado— llevada al vocabulario.

**Un verbo de B no toca la tabla de utilidad.** No compite en el `argmax`: actúa **después** de que el motor
haya decidido y resuelto, sobre el resultado. Por eso son tipos de efecto y no multiplicadores, y por eso
`Utility.cs` no se entera de que existen — que es exactamente la propiedad que ya tienen `cancelEvent` y
`lethal` *(MEDIDO)*. **Consecuencia práctica: los dos registros no se estorban.** Recalibrar `weights.json`
recalcula todo el registro A y no toca una línea del B.

**Las tres formas que la arquitectura admite hoy.** Se enuncian como **formas del verbo**, no como catálogo
de efectos: la auditoría `perks-auditoria-potencial-visual.md` (13B) pide expresamente no fijar todavía qué
fenómenos concretos existirán, y esta biblia no los fija.

| forma | qué hace el verbo | sobre qué escribe | soporte hoy *(MEDIDO, 13B §3)* |
|---|---|---|---|
| **niega** | el suceso no ocurre, o no cuenta | flujo de eventos | **existe**: `cancelEvent` (4 perks) |
| **repite / desdobla** | la jugada vuelve a resolverse | flujo de resolución | **existe**, acotado: `extraAction` sólo sabe rehacer `SHOT` y `TACKLE` |
| **impone física** | un cuerpo o el balón hacen algo que nadie decidió | `Position` · `Velocity` · `PlayerState` · `Ball` | **a medias**: el cuerpo **sí** (`setState`, y el búfer de empuje de `BodySeparation` ya resuelve el determinismo de N cuerpos); **el balón no: cero de los 19 tipos de efecto lo tocan**, teniendo `Ball` ya `Velocity`, `Z`, `VelocityZ` y `FlightArc`, y la traza ya grabándolos |

**La cuarta forma —alterar el espacio— no existe, y esta biblia no la abre**: exige estado de mundo en el
partido, que es un sistema nuevo y no una extensión (13B §4.D). **Lo que sí se decide aquí es no cerrarle la
puerta**, y se decide de una forma concreta y comprobable: **ningún verbo de B lleva «por jugador» en su
firma.** Un verbo de B declara un **objetivo**, que ya es vocabulario abierto y ya tiene un miembro que mira
posición real y no alineación (`adjacentOpponents`). Si mañana existe un objetivo espacial, los verbos ya
escritos lo admiten sin reescribirse.

### 1.6.C — Las cinco reglas que mantienen honesto el registro B

Sin ellas B no es un grado: es la puerta trasera por la que el catálogo entero se vuelve excepción y nada es
excepcional. Las cinco son **verificables al cargar** (RT-032, error explícito), no criterio de nadie:

1. **Cuota.** `ruleBreaker` ≤ 10 % del catálogo (RF-069). El cargador lo cuenta y falla. Hoy la etiqueta se
   la pone el dato a sí mismo y no la comprueba nadie — de ahí los dos que son sólo un número.
2. **Nunca en `MATCH_START` ni en `PLAY_START`.** Una excepción permanente no es una regla rota: es una
   regla nueva, y encima invisible. B se cuelga de un evento de fútbol, que es lo que le da situación,
   actor y **celda**, es decir un sitio donde verse. *(El cargador ya impone exactamente esta regla a
   `lethal` desde el paquete AY: se generaliza, no se inventa.)*
3. **Nombrable.** Un verbo de B sin plantilla de descripción (RT-035) y sin entrada en la gramática de
   momentos de `/Game` **es ruido por construcción**. Es la condición que hoy incumplen los 102 perks del
   catálogo, porque `PERK_TRIGGERED` se emite y se tira y `MomentKind` no tiene entrada de perk (13B §2.2).
4. **Previsible.** Regla 11 y las cinco condiciones de la ADR 0048: si puede hacer daño al jugador, se sabe
   antes, se ve en `LineupPerkPreview` y se puede reducir con la alineación.
5. **Presupuestada.** Un verbo de B que se route por **contacto** compite por una banda agotada
   —`injuriesPerMatch` 0,78 contra techo 0,90, con la brecha de faltas de la ADR 0147 aún sin explicación
   cerrada— y **no entra sin medición previa**. Los que se routen por el balón o por el movimiento no gastan
   de esa banda (13B §6). No es una preferencia estética: es RT-056.

**Qué compra tener dos registros en vez de uno** *(DERIVADO)*:

1. **La cuota de RF-069 pasa a ser verificable.** Con dos vocabularios disjuntos, el grado de un perk **es**
   el verbo que usa, no la etiqueta que se pone.
2. **El registro A puede ser menos ambicioso sin empobrecer el catálogo.** Buena parte del vértigo del
   sub-100 (§1.5) viene de que era la única forma que tenía el lenguaje de producir algo memorable. Con B
   existiendo, A no carga solo con la identidad del catálogo — y las cinco fichas del Nivel 3b dejan de ser
   la única fuente de personalidad del juego.
3. **Lo caro queda acotado a lo raro.** Cada verbo de B es una primitiva de motor; la cuota del 10 % es, al
   mismo tiempo, un presupuesto de trabajo de ingeniería.

### 1.6.D — Nota de diseño (skill `game-design-review`, Regla B de `CLAUDE.md`)

Se responde aquí porque la decisión **programa primitivas de motor** (C17, C18) aunque no implemente
ninguna. **No genera ADR todavía**: la generará C17 cuando se escriba, porque ahí sí hay tipo de efecto
nuevo (RT-057). Las diez, en corto:

1-3. **Qué experimenta y qué decide el jugador**: con A, un once que juega distinto; con B, un suceso
atribuido a algo que él eligió. La decisión que B añade es **apostar un slot raro a que una situación
ocurra**, y alinear para provocarla. Hoy esa decisión no existe: los 13 `ruleBreaker` del catálogo son 6 de
`MATCH_START` y 2 que sólo son un número.
4. **Qué regla representa**: **RF-069**, tercera fila. No se inventa ninguna. También RF-065/RT-031 (perks
son datos), RT-035, regla 11 y ADR 0048.
5. **Qué sistemas**: `/data` (los dos vocabularios y el cargador que los compila y cuenta la cuota), `/Sim`
(`EffectEngine` y las primitivas de C17; `Utility.cs` **no se entera de B**), `/Game` (C18: gesto declarado
y `MomentKind` de perk). Respeta RT-014 porque el puente es un vocabulario cerrado **declarado en el dato**,
no una decisión repartida entre los dos lados.
6. **Alternativas consideradas**: *(a)* un solo registro conductual —es lo que había, y es lo que cierra el
espacio—; *(b)* dejar la excepción fuera del lenguaje y escribirla a mano perk a perk en C# —rompe la regla
5—; *(c)* un registro único con los verbos mezclados —la cuota de RF-069 dejaría de ser verificable al
cargar, que es la mitad del valor de la decisión—. Se elige **dos registros disjuntos**.
7. **Trade-off**: B paga cuota, rareza, previsión obligatoria y una plantilla de descripción por verbo; el
jugador paga un slot raro en algo que **sólo cobra en una situación**. No es poder gratis.
8. **Estrategias**: A se combina **aditivamente** (dos perks al alza = dos picos, §1.4). B se combina
**multiplicativamente con la situación**: una excepción sobre `TACKLE` vale lo que valga tu tasa de
entradas, que otra parte de la build controla. **Eso es lo que produce builds en vez de estadísticas.**
9. **Degeneración**: tres vías, y las tres se cierran con reglas de carga y no con criterio — sin cuota, el
catálogo entero se vuelve excepción; en `MATCH_START`, la excepción es una regla nueva e invisible; por
contacto, choca con una banda agotada. Son las reglas 1, 2 y 5 de §1.6.C.
10. **Demostración**: cuota de `kind` comprobada al cargar (error explícito, RT-032) · tasa de activación
por perk de B en una build coherente (criterio de la tanda 4) · ningún verbo de B fuera de banda en
`injuriesPerMatch`/`shotsPerMatch` · y la prueba de legibilidad de 13B §5: **¿puede la pantalla decir, en el
instante, quién lo hizo y por qué se cumplió?**

---

# PARTE 2 — LA CONVIVENCIA DE 55-60 PERKS CON 14-20 SLOTS

## 2.1 Los números de la run

**MEDIDO**: `nodesPerAct = [11, 12, 12]` = **35 nodos**. `Progression.PerkSlots` = 2/3/4 por rareza;
`InitialPerks` = 0/1/2. Pool ofrecible por run = **52 perks universales + los ~2 de la raza del club**
(`PerkPool.Offerable` filtra por raza, ADR 0023, y descuenta lo ya poseído). Siete titulares → **14-28 slots
de perk**, más banquillo. Cuatro maestros, cada uno exige **2 ids distintos** de su línea y **cierra la línea
opuesta** (ADR 0051). Líneas actuales: `wall` 8 · `craft` 8 · `aim` 8 · `butchery` 8 · **sin línea 29**.

## 2.2 Por qué el argumento del techo de la auditoría es correcto **y no aplica**

La auditoría concluyó «una línea de más de 8 no se ve nunca» y de ahí sacó 32 obtenibles. El argumento es
válido **dentro de su premisa**: que el catálogo se organiza en cuatro líneas y que la run compra 3-4 de una.
La premisa del revisor es otra —organizar por fantasías— y **la conclusión no sobrevive al cambio de
premisa**. *(DERIVADO)*

El error concreto de la auditoría es de contabilidad: **contó los slots como si todos compitieran por lo
mismo**. No compiten. Un perk que cambia la intención y un perk que sube la cuota de una tirada no se
estorban: ocupan el mismo slot pero **no ocupan el mismo canal**.

## 2.3 El cuello de botella real: el canal de identidad, y hay que caparlo a uno por jugador

**Propuesta central de esta parte.** El catálogo se parte en dos pools con **reglas de tamaño distintas**:

| pool | qué es | regla de posesión | tamaño |
|---|---|---|---|
| **Identidad** | perks de intención (Niveles 1, 2, 3) | **máximo UNO por jugador** | **30** |
| **Soporte** | oficio, carnicería, desgaste, pacto, regla rota | libre, hasta llenar slots | **27** |

**La regla «una identidad por jugador» hace tres cosas a la vez** *(DERIVADO)*:

1. **Resuelve la dilución sin sub-100.** El problema de §1.4-2 —dos picos, ningún personaje— **desaparece por
   construcción**: nadie puede tener dos intenciones. El sub-100 deja de ser necesario *para normalizar* y
   queda solo para las cuatro casillas dominadas de §1.2-3b. Es la vía más barata de darle al revisor lo que
   busca sin tocar la ADR 0088 más que en cinco perks.
2. **Hace que el catálogo de identidad quepa.** Una run expresa **7 identidades** (una por titular). Con la
   regla de pulgar del género —el pool debe ser ≥ 4× lo que una run usa, para que la tercera run aún
   sorprenda— salen **28-30 perks de identidad**, y con la puerta por puesto (`positionOnly`) eso son **7-8
   por puesto**, que es exactamente el surtido que hace interesante la pregunta «¿qué clase de
   centrocampista quiero?».
3. **Crea una decisión que hoy no existe**: «¿sacrifico la identidad que ya tiene este jugador o se la doy a
   otro?». Hoy la respuesta a «¿a quién se lo pongo?» es «al que tenga slot»; con el cap, es «a quien no
   tenga ya carácter». *(HIPÓTESIS de diseño.)*

## 2.4 Cuántos perks ve un jugador por run

**DERIVADO** de §2.1: ~35 nodos, con recompensa 1-de-3 tras victoria (RF-071) y mercados en las capas pares
(ADR 0053). Del orden de **35-50 perks vistos como oferta** y **8-14 adquiridos** por run. Con 57 en el
catálogo: se ve el **60-80 %** y se juega el **15-25 %**. Es la proporción correcta para un roguelite
—comparable a un personaje de Slay the Spire: ~75 cartas de clase, ~25 jugadas por run— **y es la que la
reducción a 37 habría roto**: con 37 se vería el catálogo entero cada run.

**Pero tiene un prerrequisito duro y hoy incumplido:** ver 40 ofertas solo es variedad si cada oferta se lee
de un vistazo. Con la plantilla actual («multiplica por 3 sus opciones de robar el balón») **40 ofertas son
40 sumas**, y el jugador acaba ordenando por número, que es el comportamiento medido hoy. La capa de
intención de §1.6 no es un adorno: **es la condición de que 57 perks sean mejor que 37.**

## 2.5 Los maestros y los arcos de la ADR 0051: **hay que rediseñar las líneas. Sí.**

**MEDIDO:** las cuatro líneas son `wall`, `craft`, `aim`, `butchery`, y la auditoría midió que son
**canales**: `wall`+`butchery` son «×N entrada/lesión», `aim` es «×N tiro», `craft` es «×N pase». Una línea
definida por canal **es la redundancia misma**: pertenecer a `aim` significa literalmente «multiplico
`shotOnTarget`», así que ocho miembros de `aim` son ocho maneras de decir lo mismo. *(DERIVADO)*

**Si el catálogo se organiza por fantasías, una línea-canal deja de significar nada.** Propuesta:

- **Conservar intacto el mecanismo** maestro + `requiresPerks` + `blocksPerks`. **MEDIDO:** es la única
  anti-sinergia real del juego y la única decisión irreversible con coste. Hay que apoyarse **más** en ella.
- **Rebasar las líneas sobre doctrinas de equipo**, no sobre canales. Cuatro doctrinas en dos parejas
  excluyentes, cada una con su maestro que exige 2 y cierra la contraria:

| doctrina | qué hace el equipo | cierra | maestro heredado |
|---|---|---|---|
| **La Muralla** | bloque bajo: el equipo renuncia al campo rival | **La Avalancha** | `granite_line` |
| **La Avalancha** | presión alta: el equipo renuncia al repliegue | **La Muralla** | nuevo (Presión tras pérdida) |
| **El Toque** | posesión y cadena de pases | **La Carnicería** | `first_touch_school` |
| **La Carnicería** | violencia: el equipo renuncia a la pelota | **El Toque** | `blood_tithe` |

`killing_range` deja de ser maestro de línea y pasa a ser un **perk raro sin línea** (el rematador puro cabe
en cualquier doctrina; que «marcar goles» fuera una doctrina excluyente con «defender» nunca tuvo sentido de
diseño). *(DERIVADO: `aim` cierra `wall`, lo que hoy significa que un equipo que quiere rematar no puede
robar — una anti-sinergia que no describe ninguna forma de jugar al fútbol.)*

- **La doctrina es una etiqueta ortogonal**, no una taxonomía: un perk puede ser de identidad *y* de doctrina.
  Cada doctrina agrupa 6-8 perks; los otros ~25 no tienen doctrina. Así el maestro sigue siendo una decisión
  de run sin que la doctrina tenga que explicar el catálogo entero.

---

# PARTE 3 — EL ESPACIO DE FANTASÍAS

## 3.1 El reparto del revisor, criticado

Propuesta del revisor: ofensivo · movimiento · creación/pase · defensa · presión/recuperación ·
personalidad/estado · geometría · herencia/carnicería · pacto/composición · regla rota.

Tres correcciones, con motivo:

1. **«Movimiento» y «geometría» son la misma familia.** Ambas se implementan con el **mismo canal**
   (`modifyLeash` + desplazamiento de `EffectiveHome` + `FindSpace`), ambas se ven igual en pantalla (dónde
   está parado el jugador) y ambas se previsualizan igual en la pantalla de Equipo. Mantenerlas separadas
   reproduce el pecado que estamos corrigiendo: **dos nombres para un canal**. → se funden en **Espacio**.
2. **«Regla rota» no es una familia: es un grado.** `iron_gate` (anula una lesión) y `mob_instigator` (anula
   una falta) no se parecen en nada salvo en que rompen una regla; uno es carnicería y el otro es árbitro.
   Como familia no predice ni el canal ni el puesto ni lo que se ve. → pasa a ser un **atributo transversal**
   (`ruleBreaker`, que ya existe en `kind`, MEDIDO) aplicable a cualquier familia, con cuota máxima.
**Esa cuota es la del registro B y la impone el cargador, no el criterio de quien escribe la ficha: RF-069,
≤ 10 % del catálogo (§1.6.C, regla 1).** El grado es transversal a las diez familias precisamente porque
**cualquiera de ellas puede tener su excepción**: la portería y la creación también, no sólo la carnicería
—que es donde hoy se concentran doce de los trece `ruleBreaker` (MEDIDO)—.
3. **Falta la portería.** Es el único puesto con un **conjunto de acciones propio** (`Shoot=0`, `Mark=0`,
   `FindSpace=0`, `CoverSpace 700`; MEDIDO) y tiene el mejor perk del juego. Meterlo en «defensa» garantiza
   que se le sigan diseñando perks de defensa. → **Portería** como familia.

**«Herencia/carnicería» se separa**, además: la carnicería es un **canal** (`lethal`, `injure`, árbitro) y la
herencia es un **mecanismo** (contador). Su fantasía es distinta —«mata» contra «acumula»— y mezclarlas
produce justo los diez acumuladores invisibles que la auditoría midió. La herencia se renombra **Desgaste**
para atarla al recurso central de la run.

## 3.2 Las diez familias por comportamiento

| # | familia | qué comportamiento toca | canal técnico dominante | nº en el catálogo final |
|---|---|---|---|---|
| 1 | **Remate** | cuándo y desde dónde dispara | `Shoot`, `shootRangeBonusCells` | 6 |
| 2 | **Creación** | qué pase elige y a quién | `ShortPass/LongPass/ThroughPass/Dribble`, receptor | 7 |
| 3 | **Espacio** | dónde se coloca y hasta dónde se aleja | `modifyLeash`, hogar, `FindSpace` | 6 |
| 4 | **Defensa** | a quién marca y cuándo entra | `MarkOpponent/Tackle`, objetivo de marca | 6 |
| 5 | **Presión** | si va a por el balón o espera | `ChaseBall/PressCarrier/Retreat` | 4 |
| 6 | **Portería** | si sale, hasta dónde, y cómo saca | `CoverSpace/ChaseBall/LongPass`, correa | 4 |
| 7 | **Personalidad** | cómo cambia según el momento del partido | intención + reloj/marcador | 6 |
| 8 | **Carnicería** | cuánta carne cuesta | `lethal`, `injure`, `modifyBias`, `cancelEvent` | 7 |
| 9 | **Desgaste** | qué historia acumula este jugador | contador **de carne**, visible | 5 |
| 10 | **Pacto** | con quién juega | vínculo, composición, efecto sobre terceros | 6 |
| | | | **total** | **57** |

---

# PARTE 4 — 80 CANDIDATOS

Formato de cada ficha: `**Nombre** · Nivel · Complejidad(1-3) · Motor · ¿Bloqueado hoy?` y tres líneas.
Las capacidades de motor (C1…C16) están definidas en la Parte 6. Todas las «magnitudes» están ausentes a
propósito: la fantasía se enuncia **sin cifras** (criterio 1 del Perk Design Test).

## 4.1 Remate (ofensivo) — 9 candidatos

**Cazagoles** · N1 · C1 · C1+C2 · **sí**
Fantasía: dentro del área no piensa, tira.
Modifica: prefiere `Shoot` sobre `ShortPass` en el tercio rival. Puesto: delantero.
En pantalla: recibe de espaldas y se gira a rematar en vez de descargar atrás. Sinergia: Desmarque profundo. Compite: Hombre objetivo.

**Amenaza de larga distancia** · N2 · C1 · C1+C4 · **sí**
Fantasía: si ve portería, dispara; la distancia le da igual.
Modifica: `shootRangeBonusCells` (**geometría**: cambia dónde el tiro entra siquiera en la comparación) + prefiere `Shoot` sobre `LongPass`. Puesto: centrocampista.
En pantalla: arma la pierna desde fuera del área mientras los demás siguen buscando el pase. Sinergia: Rompe-líneas. Compite: Director.

**Cazador de rechaces** · N2 · C2 · C1+C2 · **sí**
Fantasía: vive del balón que escupe el portero.
Modifica: prefiere `ChaseBall` sobre `FindSpace` **solo con balón suelto en el tercio rival** (`chaseBallLooseBonus` ya distingue el balón suelto, MEDIDO). Puesto: delantero.
En pantalla: tras cada parada hay alguien ya en el rechace. Sinergia: Cazagoles. Compite: No vuelve.

**Sangre fría** · N2 · C2 · C1+C2 · **sí**
Fantasía: con el portero delante no se precipita.
Modifica: prefiere `Dribble` sobre `Shoot` cuando el tiro está tapado (`shootBlockedLanePenalty` existe, MEDIDO); una casilla más y entonces tira. Puesto: delantero.
En pantalla: en vez de estrellarla en el defensa, se la lleva un paso y luego define. Sinergia: Extremo vertical. Compite: Cazagoles.

**Llegador** · N2 · C2 · C1+C2+C8 · **sí**
Fantasía: es un centrocampista, pero aparece en el área como un delantero.
Modifica: prefiere `FindSpace` avanzado sobre `OfferSupport` en transición ofensiva; hogar desplazado hacia adelante solo en ese estado. Puesto: centrocampista.
En pantalla: el equipo ataca y hay un centrocampista dentro del área. Sinergia: Director. Compite: Ancla.

**Hombre objetivo** · N2 · C2 · C1+C6 · **sí**
Fantasía: todo el equipo juega para él.
Modifica: **preferencia de receptor** — los compañeros lo eligen antes en `LongPass`/`ThroughPass`; él prefiere `ShortPass` (descargar) sobre `Dribble`. Puesto: delantero.
En pantalla: el balón le busca a él una y otra vez y él la deja. Sinergia: Torre. Compite: Cazagoles.

**Oportunista** — **REDUNDANTE, se descarta.** Es «Cazagoles» con otro nombre: mismo canal (`Shoot`), misma
condición (cerca de portería), misma conducta observable. *(Crítica a la lista del revisor.)*

**Segundo palo** — **INVIABLE con este motor.** Exige un **centro** y un remate al segundo palo. No existe
`PlayerAction.Cross` ni ninguna resolución aérea de centro: `AerialDuel` es un `EventType` de disputa, no una
acción elegible (MEDIDO, `StateMachine.LegalActions` no la contiene). Haría falta **una acción nueva, su
resolución, su vuelo y su duelo** — un sistema nuevo (C14) que desbloquea 3 perks. **Recomendación: no.**

**Centrado** — **INVIABLE por lo mismo** (C14). Si algún día se hace el centro, este y Segundo palo entran
juntos; hasta entonces, la banda se expresa con geometría (§4.3), que sí existe.

## 4.2 Creación (pase) — 10 candidatos

**Director** · N1 · C1 · C1 · **sí**
Fantasía: el balón pasa por él, siempre.
Modifica: sube `ShortPass` y `OfferSupport`; es el que siempre está disponible. Puesto: centrocampista.
En pantalla: cada salida de balón pasa por el mismo verde. Sinergia: Llegador. Compite: Conductor. **Primer perk propio del centrocampista.**

**Rompe-líneas** · N2 · C1 · C1+C2 · **sí**
Fantasía: no juega en horizontal: busca el pase que parte la defensa.
Modifica: prefiere `ThroughPass` sobre `ShortPass` cuando hay compañero en carrera. Puesto: centrocampista.
En pantalla: en vez de la pared lateral, suelta el balón a la espalda de los centrales. Sinergia: Desmarque profundo. Compite: Director.

**Cambio de orientación** · N2 · C1 · C1+C2 · **sí**
Fantasía: cuando su lado se atasca, manda el balón al otro.
Modifica: prefiere `LongPass` sobre `ShortPass` cuando el pasillo corto está poblado (`passBlockedLanePenalty`, MEDIDO). Puesto: centrocampista/defensa.
En pantalla: bajo presión, en vez de tocarla corta, la cruza al otro lado. Sinergia: Pegado a banda. Compite: Primer toque.

**Conductor** · N1 · C1 · C1 · **sí**
Fantasía: sale de la presión con el balón en el pie, no con un pase.
Modifica: sube `Dribble` y baja nada; en la banda contendiente del pentágono con balón (42-71 %, MEDIDO) basta. Puesto: centrocampista.
En pantalla: recibe rodeado y arranca; los demás la sueltan. Sinergia: Extremo vertical. Compite: Primer toque, Director.

**Primer toque** · N2 · C2 · C1+C2 · **sí**
Fantasía: el balón no se le para en los pies.
Modifica: prefiere `ShortPass` sobre `Dribble` **en el primer tick tras recibir**. Puesto: centrocampista.
En pantalla: la suelta antes de pararse, mientras el de al lado conduce tres casillas. Sinergia: Director. Compite: Conductor. *(Fusiona `steady_hands` + `fine_touch`.)*

**Central constructor** · N2 · C2 · C1+C2 · **sí**
Fantasía: un defensa que **saca el balón jugado** en vez de despejarlo.
Modifica: prefiere `LongPass`/`ThroughPass` sobre `ShortPass` cuando arranca desde su propio tercio. Puesto: defensa.
En pantalla: el central levanta la cabeza y busca al delantero en vez de dársela al de al lado. Sinergia: Torre. Compite: Despejador. **Llena el hueco medido: «el defensa que saca el balón jugado no existe».**

**Despejador** · N2 · C1 · C1+C2 · **sí**
Fantasía: en su área no se juega: se despeja.
Modifica: prefiere `LongPass` sobre `ShortPass`/`Dribble` en su propio tercio bajo presión. Puesto: defensa.
En pantalla: la echa fuera; nadie intenta salir jugando desde la frontal. Sinergia: Ancla. Compite: Central constructor. *(El despeje no necesita acción nueva: es el pase largo bajo presión.)*

**Cerebro** — **REDUNDANTE con Director.** La idea del revisor («el que piensa») no nombra ningún
comportamiento distinto del motor: ambos acaban en `ShortPass`/`LongPass` subidos. Si se quiere conservar el
nombre, debe ser un **Pacto** (§4.10, «Capitán de juego»), no un perk de creación. *(Crítica.)*

**Interior** · N2 · C2 · C8 · **sí**
Fantasía: juega entre líneas, ni en la banda ni en el centro.
Modifica: hogar desplazado al semiespacio y zona estrecha; `FindSpace` restringido a ese corredor. Puesto: centrocampista.
En pantalla: siempre aparece en el mismo hueco, entre el lateral y el central rivales. Sinergia: Rompe-líneas. Compite: Pegado a banda.

**Comodín** — **FUERA DEL PARTIDO, no es un perk de motor.** «Puede jugar en cualquier puesto» es una regla
de **plantilla y alineación**, no una conducta en el campo: se implementa en la pantalla de Equipo y en
`RunLineup`, no en `Utility`. Buena idea, capa equivocada. *(Crítica.)*

## 4.3 Espacio (movimiento + geometría) — 9 candidatos

**Ancla** · **N3b** · C1 · C1+C2 · **sí**
Fantasía: en su tercio no pasa nadie; fuera de él no sirve para nada.
Modifica: sube `Tackle`/`Block` en su tercio y **rehúsa** `Tackle` fuera de él, subiendo `Retreat`. Puesto: defensa (`positionOnly`).
En pantalla: pierde un balón en el medio y **arranca de vuelta como si tiraran de una cuerda**. Sinergia: Pareja de centrales. Compite: Rompe-líneas. *(Fusiona `bulwark_stance`+`own_third_anchor`+`pit_veteran`+`game_management`+`last_ditch`.)*

**No vuelve** · **N3b** · C1 · C1+C2 · **sí**
Fantasía: el delantero que no defiende. Nunca.
Modifica: **rehúsa** `ChaseBall` y `PressCarrier` fuera del tercio rival; sube `FindSpace` dentro. Puesto: delantero (`positionOnly`).
En pantalla: el rival saca de banda en su campo y **hay uno nuestro parado en su área, solo**. Es a la vez la ventaja y el precio. Sinergia: Cazagoles. Compite: Presión tras pérdida. **Caso canónico del 3b: `ChaseBall` lidera su estado 2,2× (MEDIDO); subir `FindSpace` exigiría ×724.**

**Último hombre** — **REDUNDANTE con Ancla.** Misma geometría, mismo canal, misma imagen en pantalla. Si se
quiere conservar, debe ser el **Pacto** «el que cubre cuando el otro sube» (§4.10). *(Crítica.)*

**Desmarque profundo** · N2 · C2 · C1+C2+C8 · **sí**
Fantasía: vive al borde del fuera de juego que no existe.
Modifica: relaja el techo de línea de `FindSpace` (`findSpaceLineMarginCells`, MEDIDO: hoy **impide** acampar a espaldas de la defensa) y sube `FindSpace` en transición ofensiva. Puesto: delantero.
En pantalla: cuando el equipo recupera, ya está corriendo a la espalda del central. Sinergia: Rompe-líneas. Compite: Hombre objetivo. **La capacidad existe hoy como límite: el perk la abre.**

**Pegado a banda** · N1 · C1 · C8 · **sí**
Fantasía: no se despega de la cal.
Modifica: zona estrecha en columna, hogar pegado al lateral; su `FindSpace` solo encuentra hueco ahí. Puesto: extremo/carrilero.
En pantalla: el campo se abre porque hay alguien que **nunca** entra al centro. Sinergia: Cambio de orientación. Compite: Interior. *(Absorbe `flank_specialist` y la mitad de `wing_overlap`.)*

**Carrilero** — **REDUNDANTE con Pegado a banda** en la implementación (misma zona en columna); solo se
distingue si además **recorre** el carril, es decir, si su zona es larga en vez de ancha. Se conserva
**como variante explícita** con ese matiz y nombre propio: **Carrilero** = zona muy larga y muy estrecha,
`Retreat` y `FindSpace` altos, todo lo demás igual. Es el límite de lo que admito como «perk distinto». *(N1 · C1 · C8 · sí.)*

**Extremo vertical** · N2 · C1 · C1+C2 · **sí**
Fantasía: solo sabe ir hacia adelante.
Modifica: prefiere `Dribble` sobre `ShortPass` cuando tiene campo por delante (`dribbleOpenSpaceBonus`/`dribbleOpponentAheadPenalty`, MEDIDO). Puesto: extremo.
En pantalla: recibe en banda y encara, no la devuelve. Sinergia: Pegado a banda. Compite: Primer toque.

**Roots** *(racial, conservar)* · — · C1 · existe · no
Fantasía: no lo mueven.
Modifica: `Immovable`. Puesto: cualquiera. En pantalla: lo cargan y no se va. Se conserva sin cambios.

**Torre** · **N3b** · C2 · C1+C2+C6 · **sí**
Fantasía: no se mueve a buscar el balón; el balón viene a él.
Modifica: **rehúsa** `FindSpace` en posesión (la casilla DOMINADA por excelencia: 966 contra 240, MEDIDO) y sube `OfferSupport`; preferencia de receptor a su favor en pases largos. Puesto: delantero (`positionOnly`).
En pantalla: mientras todos se mueven, uno se queda quieto en el punto de penalti esperando. Sinergia: Central constructor, Cambio de orientación. Compite: Desmarque profundo.

## 4.4 Defensa — 8 candidatos

**Perro de presa** · N1 · C2 · C1+C5 · **sí**
Fantasía: le asignas un hombre y no lo suelta.
Modifica: **objetivo de marca** = el rival de más técnica (sesgo en la función de coste de `Marking.Assign`, que ya tiene `RolePreferenceCells`, MEDIDO) + sube `MarkOpponent`. Puesto: centro/defensa.
En pantalla: el crack rival recibe y **el mismo verde está encima otra vez**. Sinergia: Guardaespaldas. Compite: Cobertura.

**Marcador** — **REDUNDANTE por vacío.** «Marca mejor» es literalmente `MarkOpponent`, que todo defensa hace
ya como acción líder de su estado (600 de 600, MEDIDO). Sin un **criterio de objetivo** distinto no hay perk.
*(Crítica: es el ejemplo de la lista del revisor que no pasa el criterio 4 del Design Test.)*

**Guardaespaldas** · N2 · C3 · C1+C5 · **sí**
Fantasía: protege a un compañero: quien se acerque a él, lo tiene encima.
Modifica: **objetivo de marca** = el rival más cercano al compañero vinculado. Puesto: defensa/centro.
En pantalla: el pequeño del equipo recibe y aparece el grande. Sinergia: Pareja de centrales. Compite: Perro de presa. *(Exige que `Marking.Assign` acepte una referencia a un tercero: extensión pequeña, C5.)*

**Anticipador** · — (oficio) · C1 · existe · **no**
Fantasía: lee el pase antes de que salga.
Modifica: cuota de `Intercept`. Puesto: defensa/centro.
En pantalla: el informe cuenta intercepciones. **Es oficio, no conducta, y está bien que lo sea**: uno de los 13 de §1.3. Sinergia: Cobertura. Compite: Perro de presa.

**Cobertura** · N2 · C2 · C1+C2 · **sí**
Fantasía: no va al balón: tapa el hueco por donde vendrá.
Modifica: prefiere `CoverSpace` sobre `ChaseBall` sin posesión (69-100 % del líder para un medio: banda contendiente, MEDIDO). Puesto: defensa.
En pantalla: mientras dos van al balón, uno se coloca entre el balón y su portería. Sinergia: Ancla. Compite: Recuperador.

**Pareja de centrales** · N2 · C2 · C1+C2 · **sí**
Fantasía: dos centrales que no se despegan de la línea mientras el otro siga en pie.
Modifica: mientras el vinculado esté en el campo, ambos prefieren `MarkOpponent` sobre `Retreat`; si cae, se apaga. Puesto: defensa (par).
En pantalla: dos defensas clavados en la frontal tras un córner; cuando uno se lesiona, el otro empieza a replegar como todos. Sinergia: Ancla. Compite: Presión tras pérdida. *(Rediseño de `back_to_back`, hoy 0 % de activación en 21 partidos.)*

**Hombre libre** · N2 · C3 · C5 · **sí**
Fantasía: nadie lo marca, porque nadie sabe qué es.
Modifica: los **rivales** lo saltan al asignar marcas (penalización en el coste del emparejamiento contrario). Puesto: centrocampista.
En pantalla: siempre está solo cuando recibe. Sinergia: Director. Compite: Hombre objetivo. **Único perk que toca la IA del rival: potente y hay que medirlo con cuidado.**

**Cobarde con ojo** · **N3b** · C2 · C1+C2 · **sí**
Fantasía: no entra nunca, pero por eso siempre está de pie y bien colocado.
Modifica: **rehúsa** `Tackle` y `Block`; sube `CoverSpace` e `Intercept` (oficio). Puesto: defensa/centro.
En pantalla: el rival le encara y **no salta**: se echa atrás y espera. Y no comete faltas. Sinergia: Anticipador. Compite: Ancla, Kamikaze. *(Es el rasgo `Coward` —`Tackle 45`, MEDIDO— convertido en decisión del jugador en vez de sorteo.)*

## 4.5 Presión / recuperación — 6 candidatos

**Presión tras pérdida** · N2 · C3 · C1+C2+C9 · **sí**
Fantasía: perder el balón arriba es la señal para saltar todos.
Modifica: al perder posesión en el tercio rival, **todo el equipo** prefiere `PressCarrier` sobre `Retreat` durante unos segundos. Puesto: cualquiera (es de equipo). Es el **maestro de La Avalancha**.
En pantalla: se pierde en la frontal rival y **cinco saltan a la vez** en vez de replegar. Sinergia: No vuelve (mal: choca). Compite: Ancla. *(Rediseño de `high_press_trigger`.)*

**Recuperador** · N1 · C1 · C1 · **sí**
Fantasía: el primero en llegar al balón suelto, siempre.
Modifica: sube `ChaseBall`. Puesto: centrocampista.
En pantalla: cada segunda jugada la gana el mismo. Sinergia: Director. Compite: Cobertura.

**Incansable** · N2 · C2 · C1+C3 · **sí (C3)**
Fantasía: el único que sigue corriendo al final.
Modifica: en el **último tramo del partido**, prefiere `ChaseBall`/`PressCarrier` sobre `Retreat`. Puesto: centrocampista.
En pantalla: minuto final, todos andan, uno persigue. Sinergia: Recuperador. Compite: Segundo aire. **Bloqueado hoy: `UtilityContext` no conoce el tick (MEDIDO).** *(Rediseño de `iron_lungs`, hoy sube resistencia, el único atributo que no entra en la utilidad.)*

**Último esfuerzo** — **REDUNDANTE con Incansable.** Mismo disparador (final de partido), mismo canal
(perseguir/presionar). Uno de los dos sobra. *(Crítica.)*

**Kamikaze** · N1 · C2 · C1+C7 · **sí**
Fantasía: entra a todo aunque se rompa él.
Modifica: sube `Tackle` y `Block` sin condición, y **aumenta su propia** probabilidad de lesión. Puesto: cualquiera.
En pantalla: entradas que ganan el balón y lo dejan a él en el suelo. Sinergia: Sed de sangre. Compite: Cobarde con ojo. **Es el único perk con coste en carne propia y va al corazón de la identidad del juego.**

**Trampa** · N2 · C3 · C1+C2+C8 · **sí**
Fantasía: deja pasar al rival y le cierra la puerta detrás.
Modifica: prefiere `CoverSpace` por delante del portador y, cuando este entra en su zona, `Tackle`. Puesto: defensa.
En pantalla: el rival conduce sin oposición y de repente está rodeado. Sinergia: Pareja de centrales. Compite: Presión tras pérdida. **HIPÓTESIS: puede no ser legible en pantalla; candidato a caer en la prueba ciega (J.3).**

## 4.6 Portería — 5 candidatos

**Portero líbero** *(ampliación de `sweeper_keeper`)* · N2 · C1 · C1+existe · parcial
Fantasía: un portero que juega de último defensa.
Modifica: correa permanente mayor; prefiere `ChaseBall` sobre `Retreat` fuera del área. Puesto: portero.
En pantalla: sale a la frontal a despejar un pase en profundidad; dos minutos después el mismo pase entra por el otro lado y **la portería está vacía**. Sinergia: Ancla. Compite: Guardameta clásico. **El modelo a imitar; hoy existe pero dura una jugada.**

**Guardameta clásico** · N2 · C1 · C1+C2 · **sí**
Fantasía: de su línea no se mueve.
Modifica: prefiere `CoverSpace` sobre `ChaseBall` siempre; correa reducida. Puesto: portero.
En pantalla: nunca sale, y nunca lo pillan fuera. Sinergia: Ancla. Compite: Portero líbero. **Anti-sinergia limpia y legible con el anterior: la primera pareja excluyente que no es un maestro.**

**Saque largo** · N2 · C1 · C1+C2 · **sí**
Fantasía: no saca en corto: la manda arriba.
Modifica: prefiere `LongPass` sobre `ShortPass` al reanudar (hoy `ShortPass 600` contra `LongPass 320`, MEDIDO: banda alcanzable, basta subir). Puesto: portero.
En pantalla: cada saque de puerta cruza el medio campo. Sinergia: Torre, Hombre objetivo. Compite: Central constructor.

**Manos seguras** · — (oficio) · C1 · existe · **no**
Fantasía: lo que le llega, se queda.
Modifica: cuota de `Save`, y menos rechaces. Puesto: portero. En pantalla: el informe cuenta paradas. Sinergia: Guardameta clásico. Compite: Cazador de rechaces (del rival).

**Red de seguridad** *(`safety_net`, rediseño)* · N2 · C2 · C5 · **sí**
Fantasía: se coloca según dónde esté su defensa, no según dónde esté el balón.
Modifica: su `CoverSpace` toma como referencia al defensa más retrasado. Puesto: portero.
En pantalla: la defensa sube y el portero sube con ella. Sinergia: Portero líbero. Compite: Guardameta clásico. *(Hoy la condición es `nearAlly(...,3)` en el tick: opaca e inútil.)*

## 4.7 Personalidad / estado — 10 candidatos

**Rabia** · N2 · C2 · C1+C2+C7 · **sí**
Fantasía: si le hacen una falta, se la devuelve.
Modifica: tras recibir falta, prefiere `Tackle`; y su objetivo es **quien se la hizo**. Puesto: cualquiera.
En pantalla: lo derriban y, dos jugadas después, va a por el mismo. Sinergia: Sed de sangre. Compite: Sangre fría. **La mejor conversión de «estado» en «conducta» de la lista del revisor.**

**Matón** — **REDUNDANTE con Sed de sangre** (§4.8): ambos son «entra más y hace daño». Uno de los dos sobra
y el que debe quedarse es el de Carnicería, que además tiene el canal `injure`. *(Crítica.)*

**Héroe** · N2 · C2 · C1+C3 · **sí (C3)**
Fantasía: cuando el equipo va perdiendo, se echa el partido a la espalda.
Modifica: yendo por detrás en el marcador, prefiere `Shoot` y `Dribble` sobre el pase. Puesto: delantero/centro.
En pantalla: 0-1 y uno empieza a intentarlo todo él solo. Sinergia: Especialista en finales. Compite: Director. **Bloqueado: el marcador no llega a `UtilityContext` (MEDIDO).**

**Especialista en finales** · N2 · C2 · C1+C3 · **sí (C3)**
Fantasía: en el último tramo es otro jugador.
Modifica: en el último tramo, sube su acción de identidad (la que ya tiene). Puesto: cualquiera.
En pantalla: el mismo que jugó plano toda la primera parte aparece al final. Sinergia: cualquiera. Compite: Incansable. **HIPÓTESIS: «sube lo que ya hace» puede ser invisible; medir con la prueba ciega.**

**Segundo aire** · N2 · C2 · C1+C3+C13 · **sí (C3, C13)**
Fantasía: cuando los demás se cansan, él arranca.
Modifica: cuando la fatiga del equipo supera un umbral, sube `ChaseBall`/`Dribble`. Puesto: cualquiera.
**Bloqueado dos veces**: ni el reloj ni la fatiga entran en la decisión (la resistencia **no aparece en
`Utility.cs`**, MEDIDO). Compite: Incansable. **Recomendación: aplazar; C13 es caro y desbloquea 2 perks.**

**Cobarde** — **ABSORBIDO** por «Cobarde con ojo» (§4.4). Como perk puramente negativo choca de frente con la
ADR 0088 y no aporta nada; con su ventaja emparejada es uno de los cinco 3b. *(Crítica.)*

**Sobrevive / Superviviente** — **SON EL MISMO** y hay que quedarse con uno. Se conserva como **Superviviente**
en Desgaste (§4.9), donde el contador tiene sentido. *(Crítica.)*

**Veterano** — **REDUNDANTE y además es el patrón que la auditoría midió como roto**: contador «por partido
jugado», invisible, sin decisión. Si se conserva el nombre, su contador debe ser **de carne** (§4.9).
*(Crítica.)*

**Capitán** — **REDUNDANTE con el rasgo `Leader`**, que ya da `adjacentTeammateBonusPercent` y cuyo bono ya
entra en la fórmula (MEDIDO, `Utility.cs:160`). Como perk solo tendría sentido si hiciera algo que el rasgo
no hace: ver §4.10, «Capitán de juego». *(Crítica.)*

**Nervios** · N2 · C2 · C1+C3 · **sí (C3)**
Fantasía: cuanto más importante es el partido, peor decide.
Modifica: yendo por delante en el último tramo, prefiere `Retreat` y `LongPass` (achicar) sobre todo lo demás.
En pantalla: con 1-0 el equipo se mete atrás porque **uno** se mete atrás y arrastra el bloque. Sinergia: Ancla. Compite: Héroe. **HIPÓTESIS: probablemente se percibe como un malus; candidato a descartar en revisión.**

## 4.8 Carnicería — 8 fichas (11 perks: los cuatro letales van en una)

**Sed de sangre** *(`blood_tithe`, conservar)* · — · C2 · existe · **no** — maestro de La Carnicería.

**Iron studs / Marrow thirst / Second wound / Skullsplitter** *(los 4 letales, conservar sin cambios)* · — · C1-C2 · existe · **no**
Fantasía: mata. En pantalla: alguien no se levanta. **Son la única regla que el motor rompe de verdad y la identidad del juego.**

**Olfato de sangre** · N2 · C2 · C1+C7 · **sí**
Fantasía: va a por el que ya está tocado.
Modifica: **objetivo de entrada** = el rival que llegó al partido lesionado o con amarilla (`PhysicalState` y `YellowCards` son legibles, MEDIDO) + sube `Tackle` contra él. Puesto: defensa/centro.
En pantalla: un rival cojea y **el mismo jugador va derecho a él** en la jugada siguiente. Sinergia: los letales. Compite: Perro de presa. **Es «carnicería administrada» hecha conducta, y es previsible en el ojeo.**

**Instigador** *(`mob_instigator`, rediseño)* · N2 · C2 · C1+C2 · **sí**
Fantasía: cuando el árbitro se va, es su momento.
Modifica: en la turba, él y sus adyacentes prefieren `Tackle`/`Block` sobre todo lo demás. Puesto: cualquiera.
En pantalla: empieza la turba y **tres se lanzan** mientras el resto sigue jugando al fútbol. *(Hoy anula faltas donde ya no se pitan: valor medido −7, el peor del catálogo.)*

**Cara de inocente** *(`home_ref`, rediseño; RF-064f)* · — (regla) · C1 · existe · **no**
Fantasía: hace la entrada y pone cara de no haber roto un plato.
Modifica: sus faltas mueven el criterio del árbitro la mitad. En pantalla: **la barra de criterio es visible todo el partido (RF-062)** y baja menos de lo que debería. **Observabilidad garantizada por una pantalla que ya existe.**

**Iron gate** *(conservar sin cambios)* · — (regla) · C1 · existe · **no** — una lesión por partido no ocurre.

**Carnicero ciego** · **N3b** · C2 · C1+C2 · **sí**
Fantasía: no persigue el balón; persigue al hombre.
Modifica: **rehúsa** `ChaseBall`; sube `Tackle` y `Block`. Puesto: centro/defensa (`positionOnly`).
En pantalla: el balón pasa por su lado y él sigue yendo a por el rival. Sinergia: Sed de sangre. Compite: Recuperador. **El quinto 3b, y el más caro en carne propia y ajena.**

## 4.9 Desgaste (herencia) — 7 candidatos

Regla de familia: **el contador cuenta carne y se ve en el retrato.** Ningún contador «por partido jugado
por el equipo» sobrevive (la auditoría midió diez de ese tipo, todos invisibles).

**Veterano de cicatrices** · N1 · C2 · C1+C11 · **sí**
Fantasía: cada lesión superada le quita un poco de miedo.
Modifica: por cada **lesión propia superada**, sube `Tackle` un escalón, hasta un techo. Puesto: cualquiera.
En pantalla: el central que volvió de dos lesiones **entra a todo**. Sinergia: Kamikaze. Compite: Cobarde con ojo.

**Superviviente** · — (regla) · C2 · C11 · **sí**
Fantasía: cuantos más compañeros pierde, más difícil es matarlo a él.
Modifica: por cada **compañero muerto en la run**, más resistencia a la lesión grave. Puesto: cualquiera.
En pantalla: sobrevive a una entrada que mató a otro. **Ata el perk al recurso central de la run.**

**Sed acumulada** · N1 · C2 · C1+C11 · **sí**
Fantasía: cada rival que deja en el suelo lo vuelve más peligroso.
Modifica: por cada lesión **causada**, sube el canal `injure`. Puesto: cualquiera. En pantalla: el retrato lleva la cuenta y se ve subir.

**Puntería aprendida** · — (oficio) · C2 · C11 · **sí**
Fantasía: cada gol le enseña dónde está la portería.
Modifica: por cada gol propio, sube `shotOnTarget`. Puesto: delantero. *(Único acumulador de oficio; sustituye a `sharpshooter_drill` y `poacher_instinct`.)*

**Memoria del pasillo** · — (oficio) · C2 · C11 · **sí**
Fantasía: cada pase que le robaron le enseña por dónde no pasarla.
Modifica: por cada intercepción sufrida, sube `interceptEvasion`. Puesto: centro. *(Contador **empujable con una decisión**: jugar corto lo sube despacio, jugar largo lo sube rápido.)*

**Quick learner** — **FUERA DEL SISTEMA DE PERKS.** Da experiencia: no toca el partido, no se ve, y su sitio
natural es un **rasgo de club o de raza**. Ocupa un slot irreversible para nada. *(Crítica al catálogo actual.)*

**Numb** *(racial no-muerto)* — **BLOQUEADO POR UNA PROMESA INCUMPLIDA.** Concede inmunidad al **duelo**
(RF-104) y **el duelo no puede ocurrirle a nadie**: el sistema de vínculos RF-100..106 está declarado,
serializado y tarifado, pero `new RunBond(...)` solo aparece en el **cargador** del guardado (MEDIDO,
`tres-puntos-resueltos.md`). Su descripción generada promete lo que el motor no hace: **choca con RT-035 y con
la regla 11**. O se implementa C12, o se retira esa mitad del texto.

## 4.10 Pacto / composición — 8 candidatos

**Socio de ataque** · N2 · C2 · C1+C6 · **sí**
Fantasía: dos que se buscan a ciegas.
Modifica: preferencia mutua de receptor en `ShortPass`/`ThroughPass`. Puesto: delantero + centro.
En pantalla: la pelota va del mismo al mismo, jugada tras jugada. Sinergia: Rompe-líneas. Compite: Hombre objetivo.

**Capitán de juego** · N2 · C3 · C1 · **sí**
Fantasía: los que juegan a su lado se atreven a más.
Modifica: los compañeros con hogar adyacente suben `Dribble` y `ThroughPass`. Puesto: centrocampista.
En pantalla: la zona donde él está juega distinto a la otra. **Es el «Capitán» del revisor convertido en algo que el rasgo `Leader` no hace ya** (hoy `Leader` sube todo por igual: §1.4-1, el caso exacto de multiplicador uniforme que no cambia el `argmax`).

**Relevo** · N2 · C3 · C1+C2 · **sí**
Fantasía: cuando uno sube, el otro cubre. Nunca los dos a la vez.
Modifica: si el vinculado está por delante del centro del campo, él prefiere `CoverSpace`; y al revés. Puesto: pareja lateral/centro.
En pantalla: suben y bajan alternándose, nunca juntos. Sinergia: Pegado a banda. Compite: Presión tras pérdida. *(Es lo que «Último hombre» y `wing_overlap` querían ser.)*

**Pareja de centrales** — ya listada en §4.4; su casa es Defensa.

**Manada** *(`pack_mentality`, conservar y reorientar)* · N1 · C2 · C1 · **sí**
Fantasía: cuantos más brutos hay en el equipo, más se envalentonan todos.
Modifica: sube `Tackle` en función del número de compañeros con la etiqueta. Puesto: cualquiera. **Previsible al alinear** (`teammatesWithTag` ya lo resuelve `LineupPerkPreview`, MEDIDO).

**Gigante amable** *(`gentle_giant`, conservar)* · N2 · C2 · C1+C5 · **sí**
Fantasía: el grande protege al pequeño.
Modifica: su marca prefiere al rival más pesado cuando su vinculado es ligero. Puesto: defensa.

**Sombra** *(`covering_shadow`, rediseño)* · N2 · C2 · C1+C2 · **sí**
Fantasía: juega a la sombra del que va delante.
Modifica: mantiene su posición **detrás** del vinculado: su `FindSpace` solo acepta casillas por detrás de él. Puesto: centro.
En pantalla: nunca están los dos a la misma altura.

**Diagonal** *(`diagonal_press`, rediseño)* · N2 · C3 · C1+C2 · **sí**
Fantasía: cuando uno salta, el otro tapa la diagonal.
Modifica: si el vinculado elige `PressCarrier`, él prefiere `CoverSpace` hacia el interior.
En pantalla: la presión llega en pareja y con forma. **HIPÓTESIS: depende de la decisión de otro jugador en el mismo tick; exige orden de resolución explícito (RT-041) y puede ser difícil de leer.**

## 4.11 Resumen de la crítica a las 45 ideas del revisor

| veredicto | ideas |
|---|---|
| **Entran tal cual o casi** (28) | Cazagoles, Cazador de rechaces, Hombre objetivo, Desmarque profundo, Presión tras pérdida, Director, Rompe-líneas, Conductor, Llegador, Ancla, Recuperador, Cambio de orientación, Perro de presa, Central constructor, Despejador, Anticipador, Guardaespaldas, Pegado a banda, Interior, Extremo vertical, Carrilero, Sangre fría, Kamikaze, Incansable, Pareja de centrales, Socio de ataque, Hombre libre, Superviviente |
| **Redundantes con otra de la lista** (9) | Oportunista (=Cazagoles) · Cerebro (=Director) · Marcador (=MarkOpponent, que ya es el líder de su estado) · Último hombre (=Ancla) · Último esfuerzo (=Incansable) · Matón (=Sed de sangre) · Sobrevive (=Superviviente) · Veterano (=contador roto) · Capitán (=rasgo `Leader`) |
| **Reubicadas de capa** (2) | Comodín → alineación, no motor · Quick learner → rasgo de club |
| **Inviables con este motor** (2) | Segundo palo, Centrado — exigen la acción **centro** y su duelo aéreo (C14, sistema nuevo) |
| **Absorbidas con ventaja emparejada** (2) | Cobarde → «Cobarde con ojo» · Rabia → conservada, mejorada con objetivo |
| **Bloqueadas por el reloj/marcador** (3) | Héroe, Especialista en finales, Segundo aire (C3, y C13 para el tercero) |

---

# PARTE 5 — FILTRADO A 57

## 5.1 El criterio aplicado

Los cuatro eliminatorios del Perk Design Test (auditoría §16): **[E1]** una frase sin cifras · **[E2]** verbo
de conducta · **[E3]** atribuible en una jugada · **[E4]** distinto de todo lo que existe (canal + condición +
magnitud). Más el criterio del revisor, que es el que manda en los empates: **«¿un jugador reconocería la
diferencia jugando?»**

Y la regla de reparto, que es la que separa este documento de la auditoría:

> **Concepto único > perk único.** Si seis nombres significan «Shoot + X», es **un** perk. Pero seis perks que
> producen **conductas distintas** pueden coexistir aunque los seis acaben en más goles: la métrica de
> redundancia es el **comportamiento observable**, no el resultado agregado.

Ejemplo de la asimetría, que conviene dejar escrito: `bulwark_stance` y `own_third_anchor` producen **el mismo
partido** (MEDIDO en la auditoría: mismas entradas en los mismos ticks) → **un perk**. En cambio Cazagoles,
Amenaza de larga distancia, Cazador de rechaces, Sangre fría, Llegador y Hombre objetivo acaban todos en más
goles y producen **seis partidos distintos** → **seis perks**.

## 5.2 Qué pasa con cada uno de los 61 actuales

**CONSERVAR sin cambios o casi — 18**
`iron_studs`, `marrow_thirst`, `second_wound`, `skullsplitter` (los cuatro letales) · `iron_gate` · `roots` ·
`elf_touch` · `brute_boots` · `numb` *(condicionado a C12)* · `hot_blooded` · `gentle_giant` ·
`blood_tithe`, `granite_line`, `first_touch_school` (maestros) · `sweeper_keeper` *(ampliado)* ·
`pack_mentality` · `long_leash_legacy` · `unlikely_bulwark`.

Los tres últimos y `sweeper_keeper` son, con `iron_gate` y los letales, **los nueve que la auditoría midió
como los únicos que ya funcionan**: correa, conducta o regla rota. Son la plantilla del resto.

**FUSIONAR — 25 ficheros → 11**

| fusión | de (25) | a (11) |
|---|---|---|
| «×N entrada» | `bulwark_stance`, `own_third_anchor`, `pit_veteran`, `game_management`, `last_ditch`, `back_to_back` (6) | **Ancla** · **Pareja de centrales** (2) |
| «×N tiro» | `box_predator`, `cold_focus`, `forward_line`, `long_range_menace`, `killing_range` (5) | **Cazagoles** · **Amenaza de larga distancia** · `killing_range` degradado a raro sin línea (3) |
| acumuladores | `battle_reader`, `lane_reader`, `sharpshooter_drill`, `silky_veteran`, `clean_sheet_legacy`, `scar_tissue`, `poacher_instinct` (7) | **Puntería aprendida** · **Memoria del pasillo** (2) |
| toque | `fine_touch`, `steady_hands` (2) | **Primer toque** (1) |
| vínculos | `pivot_duo`, `spearpoint`, `wing_overlap`, `covering_shadow`, `diagonal_press` (5) | **Relevo** · **Sombra** · **Diagonal** (3) |

**REDISEÑAR conservando el fichero — 6**
`crowd_control` → **Perro de presa** · `shadow_marker` → **Olfato de sangre** · `high_press_trigger` →
**Presión tras pérdida** · `scar_veteran` → **Veterano de cicatrices** · `mob_instigator` → **Instigador** ·
`safety_net` → **Red de seguridad**.

Los seis comparten diagnóstico: **la condición era opaca** (proximidad en el tick, etiqueta del rival, fallo
propio, contador del equipo) y la auditoría midió que los siete perks opacos valen de −1 a 5 con activaciones
del 0 % al 9 %. El rediseño **conserva el fichero y la fantasía y cambia la condición por una previsible**.

**ELIMINAR sin sustituto — 8**
`bruised_knuckles`, `captains_voice`, `center_conductor`, `comeback_spirit`, `deathless_march`,
`fine_orchestra`, `natural_leader`, `road_warrior`.

**Justificación incómoda, y hay que escribirla:** `deathless_march` (valor medido **178**) y `captains_voice`
(**104**) están entre los perks de **más valor del juego**, y caen. Son contadores que suben solos: el jugador
que gana gracias a ellos no tiene forma de saber que ganó gracias a ellos (la auditoría lo llamó
**correlación inversa entre potencia y memorabilidad**). `battle_reader` (**150**) no se elimina, se **fusiona**
en un acumulador con contador visible. **DERIVADO: quitar los dos primeros baja el valor medio del pool y hay
que compensarlo en la curva de recompensas, no fingir que no pasa.** Exige medición, no estimación.

**SACAR DEL SISTEMA DE PERKS — 4**
`iron_lungs` (+resistencia: **MEDIDO, el único atributo que no aparece ni una vez en `Utility.cs`**) →
**objeto** · `quick_learner` (experiencia, no toca el partido) → **rasgo de club** · `home_ref` → conservado
como **regla de árbitro** (RF-064f), no como multiplicador · `flank_specialist` → absorbido por la geometría
de **Pegado a banda**.

**Cuadre de los 61**: 18 conservados + 25 fusionados + 6 rediseñados + 8 eliminados + 4 reubicados = **61** ✓
**Ficheros supervivientes**: 18 + 11 + 6 = **35**.

## 5.3 El catálogo final

**35 supervivientes + 22 nuevos = 57 perks** (48 obtenibles + 9 raciales). Reparto por familia, ya dado en
§3.2: Remate 6 · Creación 7 · Espacio 6 · Defensa 6 · Presión 4 · Portería 4 · Personalidad 6 ·
Carnicería 7 · Desgaste 5 · Pacto 6.

**Cobertura por puesto** *(el criterio que hoy falla, MEDIDO: el centrocampista tiene cero perks propios)*:

| puesto | perks que lo nombran | antes |
|---|---|---|
| Portero | 4 | 3 |
| Defensa | 11 | 8 (monocanal: entrada) |
| **Centrocampista** | **13** | **0** |
| Delantero | 10 | 8 (monocanal: tiro) |
| Transversales (cualquiera) | 19 | 42 |

**Comprobación del techo de `modifyProbability`** *(la auditoría pedía ≤ 40 %)*: 13 de 57 = **23 %**. Dentro.

**Comprobación de la cuota del registro B** *(RF-069, ≤ 10 %)*: **pendiente, y hoy no se puede hacer.** El
catálogo final se reparte por familia y por nivel, pero **no declara cuáles de sus 57 fichas son
`ruleBreaker`**, y la fila «sin nivel» de §1.3 mezcla oficio, regla rota y contador. **Requisito para cerrar
la Parte 5: cada ficha declara su registro (A o B), y las de B no pasan de seis.** Sin eso el catálogo puede
cuadrar en familias y puestos y estar fuera de RF-069 sin que nadie lo note — que es como llegó el catálogo
actual a tener dos rompe-reglas que sólo son un número.

---

# PARTE 6 — HOJA DE RUTA DE MOTOR

Capacidades agregadas, ordenadas por **cuántos perks del catálogo final desbloquea cada una**.

## 6.1 Existe hoy y basta usarlo mejor

| cap | qué es | perks que lo usan |
|---|---|---|
| **C15** | balón suelto (`chaseBallLooseBonus`), pasillo tapado (`passBlockedLanePenalty`, `shootBlockedLanePenalty`), techo de línea (`findSpaceLineMarginCells`), rango de tiro (`shootBaseRangeCells`) — **todos ya son estado legible en la decisión** | 8 |
| — | `modifyLeash` (precondición **dura**: `Utility` descarta la acción fuera del límite exterior) | 5 |
| — | `lethal`, `cancelEvent`, `immunity`, `modifyBias` | 7 |
| — | `LineupPerkPreview` con sus cinco funciones y el aviso flotante de la pantalla de Equipo | todos los previsibles |

## 6.2 Extensión pequeña (días, no semanas)

| cap | qué es | perks | por qué es pequeña |
|---|---|---|---|
| **C1** | **`modifyUtility(acción,%)`** → overlay sobre `_actionMultipliers` | **41** | El motor **ya compone** un segundo multiplicador mutable en la misma línea: `LeaderBonusPercent`, escrito en tiempo de partido por `MatchEngine.RecomputeLeaderBonuses` (MEDIDO). Pasar de escalar a `int[14]` es la misma pieza |
| **C2** | **Vocabulario cerrado de situaciones en la decisión** (tercio del actor, fase de posesión —ya está en `ctx.TacticalStates`—, con/sin balón, balón suelto, turba) evaluado en C# dentro de `Utility.Choose` | **31** | Enteros y comparaciones; **nada de NCalc por tick**: `CompiledCondition` guarda contexto en la instancia y no es reentrante. **Es la capacidad que define el Nivel 2 y hoy no existe** |
| **C8** | Desplazamiento de casilla-hogar y forma de zona por perk | 9 | `EffectiveHome` ya se mueve cada tick con `blockShift` (MEDIDO); falta un desplazamiento por jugador |
| **C3** | **Tick y marcador en `UtilityContext`** | 5 | Dos enteros. **MEDIDO: `UtilityContext` no tiene reloj ni marcador**, y por eso toda la familia de «momento del partido» está bloqueada |
| **C4** | `modifyTraitBonus(campo, valor)` sobre los **13 escalares por jugador** que hoy solo escriben los rasgos (`ShootRangeBonusCells`, `HardTackleBonus`, `SaveBonusFar`, `FoulChanceBonus`, `InjuryChanceBonus`…) | 6 | Mismo patrón que `AddAttributeDelta`. **`ShootRangeBonusCells` es geometría, no cuota**: decide si el tiro entra siquiera en la comparación |
| **C5** | Sesgo de objetivo en `Marking.Assign` | 5 | La función de coste **ya tiene** un término de preferencia (`RolePreferenceCells = 2.0`, MEDIDO): se añade un sesgo por jugador |
| **C7** | Criterio de objetivo de entrada (rival tocado, amonestado, o el que le hizo falta) | 4 | `Utility` ya elige entre portador y marcado; `YellowCards` y `Definition.PhysicalState` son legibles |
| **C6** | Preferencia de receptor de pase | 4 | `EvaluatePass` ya ordena receptores; se añade un sesgo |
| **C11** | **Contadores de carne por jugador** (cicatrices propias, muertos del equipo, lesiones causadas) + su lectura en el retrato | 5 | `accumulatesAcrossMatches` ya vuelca contadores a `PlayerDefinition.Counters` (MEDIDO). Falta **qué** se cuenta y **enseñarlo** |
| **C10** | **Techo por acción y por posición en el cargador**, error explícito (RT-032) | prerrequisito de C1 | Tabla de validación. Un `×3` a `Shoot` en un portero debe ser un **error de datos**, no una anécdota |
| **C16** | **Plantillas de descripción desde la intención** (l10n) | prerrequisito de §2.4 | RT-035 se conserva entero: lo que cambia es que hay un efecto **conductual** del que generar una frase de conducta |
| **C17** | **Canal de impulso: un efecto que escribe sobre el cuerpo y sobre el balón** — el cuerpo entra por el búfer de `BodySeparation`, que ya acumula empujes en esquema de Jacobi con tope por tick y orden por id (MEDIDO); el balón entra por `Ball`, que ya tiene `Velocity`, `Z`, `VelocityZ` y `FlightArc` | registro **B** (≤ 6) | **No hay física nueva ni canal de dibujo nuevo**: la traza ya graba posición, estado y balón (x, y, z) por fotograma y `/Game` ya los pinta. Es la forma «impone física» de §1.6.B, y hoy le falta la mitad del balón: **cero de 19 tipos de efecto lo tocan** |
| **C18** | **Vocabulario cerrado de gesto declarado en el dato**, leído genéricamente por `/Game`, más un `MomentKind` de perk | prerrequisito de **todo** el registro B | Es el patrón que el repositorio ya usa dos veces (`MomentKind`, `ContactCue`) y la **única** forma de que un perk se vea sin que `/Game` nombre perks (regla 5) ni decida partido (RT-014). Sin ella, C17 produce sucesos anónimos: §1.6.C regla 3 |
| **C9** | **`PerkTriggered(perkId, ownerId, tick)` en el flujo de eventos** | **0 mecánicos, todos perceptivos** | **MEDIDO: las activaciones viven en `MatchReport.PerkActivations`, que solo lee la pantalla de post-partido; `MatchScreen` no dibuja ninguna.** Durante los 60-90 s el jugador no ve un solo perk dispararse. `/Sim` no decide presentación (RT-014): deja de esconder lo que ya calcula |

## 6.3 Sistema nuevo

| cap | qué es | perks | veredicto |
|---|---|---|---|
| **C12** | **Vínculos RF-100..106**, incluido el duelo (RF-104) | 3 + arregla `numb` | **MEDIDO: no existe.** `new RunBond(...)` solo aparece en el cargador del guardado; `Bonds` nace y muere vacío, y `MarketSystem` ya multiplica un precio por un número que siempre vale 0. Los perks de Pacto de este catálogo usan `linked` (vínculo **estático de alineación**, que sí existe), así que **el catálogo no depende de C12** — pero `numb` sí, y la regla 11 obliga a resolverlo |
| **C13** | Fatiga en la decisión | 2 | **MEDIDO: la resistencia no aparece ni una vez en `Utility.cs`.** Caro y desbloquea dos perks. **Recomendación: no ahora** |
| **C14** | Acción **centro** + duelo aéreo de centro | 3 | Acción nueva, vuelo nuevo, resolución nueva, y presión sobre `injuriesPerMatch`, que ya está en el techo de RT-056. **Recomendación: no.** Las tres fantasías de banda se cubren con geometría (C8) |

## 6.4 Orden de trabajo recomendado

**Tanda 0 — instrumento, antes de tocar nada.** Histograma de acción elegida por jugador y partido (§1.4) y
su distancia L1 contra el control emparejado de la ADR 0087. **Sin esto no hay forma de saber si un perk de
intención hace algo**, y la línea base tiene que tomarse antes del primer `modifyUtility`.

**Tanda 1 — C1 + C10 + C9, con dos perks y nada más.** «Ancla» (Nivel 3b, necesita la decisión del revisor) y
«Cazagoles» (Nivel 1, no la necesita). Métricas de corte, todas ya en banda y sin presupuesto libre:
`injuriesPerMatch` (0,75 sobre techo 0,90), `shotsPerMatch` (8,41 sobre suelo 8,00), `ballThirdMaxShare`
≤ 52, cadena de pases (ADR 0111), `runWinRate` 22-27. **Si dos perks conductuales sacan de banda tres de
esas cinco, lo que falta es techo por posición, no menos ambición.**

**Tanda 2 — C2 y la capa de intención (§1.6) con C16.** Es la que convierte a `modifyUtility` en mecanismo y
no en idioma, y la que hace legibles las 40 ofertas por run.

> **Puerta de la tanda 2, añadida por la corrección del 25 sep 2026.** Es la tanda que **congela el
> vocabulario**, así que es la última oportunidad barata de que quepa el registro B. No se cierra sin las
> dos comprobaciones: **(a)** ningún verbo lleva «por jugador» en su firma —los efectos se declaran sobre un
> objetivo, §1.6.B—, y **(b)** el registro B tiene al menos su forma «niega» y su forma «repite» expresadas
> como verbos, aunque su catálogo de fenómenos siga sin decidir. Congelar el lenguaje sin esto es el
> escenario que la auditoría 13B nombra: 57 fichas escritas contra un vocabulario que no contiene la
> excepción, y reabrirlo después cuesta las 57.

**Tanda 2b — C18 y después C17.** En ese orden y no al revés: **C18 primero**, porque es lo que convierte un
suceso en un momento atribuido, y porque es barata y no toca ninguna regla (es la quinta pregunta de §6.5,
que ya estaba abierta). **C17 después**, empezando por el balón y no por el cuerpo: el balón no gasta del
presupuesto de violencia (§1.6.C, regla 5) y su canal de dibujo está entero. Métrica de corte: un verbo de B
no entra si mueve `injuriesPerMatch` o `shotsPerMatch` fuera de banda.

**Tanda 3 — C8, C3, C4, C5, C6, C7.** Geometría, reloj, bonos de rasgo y objetivos. Aquí entra el grueso del
catálogo.

**Tanda 4 — C11 y la reducción del catálogo por fases.** Métrica de corte: activación ≥ 25 % en al menos una
build coherente para **todos** los supervivientes.

## 6.5 Lo que hay que decidir antes de escribir la primera línea

1. **¿Se admite el sub-100 con las cinco condiciones de §1.5?** De la respuesta dependen **cinco perks** de
   cincuenta y siete, y son los cinco de personalidad más memorables. Si la respuesta es no, el catálogo
   pierde 5 y queda en 52; el resto del documento **no cambia**.
2. **¿Se acepta la regla «una identidad por jugador» (§2.3)?** Es la que hace que 57 quepan en 14-20 slots y
   la que resuelve la dilución sin tocar la ADR 0088.
3. **¿Se rebasan las líneas sobre doctrinas de equipo (§2.5)?** Implica degradar `killing_range` de maestro a
   raro y crear el maestro de La Avalancha.
4. **RF-104 / C12:** ¿se implementan los vínculos o se retira esa mitad del texto de `numb`? Hoy la
   descripción generada promete lo que el motor no puede dar.
5. **¿Se emite la activación como evento (C9)?** Es barato, no toca ninguna regla, y **sin ello ningún
   rediseño se nota**.
6. **¿El lenguaje de intención debe poder expresar efectos que no sean «por jugador» ni «multiplicador de
   prioridad»?** — **DECIDIDO por el revisor el 25 sep 2026: sí.** *«Los perks comunes modifican cómo juega
   la máquina; los perks excepcionales pueden hacer que la máquina haga cosas que el fútbol normal no puede
   hacer.»* Consecuencias, todas ya incorporadas: §1.6 se reescribe en dos registros, §6.2 gana C17 y C18,
   §6.4 gana la puerta de la tanda 2 y la tanda 2b, y la Parte 5 no se puede cerrar sin declarar el registro
   de cada ficha. **Lo que esta decisión NO abre**: el estado de mundo (13B §4.D) sigue sin decidir; lo
   único que se decide sobre él es no cerrarle la puerta en la firma de los verbos.
