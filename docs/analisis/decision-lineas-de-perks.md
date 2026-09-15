# ¿Son las líneas el sitio correcto para la especialización?

Informe de **decisión previa**, no de calibración. **No se ha cambiado ni una línea del repositorio**
salvo este fichero. No hay commit de código asociado.

Etiquetas, como en los tres documentos que lo preceden: **MEDIDO** (leído del código o de `/data`, o
calculado con aritmética exacta sobre ellos, o salido de un lote de `/Balance`), **DERIVADO** (conclusión
encadenada sobre algo medido), **HIPÓTESIS** (interpretación de diseño sin validar, con el experimento que
la falsaría).

**Instrumento de todo lo cuantitativo que sigue**: un lote de `--full-runs 400 --seed 1`, es decir
**1.200 runs** (400 por cada una de las tres doctrinas automáticas de la ADR 0037), sobre el árbol limpio
en `b4e8aef`. El lote sale sano —`runWinRate` 25,00 (banda 20-30), `matchesPerFullRun` 19,54,
`deathsPerRun` 1,60, `masterDivergence` 16,01, `brokeMarketRunShare` 13,25, todas dentro—, así que los
números de abajo describen el juego que hay, no un árbol roto. Una sola semilla: los promedios de
recuento que se usan aquí tienen error típico pequeño, pero **ninguna cifra de este informe debe
convertirse en una banda** sin repetirla con cuatro semillas.

---

## 0. Resumen ejecutivo

1. **La línea es el sitio correcto para el COMPROMISO y el sitio equivocado para el CATÁLOGO.** El campo
   `family` hace hoy dos trabajos incompatibles: es el **libro mayor** de una renuncia irreversible
   (`requiresPerks` cuenta, `blocksPerks` cobra) y es a la vez la **taxonomía** del catálogo. El primero
   es la mejor pieza de diseño del juego. El segundo es la causa mecánica de la inflación. *(DERIVADO)*
2. **El requisito de línea no es un cuello de botella: es un trámite.** De 1,90 maestros ofrecidos por
   run, **1,64 (86 %) llegan al mostrador con la línea ya construida**. La run no persigue la línea: se la
   encuentra hecha. *(MEDIDO)*
3. **Las ocho piezas por línea no las pide el mecanismo.** El cargador exige 2; el pool entrega ~8,9
   ofertas cobrables de una línea dada por run; con 8 miembros la run **ve 5,6 distintos y se queda con
   1,0-1,3** en las tres líneas normalmente valoradas. Del quinto miembro en adelante, el catálogo escribe
   variantes que nadie compra. *(MEDIDO + DERIVADO)*
4. **`aim` es literalmente un efecto repetido ocho veces**: los ocho miembros escriben `shotOnTarget` y
   nada más. `wall` son 7 de 8 en `tackle`. `craft` y `butchery` no: tienen tres y tres canales. La
   premisa del encargo es exacta para dos líneas de cuatro. *(MEDIDO)*
5. **El compromiso se puede pagar con el banquillo.** `PerkPool.HeldPerkIds` recorre `state.Roster`
   entero, así que las dos piezas que desbloquean un maestro pueden estar en dos suplentes que no juegan.
   *(MEDIDO en el código)*
6. **La exclusión llega tarde**: el 59 % de las runs que cierran un arco **terminan llevando perks de la
   línea que cerraron** (media 1,06). El ideal catalogado —`human_granite` y `human_bloodrange`, que «no
   comparten un solo perk»— no es lo que las runs producen. *(MEDIDO)*
7. **La run cierra un arco, no dos.** 37,5 % de runs con maestro, y de las 466, **400 tienen exactamente
   uno y 66 tienen dos**: dos arcos ocurren en el **5,5 %** de las runs. Diseñar cuatro doctrinas en dos
   parejas es diseñar para un caso del 5 %. *(MEDIDO)*
8. **El número de doctrinas no lo fija el catálogo: lo fija cuántos recursos disputa el motor de verdad.**
   Con lo implementado hoy hay **un** recurso disputado (el espacio, vía `modifyLeash`, que es
   precondición dura), así que hoy solo hay material para **un par**. El resto serían etiquetas. *(§4)*
9. **El mayor riesgo medido de la propuesta de la biblia no está en la biblia**: degradar `killing_range`
   le quita a las siete piezas de `aim` los **104 puntos de crédito de arco** de la ADR 0072, y la propia
   ADR midió que sin crédito esas piezas caen por debajo del listón del slot y dejan de comprarse. No es
   reubicar un perk: es borrar siete. *(DERIVADO de la ADR 0072 §3 + medición propia)*

---

## 1. ¿Son las líneas el sitio correcto?

### 1.1 Qué hace hoy una línea, exactamente

**MEDIDO** (`Sim/Perks/PerkLoader.cs:251-320`, `Sim/Run/Systems/PerkPool.cs:206-260`):

| pieza | implementación | quién la usa |
|---|---|---|
| `family` (string) | etiqueta en el fichero del perk, declarada en `data/build/arcs.json` | 32 de 61 perks (8 × 4); 29 sin línea |
| `requiresPerks {family,count}` | cuenta **ids distintos** de esa familia **en toda la plantilla**, banquillo incluido; mínimo 2 | los 4 maestros |
| `blocksPerks.families` | cierra la familia opuesta para lo que quede de run, **mirando hacia adelante** | los 4 maestros |
| `blocksPerks.perks` | cierra perks concretos **por id** | **cero perks**. Existe en el cargador y no lo usa nadie |
| peso en el pool | idéntico en las cuatro: **16,3 %** cada una, 34,9 % sin línea | `PerkPool` |

Y una regla del cargador que importa para todo lo que sigue: **solo un maestro puede cerrar algo**
(`PerkLoader.cs:283-289`: *«un perk que cierra sin exigir nada es una trampa sin arco detrás»*). O sea:
hoy la única forma de crear una anti-sinergia es crear una línea.

### 1.2 Los dos trabajos que la línea hace a la vez, y por qué se estorban

**Trabajo A — libro mayor del compromiso.** `requiresPerks` necesita saber si la run ya ha demostrado que
va en serio; `blocksPerks` cobra el precio. **Para esto la línea no necesita miembros: necesita un
predicado.** Le bastan dos ids.

**Trabajo B — taxonomía del catálogo.** Como el predicado es «tener 2 perks cuyo campo `family` valga X»,
la familia **tiene que tener miembros**, y como se llamó igual que un canal de probabilidad, los miembros
**tienen que ser sinónimos de canal**. *(DERIVADO, y es la causa raíz que el encargo señala.)*

La prueba **MEDIDA** de que el trabajo B es el que produce la redundancia, canal por canal:

| línea | canales escritos por sus 8 miembros | ¿monocanal? |
|---|---|---|
| `aim` | `shotOnTarget` × 8 (valores 100·4, 200·2, 300, + 2 contadores) | **sí, total** |
| `wall` | `tackle` × 7 + `save` × 1 | **sí, casi** |
| `craft` | `dribble` × 4, `pass` × 3, `intercept`/`interceptEvasion` × 2 | no |
| `butchery` | `injure` × 5, `strength` × 2, `tackleEvasion` × 1, `severeInjury` × 2 | no |

Dos de las cuatro líneas son un efecto repetido. Y no es casual **cuáles**: `aim` y `wall` son las dos que
se llamaron por el canal, mientras que `craft` («gana sin tocar al rival») y `butchery` («lesionar») se
nombraron por una **conducta**, y por eso reclutaron miembros de tres canales distintos sin forzar
sinónimos. **DERIVADO: el problema no es que la especialización viva en líneas. Es que dos de las cuatro
líneas se nombraron con la unidad de máquina en vez de con la unidad de diseño**, exactamente el error que
la biblia diagnostica en §1.0 para el porcentaje.

### 1.3 La respuesta

> **Sí, las líneas son el sitio correcto de la renuncia, y no, no son el sitio del catálogo.** Se conserva
> el mecanismo entero y se **corta el vínculo entre el contador y la taxonomía**: `requiresPerks` deja de
> contar un campo de clasificación y pasa a contar una **lista corta de ids nombrados**.

Tres formas de cortarlo, en coste creciente. **Se recomienda la primera**, que no toca `/Sim` más que el
cargador:

1. **`requiresPerks` acepta una lista de ids** (`{"perks": ["a","b","c"], "count": 2}`) además de
   `{family,count}`. El cargador **ya sabe parsear listas de ids** (`ParseIdList`, usada por
   `blocksPerks.perks`), y `ValidateArcs` ya comprueba alcanzabilidad; la comprobación se hace contra la
   lista en vez de contra el recuento de familia. Consecuencia de diseño: **una doctrina tiene exactamente
   los miembros que le pertenecen de verdad, sin mínimo**; un perk puede pertenecer a dos doctrinas o a
   ninguna; y el catálogo se puede organizar por conducta (las diez familias de la biblia §3.2) sin que
   esa organización sea una regla de juego. *(DERIVADO; coste: un campo de esquema y una rama del
   cargador.)*
2. **`requiresPerks` lee un observable de la plantilla** —composición de etiquetas, zonas de inicio,
   correas— en vez de perks poseídos. La doctrina pasa a ser **consecuencia del equipo que has fichado**,
   no de los cromos que has coleccionado, y con ello la especialización se muda a *composición* + *puesto*,
   dos de las alternativas del encargo. Más expresivo y más caro: hace falta vocabulario de predicados,
   previsualización en la pantalla de Equipo, y decidir si se lee sobre plantilla (estable) o sobre
   alineación (fluctúa con la sustitución forzada de la ADR 0094). **HIPÓTESIS**, con un aviso: el eje de
   composición existe hoy y es de los peor medidos (`fine_orchestra` no tiene ni valor medido).
3. **Rebasar sobre doctrinas de equipo** (la propuesta de la biblia §2.5). Es un buen **nombre** para (1),
   pero por sí sola no resuelve el trabajo B: si una doctrina sigue necesitando 6-8 miembros para que la
   familia «tenga cuerpo», vuelve a fabricar sinónimos bajo una etiqueta mejor. **Se adopta la intención y
   se rechaza la densidad** (§5).

### 1.4 Las alternativas del encargo, evaluadas

| alternativa | veredicto | motivo |
|---|---|---|
| **El puesto** | **adoptar como eje de IDENTIDAD, rechazar como eje de COMPROMISO** | **MEDIDO**: solo **3 de 61** perks declaran `positionOnly`, los tres del portero, y la función `position()` no la usa ningún dato; el centrocampista no tiene un solo perk propio. Es el eje más vacío y más barato del juego, y es el que hace que un catálogo grande quepa (7 titulares = 7 identidades simultáneas sin redundancia). Pero **no crea renuncia**: alineas uno de cada puesto, así que no sacrificas nada. |
| **La composición del equipo** | **adoptar como PREDICADO candidato, rechazar como taxonomía** | Tiene coste real y anunciado —`styles.json` hace a los Brutos lentos y torpes, y a los jugadores los pagas—, que es más de lo que ningún perk tiene hoy. Pero **MEDIDO**: el eje `composition` son 4 perks y están entre los menos activados. |
| **La doctrina de compra** | **rechazar** | Las tres doctrinas de la ADR 0037 son un **instrumento de `/Balance`**, no un sistema del jugador, y lo que miden ya está medido: `contextualAdvantage` dice que construir le gana a acaparar. Convertirlo en sistema duplica el mercado que las ADR 0096/0097 acaban de rehacer y **no añade nada irreversible**, que es lo único que falta. |
| **Cap de identidad por jugador** | **adoptar, pero no sustituye a nada** | Es una regla de **densidad**, no una anti-sinergia: hace que el catálogo quepa en los 11,8 ids distintos que una run ganada lleva (§2), y crea la pregunta «¿a quién?». Pero la run en su conjunto no renuncia a nada, así que no cubre el hueco que cubre el maestro. Coste: una regla del cargador. |
| **Ninguna estructura explícita** | **rechazar** | **MEDIDO/DERIVADO en la auditoría y confirmado aquí**: sin maestro+bloqueo, ningún perk estorba a ningún otro, la estrategia óptima es *ordenar por valor esperado* y el juego **publica esos valores** en `data/economy/perk-values.json`. |
| **Retirar maestro+bloqueo** | **rechazar sin reservas** | Es la única decisión irreversible con precio que existe. Ver §3. |

### 1.5 Una pieza que ya existe y no usa nadie

**MEDIDO**: `blocksPerks.perks` —cerrar perks concretos por id— está implementado, validado y **usado por
cero perks**, y el cargador prohíbe usarlo sin `requiresPerks`. **DERIVADO**: la prohibición es correcta
para el bloqueo *de run* (cerrar algo para siempre sin haber demostrado nada es una trampa), pero **no
existe hoy la exclusión barata**: *«estos dos perks no pueden ir en el mismo jugador»*. Esa es una
anti-sinergia **por jugador**, reversible por colocación, previsible en la pantalla de Equipo, que no
necesita línea, ni maestro, ni mercado, ni oro — y la biblia ya encontró una pareja natural (Ancla ↔
Portero líbero, §4). **Recomendación: dos niveles de exclusión** —la de run (maestro, cara, irreversible,
pocas) y la de jugador (barata, muchas, decidida al colocar)—. *(HIPÓTESIS de diseño; se falsaría midiendo
si `masterDivergence` sube al añadir exclusiones por jugador, que es lo que predice.)*

---

## 2. La aritmética de slots, medida

Todo **MEDIDO** sobre las 1.200 runs. `slotCensus` (ADR 0072) da ofertas y slots en cada puerta de acto;
`finalPerks` da los ids distintos con los que termina la run; el reparto por línea se calcula cruzando
`finalPerks` con el campo `family` de `/data`.

### 2.1 Cuánto cabe y cuánto pasa por delante

| | contextual (todas) | contextual **ganadas** |
|---|---|---|
| Slots de perk del once | **15,0** (7 titulares, 2/3/4/5 por rareza) | 15,0 |
| Perks del once en la puerta del acto 1 / 2 / 3 | 2,99 / 7,36 / 9,89 | 3,18 / 8,15 / **11,20** |
| Ofertas de perk vistas en toda la run | **71,1** | 75,9 |
| De ésas, **cobrables** | **54,5** | **60,0** |
| Perks en la plantilla al terminar | 9,05 | 14,00 |
| **Ids distintos** al terminar | **7,34** | **10,72** |

Sobre las **1.200 runs**, los ids distintos al terminar son 7,34 (contextual) · 7,68 (ahorradora) · 9,97
(gastadora), y **11,82 en las 213 runs ganadas** (12,10 perks en el once). **El presupuesto real de una
run es de 8 a 12 ids distintos**, no 14-20: los 14-20 *slots* del encargo son capacidad, y la run no la
llena — en la puerta del acto 3 el once lleva 11,2 de 15 (75 %).

### 2.2 Cuántos de una misma línea acumula una run

| | contextual | ahorradora | gastadora | **ganadas** |
|---|---|---|---|---|
| Máximo de una **misma** línea | 3,49 | 2,83 | 4,27 | **4,56** |
| `aim` | **3,06** | 2,21 | 2,82 | **3,79** |
| `wall` | 0,81 | 0,66 | 1,94 | 1,25 |
| `craft` | 0,47 | 0,97 | 2,08 | 1,33 |
| `butchery` | 0,34 | 0,84 | 1,12 | 1,03 |
| % de los perks de la run que son piezas de línea | 63,7 % | 60,9 % | **79,8 %** | 62,7 % |

Dos lecturas, y las dos son de diseño, no de calibración:

- **La cifra de «3-4 de una línea» del encargo es correcta en el agregado y engañosa en el detalle.** El
  4,56 de las runs ganadas es **casi todo `aim`** (3,79). En las otras tres líneas la run se queda con
  **1,0-1,3 piezas**: ni siquiera llega al 2 que el maestro exige. *(MEDIDO)*
- **Las líneas no son una capa pequeña sobre el catálogo: son el catálogo.** La ADR 0051 acotó los
  *maestros* al 5-10 % (hoy 6,6 %, correcto), pero las **líneas** son el 52 % de los ficheros y el **63 %
  de lo que una run lleva encima**. *(MEDIDO)*

La desviación de `aim` no es de pool —las cuatro líneas pesan **16,3 %** exactamente igual— sino de
**valor**: `killing_range` mide 209 contra 119/32/24 de los otros tres maestros (ADR 0072 §3), y el
crédito de arco reparte la mitad de eso (**104 puntos**) a cada pieza de `aim`. Con ese crédito, `aim` es
la única línea que la política contextual compra de verdad. *(DERIVADO)*

### 2.3 Qué fracción de la run cuesta cerrar una doctrina

El coste **mínimo estructural** es **3 ids distintos**: 2 piezas (`requiresPerks.count`, mínimo del
cargador) + el maestro. El coste **observado** es **4,58** (media de piezas de su propia línea, maestro
incluido, en las 466 runs que cerraron un arco).

| | mínimo (3 ids) | observado (4,58 ids) |
|---|---|---|
| sobre una run **ganada** (11,82 ids) | **25,4 %** | **38,7 %** |
| sobre una run contextual media (7,34 ids) | **40,9 %** | 62,4 % |
| **dos** doctrinas (una de cada pareja) | **50,8 %** de una run ganada | ~78 % |

Y lo que las runs hacen con eso, **MEDIDO**: 466 de 1.200 cierran un arco (**37,5 %**); de ésas, **400
cierran uno solo y 66 cierran dos** → dos arcos en el **5,5 %** de todas las runs.

> **DERIVADO, y es la restricción que gobierna cualquier rediseño: una run cierra UNA doctrina.** La
> estructura de «dos parejas, una de cada» describe el 5 % de las partidas. Un diseño de cuatro doctrinas
> en dos parejas excluyentes no está ofreciendo dos decisiones: está ofreciendo **una decisión entre
> cuatro opciones**, con la segunda pareja funcionando como variedad de catálogo, no como decisión.

### 2.4 Dónde está el cuello de botella (no donde se creía)

**MEDIDO**, por run: maestros **ofrecidos 1,90** → de ésos, **con la línea ya construida 1,64 (86 %)** →
de ésos, **pagables 1,18 (72 %)** → **tomados 0,38**.

El requisito de línea filtra el **14 %**. El oro filtra otro 28 %. Lo que de verdad escasea es **la
oferta**: 1,90 apariciones de maestro en toda una run, consecuencia directa de que el maestro sea
`minAct 2`, solo de mercado (ADR 0055) y con `masterPreviewPercent` 20 mientras le falte una pieza.

**DERIVADO: las ocho piezas por línea no están sosteniendo el arco.** La run llega al mostrador con la
línea hecha 86 de cada 100 veces, y en tres de las cuatro líneas se queda con 1,0-1,3 piezas — o sea que
cuando el maestro de `wall` aparece y la run tiene 1,25 piezas, el desbloqueo **ya venía de serie o no
llega**. Las piezas sobrantes no son el camino hacia el maestro: son relleno del pool.

### 2.5 Dos grietas del mecanismo que la medición destapa

- **El compromiso se paga con el banquillo.** `PerkPool.HeldPerkIds(state)` recorre `state.Roster`
  completo. Las dos piezas que desbloquean un maestro pueden estar en dos suplentes que no juegan un
  minuto. *(MEDIDO en el código.)* El precio de la doctrina es entonces menor que el 25 % de §2.3: puede
  ser cero coste de once. **Arreglo barato: contar sobre el once alineable, no sobre la plantilla.**
- **La exclusión llega tarde y se paga con lo que ya no querías.** El **59 %** de las runs con maestro
  terminan llevando perks de la línea que cerraron (media **1,06**, hasta 7). El bloqueo mira hacia
  adelante por una razón buena (RF-072: un perk no se retira), pero eso significa que **la renuncia se
  cobra sobre ofertas futuras, no sobre lo que tienes**, y en el acto 3 quedan 14,1 ofertas cobrables de
  todo el catálogo: cerrar una línea ahí vale ~2,3 ofertas. *(MEDIDO + DERIVADO.)* **El precio de la
  exclusión decae con el acto**, y nadie lo ha calibrado.

---

## 3. Qué hace bien el mecanismo actual y hay que conservar

Concreto, y con el número al lado. **No tocar nada de esta lista sin un sustituto medido.**

1. **Maestro + `blocksPerks` es la única anti-sinergia del juego.** `elseEffects` está vacío en los 61
   perks (ADR 0088) y ningún perk empeora nada. Sin este mecanismo la única pregunta del juego es «¿cuál
   suma más?», y el juego publica la respuesta. *(MEDIDO en la auditoría, confirmado aquí.)*
2. **Produce divergencia de build medible.** `masterDivergence` 16,01 (suelo de puerta: 5) y
   `buildOverlap` 30,7 % con el mismo maestro contra **14,7 % con maestros distintos**: dos runs que
   eligieron distinto construyeron **la mitad de parecido**. Es la prueba dura de RF-032 y no la da
   ninguna otra pieza del juego. *(MEDIDO)*
3. **El bloqueo hacia adelante (RF-072) es correcto.** Apagar un perk ya pagado sería borrar algo
   comprado; anunciarlo antes de aceptar (RT-035 + pantalla de recompensa) cumple RF-012d. **Se conserva
   tal cual**, con la advertencia de §2.5 sobre su precio decreciente.
4. **`requiresPerks` cuenta ids distintos, no portadores.** Evita que «llevo tres copias del mismo perk»
   cuente como construcción. *(MEDIDO, `PerkPool.FamilyCount`.)*
5. **La validación de alcanzabilidad y de ciclos** (`PerkLoader.ValidateArcs`): una línea no puede exigir
   más piezas de las que tiene sin contar maestros, y por construcción no hay ciclos. Cualquier
   sustitución del predicado **tiene que conservar esta comprobación**.
6. **Que el maestro solo se compre** (ADR 0055). Es lo que ata el arco al mercado y lo que hace que el
   mercado sea núcleo y no tienda. Con 1,90 ofertas por run es también la palanca más sensible del
   sistema: subirla es la forma más barata de mover `mastersReached` sin tocar el catálogo.
7. **El crédito de arco acotado** de la ADR 0072 (valor del maestro ÷ piezas exigidas). Es lo único que
   impide que el listón del slot mate los arcos: sin él, `mastersReached` cayó de 25,2 a 14,8. **Cualquier
   rediseño de líneas rehace este número.**

---

## 4. Criterio para fijar el número de doctrinas (no el número)

El encargo prohíbe elegir entre 4, 5 y 6, y hace bien: **ese número no es una decisión de catálogo**. Es
una consecuencia. El criterio, en tres pruebas **eliminatorias y en este orden**:

### Prueba 1 — la frase (diseño)

> «No puedes ser **X** y **Y** a la vez **porque las dos gastan Z**.»

Con `Z` nombrable en una palabra y **la misma** para las dos. Si la frase sale «X e Y son temáticamente
opuestos», no hay exclusión: hay dos etiquetas.

**Aplicada a lo que hay hoy**: `aim` cierra `wall` = «no puedes rematar **y** robar». No hay `Z`. Rematar
lo hace el delantero sobre `shotOnTarget`; robar lo hace el defensa sobre `tackle`; **son canales
distintos, jugadores distintos y momentos distintos, y no compiten por nada**. *(MEDIDO: los canales,
§1.2.)* Es una exclusión que no describe ninguna forma de jugar al fútbol, y el encargo tiene razón.

### Prueba 2 — el mecanismo (motor)

`Z` tiene que ser algo que **el motor dispute de verdad**, no una metáfora. La matriz de efectos ya dice
cuáles son los candidatos, y son pocos:

| `Z` candidato | ¿lo disputa el motor hoy? | evidencia |
|---|---|---|
| **El espacio del bloque** (dónde vive el equipo) | **SÍ** | `modifyLeash` + `EffectiveHome` son **precondición dura**: `Utility` descarta la acción entera si el destino cae fuera (`OutsideOuterLimit`). Un equipo no puede tener el bloque bajo y alto a la vez. *(MEDIDO)* |
| **El tick del jugador** (a qué dedica su decisión) | **todavía no** | `Utility` es un `argmax` puro sobre el conjunto legal, así que dos órdenes sobre acciones del mismo conjunto **sí** se disputan… pero ningún perk escribe ahí: `Utility.cs` no lee `Odds`, `Modifiers` ni `_effects`. Necesita la capa de intención (C1/C2 de la biblia). *(MEDIDO)* |
| **La carne de la plantilla** | **a medias** | `lethal`, `injure` y las bajas son un recurso agotable y compartido, y es el recurso central de la run. Pero hoy la carnicería solo gasta carne **del rival**: no hay mecanismo que haga que ser violento cueste tus propios cuerpos. |
| **Oro y slots** | **sí, pero ya está cobrado** | La ADR 0072 y la 0055 ya ponen precio al slot y al oro. Hacerlo doctrina sería **cobrar dos veces** lo mismo. **Descartado por principio, no por medida.** |
| **Cualquier cosa en `modifyProbability`** | **NO** | El 62,7 % de los efectos del juego no puede disputar nada: dos perks de probabilidad **nunca** se estorban. *(MEDIDO en `builds-matriz-efectos.md`.)* Aquí es donde viven `aim` y `wall` enteras. |

### Prueba 3 — la medida (falsable)

Un par propuesto se acepta solo si, **medido**, las dos doctrinas producen partidos distintos **y
mutuamente excluyentes**. Dos números, ambos construibles con lo que ya existe:

- **Divergencia**: `masterDivergence` y `buildOverlap_differentMaster` sobre el par nuevo, con el suelo que
  ya exige la puerta (≥ 5 puntos; hoy 16,01 y 14,7 %).
- **Conducta**: distancia L1 entre los **histogramas de acción elegida** de las dos doctrinas, contando la
  acción **ganadora** por jugador y tick con el volcado que RT-098 ya produce. Es el instrumento que la
  biblia §1.4 propone y que hoy no existe; **es el que distingue «dos formas de jugar» de «dos formas de
  puntuar»**, y sin él ningún rediseño de líneas se puede aceptar ni rechazar con datos.

### El número, entonces

> **Número de doctrinas = 2 × (número de recursos `Z` que pasan las tres pruebas).**

Con lo implementado **hoy**: `Z` = el espacio → **un par, dos doctrinas** (bloque bajo ↔ bloque alto).
Con la capa de intención (C1/C2) en pie: `Z` = el tick → **un segundo par** (ir al balón ↔ ir al hombre) →
cuatro. Con un coste de carne propia: **un tercero** → seis.

**DERIVADO, y es la conclusión incómoda: la cifra de doctrinas es una consecuencia de la hoja de ruta de
motor, no del catálogo conceptual.** Escribir hoy cuatro doctrinas sobre un motor que solo disputa una
cosa produce **un par real y uno decorativo** — que es exactamente lo que hay ahora, con `craft`/`butchery`
(dos conductas) funcionando mejor que `aim`/`wall` (dos canales).

---

## 5. Densidad mínima de una doctrina

### 5.1 El suelo no lo pone la estética: lo pone la oferta, y ya está medido

- **Suelo duro del cargador**: `requiresPerks.count + 1`, hoy **3** (`ValidateArcs` exige que la línea
  tenga al menos `count` miembros no-maestros alcanzables).
- **Suelo de oferta**: la run ve **54,5 ofertas cobrables**; con el peso actual de una línea (16,3 %) eso
  son **8,9 ofertas de esa doctrina por run**. Con `M` miembros, los distintos que la run llega a **ver**
  son `M·(1−(1−1/M)^8,9)`:

| miembros `M` | distintos **vistos** por run | distintos **tomados** (medido) |
|---|---|---|
| 3 | 2,9 | — |
| 4 | 3,7 | — |
| 5 | 4,3 | — |
| 6 | 4,8 | — |
| **8 (hoy)** | **5,6** | **1,0-1,3** (`wall`, `craft`, `butchery`) · 3,8 (`aim`) |

**DERIVADO**: a partir de `M=4` la run ya ve prácticamente la doctrina entera, y **del quinto miembro en
adelante el catálogo escribe piezas que la run ve y no compra**. Los miembros 6, 7 y 8 aportan **0,8
avistamientos** y, en tres de las cuatro líneas, **cero compras**.

- **Techo de presupuesto**: una doctrina debe ser *una forma de jugar*, no *la run*. Con 11,8 ids
  distintos en una run ganada y un coste observado de 4,58, la doctrina ya se come el **39 %**. Un `M` de
  7-8 invita a que se coma el 60 %.

> **Densidad recomendada: 4-6 miembros por doctrina, con 5 como centro**, incluido el maestro. No 8. El
> número sale de la aritmética de oferta, no del gusto.

### 5.2 Cómo se comprueba que no son sinónimos

La densidad es la mitad fácil. La difícil es que los `M` miembros **no sean el mismo perk**. Tres pruebas,
en orden de coste, y **la tercera es la que decide**:

1. **Prueba de canal (gratis, automatizable hoy).** Dos miembros de una doctrina **no pueden** compartir
   a la vez canal, disparador y forma de la condición. Es una consulta sobre `/data` y podría ser un test
   de esquema. **MEDIDO: `aim` la suspende con 8 de 8** (`shotOnTarget`, `SHOT`, condición de distancia o
   etiqueta); `wall` con 7 de 8.
2. **Prueba de valor emparejado (existe hoy).** `--perk-values` con el control de la ADR 0087. Si dos
   miembros miden lo mismo dentro de `rowDeviation` (7) **y** comparten canal, son uno. Es necesaria y no
   suficiente: mide resultado, no conducta.
3. **Prueba de conducta (instrumento a construir, barato).** Histograma de acción **elegida** por jugador
   y partido, desde el volcado de RT-098. Dos miembros de la misma doctrina se aceptan si la distancia L1
   entre sus histogramas, cada uno contra su control emparejado, supera un umbral que hay que fijar
   midiendo primero parejas que **sabemos** que son iguales (`bulwark_stance` / `own_third_anchor`) y
   parejas que sabemos que son distintas (`sweeper_keeper` / `iron_gate`). **Sin este instrumento, «no son
   sinónimos» es una opinión.** *(HIPÓTESIS con experimento asignado.)*

---

## 6. Qué significa cada exclusión

La regla: **una frase, con la `Z` nombrada, o la exclusión sobra.** Aplicada a los pares de hoy y a los
propuestos.

### Los cuatro de hoy

| par | frase | veredicto |
|---|---|---|
| `aim` ↔ `wall` | «no puedes rematar y robar» | **sobra**. No hay `Z`: canales distintos (`shotOnTarget` / `tackle`), puestos distintos, momentos distintos. *(MEDIDO)* |
| `craft` ↔ `butchery` | «no puedes ganar sin tocar al rival y a la vez ganar tocándolo» | **casi**. Hay una `Z` insinuada —a qué dedica el equipo el contacto— pero el motor no la disputa: `dribble`/`pass` e `injure` son tiradas independientes que no compiten por nada. **Frase buena, mecanismo ausente.** |

### Los dos de la biblia

| par | frase | veredicto |
|---|---|---|
| **Muralla ↔ Avalancha** | «no puedes replegar el bloque y presionar arriba, **porque las dos gastan la posición vertical del equipo**» | **vale, y es el único que vale hoy.** `Z` = el espacio; el motor lo disputa como **precondición dura** (`modifyLeash`, `OutsideOuterLimit`). **MEDIDO, con un aviso**: los 3 perks que usan `modifyLeash` lo suben **todos**; nadie lo baja nunca. El par exige un `modifyLeash` negativo, y si eso choca con la ADR 0088 es **decisión del revisor**, no mía. |
| **Toque ↔ Carnicería** | «no puedes jugar la pelota y jugar al hombre, **porque las dos gastan el tick del jugador**» | **vale solo si se reescribe así**, y solo **después** de C1/C2. Tal como la biblia la enuncia —posesión contra violencia— es temática: en Blood Bowl, que es la referencia declarada del proyecto, los elfos tienen blitzers. La versión que **sí** tiene `Z` es «mis jugadores van al balón» ↔ «mis jugadores van al hombre», y esa `Z` es el `argmax` de `Utility`, que hoy ningún perk toca. |

**DERIVADO**: la biblia sustituye **un** par arbitrario por **un** par legítimo y **uno** todavía por
demostrar, y lo presenta como cuatro doctrinas cerradas. Es medio acierto, no uno entero, y el medio que
falta depende de una capacidad de motor que no existe. **No es sobreingeniería en la idea —el diagnóstico
de §2.5 de la biblia es correcto y está medido— pero sí lo es en el calendario**: propone cuatro doctrinas
para un motor que hoy sostiene una.

### El par que nadie ha propuesto y que el juego pide

«No puedes cobrarte carne y conservar la tuya, **porque las dos gastan la plantilla**.» Es la única `Z`
que ya es el recurso central declarado de la run y que el jugador **ya siente** (la clínica, el matasanos,
las prótesis). Hoy la carnicería solo gasta carne ajena. *(HIPÓTESIS: un mecanismo que haga que la
violencia propia cueste bajas propias convierte `butchery` en la primera doctrina con precio en la moneda
del juego. Se mediría con `deathsPerRun` y `ownInjuries`, que ya están en `runs.csv`.)*

---

## 7. Coste del cambio

### 7.1 `killing_range` — el riesgo que la biblia no contabiliza

Degradarlo a raro sin línea, **MEDIDO/DERIVADO**:

- Es el maestro más tomado con diferencia: **312 de 532** maestros tomados en el lote (**59 %**), contra 91
  `granite_line`, 84 `first_touch_school`, 45 `blood_tithe`.
- Es el maestro de **mayor valor medido**: 209 contra 119 / 32 / 24 (ADR 0072 §3).
- Y por eso es el que sostiene el **crédito de arco** de su línea: **104 puntos por pieza**. La ADR 0072
  midió qué pasa sin crédito: `spearpoint` (−141) y `forward_line` (−115) caen por debajo del listón y la
  política deja de comprarlos; `mastersReached` bajó de 25,2 a **14,8**.

> **DERIVADO: degradar `killing_range` no reubica un perk. Deja a las siete piezas de `aim` sin el único
> motivo por el que la política las compra.** Como `aim` es el 63 % de las piezas de línea que una run
> ganada lleva encima (3,79 de 4,56), el efecto sobre la densidad de build del once es grande y hay que
> medirlo **antes**, no después. Es el punto más frágil de la propuesta de la biblia y no aparece en ella.

Si aun así se degrada, hacen falta a la vez: (a) un maestro nuevo para la cuarta doctrina, (b) rehacer el
crédito de arco de la ADR 0072 sobre la lista nueva, y (c) rehacer `data/economy/perk-values.json`, porque
los valores se midieron con estas líneas.

### 7.2 Las builds catalogadas de fase 1

- **`human_bloodrange` deja de existir**: está construida sobre `killing_range` + `blood_tithe`. Su pareja
  `human_granite` (`granite_line` + `first_touch_school`) sobrevive solo si `wall` y `craft` siguen siendo
  doctrinas con maestro.
- **`Sim.Tests/Analysis/CataloguedBuildArcTests.cs` falla en sus dos tests**: uno comprueba que toda build
  catalogada es legal bajo la ADR 0051; el otro, `TheTwoMasterBuildsOfTheSameRaceExcludeEachOther`, exige
  literalmente que esas dos builds existan, tomen maestros opuestos y **no compartan ni un perk**. Hace
  falta una pareja nueva de builds excluyentes **antes** de tocar `/data`.
- **`data/balance/groups.json`**: `coherent`/`bad` no incluyen a ninguna de las dos, así que las puertas
  automáticas de fase 1 **no se mueven** por este cambio. Lo que sí se mueve es `actDensity`, cuyos anclas
  (4,3/9,5/11,3 perks del once) se midieron con la política y el catálogo de hoy.
- **`data/rivals/*.json`**: cada rival ordinario conserva a propósito «una pieza de la línea que le da
  identidad» (ADR 0059 §4). Si desaparecen líneas, hay que reasignar esas piezas en los 12 ficheros de
  rival y volver a medir `deathsPerRun`, que fue la métrica que esa decisión movió (1,44 → 0,55 al
  quitarlas).

### 7.3 Qué hay que remedir, en orden

1. **Puertas con maestro dentro**: `masterDivergence` (suelo 5) y `mastersReached` (banda 2-90).
2. **Las bandas que el crédito de arco movió**: `runWinRate` (20-30), `contextualAdvantage` (suelo 8) y
   los perks del once en las tres puertas. La ADR 0072 midió que el listón vale 3,17 puntos de run y el
   crédito 0,91 más: **ambos se recalculan**.
3. **`data/economy/perk-values.json` entero**, con `--perk-values`, porque el crédito de arco se deriva de
   los valores de los maestros.
4. **La curva de puertas de la ADR 0033** (`BossGateTests`), porque sus escalones de calidad se derivan de
   builds y de `actDensity`.
5. **`data/l10n/{es,en}/templates.json`, sección `families`**, y la descripción generada (RT-035) y el
   aviso de la pantalla de recompensa, que nombran las líneas al jugador.
6. **Esquema de `/data`** (`requiresPerks` con lista de ids) y `tools/DataValidator`.

**No hace falta remedir** nada del motor de partido: ninguna de estas propuestas toca `/Sim` fuera del
cargador y de `PerkPool`.

---

## 8. Riesgos y modos de fallo de esta propuesta

1. **Convertir `requiresPerks` en lista de ids hace el compromiso más barato, no más caro.** Con una
   doctrina de 5 en vez de 8, las piezas son más valiosas individualmente y la run las junta antes.
   `mastersReached` puede **subir** por encima de su techo (90) y el arco dejar de ser una decisión.
   *Mitigación*: `count` es un parámetro; subirlo de 2 a 3 es una palanca directa. *Aviso*: subirlo lleva
   el coste mínimo de 3 a 4 ids, o sea del 25 % al 34 % de una run ganada (§2.3).
2. **Reducir a un solo par legítimo empobrece el catálogo antes de enriquecerlo.** Si se aplica el
   criterio de §4 con rigor, hoy **sobran dos de las cuatro líneas** y no hay con qué sustituirlas hasta
   que exista la capa de intención. El juego pasaría por una ventana con **menos** decisiones que hoy.
   *Mitigación*: no retirar `aim`/`wall` hasta tener el par de espacio implementado y medido; conservarlas
   como doctrinas imperfectas es mejor que no tener ninguna.
3. **El par del espacio exige un `modifyLeash` negativo, y eso roza la ADR 0088.** Una correa más corta a
   cambio de algo es «renuncia», no «castigo» —no hay rama `else`—, pero es **exactamente la lectura que
   la ADR 0088 dejó abierta** y la decisión es del revisor, no de este informe. Si se decide que no, el
   único par legítimo de hoy **también** se cae, y la respuesta correcta pasa a ser «conservar las líneas
   actuales y arreglar solo la densidad».
4. **Contar sobre el once en vez de sobre la plantilla (§2.5) puede romper el arco.** Es el arreglo
   correcto de diseño y encarece el compromiso justo cuando §5 lo está abaratando. Las dos palancas se
   mueven en sentidos opuestos: **hay que medirlas juntas, en la misma tanda**, o una tapará a la otra.
5. **La prueba de conducta puede no separar nada.** Si el histograma de acción elegida resulta insensible
   a los perks —lo que es **plausible**, porque el 62,7 % de los efectos no entra en `Utility`—, entonces
   el instrumento devolverá «todos los perks son sinónimos» y no servirá para decidir. *Eso no sería un
   fallo del instrumento: sería la confirmación más dura posible del diagnóstico de la auditoría*, y
   obligaría a construir la capa de intención antes que el catálogo.
6. **Todo lo cuantitativo de aquí sale de una semilla.** Los recuentos son robustos, pero
   `mastersReached` (37,5 %) y el reparto entre maestros salen de 466 observaciones y **no deben
   convertirse en banda** sin repetir con cuatro semillas, que es la norma del proyecto desde la ADR 0072.
7. **Riesgo de proceso, el mayor de todos**: este informe propone tocar el campo que define el 52 % del
   catálogo, el crédito de la política de compra, la tabla de valores, los rivales, las builds de
   referencia y la curva de puertas. **Si se hace en una tanda, no habrá forma de atribuir lo que se
   mueva.** El orden mínimo es: (1) instrumento de conducta, (2) par del espacio en paralelo a las líneas
   actuales, medido contra ellas, (3) retirada de las líneas que el criterio de §4 suspenda.
