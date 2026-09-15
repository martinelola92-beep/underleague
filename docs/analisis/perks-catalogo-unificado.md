# Catálogo unificado de perks — Fase A + las ~95 ideas del revisor

**Qué es esto.** La fusión de las **76 fichas** de `catalogo-conceptual-fase-a.md` con las **~95 ideas**
aportadas por el revisor (campo · balón y disparo · movimiento · economía · reglas especiales), pasada por
los cuatro eliminatorios, clasificada por **tipo de impacto** (PD-6) y —la aportación principal— por
**tanda de coste de motor**: qué se puede construir hoy, qué necesita una primitiva nueva compartida, y qué
está detrás de `modifyUtility`.

**No se ha modificado ningún fichero del repositorio salvo este.** Ni código, ni JSON, ni pesos, ni
probabilidades. No se ha lanzado ningún lote de `/Balance`. Este documento **no propone ni un número**.

**Etiquetas:** **MEDIDO** (leído del código, de `/data` o de un experimento ya ejecutado) · **DERIVADO**
(conclusión encadenada sobre algo medido) · **HIPÓTESIS** (interpretación de diseño sin validar).

**Decisiones que obligan** (`docs/pendientes.md`): **PD-1** un perk sí puede penalizar si expresa identidad ·
**PD-2** vínculos RF-100..106 fuera de alcance (se usa `linked`, el vínculo estático de alineación) ·
**PD-3** taxonomía de líneas congelada · **PD-4** 5-6 miembros funcionalmente distintos por familia ·
**PD-5** `killing_range` congelado · **PD-6** los perks pueden disparar habilidades; cinco tipos de impacto;
**ninguna primitiva nueva entra si la comparten menos de tres perks**.

---

## 0. Las tres conclusiones, antes de la lista

**(1) La tanda 1 existe y es la identidad declarada del juego.** *(DERIVADO)* **21 de los 63 perks del
catálogo final se construyen hoy, sin tocar la IA de utilidad**: los cuatro letales, la sed de sangre, el
terremoto, el que juega roto, el que se cura solo, el que engaña al árbitro, el que no pita, el que no
muere, el portero que saca una que iba dentro, el defensa que aparece de la nada, y los tres perks que
convierten cuerpos en oro. Esa tanda **no es un subconjunto arbitrario: es la carnicería administrada
entera**, y se puede jugar sola.

**(2) La clase que el revisor ha aportado —las habilidades— es la barata; la clase de la Fase A —las
preferencias— es la cara.** *(DERIVADO, y es contraintuitivo)* De las ~95 ideas nuevas, las de campo y
reglas especiales caen casi todas en tanda 1 o 2, porque usan `KnockDown`, `AddTacklePush`, `cancelEvent`,
`immunity`, reubicación y `CounterDeltas`, que **existen**. De las 76 fichas de Fase A, **42 necesitaban
`modifyUtility`** y siguen necesitándolo. Invertir el orden de trabajo —habilidades antes que
preferencias— entrega juego visible antes y deja el recalibrado del motor para después.

**(3) La regla de las tres compartidas mata siete primitivas y con ellas catorce ideas.** *(DERIVADO)*
Caen: intangibilidad (2 consumidores), sacar del campo (1), repetir una tirada (1), ignorar bloqueo de
línea (2), redirigir un evento a otro jugador (2), debuff a rival (1 tras depurar), y preferencia de
receptor / C6 (2). Las siete que sí entran están en §3.2. **La regla es el filtro más productivo del
encargo**: no descarta por gusto, descarta por censo.

---

## 1. Correcciones de instrumento que se conservan de la Fase A

Se mantienen las dos correcciones ya adoptadas, y se añade una tercera que sale de las ideas nuevas.

1. **El oficio puro no es un perk, es una estadística.** *(MEDIDO: 34 de 34 objetos suben atributos y nada
   más.)* `modifyProbability` sobre una tirada que no cambia la rama del resultado va a `/data/items`.
   **Excepción admitida:** cuando el canal es la **rama del resultado** y no la tirada (la parada que se
   queda contra la que escupe). Las ideas nuevas añaden dos consumidores a esa excepción y la convierten en
   primitiva legítima (§3.2, «el balón se queda»).
2. **[E3] se lee «atribuible en un cuerpo»** para la clase **[carne]**: existe un momento de la run —una
   factura de clínica que no se paga, un cuerpo que sigue en la plantilla— tras el cual el jugador dice
   «eso ha sido el perk».
3. **Nuevo — [E3] para ABILITY es más duro, y tiene dos mitades.** *(Criterio del revisor, aceptado)*
   (a) *¿puede el jugador describir el momento exacto en que se disparó, sin consultar el informe?*
   (b) *¿es la condición algo que el jugador pueda **disponer** antes del partido?* Terremoto cumple las
   dos (lo coloco donde habrá aglomeración). «Cuando el equipo pierde por dos» cumple (a) y **falla (b)**:
   eso te pasa, no lo dispones. **Consecuencia:** las condiciones de marcador y de reloj sirven para
   PREFERENCE (que no exige disponibilidad, porque describe una identidad permanente) y **no** para ABILITY.

---

## 2. Clasificación por tipo de impacto (PD-6)

| tipo | qué es | objetivo PD-6 | catálogo final |
|---|---|---|---|
| **PREFERENCE** | cambia **qué quiere hacer**: el `argmax` de la utilidad, la correa, el hogar, el objetivo | 25-30 | **33** |
| **ABILITY** | introduce una acción o un efecto que no existía | 10-15 | **11** |
| **RULE** | cambia **cuándo o por qué**: cancela, inmuniza, mueve el criterio del árbitro, rompe una regla | 5-10 | **10** |
| **META** | economía, progresión, plantilla | 5-10 | **6** |
| **HYBRID** | excepcional, dos canales a la vez | — | **3** |
| | | | **63** |

> **Nota sobre el reparto objetivo.** El encargo lista los tipos en el orden `PREFERENCE · RULE · ABILITY ·
> META` con las cifras `25-30 · 10-15 · 5-10 · 5-10`; **PD-6, que es la decisión vinculante, dice
> literalmente «25-30 preferencia/comportamiento · 10-15 habilidades · 5-10 economía/metajuego · 5-10 reglas
> excepcionales»**. Se adopta PD-6: **ABILITY 10-15, RULE 5-10**. Si el revisor quería lo contrario, la
> corrección es barata: seis PREFERENCE de canal `cancelEvent`/`immunity` se releen como RULE.

**PREFERENCE queda tres por encima de su banda (33 contra 25-30), y eso es un resultado, no un descuido.**
*(DERIVADO)* El catálogo de la Fase A **es** un catálogo de preferencias: 42 de sus 76 fichas pedían
`modifyUtility`. Bajar de 33 exige borrar doctrinas enteras (El Toque y El Envío son diez perks, todos
PREFERENCE), no afinar. La recomendación es aceptar 33 y recortar por **fichas enteras** si hace falta, como
pide PD-4.

**El total son 63, tres por encima de la banda 50-60.** Los tres primeros candidatos a caer, por orden:
`Capitán de juego` (efecto sobre terceros, invisible sin C9), `Sombra` (comparte C8 y lectura de tercero
con `Relevo`) y `Doble disparo` (si cae, la primitiva «acción extra» se queda en dos y arrastra a
`Embestida` y `Arrollador`: **es el recorte más caro de los tres y debería ser el último**).

### 2.1 Los once ABILITY, pasados por el test del revisor

| perk | ¿describe el momento sin el informe? | ¿la condición se **dispone** antes? |
|---|---|---|
| Terremoto | sí — cae todo el mundo a su alrededor | sí: lo coloco donde habrá aglomeración |
| Embestida | sí — entra en línea y aparta cuerpos | sí: puesto y carril de salida |
| Arrollador | sí — derriba y sigue andando | sí: es el mismo cuerpo que el anterior |
| Cañón | sí — arma la pierna desde el centro del campo | sí: puesto y altura de inicio |
| Doble disparo | sí — dispara dos veces seguidas | sí: es el que remata |
| Mano de dios | sí — saca una que ya iba dentro | sí: es el portero |
| Último hombre | sí — aparece delante del delantero de la nada | sí: lo alineo de último defensa |
| Juego sucio | sí — la falta deja a uno en el suelo | sí: es el que más entra |
| No hay árbitro | sí — entra fortísimo y el árbitro no pita | sí: es el que más entra |
| Prohibido morir | sí — el que debía morir se levanta | sí: lo alineo sabiendo que es él |
| **Sed acumulada** | **no del todo** — la escalada se ve **entre** partidos, no en el momento | sí: se empuja entrando |

**`Sed acumulada` es el único ABILITY que no pasa (a) limpio.** Se conserva porque el contador **sí** es
visible entre partidos (C11, en el retrato) y porque su condición se empuja con una decisión; pero **queda
marcado: sin C11 visible y sin C9, es un multiplicador con historia y debe caer.**

---

## 3. Clasificación por tanda de coste

### 3.1 TANDA 1 — construible hoy (21 perks)

Solo primitivas existentes. Las diez verificadas en el encargo, más las cinco que la Fase A midió como
existentes en `/data` y que aquí se usan igual: `modifyLeash`, `lethal`, `modifyBias`,
`modifyKnockdownTicks` y el estado de run (clínica, prótesis, muerte).

| perk | primitiva que usa |
|---|---|
| Bloque bajo | `modifyLeash` de alcance equipo con **signo negativo** (PD-1) |
| Guardameta clásico | `modifyLeash` negativo por jugador (PD-1) |
| Portero líbero | `modifyLeash` positivo |
| Distancia de tiro *(maestro, PD-5 congelado)* | `modifyProbability` de alcance equipo |
| Sed de sangre *(maestro)* | `modifyProbability` de alcance equipo sobre `injure` |
| Los cuatro letales | `lethal` |
| Terremoto | `MatchEngine.KnockDown(player, ticks)` sobre los contiguos |
| Curtido | estado de run (la lesión leve no llega a la clínica) |
| A media pierna | estado de run + atributos efectivos |
| Cara de inocente | `modifyBias` |
| Teatrero | `modifyBias` con el sujeto invertido |
| No hay árbitro | `cancelEvent` sobre la falta |
| Juego sucio | `KnockDown` + `injure` colgados de la falta cometida |
| Raíces | `ImmunityKind` / `MatchPlayer.Immovable` |
| Puerta de hierro | `cancelEvent` sobre la lesión |
| Prohibido morir | `cancelEvent` sobre la muerte |
| Mano de dios | `cancelEvent` sobre el gol, con contador de 1 por partido |
| Último hombre | reubicación (fijar `Position`), disparada por contador+evento |
| Ídolo local | `MatchResult.CounterDeltas` → oro (`MatchResolution.cs:272` ya lo aplica) |
| Taquillero | contador por jugador + `CounterDeltas` → oro |
| Máquina de publicidad | contador de lesiones causadas + `CounterDeltas` → oro |

**Familias cubiertas:** **El Árbitro** entera menos un miembro (4 de 5) · **La Carne Ajena** en su mitad
letal (3 de 6) · **La Carne Propia** en su mitad de clínica (2 de 5) · los dos extremos de la correa
(**Bloque Bajo** 2 de 5, **La Presión** 1 de 5) · **el maestro de El Remate** · los **universales** enteros
(5 de 6) · **El Negocio** en su mitad de partido (3 de 6).

**Qué se puede jugar si solo se construye la tanda 1** *(DERIVADO)*: **la carnicería administrada completa,
sin tocar una línea de `Utility.cs`.** Un equipo que mata (cuatro letales + sed de sangre), que tira gente
al suelo sin que sea falta (terremoto), que engaña al árbitro y a veces se salta la falta entera, que
alinea rotos y no paga clínica, que tiene un hombre al que la muerte no le toca y un portero que saca una
imposible, y que **convierte cuerpos en oro** por tres canales distintos. Más las dos posturas extremas del
equipo: el bloque metido atrás y el portero adelantado.

**Lo que NO se puede jugar con la tanda 1: ninguna doctrina de balón.** *(MEDIDO: el `argmax` de
`Utility.cs` no lo escribe ningún perk.)* Ni El Toque, ni El Envío, ni La Presa, ni la mitad conductual de
La Presión. Con la tanda 1 el jugador decide **qué le pasa a los cuerpos**; no decide **a qué juega el
equipo**.

### 3.2 TANDA 2 — primitiva nueva compartida (20 perks, 7 primitivas)

**Regla dura de PD-6 aplicada:** solo entra la primitiva que compartan **tres o más** perks.

| primitiva | consumidores | nº | veredicto |
|---|---|---|---|
| **C8 · desplazamiento de hogar y forma de zona por jugador** | Línea adelantada · Pivote hondo · Desmarque profundo · Sombra | 4 | **entra**. `EffectiveHome` ya se mueve cada tick con el desplazamiento de bloque; falta el desplazamiento **por jugador** |
| **C5 · sesgo de objetivo en el reparto de marcas** | Perro de presa · Guardaespaldas · Hombre libre *(aplicado al emparejamiento contrario)* | 3 | **entra, justo**. La función de coste de `Marking.Assign` ya tiene término de preferencia |
| **C7 · criterio de objetivo de entrada** | Olfato de sangre · Rabia · Carnicero ciego *(comparte, aunque su tanda es la 3)* | 3 | **entra, al límite**: dos propios y uno compartido. Si `Carnicero ciego` cae, C7 se queda en dos y arrastra a los otros dos |
| **C4 · que un **perk** escriba sobre los trece escalares de rasgo** | Cañón (`ShootRangeBonusCells`) · Kamikaze · Pagar el hierro (`InjuryChanceBonus`) | 3 | **entra**. Aclaración necesaria: el **campo** existe y el encargo lo lista como primitiva existente, pero **MEDIDO: hoy solo lo escriben los rasgos**; lo que no existe es el efecto de perk que lo escribe |
| **acción extra tras un evento** | Doble disparo · Embestida · Arrollador | 3 | **entra**. Es la primitiva con más riesgo de coste oculto: una acción fuera del tick rompe el orden determinista si no se resuelve dentro del mismo tick con orden explícito (RT-041) |
| **C11 · contadores de carne por jugador, visibles** | Sed acumulada · Superviviente · Taquillero | 3 | **entra**. El volcado entre partidos ya existe; falta **qué** se cuenta y **enseñarlo en el retrato** |
| **run-level: oro y atributos al salir de la plantilla** | Seguro de vida · Herencia · Préstamo | 3 | **entra**. No toca `/Sim`: es estado de run y mercado |

**Las siete primitivas que NO entran, y los catorce perks que caen con ellas** *(la aplicación literal de
PD-6; es el filtro que más ideas mata)*:

| primitiva rechazada | consumidores | por qué cae |
|---|---|---|
| **intangibilidad** | Fantasma · Atajo *(Salto se funde en Fantasma por [E4])* | 2. Y `Atajo` ya moría por [E3]: un salto de una casilla es indistinguible de un movimiento normal |
| **sacar del campo temporalmente** | Destierro | 1. **Salvable** reescrito como derribo muy largo (`KnockDown`, existe), pero entonces es `Sangre caliente` con otra magnitud y muere por [E4] |
| **repetir una tirada** | Segunda oportunidad *(funde Tramposo y Milagro)* | 1. **Salvable** como `cancelEvent` sobre la acción fallida, pero «la acción no ocurrió» no es «se repitió» y no se ve |
| **ignorar bloqueo de línea** | Obús *(funde Misil)* · Pase imposible | 2. **Salvable** reescrito como C4 sobre `shootBlockedLanePenalty` / `passBlockedLanePenalty`, que el motor **ya calcula**; pero entonces son dos `modifyProbability` con nombre bonito y mueren por la Corrección 1 |
| **redirigir un evento a otro jugador** | Escudo humano · Pararrayos *(funde Sacrificio)* | 2. **Es la baja que más duele**: la fantasía («lo que iba a otro le llega a él») es buena y muy del juego. Entra el día que aparezca un tercero — el candidato natural sería «la tarjeta se la lleva otro», que no está en la lista |
| **debuff a rival** | Terror · Intimidación · Provocador | 3 nominales, **1 tras depurar**: Terror e Intimidación son debuffs de atributo temporales, y **MEDIDO: los siete perks de proximidad en el tick valen de −1 a 5 con activaciones del 0 % al 9 %**. Sin ellos, Provocador se queda solo |
| **C6 · preferencia de receptor de pase** | Socio de ataque · Hombre objetivo *(funde Torre)* | 2. Los dos son el mismo sesgo sobre el orden que `EvaluatePass` ya calcula, con distinta condición. Con un tercero entraría, y sería barata |

**Aparte, C3 (reloj y marcador en `UtilityContext`) tiene cuatro consumidores y aun así no se construye.**
*(DERIVADO)* Pasa la regla de las tres, pero sus cuatro perks —Héroe, Verdugo, Arranque, Revulsivo— caen por
otros motivos (§5 y §6): son la familia **El Momento**, se queda en cuatro miembros y **PD-4 exige 5-6**.
Son dos enteros de coste; la decisión de no gastarlos es de diseño, no de motor.

**Qué se puede jugar con tanda 1 + tanda 2** *(DERIVADO)*: se completan **La Presa** entera (el que va al
hombre y no al balón, el que va al roto, el que devuelve la falta), **La Carne Ajena** y **La Carne
Propia** enteras —con lo que se abre por fin el par disputado «no puedes cobrarte carne y conservar la
tuya», que la Fase C clasificó como el más barato de los tres—, **El Negocio** entero, y la geometría que
da las bandas y los espacios (C8). Siguen sin existir El Toque y El Envío.

### 3.3 TANDA 3 — necesita `modifyUtility` (C1) y/o vocabulario de situación (C2) (22 perks)

| familia | perks |
|---|---|
| El Bloque Bajo | Ancla · Cobertura · Despejador |
| La Presión | Presión tras pérdida · Recuperador · Cazador de porteros |
| La Presa | Carnicero ciego |
| El Toque | Director · Primer toque · Conductor · Capitán de juego |
| El Envío | Rompe-líneas · Cambio de orientación · Central constructor · Saque largo |
| El Remate | Cazagoles · Sangre fría |
| El Pacto | Pareja de centrales · Manada · Solitario · Relevo |
| El Árbitro | Instigador |

**Lo que compra la tanda 3 es el juego, no los perks.** *(DERIVADO)* Son las dos doctrinas de balón
completas (El Toque y El Envío, diez perks), la mitad conductual del Bloque Bajo y de la Presión, y el
Pacto. Y es, por definición, **un recalibrado**: un multiplicador sobre `Tackle` cambia **cuántas entradas
hay**, y las entradas son la fuente de `injuriesPerMatch`, que **MEDIDO ya está en el techo de RT-056**. La
tanda 3 se presupuesta como recalibrado (con C10, techos por acción y puesto en el cargador), no como una
tanda de datos.

---

## 4. Deduplicación: qué se fundió y qué chocaba de nombre

### 4.1 Los cinco duplicados que el revisor ya había aceptado

| duplicado | resolución | motivo |
|---|---|---|
| Retorno ≡ Retirada táctica | **uno**, y ese uno se funde además en **Ancla** | los tres dicen «fuera de su sitio no juega, vuelve»; mismo canal (`Retreat`), misma imagen |
| Resurrección ≡ Muerte fingida ≡ Prohibido morir | **Prohibido morir** | mismo canal (`cancelEvent` sobre la muerte); las tres sólo cambian cuánto dura el milagro, y [E4] dice que una magnitud distinta no es un perk distinto |
| Vendedor de camisetas ≡ Estrella ≡ Ídolo local | **Ídolo local** | «gol → oro» tres veces; «participación» y «gol de la victoria» son umbrales del mismo canal |
| Tramposo ≡ Segunda oportunidad ≡ Milagro | **Segunda oportunidad**, y **cae** | el perk resultante es el único consumidor de «repetir una tirada» (§3.2) |
| Cazador ≡ Perseguidor | **uno**, y ese uno se funde en **Perro de presa** | «se va detrás de un hombre» ya es Perro de presa; lo que Perseguidor añade (lo sigue fuera de su zona) es `modifyLeash`, que existe, y entra como parte del mismo perk |

### 4.2 Los choques de nombre (ocho, no uno)

El encargo señalaba uno. Buscándolos activamente salen **ocho**, y hay que resolverlos todos o el catálogo
tendrá dos fichas con el mismo rótulo.

| nombre en disputa | significado A | significado B | resolución |
|---|---|---|---|
| **Último hombre** | defensa que retrocede en vez de entrar | se teletransporta delante del rival que va solo | **B se queda con el nombre** (es la habilidad, y es la imagen fuerte). **A se funde en `Ancla`**, que ya era su duplicado en la Fase A |
| **Ancla** | defensa que no sale de su tercio (C-10) | no puede ser desplazado ni derribado | **A se queda con el nombre**. **B se funde en `Raíces`** (C-44), que es exactamente eso y ya existe en `/data` |
| **Relevo** | la pareja que se alterna (C-43) | ocupa la posición del compañero lesionado | **A se queda**. B pasaría a llamarse `Cubrehuecos` — y **cae por presupuesto** (§5) |
| **Sombra** | se coloca por detrás de su vinculado (C-42) | se pega a su marcado | **A se queda**. **B se funde en `Perro de presa`** |
| **Hombre libre** | los rivales lo saltan al repartir marcas (C-03) | busca el espacio más peligroso cuando todos están marcados | **A se queda** (usa C5 y es el tercer consumidor que salva esa primitiva). **B se funde en `Desmarque profundo`** |
| **Kamikaze** | entra a todo aunque se rompa él (C-13) | perdiendo por dos, conducta ofensiva extrema | **A se queda**. **B se funde en `Héroe`** — y Héroe cae con toda la familia El Momento |
| **Guardaespaldas** | marca al rival más cercano a su vinculado (C-02) | una vez por partido intercepta un tackle a un compañero | **A se queda**. **B se funde en `Escudo humano`**, que cae con su primitiva |
| **Superviviente** | cuantos más compañeros pierde, más aguanta (C-56) | cuanto más cerca de abandonar, más poderoso | **A se queda**. **B cae**: depende de la fatiga, y **MEDIDO: la resistencia no aparece ni una vez en `Utility.cs`** |

### 4.3 Duplicados nuevos encontrados (no estaban señalados)

| se funde | en | motivo |
|---|---|---|
| Pisotón | **Terremoto** | mismo canal (`KnockDown`), misma condición (contiguo), distinto número de objetivos = magnitud |
| Carga · Empujón | **Embestida** | los tres son `AddTacklePush`; «corre antes» y «lo lanza más lejos» son magnitudes |
| Misil | **Obús** | atravesar al primero o a varios es la magnitud del mismo canal |
| Francotirador · Cañón · Amenaza de larga distancia (C-30) | **Cañón** | los tres son `ShootRangeBonusCells`; «una vez por partido» es un presupuesto de frecuencia, no un perk |
| Rechace mortal | **Doble disparo** | «tras fallar» y «tras el rechace del portero» son la misma condición vista desde los dos lados |
| Última palabra · Oportunista | **Cazagoles** | preferir `Shoot` en el área es un solo perk |
| Guardián · Teletransporte | **Último hombre** | los tres son reubicación disparada por «el rival amenaza el área» |
| Cortafuegos | **Cortacircuitos** (C-05) | ponerse en la trayectoria es el mismo destino de `CoverSpace` |
| Carrera suicida | **No vuelve** (C-11) | mismo perk |
| Imán | **Recuperador** (C-06) | y la versión literal (el balón se mueve solo hacia él) **contradice la ADR 0091**, que acaba de hacer el balón físico |
| Cambio de banda | **Cambio de orientación** (C-21) | misma fantasía, misma imagen en pantalla: el juego cambia de lado |
| Torre (C-38) | **Hombre objetivo** (C-27) | **duplicado que la Fase A no vio**: los dos son «el ataque se juega para él», los dos usan C6 |
| Extremo vertical (C-24) | **Conductor** (C-18) | preferir `Dribble` sobre el pase; el puesto es un filtro, no un eje |
| Llegador (C-25) | **Desmarque profundo** (C-39) | los dos desplazan el hogar hacia adelante en transición ofensiva; distinto puesto no es distinto perk |
| Cazador de rechaces (C-29) | **Recuperador** (C-06) | `ChaseBall` sobre balón suelto; la geometría es una condición, no un perk |
| Venganza · Ojo por ojo | **Rabia** (C-53) | «se la devuelve al que se la hizo a él o a su vinculado» es un perk con la condición ensanchada |
| Cosecha | **Sed acumulada** (C-50) | «una carga por lesionado» sin decir de qué |
| Coleccionista de cicatrices | **Veterano de cicatrices** (C-57) | y «una bonificación **distinta** cada vez» no se puede escribir como dato (RT-031/RT-035) |
| Adrenalina · Berserker | **Kamikaze** (C-13) | los tres son «entra más y se rompe más»; los dos primeros llegan además con un buff de atributo temporal, **MEDIDO invisible** |
| Inspirador · Líder | **Capitán de juego** (C-69) | un multiplicador uniforme sobre los vecinos **no cambia el `argmax`**; lo único salvable es cambiar **qué** acción eligen, que es C-69 |
| Manos de cepo (C-34) · Ladrón de balones · Rebote | **Manos de cepo** — y el conjunto **cae por presupuesto** | los tres son la misma primitiva («el balón se queda controlado en vez de suelto»), y por primera vez esa excepción tiene tres consumidores: **si el revisor sube el techo del catálogo a 66, esta es la primera que debe volver** |
| Testamento | — | **ya ocurre**: la ADR 0048 devuelve el equipo del muerto al inventario. Es una regla existente, no un perk |
| Carroñero (C-61) | **Herencia** | mismo canal (transferencia al morir); y su carga útil, el equipo, ya vuelve al inventario por la ADR 0048 |
| Veterano · Patrocinador · Apostador | **Taquillero** | cuatro formas de decir «cada partido que termina de pie paga» |
| Muro | — | **ya ocurre**: `BodySeparation` impide que dos cuerpos ocupen la misma casilla. Lo salvable es `Raíces` |
| Gigante amable (C-67) | — | oficio sobre un tercero: suspende [E2] y vive del lado flojo de la Corrección 1 |

---

## 5. Los cuatro eliminatorios: las bajas, con motivo

**[E1]** una frase sin cifras · **[E2]** verbo de conducta del motor · **[E3]** atribuible en una jugada
—o **en un cuerpo**, para los [carne]— · **[E4]** distinto en canal + condición + magnitud.

### 5.1 Bajas por eliminatorio

| baja | suspende | motivo |
|---|---|---|
| Pase relámpago | [E3] + coherencia | **contradice la ADR 0091**, que hizo el pase físico tick a tick precisamente para que se pudiera interceptar. Un pase instantáneo borra la geometría que se acaba de construir |
| Volea | [E3] + coste | exige la acción **centro** y su duelo aéreo: acción nueva, vuelo nuevo, resolución nueva, y presión sobre `injuriesPerMatch`, que ya está en el techo. Y su condición no se dispone: es suerte |
| Mártir | [E3] | buff colectivo temporal, **MEDIDO invisible**; y premia perder al portador, que es lo contrario de lo que el juego quiere |
| Inspirador | [E2] | un multiplicador uniforme sobre los vecinos no cambia ninguna decisión. Se funde en Capitán de juego |
| Líder | [E1] + [E2] | «elimina una penalización de un compañero» no nombra ninguna conducta, y «penalización» no es un concepto del motor |
| Capitán de emergencia | [E3] | transferencia de un multiplicador: no hay jugada que señalar. **MEDIDO: el rasgo `Leader` ya sube todo por igual** |
| Caos | [E1] + [E3] | «acciones que normalmente no podría» no nombra nada; no existe como concepto |
| Último superviviente | [E3] | «su línea» no es un concepto del motor, y el efecto es un buff de atributo |
| Segundo aire · Último esfuerzo · Superviviente-B | [E2] | los tres piden fatiga en la decisión. **MEDIDO: la resistencia no aparece ni una vez en `Utility.cs`** |
| Cazatalentos | coste (criterio 6) | los compañeros suben de nivel igual: es un regalo sin decisión |
| Mercenario | [E4] | es Taquillero con el precio de compra cambiado; la economía ya tiene sus palancas (ADR 0096-0100) |
| Intercambio | [E3] | en pantalla es indistinguible de dos jugadores que se cruzan |
| Atajo | [E3] | un salto de una casilla no se ve |
| Intocable | [E4] | mismo canal que Raíces (`ImmunityKind`); se funde en él |
| Terror | [E3] | condición de proximidad en el tick: **MEDIDO, los siete perks opacos del catálogo valen de −1 a 5 con activaciones del 0 % al 9 %** |
| Intimidación | [E3] | debuff de atributo temporal sobre un rival: invisible |
| Comodín | capa | **PD-6 lo rescataba** («plantilla» está en la definición de META) y aun así cae: no hay hoy una penalización de puesto que ignorar, así que el perk no hace nada. **Vuelve el día que `positionOnly` tenga precio** |
| Fantasma · Destierro · Obús · Pase imposible · Escudo humano · Pararrayos · Segunda oportunidad · Provocador · Socio de ataque · Hombre objetivo | primitiva | **no suspenden ningún eliminatorio**: caen por la regla de las tres compartidas (§3.2). Son las bajas recuperables |

### 5.2 Bajas de la Fase A por presupuesto de catálogo

No suspenden nada: salen para que el total quepa y para que ninguna familia pase de seis. Si el revisor
sube el techo, **este es el orden de vuelta**: `Manos de cepo` + `Ladrón de balones` + `Rebote` (traen una
primitiva entera con tres consumidores) · `Hierro en la pierna` · `Cortacircuitos` · `Pegado a banda` ·
`Interior` · `Red de seguridad` · `De área a área` · `Sangre caliente` · `Se levanta solo` · `Correa
curtida` · `Especialista a balón parado` · `Cobarde con ojo` · `No vuelve` · `Contrataque` · `Cubrehuecos`
· `Correcaminos`.

> **Aviso sobre `Cobarde con ojo` y `No vuelve`.** *(HIPÓTESIS)* La Fase A los llamaba «contrarios
> declarados»: existen para **negar** una doctrina, y bajo PD-1 son los ejemplos más limpios de penalización
> que expresa identidad. Salen por presupuesto y por nada más. Si PD-1 se consolida en ADR, **son los dos
> primeros que deben volver**, por delante incluso de `Manos de cepo`.

---

## 6. Familias

**Método conservado de la Fase B:** se agrupa por **la conducta que producen**, nunca por canal, nombre ni
puesto, y el corte es la prueba ciega —*«un revisor que mira el partido sin saber la alineación describiría
a estos jugadores con la misma frase»*. **PD-4: 5-6 miembros funcionalmente distintos, nunca 8.**

### 6.1 Las diez familias finales

| # | familia | frase de la prueba ciega | miembros | doctrina |
|---|---|---|---|---|
| **F1** | **El Bloque Bajo** | «ese equipo no sale de su campo» | Bloque bajo · Ancla · Cobertura · Guardameta clásico · Despejador | sí, **VALIDADA** contra F2 |
| **F2** | **La Presión** | «esos saltan encima en cuanto pierden» | Presión tras pérdida · Línea adelantada · Recuperador · Cazador de porteros · Portero líbero | sí |
| **F3** | **La Presa** | «ese tío va a por la gente, no a por la pelota» | Perro de presa · Guardaespaldas · Olfato de sangre · Rabia · Carnicero ciego | sí, **VALIDABLE** contra F2 (falta C1+C2) |
| **F4** | **El Toque** | «esos tocan y no la pierden» | Director · Primer toque · Conductor · Pivote hondo · Capitán de juego | sí, **VALIDABLE** contra F5 |
| **F5** | **El Envío** | «esos la mandan arriba en cuanto pueden» | Rompe-líneas · Cambio de orientación · Central constructor · Saque largo · Desmarque profundo | sí |
| **F6** | **El Remate** | «ese acaba las jugadas, y le da igual cómo» | **Distancia de tiro** (maestro) · Cazagoles · Sangre fría · Cañón · Doble disparo | maestro sí, contrario legítimo **no** |
| **F7** | **La Carne Ajena** | «a ese equipo se le van quedando rivales por el suelo» | **Sed de sangre** (maestro) · Los cuatro letales · Sed acumulada · Terremoto · Embestida · Arrollador | sí, **VALIDABLE** contra F8 (falta C4) |
| **F8** | **La Carne Propia** | «a ese equipo no se le rompe nadie» | Pagar el hierro · Kamikaze · Superviviente · Curtido · A media pierna | sí |
| **F9** | **El Pacto** | «esos dos juegan siempre juntos» | Pareja de centrales · Sombra · Manada · Solitario · Relevo | **no es doctrina**: no tiene contrario |
| **F10** | **El Árbitro** | «a ese le da igual la pelota: juega al árbitro» | Cara de inocente · Teatrero · No hay árbitro · Juego sucio · Instigador | **no es doctrina**: el recurso es la barra de criterio, que no se disputa con nadie |

Fuera de familia, **dos bloques**:

- **Universales** (6): Raíces · Puerta de hierro · Prohibido morir · Mano de dios · Último hombre · Hombre
  libre. Sirven en cualquier once y no arrastran doctrina. *(`Hombre libre` es además el tercer consumidor
  que salva C5: si cae, caen `Perro de presa` y `Guardaespaldas` con él.)*
- **El Negocio** (6): Ídolo local · Taquillero · Máquina de publicidad · Seguro de vida · Herencia ·
  Préstamo.

### 6.2 ¿Crean las ideas nuevas alguna familia? Sí, una — y no la que parecía

**RESCATADA: El Árbitro.** *(DERIVADO)* La Fase A la dejó en tres o cuatro miembros y la declaró «reglas
rotas sueltas». Dos ideas del revisor —**No hay árbitro** (`cancelEvent` sobre la falta) y **Juego sucio**
(la falta cometida arrastra un derribo o una lesión)— la suben a **cinco miembros funcionalmente
distintos**, y los cinco son **tanda 1 menos uno**. Es la familia más barata del catálogo y la única que se
podría entregar entera antes de tocar `Utility.cs`. Su recurso es **la barra de criterio, que el jugador ve
todo el partido (RF-062)**: la observabilidad está garantizada por una pantalla que ya existe.

**NO RESCATADA: El Momento.** *(DERIVADO)* Se queda en los cuatro de la Fase A —Héroe, Verdugo, Arranque,
Revulsivo— y **ninguna idea nueva la salva**: sus tres candidatos naturales (Último esfuerzo, Segundo aire,
Superviviente-B) piden fatiga, y **MEDIDO, la resistencia no aparece en `Utility.cs`**. Con cuatro miembros
**PD-4 la deja fuera**, y por eso C3 (dos enteros en `UtilityContext`) no se construye pese a pasar la regla
de las tres. **Recomendación:** no se abre El Momento hasta que exista un quinto miembro que no dependa de
la fatiga.

**El Choque no es una familia nueva.** *(DERIVADO, y es la corrección más útil de esta sección)* Las
habilidades de campo del revisor —Terremoto, Embestida, Arrollador, Pisotón, Empujón, Carga— parecen un
grupo propio («esos tiran gente al suelo todo el rato»), pero **fallan la prueba ciega contra La Carne
Ajena**, cuya frase es literalmente *«a ese equipo se le van quedando rivales por el suelo»*. Son la misma
descripción. **Verdicto: no hay familia nueva; entran en F7 y desplazan a sus miembros más flojos.** El
efecto secundario es bueno: F7 deja de ser cinco ficheros de `lethal` y `injure` y pasa a tener **tres
habilidades visibles** que un espectador puede señalar sin el informe.

**El Negocio no es una familia, es una clase.** *(DERIVADO)* Los seis perks META no pasan la prueba ciega
—no hay nada que ver en el partido— y se agrupan por el libro mayor, no por la conducta. Se registran como
**bloque**, igual que la Fase A hizo con los universales. Que PD-6 los legitime como tipo de impacto no los
convierte en familia.

### 6.3 Dónde cae `killing_range` (PD-5: congelado, no se toca)

**En F6 El Remate, como maestro, y sin cambios.** Se confirma el veredicto de la Fase D: su fantasía es de
remate y es el **único miembro de alcance equipo** de la familia; los otros cuatro son conductas de un
hombre y él es la consecuencia colectiva de haberlas juntado.

**Un dato nuevo que la Fase D no tenía.** *(DERIVADO)* La Fase D dejó pendiente un experimento: medir cada
miembro de F6 con el crédito de arco a cero, porque el crédito «no medía el valor del maestro, medía el
vacío de sus miembros» —ocho ficheros escribiendo `shotOnTarget`. Con el catálogo unificado, **dos de los
cuatro miembros de F6 son ahora habilidades con valor propio y visible** (`Cañón`, tanda 2 por C4;
`Doble disparo`, tanda 2 por acción extra), no cuotas. Eso **refuerza** la hipótesis de la Fase D —que los
miembros nuevos necesitan **menos** crédito, no más— pero no la sustituye: **el experimento sigue siendo
obligatorio antes de descongelar nada** (PD-5), y hay que hacerlo en el mismo lote que la medición
pendiente del 57,1 % de runs con maestro que siguen comprando de la línea cerrada
(`medicion-bloqueo-cosmetico.md`).

---

## 7. Aviso obligatorio de visibilidad

**MEDIDO** (`perks-auditoria-diseno.md` §11): de 16 perks con contador, **cero** son expresivos, **diez son
invisibles**, y los diez incluyen los tres perks de más valor del juego. **MEDIDO** (Fase A): durante los
60-90 s del partido **el jugador no ve dispararse ni un perk**. La conclusión de PD-6 es que **C9 —emitir
`PerkTriggered` al flujo de eventos— deja de ser opcional**: una habilidad invisible es peor que un
multiplicador invisible porque cuesta cincuenta veces más.

### 7.1 Las cuatro ideas que el encargo señala

Las cuatro son buffs colectivos temporales, y las cuatro **caen** —ninguna se sostiene ni siquiera con C9:

| idea | veredicto | por qué C9 no basta |
|---|---|---|
| **Mártir** | **baja** | C9 haría visible *que se disparó*, pero el efecto sigue siendo un multiplicador uniforme sobre los compañeros: **no cambia el `argmax` de nadie**, así que no hay nada que ver aunque se avise |
| **Inspirador** | **baja**, fundido en Capitán de juego | mismo motivo. Lo salvable no es el aviso: es cambiar **qué acción** eligen los vecinos, y eso es C1 |
| **Líder** | **baja** | además de invisible, no nombra conducta ([E1]/[E2]) |
| **Capitán de emergencia** | **baja** | transfiere un multiplicador que **MEDIDO ya sube todo por igual** (rasgo `Leader`); C9 avisaría de una transferencia que no cambia ninguna decisión |

> **La regla que sale de aquí, y vale para todo el catálogo futuro:** *(DERIVADO)* **C9 arregla la
> atribución, no el diseño.** Un efecto que no cambia qué intenta hacer alguien sigue sin verse aunque el
> juego lo anuncie. C9 es condición **necesaria** de visibilidad para las habilidades; para los buffs
> colectivos no es ni necesaria ni suficiente: esos hay que rediseñarlos como cambios de intención.

### 7.2 Perks del catálogo final que dependen de C9 o de C11 visible

| perk | de qué depende | qué pasa si no se construye |
|---|---|---|
| **Sed acumulada** | C11 visible + C9 | es un multiplicador con historia; **debe caer** |
| **Superviviente** | C11 visible | el contador sube solo y no se ve: es la definición de escalada no ganada |
| **Taquillero** | C11 visible | se ve en el oro, pero el jugador no sabe **por qué**; aceptable sin C11, mejor con él |
| **Capitán de juego** | C9 | sin él, el jugador no puede separar «mis vecinos juegan distinto» de la casualidad |
| **Sed de sangre** (maestro) | C9 | alcance equipo; hoy se lee en `injuriesPerMatch`, no en el partido |
| **Distancia de tiro** (maestro) | C9 | idem, y **MEDIDO es el 59 % de los maestros tomados**: es el perk más comprado del juego y nadie lo ve dispararse |
| **Pagar el hierro** | C9 | su espiral es la mitad del par disputado F7↔F8; sin aviso, «se rompió» parece mala suerte y **choca con la regla 11** |
| **Prohibido morir** · **Puerta de hierro** | ninguna | **se ven solos**: el que debía caer se levanta. Son el patrón |
| **Cara de inocente** · **Teatrero** | ninguna | **MEDIDO: la barra de criterio es visible todo el partido (RF-062)** |
| **Curtido** · **A media pierna** | ninguna | [carne]: se ven en la factura de clínica y en la alineación |

---

# 8. LA TABLA — catálogo completo, 63 perks

Ordenada **por tanda**, y dentro de cada tanda **por familia**. `PRE` = PREFERENCE, `ABI` = ABILITY,
`RUL` = RULE, `MET` = META, `HYB` = HYBRID. La columna **Coste** es siempre coste u oportunidad renunciada,
nunca un número.

## 8.1 TANDA 1 — construible hoy (21)

| Nombre | Tipo | Primitiva | Familia | Fantasía | Qué se ve en el partido | Puesto | Condición | Coste u oportunidad |
|---|---|---|---|---|---|---|---|---|
| **Bloque bajo** | PRE | `modifyLeash` equipo, signo negativo (PD-1) | F1 Bloque Bajo | El equipo entero renuncia al campo contrario | Los siete viven por debajo del medio campo; el rival juega en su área toda la tarde | cualquiera (equipo) | decidida al alinear | Regala el campo y juega a no perder: sin salida, el balón vuelve enseguida |
| **Guardameta clásico** | PRE | `modifyLeash` negativo (PD-1) | F1 Bloque Bajo | De su línea no se mueve | Nunca sale, y nunca lo pillan fuera | portero | ninguna | Los balones a la espalda de la defensa no los sale a cortar nadie |
| **Portero líbero** | PRE | `modifyLeash` | F2 Presión | Un portero que juega de último defensa | Sale a la frontal a despejar un pase en profundidad | portero | geométrica | Dos minutos después la misma jugada entra por el otro lado con la portería vacía |
| **Distancia de tiro** *(maestro, PD-5 congelado)* | HYB | `modifyProbability` equipo | F6 Remate | Cerca de la portería, este equipo no perdona | Los remates de dentro del área acaban dentro | cualquiera (equipo) | geométrica | El bloqueo de línea que arrastra hoy |
| **Sed de sangre** *(maestro)* | HYB | `modifyProbability` equipo sobre `injure` | F7 Carne Ajena | Un equipo de brutos hace más daño del que debería | El rival termina el partido con gente en la banda | cualquiera (equipo) | previsible al alinear: cuántos brutos hay en el once | Los brutos son lentos y torpes: la plantilla que lo enciende juega peor al fútbol |
| **Los cuatro letales** | RUL | `lethal` | F7 Carne Ajena | Mata | Alguien no se levanta. No hace falta más | cualquiera | una cada uno, y las cuatro distintas | El árbitro, la expulsión y la espiral |
| **Terremoto** | ABI | `KnockDown` sobre los contiguos | F7 Carne Ajena | Golpea el suelo y se cae todo el mundo a su alrededor | Cuatro cuerpos por el suelo a la vez, sin que haya falta | defensa o centrocampista | una, de suceso: la aglomeración que él decide dónde estará | Él también pierde el tick; y donde hay aglomeración hay balón suelto para el otro |
| **Curtido** | RUL | estado de run | F8 Carne Propia | Se cura solo lo que a los demás les cuesta dinero | *(en un cuerpo)* La factura de clínica que no se paga | cualquiera | haber terminado con lesión leve | Ocupa un slot irreversible en un jugador que puede morir igual |
| **A media pierna** | RUL | estado de run + atributos efectivos | F8 Carne Propia | Juega roto y casi no se le nota | *(en un cuerpo)* Juega el partido que no debía jugar, y a veces sale entero | cualquiera | **decisión del jugador**: alinearlo tocado | El riesgo de que la leve se vuelva grave sigue intacto |
| **Cara de inocente** | RUL | `modifyBias` | F10 Árbitro | Hace la entrada y pone cara de no haber roto un plato | Hace tres faltas y la barra de criterio baja lo que bajaría con una y media (RF-062) | el que más entra | ninguna | Solo vale en un equipo que hace faltas, y ese equipo ya paga en otro sitio |
| **Teatrero** | RUL | `modifyBias`, sujeto invertido | F10 Árbitro | Se cae antes de que lo toquen y el árbitro se lo cree | El equipo saca faltas en sitios donde antes no las sacaba | cualquiera | ninguna | Ventaja limpia: su precio es de rareza, no de conducta |
| **No hay árbitro** | ABI | `cancelEvent` sobre la falta + contador | F10 Árbitro | Una vez por partido el árbitro mira para otro lado | Entra fortísimo, el rival cae, y el juego sigue | el que más entra | ninguna; presupuesto de una por partido | La entrada que se ahorra la falta es también la que puede matar |
| **Juego sucio** | ABI | `KnockDown` + `injure` colgados de la falta | F10 Árbitro | Cuando comete falta, además hace daño | Pita el árbitro y el rival no se levanta | defensa o centrocampista | la falta cometida | Cada falta suya le gasta el criterio del árbitro el doble de rápido |
| **Raíces** *(racial)* | RUL | `ImmunityKind` / `Immovable` | Universal | No lo mueven | Lo cargan y no se va | cualquiera | ninguna | Ninguno: su precio está en elegir el club |
| **Puerta de hierro** | RUL | `cancelEvent` sobre la lesión | Universal | Una lesión por partido, sencillamente, no le ocurre | Cae, y se levanta | cualquiera | ninguna | Un slot raro |
| **Prohibido morir** | ABI | `cancelEvent` sobre la muerte | Universal | La primera vez que debería morir, no muere | El que se queda quieto en el suelo se levanta | cualquiera | ninguna; una vez por run | Un slot irreversible gastado en un seguro que quizá no se cobre |
| **Mano de dios** | ABI | `cancelEvent` sobre el gol + contador | Universal | Una vez por partido saca una que ya iba dentro | El balón cruza la línea y no cuenta | portero | ninguna; una por partido | El slot del portero, que compite con la correa |
| **Último hombre** | ABI | reubicación (`Position`) + contador | Universal | Cuando uno se le escapa solo, aparece delante | El delantero encara la portería vacía y de pronto hay un defensa ahí | defensa | el rival entra solo en el área; una por partido | Deja el sitio del que salió: si la rechazan, hay un hueco donde él estaba |
| **Ídolo local** | MET | contador + `CounterDeltas` → oro | Negocio | Cada gol suyo llena la grada | *(en el libro mayor)* El acta de un 3-0 paga la clínica | delantero | el gol | Un slot en un hombre que ya iba a marcar; no cambia nada en el campo |
| **Taquillero** | MET | contador por jugador + `CounterDeltas` | Negocio | Cada partido que termina de pie llena un poco más la taquilla | *(en un cuerpo)* Sobrevivir empieza a pagar | cualquiera | terminar el partido sin lesionarse | La decisión es alinearlo **aunque** esté tocado, y eso lo mata antes |
| **Máquina de publicidad** | MET | contador de lesiones causadas + `CounterDeltas` | Negocio | Lo que le hace al rival, se vende | *(en el libro mayor)* La carnicería empieza a financiarse sola | cualquiera | la lesión causada | Empuja al equipo a jugar sucio, y el criterio del árbitro no perdona |

## 8.2 TANDA 2 — primitiva nueva compartida (20)

| Nombre | Tipo | Primitiva | Familia | Fantasía | Qué se ve en el partido | Puesto | Condición | Coste u oportunidad |
|---|---|---|---|---|---|---|---|---|
| **Línea adelantada** | PRE | C8 hogar | F2 Presión | Su defensa juega en el medio campo y deja la espalda a la vista | La línea de atrás vive veinte metros más arriba de lo que debería | defensa | decidida al alinear | Un pase en profundidad y no hay nadie detrás |
| **Perro de presa** | PRE | C5 marca | F3 Presa | Le asignas un hombre y no lo suelta en todo el partido | El mismo verde encima del mismo rival en tres recepciones seguidas | centrocampista o defensa | identidad: siempre que su equipo no tenga el balón | Deja de cubrir el hueco que le tocaba; si el crack se va a la banda, él se va detrás |
| **Guardaespaldas** | PRE | C5 marca sobre un tercero | F3 Presa | Protege a un compañero: quien se le acerca, lo tiene encima | El pequeño del equipo recibe y aparece el grande | defensa o centrocampista | decidida al alinear: tener un `linked` | Su marca depende de dónde esté otro; si el vinculado cae, el perk se apaga |
| **Olfato de sangre** | PRE | C7 objetivo de entrada | F3 Presa | Va a por el que ya está tocado | Un rival sale renqueando y el mismo jugador va derecho a él | defensa o centrocampista | previsible **en el ojeo**: el estado físico del rival | Deja de entrar al portador: el balón sigue, el rival no |
| **Rabia** | PRE | C7 objetivo de entrada | F3 Presa | Si le hacen una falta —a él o al suyo—, se la devuelve | Lo derriban y dos jugadas después va a por el mismo | cualquiera | suceso del partido que el jugador reconoce al instante | La devuelve donde no toca, y la amarilla se la lleva él |
| **Pivote hondo** | PRE | C8 hogar | F4 Toque | Baja a buscarla entre sus centrales cuando el equipo no sabe salir | En la salida de balón hay tres hombres donde había dos | centrocampista | de estado: con balón | El equipo ataca con un hombre menos por delante del balón |
| **Desmarque profundo** | PRE | C8 (abre `findSpaceLineMarginCells`) | F5 Envío | Vive al borde del fuera de juego que no existe | Cuando el equipo recupera, ya está corriendo a la espalda del central | delantero | de estado: transición ofensiva | Juega solo y de espaldas al juego; si el pase no llega, no participa |
| **Cañón** | ABI | C4 (`ShootRangeBonusCells` escrito por un perk) | F6 Remate | Si ve portería, dispara; la distancia le da igual | Arma la pierna desde el centro del campo mientras los demás buscan el pase | centrocampista o delantero | geométrica | Regala posesión desde lejos; el rechace cae en campo propio |
| **Doble disparo** | ABI | acción extra tras evento | F6 Remate | Si la primera sale mal, la segunda sale sola | Dispara, le sale rechazada, y dispara otra vez antes de que nadie llegue | delantero | el disparo fallado o rechazado | Se queda clavado rematando: la jugada no se recicla en pase |
| **Sed acumulada** | ABI | C11 contadores de carne | F7 Carne Ajena | Cada rival que deja en el suelo lo vuelve más peligroso | El retrato lleva la cuenta y se ve subir entre partidos | cualquiera | carne acumulada por él, con el contador a la vista | El criterio del árbitro se le gasta en la misma proporción: cuanto más sube, antes lo expulsan |
| **Embestida** | ABI | `AddTacklePush` + acción extra | F7 Carne Ajena | Avanza en línea recta y aparta lo que haya | Entra por el centro y salen dos despedidos a los lados | delantero o centrocampista | con campo por delante y el balón cerca | Va en línea recta: si el balón cambia de carril, él ya no está |
| **Arrollador** | ABI | acción extra tras evento | F7 Carne Ajena | Al derribar a alguien no se para: sigue | Tumba a uno y sigue andando como si nada | cualquiera | el derribo provocado | Sigue de frente aunque la jugada haya cambiado de sitio |
| **Pagar el hierro** | RUL | C4 (`InjuryChanceBonus`) | F8 Carne Propia | Cada rival que rompe se lo cobra su propio cuerpo | *(en un cuerpo)* El que más lesiona es el que acaba en la clínica | cualquiera | suceso propio: haber lesionado a alguien | Carne propia, que es la moneda del juego |
| **Kamikaze** | HYB | C4 (`InjuryChanceBonus`) + C1 | F8 Carne Propia | Entra a todo aunque se rompa él | Entradas que ganan el balón y lo dejan a él en el suelo | cualquiera | ninguna: identidad pura | Sus propias lesiones por encima de las de todo el equipo |
| **Superviviente** | RUL | C11 contadores de carne | F8 Carne Propia | Cuantos más compañeros pierde, más difícil es matarlo a él | *(en un cuerpo)* Sobrevive a la entrada que mató a otro | cualquiera | carne de la run, con el contador en el retrato | Su escalada la pagan otros cuerpos: **el contador solo sube si la run va mal** |
| **Sombra** | PRE | C8 hogar con referencia a un tercero | F9 Pacto | Juega a la sombra del que va delante | Nunca están los dos a la misma altura | centrocampista | decidida al alinear: `linked` | Su altura la decide otro; si el vinculado se queda atrás, él también |
| **Hombre libre** | RUL | C5 aplicado al emparejamiento **contrario** | Universal | Nadie lo marca, porque nadie sabe qué es | Recibe solo, una y otra vez, en el mismo sitio | centrocampista | identidad: siempre | Ninguno propio, y **ese es su problema**: su precio tiene que ser de rareza y de slot. **Único perk que toca la IA del rival: se mide aparte** |
| **Seguro de vida** | MET | run-level: oro al salir de plantilla | Negocio | Si se lo llevan, algo vuelve | *(en el libro mayor)* El muerto caro deja de arruinar la run | cualquiera | su muerte | Solo paga si muere: es un slot apostado a la desgracia |
| **Herencia** | MET | run-level: atributos al salir | Negocio | Lo que era suyo pasa al que se queda | *(en un cuerpo)* El acta dice que murió uno y otro vuelve mejor | cualquiera | su muerte, con un `linked` que lo reciba | Premia perder: hay que vigilar que no empuje a sacrificar gente |
| **Préstamo** | MET | run-level: valor de venta | Negocio | Este no se compra, se alquila | *(en el mercado)* Se revende sin perder la mitad | cualquiera | decidida al fichar | Ocupa un slot en un cuerpo que el jugador ya sabe que no se queda |

## 8.3 TANDA 3 — necesita `modifyUtility` (C1) y/o vocabulario de situación (C2) (22)

| Nombre | Tipo | Primitiva | Familia | Fantasía | Qué se ve en el partido | Puesto | Condición | Coste u oportunidad |
|---|---|---|---|---|---|---|---|---|
| **Ancla** | PRE | C1 + C2 (rama bajo cien, PD-1) | F1 Bloque Bajo | En su tercio no pasa nadie; fuera de él, vuelve | Pierde un balón en el medio y arranca de vuelta como si tiraran de una cuerda | defensa (`positionOnly`) | geométrica, previsible en la pantalla de Equipo | Su equipo no roba en campo rival; la presión alta con él dentro se cae por un lado |
| **Cobertura** | PRE | C1 + C2 | F1 Bloque Bajo | No va al balón: tapa el sitio por donde va a venir | Mientras dos van al balón, uno se coloca entre el balón y su portería | defensa | de estado: sin balón | Nunca es el que roba; su nombre no sale en el informe aunque el equipo encaje menos |
| **Despejador** | PRE | C1 + C2 | F1 Bloque Bajo | En su área no se juega: se despeja | Nadie intenta salir jugando desde la frontal | defensa | geométrica (la presión la añade el motor) | Regala la posesión cada vez; el equipo no descansa nunca con el balón |
| **Presión tras pérdida** | PRE | C1 + C2, alcance equipo | F2 Presión | Perder el balón arriba es la señal para saltar todos | Se pierde en la frontal rival y cinco saltan a la vez en vez de replegar | cualquiera (equipo) | de estado: transición defensiva en campo rival | Si la presión se salta, el equipo está partido y el contragolpe llega solo |
| **Recuperador** | PRE | C1 + C2 | F2 Presión | El primero en llegar al balón suelto, siempre | Gana la mayoría de las segundas jugadas, dentro y fuera del área | centrocampista o delantero | ninguna: identidad pura | Abandona su zona cada vez que hay un rebote; deja hueco detrás |
| **Cazador de porteros** | PRE | C1 + C2 | F2 Presión | No deja sacar al portero rival | El portero rival la manda arriba en vez de sacarla jugada | delantero | de situación: el rival reanuda desde su área | Cuarenta metros de carrera cada saque, y el bloque sin su primera línea |
| **Carnicero ciego** | PRE | C1 + C2 + C7 (rama bajo cien, PD-1) | F3 Presa | No persigue el balón; persigue al hombre | El balón le pasa al lado y él sigue a por el rival | centrocampista o defensa (`positionOnly`) | identidad: sin balón | Su equipo juega con un hombre menos en la disputa del balón. Carísimo y visible |
| **Director** | PRE | C1 + C2 | F4 Toque | El balón pasa por él, siempre | Cada salida de balón pasa por el mismo verde | centrocampista | de estado: con balón | No ataca, no remata y no roba; ocupa un slot en el hombre que más podría hacer otras cosas |
| **Primer toque** | PRE | C1 + C2 | F4 Toque | El balón no se le para en los pies | La suelta antes de pararse mientras el de al lado conduce | centrocampista | de situación: acaba de recibir | Nunca aguanta el balón; con el equipo replegado, su pase sale a otro presionado |
| **Conductor** | PRE | C1 | F4 Toque | Sale de la presión con el balón en el pie, no con un pase | Recibe rodeado y arranca | centrocampista o extremo | ninguna | Cada conducción es un duelo, y un duelo perdido en su campo es una ocasión en contra |
| **Capitán de juego** | PRE | C1 sobre adyacentes | F4 Toque | Los que juegan a su lado se atreven a más | La zona donde él está juega distinto a la otra | centrocampista | decidida al alinear: quién está a su lado | Su lado del campo pierde más balones que el otro. **Depende de C9 para ser atribuible** |
| **Rompe-líneas** | PRE | C1 + C2 | F5 Envío | No juega en horizontal: busca el pase que parte la defensa | En vez de la pared lateral, el balón sale a la espalda de los centrales | centrocampista | hay un compañero en carrera por delante | El pase en profundidad falla más y lo pierde arriba, con el equipo subido |
| **Cambio de orientación** | PRE | C1 + C2 | F5 Envío | Cuando su lado se atasca, manda el balón al otro | Bajo presión la cruza al otro lado en vez de insistir en corto | centrocampista o defensa | de situación que el motor ya calcula (`passBlockedLanePenalty`) | El pase largo se pierde más y la posesión se rompe |
| **Central constructor** | PRE | C1 + C2 | F5 Envío | Un defensa que saca el balón jugado en vez de quitárselo de encima | El central levanta la cabeza y busca al delantero | defensa | geométrica, previsible al alinear | Una pérdida suya es una ocasión inmediata: está jugando en su área |
| **Saque largo** | PRE | C1 + C2 | F5 Envío | No saca en corto: la manda arriba | Cada saque de puerta cruza el medio campo | portero | de situación: reanudación propia | Cede el balón la mitad de las veces; el equipo no sale jugando nunca |
| **Cazagoles** | PRE | C1 + C2 | F6 Remate | Dentro del área no piensa: tira | Recibe de espaldas y se gira a rematar en vez de descargar | delantero | geométrica | Mata jugadas que seguían vivas; con él, el equipo remata peor y más |
| **Sangre fría** | PRE | C1 + C2 | F6 Remate | Con el portero delante no se precipita | En vez de estrellarla en el defensa, se la lleva un paso y define | delantero | de situación que el motor ya calcula (`shootBlockedLanePenalty`) | Un tick más con el balón es un tick más para que le entren |
| **Pareja de centrales** | PRE | C1 + C2 con lectura de un tercero | F9 Pacto | Dos centrales que no se despegan mientras el otro siga en pie | Dos defensas clavados en la frontal tras un córner; cuando uno cae, el otro empieza a replegar | defensa (pareja) | decidida al alinear | **El efecto se apaga con una lesión**: es el único perk de pareja cuyo precio lo cobra la enfermería |
| **Manada** | PRE | C1 | F9 Pacto | Cuantos más de los suyos hay, más se envalentonan todos | El número de brutos del once cambia la forma del partido | cualquiera | previsible en la pantalla de Equipo (`teammatesWithTag`) | Exige una plantilla monotemática, y esa plantilla juega peor al fútbol |
| **Solitario** | PRE | C1 | F9 Pacto | No se fía de nadie que no sea como él, y cuando está solo lo da todo | El elfo del equipo de orcos juega como un poseído | cualquiera | previsible al alinear: ser el único de su etiqueta | Fichar un segundo de su etiqueta **lo apaga**. **La tienda tiene que avisarlo o rompe la regla 11** |
| **Relevo** | PRE | C1 + C2 con lectura de la **posición** de un tercero | F9 Pacto | Cuando uno sube, el otro cubre. Nunca los dos a la vez | Suben y bajan alternándose, siempre al revés | pareja de lateral o de centro | decidida al alinear; la enciende la posición del otro, no su decisión | El equipo nunca ataca con los dos; renuncia a una superioridad que otra pareja sí tendría |
| **Instigador** | PRE | C1 + C2 | F10 Árbitro | Cuando el árbitro se va, es su momento | Empieza la turba y tres se lanzan mientras el resto sigue jugando al fútbol | cualquiera | de suceso del partido, inconfundible | La turba es donde se muere; el que la lidera es el que más se expone |

---

# 9. Riesgos materiales

1. **La tanda 3 es un recalibrado, no una tanda de datos.** *(MEDIDO)* `injuriesPerMatch` y `shotsPerMatch`
   están en el techo de RT-056 y `ballThirdMaxShare` tiene banda por la ADR 0093. Veintidós perks que
   escriben sobre el `argmax` mueven las cinco métricas a la vez. Se presupuesta con **C10** (techos por
   acción y puesto en el cargador) desde el primer día: un `×3` a `Shoot` en un portero tiene que ser un
   **error de datos** (RT-032), no una anécdota.
2. **La primitiva «acción extra tras un evento» es la única con riesgo de determinismo.** Una acción
   resuelta fuera del tick rompe el orden de RT-041. Tiene que resolverse **dentro del mismo tick**, con
   orden explícito (rareza descendente, id de jugador ascendente, id de perk ascendente), o no entra.
3. **Los tres perks [carne] de tanda 1 tocan la economía de la clínica**, que las ADR 0099-0100 acaban de
   rehacer y que lleva dos lotes en banda. `Curtido`, `A media pierna` y `Taquillero` mueven el gasto de
   clínica por run: **se miden juntos o no se miden**.
4. **`Hombre libre` es el único perk que toca la IA del rival.** Se mide en un lote aparte o contamina todo
   lo demás. Y si cae, arrastra a `Perro de presa` y `Guardaespaldas` por la regla de las tres (C5).
5. **`Solitario` tiene una condición que un fichaje futuro puede apagar.** O la tienda lo avisa, o choca con
   la regla 11 y el candidato cae.
6. **PD-1 no está cerrada y cuatro perks dependen de ella.** `Bloque bajo`, `Guardameta clásico`, `Ancla` y
   `Carnicero ciego` necesitan la rama por debajo del cien. **Y son, precisamente, dos de los cuatro perks
   de tanda 1 de la familia F1**: sin PD-1 consolidada, El Bloque Bajo no tiene ni un miembro construible.
7. **El crédito de arco de `killing_range` sigue siendo el riesgo medido más grande** *(MEDIDO: 59 % de los
   maestros tomados, valor 209, 104 puntos de crédito por pieza; sin él `mastersReached` cae de 25,2 a
   14,8)*. PD-5 lo congela; el experimento de la Fase D hay que hacerlo **antes** de descongelar nada.
8. **63 perks no caben en 14-20 slots de run.** *(DERIVADO de PD-4)* Con M=63 la run ve ~5 miembros de una
   familia y compra 1,0-1,3: el catálogo está dimensionado para que **la segunda run no repita la primera**,
   no para que el jugador lo vea entero. Si el revisor recorta, se recortan **fichas enteras** (§5.2), nunca
   miembros de familia hasta cuadrar una cifra.
