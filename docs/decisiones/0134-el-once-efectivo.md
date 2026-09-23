# ADR 0134 — El once efectivo: quién juega se calcula una vez y se enseña

**Fecha:** 23 sep 2026
**Estado:** Aceptada
**Cierra:** [BB-F](../pendientes/BB-F.md), [BA-I](../pendientes/BA-I.md); [BC-H](../pendientes/BC-H.md) salvo su mitad de antes del partido (RF-002d)
**Diseño previo:** `docs/analisis/once-efectivo-diseno.md` (`game-design-review`, diez preguntas)
**Requisitos:** RF-002d, RF-012c, RF-012d, UI-003, RT-014 · condición 3 de la [ADR 0048](./0048-morir-estando-sano.md)

## Contexto

`/Sim` decide quién juega dentro de `RunLineup.Build` → `SelectStarters`, en el momento de construir el
partido: completa la alineación guardada por rol y luego por id hasta siete. **Todos los consumidores de
fuera vuelven a derivar ese once de `state.Lineup`**, que es la alineación *guardada* —la intención del
jugador— y no la que salta al campo. Dos representaciones del mismo hecho que pueden contradecirse, que es
justo lo que RT-014 prohíbe. Y se contradicen:

1. **El aviso miente.** `RunEngine.LineupWarnings` (`:265`) emite `Shorthanded` cuando la alineación guardada
   tiene menos de siete, y el Ojeo dice «juegas en inferioridad: con 5 en campo…». Pero el partido se juega
   con siete: **CONFIRMED** con un test temporal (9 disponibles, 6 guardados → aviso e igualmente 7
   titulares).
2. **El relleno no recibe indicador de riesgo de muerte.** `RunEngine.LethalRisks` (`:345`) también recorre la
   alineación guardada, así que el suplente que el motor mete de oficio **no aparece** en el indicador de
   RF-012c. Es daño no anunciado (RF-012d, regla 11 del `CLAUDE.md`) sobre alguien que el jugador no sabía
   que iba a jugar, y rompe la **condición 3 de la ADR 0048**: «se puede reducir el riesgo con la
   alineación». No se puede reducir el riesgo de quien no sabes que juega.
3. **RF-002d es inalcanzable.** «Se puede jugar en inferioridad numérica […] es una **decisión legítima**».
   Mientras quede un suplente, el relleno la impide: no hay forma de expresar «esta casilla la dejo vacía».
4. **La ventana de sustitución no informa.** `MatchScreen.cs:463` enseña el nombre del que sale y una lista
   de nombres. Ni posición, ni casilla, ni atributos, ni riesgo, ni qué elegiría la política por defecto. Y
   BB-F midió que la elección **sí** cambia el partido: en 467 pares de candidatos, el marcador cambia el
   70 % de las veces y el ganador el 31 %. Consecuencia grande con información cero.

## Decisión

**El once efectivo se calcula una sola vez, en `/Sim`, y es lo que ven todos.**

**(A0) La colocación del jugador llega al campo.** Lo destapó (A) y es el hallazgo más grande del paquete:
`RunLineup.Build` **reasignaba las siete casillas por rol en cada partido**, así que arrastrar un defensa al
puesto de delantero se guardaba en el estado y el motor lo devolvía a su casilla. Y como `LethalRisks` sí
leía las casillas guardadas, **mover fichas movía el número y no movía el partido**: el jugador reducía un
riesgo contra una colocación que no se iba a jugar, que es la condición 3 de la ADR 0048 incumplida en su
raíz. `RunLineup.PlaceOutfield` respeta ahora la casilla elegida y reparte el 2-3-1 solo entre los huecos.
Evidencia, alcance y lo que queda por medir: [BG-B](../pendientes/BG-B.md).

**(A) Una computación, no dos.** Se extrae de `RunLineup.Build` la selección de titulares y la asignación de
casillas —relleno por rol, portero de emergencia incluido— a `RunLineup.Effective(state)`, que devuelve la
colocación final, **qué ids entraron de relleno** y cuántos disponibles hay. `Build` pasa a llamarla, así que
no pueden divergir: es la misma función, no dos implementaciones de acuerdo. `LineupWarnings` y `LethalRisks`
dejan de caer por defecto en `state.Lineup` y caen en el once efectivo.

**(B) El aviso dice la verdad.** `LineupWarningKind` distingue **`FilledFromBench`** (por jugador: «entra X
de oficio, tú no lo pusiste») de **`Shorthanded`**, que queda para la inferioridad **real** —menos de siete
disponibles—. **El Ojeo** enseña como «TU ONCE» el once efectivo, marcando al de relleno.

**La pantalla de Equipo queda fuera y hay que decirlo**: `TeamScreen.BuildRoster` (`:628`) sigue partiendo
la plantilla en titulares y suplentes con la alineación **guardada**, así que la misma pantalla puede listar
el riesgo de muerte de alguien a quien pinta como suplente. Es una cuarta instancia de la causa que censa
[BG-A](../pendientes/BG-A.md) y allí queda anotada.

**(C) La ventana de sustitución informa.** La bandeja viva (`BroadcastScreen`) **ya** daba posición, casilla y
candidato recomendado desde `f82a0c1` —lo que BB-F midió era `MatchScreen`, la vista de depuración; ver la
corrección en ese pendiente—. Lo que falta y añade `SubstitutionPoint`: si la lesión fue **leve o grave**
(porque decide qué opciones hay) y, por candidato, su **riesgo letal**.

**Ese riesgo es `Lethality.ChanceAgainst`, no `MarkedRisks`,** y la diferencia importa. `MarkedRisks` —el
número que enseña el Ojeo— es **conjunto**: el perk letal marca a `VictimsPerActivation` rivales, los peor
parados, así que reparte el riesgo por el once y enseña **0 a los demás**. Por candidato sería inservible y
además mentiroso: cuatro de cinco saldrían a cero. `ChanceAgainst` responde a otra pregunta —«**si te toca la
entrada**, ¿mueres?»— y es, según la propia documentación de `Lethality` (`:226-231`), lo único que sigue
siendo **exacto** desde el paquete AY; el pendiente AY-A está abierto justo sobre el otro. Es además la
pregunta que el jugador se hace al elegir a quién mete. Se compone sobre todos los portadores rivales como
«que acierte al menos uno», igual que `LethalRisks`, y **el texto tiene que decir la condición** («si recibe
la entrada: X %»), nunca «puede morir: X %», que es la frase del Ojeo y significa otra cosa.

**(D) «Que se quede el hueco» es una respuesta válida.** Rechazar el sustituto se representa en
`MatchDecisions`, donde ya viven las decisiones del jugador, **no** en `TeamSetup.Substitutions`. El motor no
se entera: `Simulator` y `MatchEngine` siguen leyendo exactamente la misma lista de sustituciones reales que
hoy. Jugar el resto del partido con seis ya está soportado (es lo que pasa hoy con el banquillo vacío).

**(E) La lesión LEVE no saca del campo si el jugador no quiere** *(decisión del revisor, 23 sep 2026, en
mitad de este paquete; es la literalidad de BA-I, que esta ADR había dejado fuera por creerla inviable)*. La
ventana pasa a tener **tres** respuestas: sustituir por X, dejar el hueco, o **seguir jugando con él**. Solo
en lesión **leve**: la grave sigue apartando (RF-092) y la muerte, obviamente, también.

**El coste de seguir jugando NO es solo la lesión.** Así se escribió primero, y la revisión independiente
demostró que era falso: el coste dominante es que **quien se queda tocado multiplica por ocho su
probabilidad de morir**. `MatchEngine.LethalChanceAgainst` (`:3281`) pasa `victim.Injured` como
`hurtInThisMatch` a `Lethality.StatePercent`, que devuelve `minorInjuryPercent` = 800; y como el perk letal
marca al de mayor probabilidad, el que se queda suele pasar a ser **el marcado**. Antes de (E) esa rama era
**inalcanzable** —toda lesión sacaba del campo, así que un tocado no podía volver a recibir una entrada—:
la enciende este paquete.

Eso no se corrige quitándolo —el multiplicador es la regla de la ADR 0048 y está bien— sino **diciéndolo**:
`SubstitutionPoint.PlayOnRisk` lleva ese número a la bandeja con la misma frase condicional que los
candidatos («si le entran, muere: X %»). Sin él, quedarse se anunciaba como «−15 % en sus atributos» y se
cobraba como «×8 de morir», que es exactamente el daño no anunciado que prohíben RF-012d y la regla 11 del
`CLAUDE.md`. Con él, la bandeja enseña el riesgo de **todas** las respuestas y no solo el de los suplentes.

**Decisión del revisor (23 sep 2026), sobre el ×8 ya medido y ya visible en la bandeja: se queda así.**
*«El player sabe que dejar un lesionado sube sus posibilidades de morir. Es su decisión.»* El multiplicador
no es un defecto que corregir sino el contenido de la decisión: quedarse es arriesgar al que ya está tocado,
y la única condición era que se supiera antes de elegir. Con `PlayOnRisk` en la bandeja —junto al de cada
candidato, en la misma frase y las mismas unidades— se sabe, así que RF-012d y la regla 11 quedan
satisfechas por información, no por suavizar el número.

Lo que esto convierte en decisión legible: contra un rival sin perk letal, quedarse cuesta el −15 % y poco
más; contra uno que sí lo lleva, quedarse es poner al más frágil justo donde más duele. Las dos cosas se
leen en la misma bandeja antes de pulsar.

**Deuda de proceso que se deja escrita, no tapada**: (E) no pasó por `game-design-review` —la nota de diseño
previa la excluyó explícitamente («es otra mecánica, mayor») y se incorporó a mitad del paquete—, así que
las diez preguntas no se rehicieron con ella dentro. El revisor ha resuelto la pregunta de diseño que
quedaba; lo que sigue sin existir es una **medición** del canje, y no puede existir con los instrumentos de
hoy: la política automática de `/Balance` nunca ejerce (D) ni (E). Medirlo exigiría darle una doctrina de
decisión, que es el mismo hueco que [CAT-C](../pendientes/CAT-C.md) tiene abierto para el consumible manual.

Además de eso, quien se queda arrastra desde ese tick la penalización de RF-091 en vez de esperar al final
del partido para notarla.

**Y el motor no la calcula: la recibe hecha.** Esta es la parte que cambió después de implementarla, y por
una razón que merece quedar escrita. La penalización de RF-091 es **lineal** en las lesiones acumuladas
(`100 − 15·n`, `RunState.ToDefinition:313-321`), y el atributo con el que un jugador entra al partido **ya
trae aplicadas las anteriores**. Descontarle otro 15 % dentro del motor compone *multiplicativamente*, que
no es lo mismo: con **una sola** lesión previa —el caso normal a media run, no un extremo— un atributo de 99
sale **71** en vez de 69, y la brecha crece con cada lesión (0,85² = 0,7225 contra 0,70). Desde el atributo
del partido no se puede recuperar el de la plantilla ni cuántas lesiones traía, así que el cálculo **no
puede** vivir en el motor.

Por eso `PlayOn` lleva `Attributes After`: los atributos resueltos, que calcula `/Sim/Run` llamando al
**mismo** `ToDefinition` de siempre —el que ya resuelve la inmunidad `MinorInjuryPenalty` de la ADR 0026, el
mismo truncamiento entero y el que la correa no se toque—. Es la misma idea que `TeamSetup`, que tampoco
lleva reglas sino jugadores ya resueltos; aquí se resuelven en un tick que no es el cero. La regla queda en
**un solo sitio**, así que la divergencia deja de ser algo que un test vigila y pasa a **no ser
representable** — que es el criterio que esta ADR aplica en todo lo demás.

**Un fallo hermano que destapó (E) y se arregla con ella:** `MatchStat.Down` (`EffectEngine.cs:1242`) era
`Injured || Dead`, y con alguien lesionado que sigue en el campo eso daría «caído» a quien está corriendo,
así que un perk con ámbito de compañeros caídos lo contaría. Pasa a `(Injured || Dead) && !OnPitch`. Hoy las
dos expresiones valen lo mismo —toda lesión sacaba del campo—, así que **no mueve ninguna tirada**, y se
comprueba con la huella de determinismo. Sin ese coste, «seguir jugando» sería estrictamente mejor que
sustituir y la decisión no existiría: sería poder gratis. Con él, las tres opciones son un canje real —un
titular tocado, un suplente fresco pero expuesto, o una casilla vacía—, y el desgaste de la plantilla sigue
siendo el recurso central.

**Y por eso el no-muerto hace lo que el revisor esperaba.** `ImmunityKind.MinorInjuryPenalty` (ADR 0026) ya
existe y el motor ya sabe preguntarlo (`Modifiers.HasImmunity`): el inmune se queda en el campo **entero**.
La inmunidad deja de ser una línea de ficha y pasa a ser una decisión visible en mitad del partido, que es
`identidad memorable > bonus genéricos`. Es la misma regla de RF-091 en los dos sitios —el mismo predicado de
inmunidad y el mismo porcentaje—, no una regla nueva parecida.

**Mecánicamente** el motor no aparta a la víctima en `ResolveInjury` (`MatchEngine.cs:2604`) cuando la
respuesta lo dice, y le suma los deltas de atributo con el mismo `AddAttributeDelta` que usan los perks. El
porcentaje **no se duplica**: viaja en `SimConfig`, que lo rellena `/Sim/Run` desde
`RunRules.MinorInjuryPenaltyPercent` —así el motor no necesita conocer `/Sim/Run` ni se toca `/data`—. El
resto sale solo: `victim.Injured` y el recuento del informe no cambian (la lesión ocurrió y la run la
apunta), y como `LeftPitchTick` se queda en −1, el punto de decisión **deja de pender sin bookkeeping
alguno**.

## Por qué así, y no de las otras formas

- **Pedir confirmación del once cada vez: rechazada por UI-003** (veinte segundos entre partidos, del mapa al
  saque en dos pulsaciones). El caso normal —no ha pasado nada— no puede costar una pulsación más.
- **Quitar el relleno automático: rechazada.** Castiga al jugador por una lesión que ya le castigó, y con
  plantilla mínima no hay elección que tomar.
- **Rechazar con un centinela `InPlayerId = -1` dentro de `TeamSetup.Substitutions`: rechazada**, y esta es la
  parte que más cambió por la revisión de arquitectura. Habría metido en la superficie pública de `/Sim` un
  valor que *cada consumidor debe acordarse de excluir* — el motor, la validación de `Simulator`, el replay,
  `MatchMomentView`. Es literalmente el patrón por el que [BE-F](../pendientes/BE-F.md) sigue abierto
  (`NodeKinds.IsMatch` incluye `Boss`). Un rechazo **no es una sustitución**, así que no viaja en la lista de
  sustituciones.
- **El precedente que se reutiliza, en vez de inventar uno:** `RunLineup.RiskCounterPrefix`, la marca de
  «alineado a sabiendas con lesión grave» (RF-093 vía 1), que pone la única puerta por la que pasa una
  decisión del jugador y borra `MatchResolution` al acabar el partido. La mitad de RF-002d que queda fuera de
  esta ADR (el hueco deliberado **antes** del partido) se hará con ese mismo mecanismo de contadores (W-11),
  que no sube la versión del guardado.

## Lo que la revisión independiente dejó abierto

Cinco cosas que la revisión encontró y que **no** se arreglan aquí, cada una con su sitio:

1. **El nodo de jefe rompe (A)**: `BossRules.PushBack` mueve casillas del equipo del jugador dentro de
   `systems.TransformMatch` (`RunEngine.cs:498`), por el que `Effective` no pasa. Era inocuo mientras la
   colocación fuera siempre el 2-3-1 —`eternal_crown` empuja la columna 6, donde ya estaba el delantero—,
   y **(A0) lo despierta** al permitir colocar en la 7. Es BG-B un nivel más arriba: → [BG-B](../pendientes/BG-B.md).
2. **`TeamScreen` sigue partiendo la plantilla por la guardada** → [BG-A](../pendientes/BG-A.md).
3. **Un `PlayOn` huérfano se descarta en silencio** —no entra en `Answers`, nadie comprueba que su punto
   exista— mientras que una sustitución o un rechazo huérfanos son `ArgumentException`. Si el partido
   diverge antes de su tick (BC-E midió un 9,4 % de los puntos), la decisión del jugador desaparece y la
   política sustituye sola. → [BG-C](../pendientes/BG-C.md).
4. **`Apply(SetLineup)` no valida casillas** (rango, repetición, portería) y desde (A0) esas casillas
   viajan verbatim hasta `Simulator`, que lanza a mitad de partido. → [BG-C](../pendientes/BG-C.md).
5. **`LineupWarnings` y `LethalRisks` pasan de no lanzar nunca a poder lanzar** (entran en `Build`, que
   exige plantilla viva). Documentado en su XML doc; sin camino conocido desde `/Game`. → [BG-C](../pendientes/BG-C.md).

## Lo que esta ADR NO hace

- **No implementa el hueco deliberado antes del partido.** RF-002d sigue sin poder elegirse mientras haya
  banquillo; lo que esta ADR da es la mitad de dentro del partido («que se quede el hueco») y la verdad sobre
  quién juega. Queda abierto en BC-H con el mecanismo ya decidido.
- ~~**No deja al lesionado leve en el campo.**~~ **Revocado el 23 sep 2026 por decisión del revisor**: sí lo
  deja, es el apartado (E). Lo que la investigación había establecido sigue siendo cierto y es lo que obligó a
  diseñar (E) con coste: `MatchEngine.RemoveFromPitch` (`:528-533`) apartaba *toda* lesión sin mirar la raza,
  y el `minorInjuryPenalty` de los no-muertos (ADR 0026) solo se notaba **entre** partidos
  (`RunState.cs:312`). La premisa del revisor («los no-muertos no reciben penalización, igual prefiero seguir
  con él») no describía el comportamiento de entonces, pero **sí describe el que esta ADR implanta**.
- **No toca los dos hermanos que mueven balance**, encontrados por el camino y anotados aparte:
  `EventSystem.Experience(onlyStarters: true)` (`:155`) reparte la experiencia de carta sobre la alineación
  **guardada**, así que el de relleno no cobra; y `RunPolicy.cs:1441` cuenta titulares igual. Corregirlos
  redistribuye experiencia y **desplaza las puertas**, así que exigen su propia medición.

## Consecuencias medibles y criterio de aceptación

### Medido (23 sep 2026)

- **Línea base, tomada en HEAD limpio (`f164461`) antes de tocar nada**: 43 puertas → **4 rojas**, las tres
  conocidas de [BF-B](../pendientes/BF-B.md) más su agregado (`badBuildsLoseToNone_orc_misplaced` 45,18 ·
  doctrinas −0,12 · `CommonAtMaxLevelMatchesALegendaryAtLevelTwo` 43,75).
- **Después: las mismas 4 rojas y las mismas 39 verdes**, con `orc_misplaced` **45,18** y rareza **43,75**
  idénticas al dígito.
- **La huella de determinismo no se mueve**: `DeterminismTests.CrossPlatformFingerprint` sigue en
  `209287DE9EDC1BB0` sobre sus 100 semillas. **Ningún partido de un estado inicial fijo cambia**, que es lo
  que había que demostrar de (D) y (E): la política automática nunca rechaza ni deja seguir a nadie, así que
  no ejerce ninguna de las dos.
- **Lo que sí se movió, y por qué no es un accidente**: la puerta de doctrinas pasó de **−0,12 a −0,11**
  (ahorradora 9,01 contra contextual 9,13). Es consecuencia directa de **(A0)**. Cuando un titular no puede
  jugar, antes se repartía **el equipo entero** por rol otra vez; ahora los que siguen **conservan su
  casilla** y el suplente ocupa la que queda libre. Las dos son legales; la nueva es la que respeta la
  intención del jugador —que una lesión no le recoloque a los otros seis—, y es exactamente la diferencia que
  (A0) existe para introducir. Afecta solo al caso «alguien de la alineación guardada no puede jugar», que es
  el único que la política ejercita, y el efecto medido es **una centésima de oro** sobre una puerta que ya
  estaba roja en la línea base y que BF-B tiene anotada por decidirse **sin margen**. Se registra aquí en vez
  de redondearlo (RT-057).
- **RT-024 en verde**; rechazar el sustituto y volver a resolver da el mismo partido.
- **1.124 tests rápidos en verde** (1.122 pasan, 2 no ejecutados), incluidos los 10 del apartado E.
- **Tests nuevos en `/Sim`**: el once efectivo coincide con el que juega (`Effective` contra `Build`);
  `LethalRisks` devuelve riesgo **para el jugador de relleno** (regresión de RF-012c, hoy falla);
  `FilledFromBench` y `Shorthanded` no se confunden; un rechazo no reabre su punto de decisión y no llega al
  motor.
- **Tests de (E)**: quien sigue jugando no deja el campo (`LeftPitchTick` −1) y **sí** cuenta como lesión en
  la run; sus cinco atributos bajan un 15 % **desde ese tick y no antes**; un inmune
  (`MinorInjuryPenalty`) se queda **sin** penalización; seguir jugando es ilegal en lesión **grave** y en
  muerte, y pedirlo es `ArgumentException` (RT-032, como la sustitución ilegal de la ADR 0094); una respuesta
  que el partido no llegó a pedir no se descarta en silencio.
- **Que (E) no mueva las puertas es parte de la prueba, no una suposición**: la política automática siempre
  sustituye, así que nunca ejerce (D) ni (E). Si `summary.csv` se moviera, sería que el motor cambió de
  comportamiento sin que nadie lo decidiera.
### Capturas (Xvfb, skill `visual-review`)

- **`pregon-bandeja.png` y `retrans-decision.png`**: la bandeja con las cinco respuestas cabe sin comerse el
  botón de Confirmar ni crecer sobre el campo. La primera versión **cortaba** dos subtítulos («…y no expon»,
  «−15» sin el «%»): con cinco respuestas cada casilla baja a ~135 px y el texto no puede pasar de ~24
  caracteres, así que se acortaron. En la pantalla real el que sale se lee «DEL · casilla G4 · **tocado**»
  —leve, no «lesionado» a secas— y por eso aparece «Que siga jugando»; los candidatos salen **sin** línea de
  riesgo porque ese rival no lleva perk letal, que es lo correcto.
- **`ojeo.png`**: el once efectivo se lista entero. **No verifica el aviso de relleno**: en el escenario de
  la secuencia nadie está lesionado, así que no hay hueco que tapar ni aviso que enseñar. La lógica la
  cubren los tests de `/Sim`, pero **falta un escenario de captura con un titular no disponible**, y sin él
  ese aviso no se regresiona solo. Queda anotado como paso concreto en [BC-H](../pendientes/BC-H.md).
