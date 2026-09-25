# De cuota a acto — cómo se convierten los 102 perks que ya tenemos

**Encargo del revisor (25 sep 2026):**

> Si no quieres crear, haz que los que ya tenemos tengan ese efecto de situación/conducta que lo aleje de
> *«tira más»*, *«más posibilidad de regate»* y lo acerque a *«tiro cañonazo»* o *«sombrerito»*. Puedes
> ponerle cooldown a esas habilidades. […] No te ciñas a los números. Ya lo balancearemos después.

Deriva de la **[ADR 0149](../decisiones/0149-el-catalogo-se-mide-por-lo-que-se-ve.md)** (RF-069 reescrita) y
de las auditorías [13B](./perks-auditoria-potencial-visual.md) y [13C](./perks-auditoria-situaciones.md).
**No modifica `/data` ni código.** Etiquetas: **MEDIDO**, **DERIVADO**, **HIPÓTESIS**.

**Y no lleva un solo número**, por instrucción expresa. Todas las magnitudes —velocidades, alcances,
duraciones, enfriamientos— se dejan sin fijar; lo que se fija es **qué acto es cada perk y qué se ve**.

---

# 1. La receta: qué separa una cuota de un acto

Un `modifyProbability(+N)` dice: *el mismo acto, un poco más a menudo bien*. Un **acto** dice: *un acto
distinto, reconocible, que ocurre ahora y no vuelve a ocurrir en un rato*. La conversión tiene cuatro
partes y **las cuatro hacen falta**; con tres se obtiene un número con nombre bonito.

1. **Un acto nombrado.** El perk nombra una **variante de una resolución que el motor ya hace** —un disparo
   que es un cañonazo, un regate que es un sombrerito—, no un porcentaje. El nombre es el dato: es lo que
   verá el jugador y lo que dirá la pantalla.
2. **Un enfriamiento.** Está disponible cada tanto, no siempre. **Es lo que convierte un buff permanente en
   un suceso**, y sin él el acto se funde con el estilo del jugador y deja de notarse (RF-069c).
3. **Una consecuencia distinta, no sólo mejor.** El cañonazo no es «más probable que entre»: es «el portero
   casi no llega, **y si no va a puerta la regalas**». La asimetría es lo que lo hace memorable, lo que lo
   hace una decisión, y lo que impide que el balance se descontrole mientras no lo miramos.
4. **Se ve sin arte nuevo.** La variante escribe algo que **la traza ya lleva** —velocidad y altura del
   balón, estado del jugador, posición— para que sea visible hoy, con placeholders, sin romper la regla 10.

**El punto 3 es el que hace que «ya lo balancearemos después» sea una decisión sensata y no una deuda**
*(DERIVADO)*: un acto asimétrico se autolimita —cuanto más fuerte es su mitad buena, más caro es su
fracaso—, mientras que una cuota sólo crece.

---

# 2. Las dos primitivas que hacen falta, y por qué son más baratas de lo que parecen

## A1 · Variante de acción

Un efecto marca la **próxima resolución** de cierto tipo del portador como una **variante nombrada**. El
motor la resuelve con parámetros distintos y **emite el evento con el nombre de la variante en `Detail`**.

**Por qué es barata, y es el hallazgo que abarata toda la conversión** *(MEDIDO)*: `Detail` **ya es el canal
de matiz del motor** y ya lo consume la presentación.

- El motor ya emite `Shot` con `onTarget`/`offTarget`, `ShotPost` con `post`/`crossbar`, `Save` con
  `held`/`parried`/`corner`/`penalty`, `Cross` con `attempted`/`volleyed`/`loose`, `Injury` con
  `minor`/`severe`.
- `MatchEventSounds.PoolsFor` **ya conmuta por `Detail`** (`e.Detail == "severe" ? Crush : None`).
- `MatchMomentView.BaseClassification(type, detail, …)` **ya clasifica momentos con cláusulas `when detail
  == …`**.

**DERIVADO: un acto nombrado se convierte en momento narrable, con sonido y con sello, añadiendo casos a
tablas que ya existen — no sistemas.** Es una respuesta mucho más barata a la capacidad «atribución» (P4 de
13C) que la que yo mismo había estimado, y llega por el mismo sitio que el acto.

**Las dos reglas del proyecto se respetan sin esfuerzo**: el **vocabulario de variantes es cerrado** (un
enumerado, error explícito si el dato inventa una, RT-032) y sus parámetros viven en `tuning`, así que el
dato dice *«tu próximo disparo es Cañonazo»* y **el motor sabe qué es un cañonazo sin nombrar ningún perk**
(regla 5, RT-034). Es el mismo patrón que ya usan `RelocationPoint`, `ImmunityKind` y `TraitScalarKind`.

## A2 · Enfriamiento — **IMPLEMENTADO el 25 sep 2026**

`LimitDefinition` es hoy `(LimitScope Per, int Times)` con ámbitos jugada / partido / turba / run
*(MEDIDO)*. Falta **«cada N»**. Es un campo más en un concepto que ya existe, ya se reinicia en los sitios
correctos y ya aparece en la descripción generada (RT-035).

**Ya existe.** `LimitDefinition` tiene `CooldownTicks`, el dato lo declara en **segundos**
(`limit: { "cooldownSeconds": N }`), el cargador los pasa a ticks una sola vez (RT-020/RT-023), la
suscripción recuerda el tick de su última activación y la descripción generada lo dice sin que nadie
escriba texto (RT-035). `per`/`times` pasan a ser opcionales cuando hay enfriamiento, y un `limit` que no
declare ni lo uno ni lo otro es un error de carga (RT-032).

**Lo que desbloquea, y es el motivo de haberlo hecho antes que nada**: un perk puede colgarse de un evento
**frecuente** —el pase, que ocurre ~100 veces por partido— sin convertirse en ruido. Sin enfriamiento, sólo
se podían mover a un evento de fútbol los perks cuya resolución es rara (entrada, tiro, regate).

## Nota de diseño — `game-design-review`, las diez preguntas

1-3. **Qué ve y qué decide el jugador**: ve un acto reconocible con nombre; decide **construir hacia la
situación que lo dispara** y, al alinear, aceptar su mitad mala. Hoy no decide nada: 72 de 102 perks son
invisibles *(MEDIDO)*.
4. **Qué regla representa**: **RF-069a/b/c** tal como quedan tras la ADR 0149. No se inventa ninguna.
5. **Qué sistemas**: `/data` (el perk declara acto + enfriamiento + su soporte), `/Sim` (`EffectEngine` marca
la variante; la resolución correspondiente la aplica y la nombra en `Detail`), `/Game` (casos nuevos en dos
tablas que ya conmutan por `Detail`). **RT-014 intacto**: `/Sim` no decide presentación, `/Game` no decide
partido.
6. **Alternativas**: *(a)* subir los porcentajes —es lo que hay y es lo que el revisor descarta—; *(b)* un
tipo de efecto por fenómeno (un `cannonShot`, un `chip`…) → explota el enumerado y mete fútbol en el
vocabulario de efectos; *(c)* **una sola primitiva «variante» con vocabulario cerrado** ← elegida, porque
añade fenómenos sin añadir tipos de efecto.
7. **Trade-off**: el acto cuesta enfriamiento y mitad mala. Para el proyecto, cada variante es trabajo de
resolución: **el vocabulario de variantes es el presupuesto real de esta línea de diseño.**
8. **Estrategias**: los actos **componen con la situación**, no entre sí — un cañonazo vale lo que valga tu
volumen de tiro, que otra parte de la build controla. Eso es lo que produce builds en vez de estadísticas.
9. **Degeneración**: tres, y las tres se cierran con reglas de carga — acto sin enfriamiento (lo prohíbe
RF-069c), acto sin mitad mala (lo detecta la revisión de ficha, no el cargador: **es el punto flojo, y se
anota**), y **saturación**: un partido con demasiados actos pisa su propia gramática de momentos (cola de
una plaza, caducidad 1,5 s, MEDIDO). La tercera es la que hay que medir primero.
10. **Demostración**: sucesos atribuibles por partido · perks distintos de la alineación que producen al
menos un acto · y las bandas de siempre como restricción, no como objetivo.

---

# 3. El mapa de conversión de los 72 invisibles

*(MEDIDO: los 72 perks de grado Soporte se agrupan en muy pocos canales, así que la conversión no son 72
decisiones sino siete.)* Los actos van **sin magnitudes**, por instrucción del revisor, y **sin pretender
ser el catálogo final**: son la forma que toma cada canal.

## 3.1 Entrada — 15 perks, el canal más poblado

`back_to_back` · `blood_in_the_water` · `bulwark_stance` · `captains_voice` · `deathless_march` ·
`diagonal_press` · `duelist` · `game_management` · `granite_line` · `last_ditch` · `natural_leader` ·
`own_third_anchor` · `pit_veteran` · `pivot_duo` · `road_warrior`

Hoy los quince dicen lo mismo: *más probabilidad de ganar la entrada*. Actos en los que se reparten:

| acto | qué se ve | su mitad mala |
|---|---|---|
| **Barrida** | Se tira al suelo desde más lejos y el balón sale despedido | Si falla, **se queda él en el suelo** |
| **Plancha** | Derriba seguro y manda al rival hacia atrás | Falta casi segura, y la tarjeta con ella |
| **Robo limpio** | Le quita el balón sin tocarlo y **sale jugando** con él | Sólo de frente: por detrás no existe |
| **Muro** | El que intenta regatearle **rebota** | No recupera el balón: sólo lo detiene |
| **Dos contra uno** | Con un vinculado cerca, entran los dos y el rival sale despedido entre ambos | Depende de dónde esté el otro |

## 3.2 Tiro — 9 perks

`box_predator` · `cold_focus` · `forward_line` · `killing_range` · `long_range_menace` · `poacher_instinct` ·
`sharpshooter_drill` · `spearpoint` · (+ `slow_pulse` y `cannon`, hoy escalares de soporte)

| acto | qué se ve | su mitad mala |
|---|---|---|
| **Cañonazo** | El balón sale mucho más rápido; el portero apenas llega | Si no va a puerta, **se va lejísimos**: posesión regalada |
| **Sombrerito** | Sólo con el portero adelantado: el balón le pasa por encima | Con el portero en su sitio es un globo ridículo que atrapa sin moverse |
| **Rosca** | El balón se curva y **el bloqueo del defensa no cuenta**, porque no pasa por donde él está | Lento: da tiempo al portero |
| **A bocajarro** | Desde muy cerca, sin tiempo de parada | Sólo existe muy cerca, donde llegar es lo difícil |

**`cannon` (+alcance de tiro) y `slow_pulse` (+calidad) dejan de poder existir solos** (RF-069b) y pasan a
ser **el soporte del Cañonazo dentro del mismo perk**: la estadística que decide si sale bien o mal.

## 3.3 Regate — 4 perks

`crowd_control` · `flank_specialist` · `silky_veteran` · `wing_overlap`

| acto | qué se ve | su mitad mala |
|---|---|---|
| **Sombrerito** | El balón pasa **por encima** del defensa y él lo rodea | El balón queda suelto un instante: cualquiera llega |
| **Caño** | El balón entre las piernas; el defensa se queda **clavado** un momento | Si no cuela, el balón queda en el defensa |
| **Ruleta** | Cambia de dirección y el defensa **se pasa de largo** | Pierde velocidad: la defensa se recompone |

## 3.4 Intercepción — 7 perks

`battle_reader` · `center_conductor` · `covering_shadow` · `gentle_giant` · `high_press_trigger` ·
`lane_reader` · `no_nonsense`

| acto | qué se ve | su mitad mala |
|---|---|---|
| **Manotazo** | Corta y el balón **sale despedido hacia arriba**: segunda jugada aérea | Nadie tiene la posesión, ni él |
| **Lectura** | **Aparece en la línea de pase** antes de que el pase salga | Deja su sitio: si se equivoca, el hueco es suyo |

## 3.5 Pase — 5 perks

`fine_touch` · `first_touch_school` · `quick_study` · `squad_player` · `steady_hands`

| acto | qué se ve | su mitad mala |
|---|---|---|
| **Taconazo** | Pase hacia atrás sin mirar | A ciegas: no comprueba que haya alguien |
| **Bombeado** | Por encima de la línea defensiva | Lento: da tiempo a replegar |
| **Filtrado** | Entre dos rivales | Si lo cazan, el contraataque sale de ahí |

## 3.6 Parada y evasión — 6 perks

`clean_sheet_legacy` · `safety_net` · `elf_touch` · `iron_studs` · `light_feet` (+ la mitad de
`first_touch_school`)

| acto | qué se ve | su mitad mala |
|---|---|---|
| **Estirada** | Llega a una que no debería | El rechace sale largo y vivo |
| **A puños** | Despeja lejos en vez de atrapar | Nunca conserva el balón |
| **Pies de plomo** | El primer contacto **no le mueve**; el rival rebota | No le da ventaja: sólo le quita la desventaja |

## 3.7 Carnicería — 8 perks

`blood_tithe` · `bruised_knuckles` · `gathering_thirst` · `marrow_thirst` · `scar_tissue` · `second_wound` ·
`skullsplitter` · `survivor`

Es el canal que **menos conversión necesita**: ya produce sucesos (lesión, muerte). Lo que le falta es
**el acto visible del impacto** —el cuerpo que sale despedido, el impulso de 13C— **y el enfriamiento**. Su
soporte actual (`modifyProbability(injury/severeInjury)`) es exactamente lo que RF-069b quiere: el
estadístico que decide si sale bien o mal, pegado a un acto.

**Restricción medida, y manda sobre este canal**: `injuriesPerMatch` está en 0,78 contra un techo de 0,90 y
la brecha de faltas de la ADR 0147 sigue sin explicación cerrada. **Es el único canal de los siete donde
«ya lo balancearemos después» no se puede aplicar entero**: convertir aquí exige medir en la misma tanda.

## 3.8 Economía — 6 perks, y no se convierten

`ad_machine` · `box_office` · `inheritance` · `life_insurance` · `loan` · `local_idol`

**No tocan el partido.** Pasan a grado `economy` (RF-069e) y quedan fuera de la cuota de visibilidad. Queda
anotado lo incómodo: **compiten por un slot de perk sin usar el partido**, lo que es una decisión distinta y
no la toma este documento.

---

# 4. Qué pasa con los 30 que ya se ven

- **Los 15 de SITUACIÓN** ya son actos en todo menos en el nombre y en la pantalla *(MEDIDO: `earthquake`,
  `charge`, `double_shot`, `steamroller`, `dirty_play`, `last_man`, `hand_of_god`, `iron_gate`,
  `mob_instigator`, `no_dying`, `hot_blooded`, `roots`, `half_leg`, `tough_hide`, `numb`)*. Lo que necesitan
  es **nombre, enfriamiento y atribución** — no efectos nuevos.
- **Los 15 de CONDUCTA** se quedan como están y son la otra mitad del catálogo (RF-069a, ≥ 25 %). La
  advertencia de 13C sigue en pie: se leen como patrón, no como anécdota, así que **no pueden ser la
  respuesta principal a la visibilidad**.

---

# 5. Lo que se puede escribir **hoy**, sin tocar el motor

*(MEDIDO, contra el vocabulario de efectos actual.)* No es un plan completo: es la tanda que demuestra la
receta con lo que ya hay, usando `limit: { per: play, times: 1 }` como enfriamiento pobre.

| acto | con qué se escribe hoy | qué le falta para estar terminado |
|---|---|---|
| **Caño** | `setState(KnockedDown, ticks)` sobre el rival del duelo | el balón por encima; el nombre en pantalla |
| **Lectura** | `relocate(betweenBallAndOwnGoal)` ya existe | enfriamiento de verdad |
| **A bocajarro** | `extraAction` sobre `SHOT` ya existe | que se llame así |
| **Plancha** | `setState` + el `injure` que ya existe | el impulso (P1) |
| **Pies de plomo** | `immunity(push)` ya existe y `roots` ya la usa | que el empuje **rebote** (P1) |
| **Falso Muerto** | `cancelEvent` sobre `DEATH` + `setState` | que el partido lo cuente |
| **Árbitro sobornado** | `cancelEvent` sobre `FOUL` + `modifyBias` | que el árbitro mire a otro lado |

**Siete actos sin una línea de C#.** Ninguno está escrito hoy.

---

# 6. Orden, y qué se mide

1. **Tanda A — los siete de §5, con el enfriamiento pobre.** Cero motor. Sirve para **medir la saturación
   antes de construir nada**: cuántos actos por partido soporta la gramática de momentos con cola de una
   plaza. **Es la pregunta 9 de la nota de diseño y la que más puede tirar el plan.**
2. **Tanda B — A2 (enfriamiento) y la atribución por `Detail`.** Dos cosas pequeñas que convierten los
   siete anteriores y los 15 de SITUACIÓN en actos de verdad. **Es donde el catálogo actual empieza a
   notarse sin haber escrito un perk nuevo.**
3. **Tanda C — A1 (variante de acción), empezando por el tiro.** Cañonazo y sombrerito son los dos que el
   revisor nombró, los dos escriben estado del balón que la traza **ya dibuja**, y el tiro no gasta del
   presupuesto de violencia.
4. **Tanda D — el impulso (P1 de 13C)**, que es lo que le falta a la entrada y a la carnicería. Con
   medición en la misma tanda, por §3.7.
5. **Tanda E — el resto de canales** y la retirada de los perks que queden sin acto.

**Criterio de éxito, y no es una banda**: *¿el partido se cuenta solo?* Se mide con **sucesos atribuibles
por partido** y **cuántos perks distintos de la alineación producen al menos un acto**. Las bandas de
RT-056 siguen siendo la restricción.

---

# 7. Para el revisor

1. **RF-069 reescrita y `docs/requisitos.md` en v0.10** (ADR 0149). La regla que traduce tu frase es
   **RF-069b: ningún perk tiene efectos exclusivamente de soporte** — la estadística se pega al acto y
   decide si sale bien o mal. **Hoy fallarían 72 de 102.**
2. **Los 72 invisibles no son 72 decisiones: son siete canales.** Entrada (15), tiro (9), carnicería (8),
   intercepción (7), pase (5), regate (4), parada y evasión (6), más 6 de economía que **no** se convierten.
3. **Siete actos se pueden escribir hoy sin tocar el motor.** Ninguno está escrito.
4. **La atribución sale mucho más barata de lo que dije en 13C**: `Detail` ya es el canal de matiz y las dos
   tablas de presentación ya conmutan por él. Nombrar un acto es añadir casos, no sistemas.
5. **Lo que sigue necesitando decisión tuya**: el **segundo balón** de tu lista (255 líneas de motor suponen
   uno solo), y si un perk de economía debe seguir ocupando un slot de perk.
6. **Y una advertencia honesta**: el punto flojo de todo esto es la **mitad mala**. El cargador puede
   comprobar que un acto tiene enfriamiento; **no puede comprobar que tenga consecuencia asimétrica**. Eso
   lo sostiene la revisión de cada ficha, y si se relaja, en seis meses tendremos cien actos que sólo son
   buenos — que es el mismo problema de hoy con mejores nombres.

---

# CORRECCIÓN MEDIDA (25 sep 2026) — el cartelito existe, y el dato que importa es otro

**Lo que este documento afirmaba y es falso**: que `PERK_TRIGGERED` se emite y se tira, y que ningún perk
se puede ver durante el partido. **Se puede.** *(MEDIDO, leyendo el código.)*

- **`Sim/Run/View/MatchFlashView.cs`** compone los avisos del partido (`MatchFlash`: fotograma, índice de
  jugador, equipo, id y **nombre del perk**), ordenados por RT-041, con `DurationFrames = 15` (1 s).
- **`MatchMomentView`** los devuelve como `MomentMark`, anotando si el aviso quedó **absorbido** por un
  momento del director.
- **`MatchPitchView.DrawFlashes`** (2D) y **`MatchPitchView3D.DrawMarks`** (3D, la pantalla vigente) los
  pintan: un **cartel de pergamino con el nombre del perk anclado sobre la cabeza del jugador**, tamaño
  fijo en pantalla, con el último tercio desvaneciéndose y **apilado** cuando coinciden dos.

Es exactamente el cartelito del encargo del revisor, y está desde C9.

**Por qué me equivoqué, que es lo aprovechable**: busqué `PerkTriggered` en `/Game` y sólo apareció un
comentario de `MatchEventSounds` diciendo que como **sonido** sería ruido. Generalicé de *no suena* a *no se
ve*. La ruta de dibujo no nombra el tipo de evento —va por `MatchFlash`— así que el `grep` no la tocó. Es
la pregunta 7 de `CLAUDE.md`, *«¿existe ya una abstracción del propio repositorio para esto?»*, saltada.

**Y el dato que sustituye al error es peor para el catálogo, no mejor** *(MEDIDO con un instrumento nuevo,
`BroadcastCapture.FindPerkBurst`, sobre seis partidos de semillas distintas)*:

| partido | avisos de perk | de ellos en los **primeros 6 s** | en los 84 s restantes |
|---|---:|---:|---:|
| 1 | 16 | 15 | **1** |
| 2 | 14 | 14 | **0** |
| 3 | 14 | 14 | **0** |
| 4 | 16 | 14 | 2 |
| 5 | 9 | 8 | **1** |
| 6 | 14 | 14 | **0** |

**Un partido entero produce entre cero y dos activaciones de perk fuera del saque inicial.** La causa está
medida y es la misma de siempre: **48 de los 102 perks se cuelgan de `MATCH_START`**, y ahí el pregón del
saque tapa media pantalla y no hay fútbol que mirar. La única activación en juego abierto que capturé
coincidía además con un momento de lesión grave, que la **absorbe** y la dibuja pequeña.

**DERIVADO, y refuerza la tesis en vez de debilitarla**: el canal de atribución no hay que construirlo —está
construido y funciona—. Lo que falta es **que haya algo que atribuir, y en un momento en el que se pueda
mirar**. Eso no se arregla en `/Game`: se arregla en el catálogo, colgando los actos de eventos de fútbol
en vez de del pitido inicial, que es precisamente lo que hace
[`perks-catalogo-de-actos.md`](./perks-catalogo-de-actos.md).

---

# 8. La reducción de `MATCH_START` — primera tanda hecha (25 sep 2026)

Encargo del revisor: *«debes reducir los perks de match start»*.

**Cómo se clasificaron los 48** *(MEDIDO)*. No todos son un problema, y decirlo importa:

| clase | nº | ¿es legítimo en `MATCH_START`? |
|---|---:|---|
| **Conducta** (correa, hogar, forma de zona, sesgo de marca o de entrada) | 12 | **Sí.** Una conducta es una disposición permanente, no un suceso: tiene que estar puesta antes de que ruede el balón |
| **Estado** (inmunidades, duración del derribo que provoca) | 5 | **Sí**, por el mismo motivo |
| **Contador de economía** | 1 | **Sí**: no toca el partido (RF-069e) |
| **Número** (probabilidad, atributo, escalar) | **30** | **No.** Es el bono invisible puesto en el pitido |

**Se han movido seis**, los que tienen un evento de fútbol **poco frecuente** donde caer, sin necesitar el
enfriamiento:

| perk | de | a | por qué ahí |
|---|---|---|---|
| `bulwark_stance` · `own_third_anchor` · `duelist` | `MATCH_START` | **`TACKLE`**, scope actor | su bono se resuelve en la entrada |
| `elf_touch` *(racial élfica)* | `MATCH_START` | **`TACKLE`**, scope **opponent** | el dueño es el rival implicado: es el instante en que le entran y se escurre |
| `forward_line` | `MATCH_START` | **`SHOT`** | su bono se resuelve en el disparo |
| `flank_specialist` | `MATCH_START` | **`DRIBBLE_ATTEMPTED`** | se activa al encarar |

Los seis pasan además de `duration: match` a `duration: play`: el bono vive en la jugada en la que hace
falta, no todo el partido. **`MATCH_START`: 48 → 42.**

**Lo que queda, y ya no está bloqueado**: de los 24 números que siguen ahí, **16 son probabilidades de
resolución** que pueden moverse al pase o a la intercepción **ahora que el enfriamiento existe** (sin él se
dispararían ~100 veces por partido), y **8 son constantes permanentes** —atributos y escalares de rasgo—
que alimentan la decisión o la geometría y que **tienen que estar puestas antes**: `brute_boots`,
`iron_lungs`, `scar_veteran`, `cannon`, `kamikaze`, `shoulder_to_shoulder`, `slow_decay`, `slow_pulse`. A
esas ocho RF-069b no les exige cambiar de disparador: les exige **acompañarse de un acto en el mismo perk**.

**Por qué paré en seis y no seguí hasta 26**: los seis primeros ya cambiaron la conducta de **cinco tests**
—todos afirmaban «este perk está puesto en el pitido inicial»— y eso es exactamente el tipo de cambio que
la Regla E manda pasar por revisión independiente antes de seguir acumulando. **No se ha ejecutado ninguna
puerta ni ningún lote de `/Balance`**: el revisor aplazó el balance expresamente, pero eso no es lo mismo
que no medirlo nunca, y queda anotado como pendiente.

