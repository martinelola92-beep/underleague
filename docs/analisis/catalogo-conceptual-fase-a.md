# Catálogo conceptual: el proceso limpio (Fases A-D)

**Qué es esto.** El encargo del revisor, textual: *«Con 14-20 slots, nuestro enemigo no es tener 60 perks.
Nuestro enemigo es que 60 perks sean intercambiables.»* La biblia (`perks-design-bible.md`) escribió 80
candidatos **con diez familias impuestas de antemano**; el revisor ha decidido que ese orden estaba
invertido. Aquí cada candidato se diseña **solo**, y las familias **aparecen** al agrupar por conducta.

**No se ha modificado ningún fichero del repositorio salvo este.** Ni código, ni JSON, ni pesos, ni
probabilidades. No se ha lanzado ningún lote de `/Balance`: este documento **no propone ni un número**, y
la única medida que pide (el histograma de acción elegida) todavía no existe como instrumento.

**Etiquetas:** **MEDIDO** (leído del código, de `/data` o de un experimento ya ejecutado) · **DERIVADO**
(conclusión encadenada sobre algo medido) · **HIPÓTESIS** (interpretación de diseño sin validar).

**Decisiones provisionales que obligan** (`docs/pendientes.md`): **PD-1** un perk sí puede penalizar si
expresa una identidad · **PD-2** vínculos RF-100..106 fuera del alcance (se usa `linked`, el vínculo
**estático de alineación**, que sí existe) · **PD-3** la taxonomía de líneas está **congelada**; los
conflictos se **clasifican**, no se descartan · **PD-4** 5-6 miembros funcionalmente distintos por familia,
y el total **no se fija en 36** · **PD-5** `killing_range` **congelado**: no se mueve ni se degrada.

---

## Dos correcciones al instrumento, antes de usarlo

El encargo exige que cada candidato pase los cuatro eliminatorios del Perk Design Test (auditoría §16):
**[E1]** una frase sin cifras · **[E2]** verbo de conducta · **[E3]** atribuible en una jugada · **[E4]**
distinto en canal + condición + magnitud. Aplicándolos en serio aparecen dos agujeros del propio test, y
conviene dejarlos escritos antes de la lista porque explican dos de las decisiones de reparto.

**Corrección 1 — los cuatro eliminatorios matan la clase entera del oficio.** *(DERIVADO)* Un perk de
`modifyProbability` puro («intercepta mejor», «para mejor», «remata mejor») **nunca** completa la frase de
[E2] con un verbo del motor ni sobrevive a [E3]: la acción es la misma, cambia el dado. La auditoría pedía
un techo del 40 % para esa clase; el test, aplicado literalmente, pide **cero**. Los dos no pueden tener
razón a la vez.

> **Propuesta:** el oficio no es un perk, es una **estadística**, y el juego ya tiene una capa para eso.
> La ADR 0036 dice «perk = una regla, objeto = una estadística» y **MEDIDO: 34 de 34 objetos suben
> atributos y nada más**. Mover el oficio a `/data/items` —con `modifyProbability` en el objeto, no en el
> perk— cumple la ADR 0036 por primera vez, devuelve profundidad a una capa que hoy es un +10 con nombre,
> y deja el catálogo de perks entero para la conducta. **Es decisión del revisor**; aquí solo se registra
> que la alternativa es una excepción declarada y acotada. En este documento **el oficio puro está en la
> lista de descartados**, con una excepción argumentada (`C-34 Manos de cepo`), y esa excepción enseña la
> regla: se admite cuando el canal no es la tirada sino **la rama del resultado** (una parada que se queda
> **es un suceso distinto** de una parada que escupe; la segunda jugada no existe, y eso se ve).

**Corrección 2 — el test tiene un punto ciego: la carne de la run.** *(DERIVADO)* [E2] y [E3] están
escritos para el partido («un verbo del motor», «una jugada concreta»). La identidad declarada del juego
**no es el partido, es la carnicería administrada**: lesiones, clínica, prótesis, plantilla. Un perk que
cambia el libro mayor de la carne —el que juega roto, el que se cura solo, el que hereda las botas del
muerto— no tiene «una jugada» y sin embargo es **lo más reconocible que puede llevar un jugador**, porque
se recuerda entre partidos y cuesta oro.

> **Lectura adoptada:** para esa clase, [E3] se lee como **«atribuible en un cuerpo»**: existe un momento
> concreto de la run —una factura de clínica que no se paga, un cuerpo que sigue en la plantilla cuando no
> debería— tras el cual el jugador dice «eso ha sido el perk». Es la misma exigencia, con el suceso en la
> moneda correcta. Los candidatos de esta clase van marcados **[carne]**.

---

# FASE A — Candidatos, sin familia

**Regla de la fase:** aquí no existen `family`, líneas ni doctrinas. Cada ficha se juzga sola. El orden es
el de generación y **las tandas son lotes de trabajo, no una taxonomía**: agrupar es trabajo de la Fase B,
y si un lote parece una familia es casualidad de escritura, no un resultado.

**Formato**, en el orden exigido por el encargo:
`Fantasía` (una frase, sin cifras) · `Conducta` (qué hace que antes no hacía, con un verbo del motor) ·
`Condición` (una; si son dos, la segunda es una decisión del jugador) · `Canal` · `Puesto` · `Coste` ·
`Conflicto` (con la frase que lo explica) · `Motor` (existente / extensión pequeña / sistema nuevo) ·
`Evidencia` (qué lo demostraría en la prueba ciega).

**Vocabulario de motor usado** *(MEDIDO)*. Catorce acciones elegibles: `ChaseBall`, `MarkOpponent`,
`OfferSupport`, `CoverSpace`, `Dribble`, `Shoot`, `Tackle`, `Retreat`, `FindSpace`, `PressCarrier`,
`ShortPass`, `LongPass`, `ThroughPass`, `Block`. Cuatro estados tácticos por equipo: `InPossession`,
`OutOfPossession`, `OffensiveTransition`, `DefensiveTransition`. Trece escalares por jugador que hoy solo
escriben los rasgos (`ShootRangeBonusCells`, `HardTackleBonus`, `FoulChanceBonus`, `InjuryChanceBonus`,
`SaveBonusClose/Far`, `LeashBonus`…). Y las capacidades que **no** existen, con el nombre que se usa en la
hoja de ruta del final: **C1** `modifyUtility` · **C2** vocabulario de situación dentro de la decisión ·
**C3** reloj y marcador en `UtilityContext` · **C4** escritura sobre los trece escalares · **C5** sesgo de
objetivo de marca · **C6** preferencia de receptor · **C7** criterio de objetivo de entrada · **C8**
desplazamiento de hogar y forma de zona · **C9** la activación como evento · **C10** techos por acción y
puesto en el cargador · **C11** contadores de carne por jugador.

---

## Tanda 1 (C-01 … C-14)

**C-01 · Perro de presa**
- **Fantasía:** le asignas un hombre y no lo suelta en todo el partido.
- **Conducta:** elige `MarkOpponent` sobre el rival de más técnica, no sobre el que le toca por geometría.
- **Condición:** una, de identidad: siempre que su equipo no tenga el balón.
- **Canal:** C5 (sesgo en la función de coste de `Marking.Assign`, que ya tiene un término de preferencia) + C1. · **Puesto:** centrocampista o defensa.
- **Coste:** deja de cubrir el hueco que le tocaba; si el crack rival se va a la banda, él se va detrás y abre el centro.
- **Conflicto:** `C-04 Cobertura` — «no puede ir a por un hombre y quedarse tapando el hueco: es el mismo tick».
- **Motor:** extensión pequeña (C5 + C1).
- **Evidencia:** el mismo verde aparece encima del mismo rival en tres recepciones seguidas; en el volcado, `MarkOpponent` sobre un único `targetId` durante la mayor parte del partido.

**C-02 · Guardaespaldas**
- **Fantasía:** protege a un compañero: quien se le acerca, lo tiene encima.
- **Conducta:** elige `MarkOpponent` sobre el rival más cercano a su compañero vinculado.
- **Condición:** una, decidida al alinear: tener un vinculado (`linked`, estático de alineación; **no** RF-100..106, PD-2).
- **Canal:** C5. · **Puesto:** defensa o centrocampista.
- **Coste:** su marca depende de dónde esté **otro**; si el vinculado sale lesionado, el perk se apaga.
- **Conflicto:** `C-01 Perro de presa` — «no puede marcar al mejor del rival y a la sombra del suyo a la vez».
- **Motor:** extensión pequeña (C5 con referencia a un tercero).
- **Evidencia:** el pequeño del equipo recibe y aparece el grande; distancia media al vinculado muy por debajo de la de su puesto.

**C-03 · Hombre libre**
- **Fantasía:** nadie lo marca, porque nadie sabe qué es.
- **Conducta:** los **rivales** lo saltan al repartirse las marcas y eligen a otro.
- **Condición:** una, de identidad: siempre.
- **Canal:** C5 aplicado al emparejamiento contrario. · **Puesto:** centrocampista.
- **Coste:** ninguno propio, y eso es su problema: es el candidato más potente y el que menos paga. Su precio tiene que ser de rareza y de slot, no de conducta.
- **Conflicto:** `C-27 Hombre objetivo` — «no puedes ser el hombre que nadie mira y el hombre al que todos buscan».
- **Motor:** extensión pequeña (C5), con aviso: **es el único candidato que toca la IA del rival** y hay que medirlo aparte.
- **Evidencia:** recibe solo; en el volcado, ningún rival lo tiene como `markTarget` en la mayoría de los ticks.

**C-04 · Cobertura**
- **Fantasía:** no va al balón: tapa el sitio por donde va a venir.
- **Conducta:** prefiere `CoverSpace` sobre `ChaseBall` cuando su equipo no tiene el balón.
- **Condición:** una, de estado de partido evidente.
- **Canal:** C1 + C2. · **Puesto:** defensa.
- **Coste:** nunca es el que roba; su nombre no sale en el informe aunque el equipo encaje menos.
- **Conflicto:** `C-06 Recuperador` — «no puedes ser el primero en llegar al balón y el que se queda tapando: es el mismo tick».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** mientras dos van al balón, uno se coloca entre el balón y su portería; `CoverSpace` domina su histograma frente al control emparejado.

**C-05 · Cortacircuitos**
- **Fantasía:** se pone donde iba a ir el pase.
- **Conducta:** su `CoverSpace` elige la casilla del pasillo entre el portador y su mejor receptor, no la casilla hacia su portería.
- **Condición:** una: el rival tiene el balón y hay pasillo que tapar.
- **Canal:** C8 (destino de `CoverSpace`) + el pasillo, que el motor ya evalúa (`passBlockedLanePenalty`). · **Puesto:** centrocampista.
- **Coste:** se coloca por delante de su sitio; si el pase sale por el otro lado, queda descolgado.
- **Conflicto:** `C-04 Cobertura` — «tapar el pasillo y tapar la portería son dos sitios distintos y solo hay un cuerpo».
- **Motor:** extensión pequeña (C8 + lectura de lo que ya calcula `EvaluatePass`).
- **Evidencia:** los pases del rival empiezan a salir por fuera; cadena de pases del rival más corta por su banda.

**C-06 · Recuperador**
- **Fantasía:** el primero en llegar al balón suelto, siempre.
- **Conducta:** sube `ChaseBall` y lo elige también cuando no es el perseguidor designado.
- **Condición:** ninguna: es identidad pura.
- **Canal:** C1. · **Puesto:** centrocampista.
- **Coste:** abandona su zona cada vez que hay un rebote; deja hueco detrás.
- **Conflicto:** `C-04 Cobertura` — «el que va a por el balón no es el que se queda».
- **Motor:** extensión pequeña (C1).
- **Evidencia:** gana la mayoría de segundas jugadas; `ChaseBall` muy por encima de su puesto en el histograma.

**C-07 · De área a área**
- **Fantasía:** juega en las dos áreas y no se le acaba el campo.
- **Conducta:** su zona de acción es mucho más larga, así que `ChaseBall` y `Retreat` dejan de estar descartados por la precondición dura de la correa.
- **Condición:** ninguna.
- **Canal:** `modifyLeash` (**existe hoy**, y es el mejor efecto del sistema: `Utility` descarta la acción entera fuera del límite exterior). · **Puesto:** centrocampista.
- **Coste:** está lejos de su hogar la mitad del partido; el hueco que deja lo tiene que tapar otro.
- **Conflicto:** `C-10 Ancla` — «una correa larga y una correa corta son la misma cuerda».
- **Motor:** **existente**.
- **Evidencia:** aparece en las dos áreas en el mismo partido; recorrido y dispersión de posición muy por encima de su puesto.

**C-08 · Carnicero ciego**
- **Fantasía:** no persigue el balón; persigue al hombre.
- **Conducta:** **rehúsa** `ChaseBall` y elige `Tackle`/`Block` sobre el rival más cercano aunque el balón esté a su lado.
- **Condición:** una, de identidad: sin balón.
- **Canal:** C1 + C2, con rama por debajo del cien (**PD-1**). · **Puesto:** centrocampista o defensa (`positionOnly`).
- **Coste:** su equipo juega con un hombre menos en la disputa del balón. Es carísimo y visible.
- **Conflicto:** `C-06 Recuperador` — «no puedes ir al balón y al hombre: gastan el mismo tick».
- **Motor:** extensión pequeña (C1 + C2) + **decisión del revisor pendiente** (PD-1).
- **Evidencia:** el balón le pasa al lado y él sigue a por el rival; `ChaseBall` casi ausente y `Tackle` disparado.

**C-09 · Cobarde con ojo**
- **Fantasía:** no entra nunca, y por eso siempre está de pie y bien colocado.
- **Conducta:** **rehúsa** `Tackle` y `Block`; elige `CoverSpace` y llega a interceptar.
- **Condición:** una, de identidad: siempre.
- **Canal:** C1 + C2, con rama por debajo del cien (**PD-1**). · **Puesto:** defensa o centrocampista.
- **Coste:** el equipo pierde un robador; con él en el campo, alguien tiene que hacer las faltas. A cambio no se lesiona y no ve amarillas.
- **Conflicto:** `C-13 Kamikaze` — «no puedes no saltar nunca y saltar siempre».
- **Motor:** extensión pequeña (C1 + C2) + PD-1.
- **Evidencia:** el rival le encara y no salta; cero faltas y cero entradas suyas en el informe, con el equipo aguantando igual.

**C-10 · Ancla**
- **Fantasía:** en su tercio no pasa nadie; fuera de él no sirve para nada.
- **Conducta:** en su propio tercio elige `Tackle`/`Block`; fuera **rehúsa** `Tackle` y elige `Retreat`, y vuelve corriendo.
- **Condición:** una, geométrica y previsible en la pantalla de Equipo (dónde arranca).
- **Canal:** C1 + C2, con rama por debajo del cien (**PD-1**). · **Puesto:** defensa (`positionOnly`).
- **Coste:** su equipo no roba en campo rival; la presión alta con él dentro se cae por un lado.
- **Conflicto:** `C-12 Presión tras pérdida` — «no puedes replegar y presionar arriba: gastan la posición vertical del equipo».
- **Motor:** extensión pequeña (C1 + C2) + PD-1. *(Fusiona cinco perks actuales que dicen «×N entrada»: `bulwark_stance`, `own_third_anchor`, `pit_veteran`, `game_management`, `last_ditch`.)*
- **Evidencia:** pierde un balón en el medio y arranca de vuelta como si tiraran de una cuerda; el histograma de `Retreat` fuera de su tercio es inconfundible.

**C-11 · No vuelve**
- **Fantasía:** el delantero que no defiende. Nunca.
- **Conducta:** **rehúsa** `ChaseBall` y `PressCarrier` fuera del tercio rival; dentro elige `FindSpace`.
- **Condición:** una, geométrica.
- **Canal:** C1 + C2, rama por debajo del cien (**PD-1**). · **Puesto:** delantero (`positionOnly`).
- **Coste:** el equipo defiende con seis. Es a la vez la ventaja y el precio, y se ve en la misma imagen.
- **Conflicto:** `C-12 Presión tras pérdida` — «no puede saltar con todos el que no vuelve nunca».
- **Motor:** extensión pequeña (C1 + C2) + PD-1.
- **Evidencia:** el rival saca de banda en su campo y hay uno nuestro parado en su área, solo.

**C-12 · Presión tras pérdida**
- **Fantasía:** perder el balón arriba es la señal para saltar todos.
- **Conducta:** al perder la posesión en el tercio rival, **todo el equipo** prefiere `PressCarrier` sobre `Retreat` durante unos segundos.
- **Condición:** una, de estado de partido legible (transición defensiva en campo rival).
- **Canal:** C1 + C2 con alcance de equipo. · **Puesto:** cualquiera; es de equipo.
- **Coste:** si la presión se salta, el equipo está partido y el contragolpe llega solo.
- **Conflicto:** `C-10 Ancla` y `C-14 Bloque bajo` — «no puedes subir el equipo y bajarlo: es la misma línea».
- **Motor:** extensión pequeña (C1 + C2). *(Rediseño de `high_press_trigger`, hoy opaco y con valor 0.)*
- **Evidencia:** se pierde en la frontal rival y cinco saltan a la vez en lugar de replegar; se ve en un segundo.

**C-13 · Kamikaze**
- **Fantasía:** entra a todo aunque se rompa él.
- **Conducta:** elige `Tackle` y `Block` mucho más a menudo, y **su propia** probabilidad de lesionarse sube mientras lo hace.
- **Condición:** ninguna: es identidad pura.
- **Canal:** C1 + C4 (`InjuryChanceBonus`, escalar que ya existe por jugador). · **Puesto:** cualquiera.
- **Coste:** **carne propia**, que es la moneda del juego. Es el único candidato que paga en la moneda correcta sin ninguna condición.
- **Conflicto:** `C-09 Cobarde con ojo` — «uno paga con su cuerpo lo que el otro se ahorra no saltando».
- **Motor:** extensión pequeña (C1 + C4).
- **Evidencia:** entradas que ganan el balón y lo dejan a él en el suelo; `ownInjuries` de ese jugador por encima de todo el equipo.

**C-14 · Bloque bajo**
- **Fantasía:** el equipo entero renuncia al campo contrario.
- **Conducta:** acorta la zona de acción de **todo el equipo** y sube `Retreat`, así que la línea vive más cerca de su portería.
- **Condición:** una, decidida al alinear.
- **Canal:** `modifyLeash` de alcance equipo con valor **negativo** (el canal existe; **MEDIDO: los tres perks que lo usan lo suben todos, nadie lo baja nunca**) + C1. · **Puesto:** cualquiera; es de equipo.
- **Coste:** el equipo regala el campo y juega a no perder; sin salida, el balón vuelve enseguida.
- **Conflicto:** `C-12 Presión tras pérdida` — «no puedes tener el bloque abajo y arriba: es la misma línea, y el motor la disputa como precondición dura».
- **Motor:** **existente** en el canal, **decisión del revisor pendiente** en el signo (PD-1: una correa más corta es una penalización que expresa una identidad).
- **Evidencia:** `ballThirdMaxShare` del rival arriba y el equipo entero por debajo del medio campo en el mapa de calor; distancia L1 máxima contra `C-12`.

## Tanda 2 (C-15 … C-30)

**C-15 · Línea adelantada**
- **Fantasía:** su defensa juega en el medio campo y deja la espalda a la vista.
- **Conducta:** desplaza el hogar de la línea de atrás hacia adelante, así que los defensas se colocan más arriba y llegan antes a la disputa.
- **Condición:** una, decidida al alinear (llevarlo en un defensa).
- **Canal:** C8 (desplazamiento de hogar; el motor **ya mueve** `EffectiveHome` cada tick con el desplazamiento de bloque). · **Puesto:** defensa.
- **Coste:** un pase en profundidad y no hay nadie detrás. Es el riesgo anunciado más limpio del catálogo.
- **Conflicto:** `C-14 Bloque bajo` — «no puedes adelantar la línea y meterla atrás».
- **Motor:** extensión pequeña (C8).
- **Evidencia:** los goles encajados cambian de forma: menos remates desde la frontal, más balones a la espalda.

**C-16 · Director**
- **Fantasía:** el balón pasa por él, siempre.
- **Conducta:** elige `OfferSupport` cuando su equipo tiene el balón y `ShortPass` cuando lo recibe: está siempre ofrecido.
- **Condición:** una, de estado (con balón).
- **Canal:** C1 + C2. · **Puesto:** centrocampista. **Primer perk propio del centrocampista** *(MEDIDO: hoy tiene cero)*.
- **Coste:** no ataca, no remata y no roba; ocupa un slot en el hombre que más podría hacer otras cosas.
- **Conflicto:** `C-18 Conductor` — «el que la suelta siempre y el que se la lleva siempre no caben en el mismo cuerpo».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** cada salida de balón pasa por el mismo verde; su recuento de pases dobla al del resto.

**C-17 · Primer toque**
- **Fantasía:** el balón no se le para en los pies.
- **Conducta:** en el primer tick tras recibir, elige `ShortPass` en vez de `Dribble`.
- **Condición:** una, de situación («acaba de recibir»), que el motor ya conoce porque cambia de estado.
- **Canal:** C1 + C2. · **Puesto:** centrocampista.
- **Coste:** nunca aguanta el balón; con el equipo replegado, su pase sale a un compañero también presionado.
- **Conflicto:** `C-18 Conductor` — «no puedes soltarla antes de pararte y llevártela tres casillas».
- **Motor:** extensión pequeña (C1 + C2). *(Fusiona `fine_touch` y `steady_hands`.)*
- **Evidencia:** la suelta antes de pararse mientras el de al lado conduce; ticks medios con el balón en el pie, los más bajos del equipo.

**C-18 · Conductor**
- **Fantasía:** sale de la presión con el balón en el pie, no con un pase.
- **Conducta:** elige `Dribble` donde sus compañeros eligen `ShortPass`.
- **Condición:** ninguna.
- **Canal:** C1. · **Puesto:** centrocampista.
- **Coste:** cada conducción es un duelo, y un duelo perdido en su campo es una ocasión en contra.
- **Conflicto:** `C-17 Primer toque` — «la misma pelota no se puede soltar al instante y conducir».
- **Motor:** extensión pequeña (C1).
- **Evidencia:** recibe rodeado y arranca; regates intentados muy por encima del resto.

**C-19 · Pivote hondo**
- **Fantasía:** baja a buscarla entre sus centrales cuando el equipo no sabe salir.
- **Conducta:** con balón, su hogar se desplaza hacia atrás y su `FindSpace` solo acepta casillas por detrás de la línea del balón.
- **Condición:** una, de estado (con balón).
- **Canal:** C8 + C2. · **Puesto:** centrocampista.
- **Coste:** el equipo ataca con un hombre menos por delante del balón.
- **Conflicto:** `C-25 Llegador` — «no puedes bajar a sacarla y aparecer dentro del área».
- **Motor:** extensión pequeña (C8 + C2).
- **Evidencia:** en la salida de balón hay tres hombres donde había dos; su posición media, la más retrasada del centro del campo.

**C-20 · Rompe-líneas**
- **Fantasía:** no juega en horizontal: busca el pase que parte la defensa.
- **Conducta:** elige `ThroughPass` donde los demás eligen `ShortPass`.
- **Condición:** una: hay un compañero en carrera por delante.
- **Canal:** C1 + C2. · **Puesto:** centrocampista.
- **Coste:** el pase en profundidad falla más y lo pierde arriba, con el equipo subido.
- **Conflicto:** `C-16 Director` — «el que la pone entre líneas no es el que la toca y la ofrece».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** en vez de la pared lateral, el balón sale a la espalda de los centrales; recuento de `ThroughPass` intentados sin comparación en su puesto.

**C-21 · Cambio de orientación**
- **Fantasía:** cuando su lado se atasca, manda el balón al otro.
- **Conducta:** elige `LongPass` cuando el pasillo corto está poblado, en vez de insistir en corto.
- **Condición:** una, de situación que el motor ya calcula (`passBlockedLanePenalty`).
- **Canal:** C1 + C2. · **Puesto:** centrocampista o defensa.
- **Coste:** el pase largo se pierde más y la posesión se rompe.
- **Conflicto:** `C-17 Primer toque` — «bajo presión, o la tocas corta o la cruzas; el tick es uno».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** bajo presión la cruza al otro lado; `ballThirdMaxShare` menos concentrado por su banda.

**C-22 · Central constructor**
- **Fantasía:** un defensa que saca el balón jugado en vez de quitárselo de encima.
- **Conducta:** desde su propio tercio elige `LongPass`/`ThroughPass` en vez de `ShortPass` al de al lado.
- **Condición:** una, geométrica y previsible al alinear.
- **Canal:** C1 + C2. · **Puesto:** defensa.
- **Coste:** una pérdida suya es una ocasión inmediata: está jugando en su área.
- **Conflicto:** `C-23 Despejador` — «desde la frontal, o sales jugando o la echas fuera».
- **Motor:** extensión pequeña (C1 + C2). **Llena el hueco medido: «el defensa que saca el balón jugado no existe».**
- **Evidencia:** el central levanta la cabeza y busca al delantero; su cadena de pases arranca jugadas en vez de cortarlas.

**C-23 · Despejador**
- **Fantasía:** en su área no se juega: se despeja.
- **Conducta:** en su propio tercio y con un rival encima, elige `LongPass` en vez de `ShortPass` o `Dribble`.
- **Condición:** una, geométrica (la presión la añade el motor, no el dato).
- **Canal:** C1 + C2. · **Puesto:** defensa.
- **Coste:** regala la posesión cada vez; el equipo no descansa nunca con el balón.
- **Conflicto:** `C-22 Central constructor` — «la misma pelota no se saca jugada y se echa fuera».
- **Motor:** extensión pequeña (C1 + C2). *(El despeje no necesita acción nueva: es el pase largo bajo presión.)*
- **Evidencia:** nadie intenta salir jugando desde la frontal; posesión propia baja y balón en disputa alto.

**C-24 · Extremo vertical**
- **Fantasía:** solo sabe ir hacia adelante.
- **Conducta:** con campo por delante elige `Dribble` en vez de `ShortPass`.
- **Condición:** una, de situación que el motor ya calcula (`dribbleOpenSpaceBonus`).
- **Canal:** C1 + C2. · **Puesto:** extremo.
- **Coste:** cuando el campo está cerrado no tiene plan B y la pierde en banda.
- **Conflicto:** `C-17 Primer toque` — «encarar y soltarla al primer toque son la misma decisión resuelta al revés».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** recibe en banda y encara, no la devuelve; regates en el último tercio, los más altos del equipo.

**C-25 · Llegador**
- **Fantasía:** es un centrocampista, pero aparece en el área como un delantero.
- **Conducta:** en transición ofensiva su hogar se desplaza hacia adelante y elige `FindSpace` avanzado en vez de `OfferSupport`.
- **Condición:** una, de estado (transición ofensiva).
- **Canal:** C8 + C1 + C2. · **Puesto:** centrocampista.
- **Coste:** cuando el ataque muere, está a cuarenta metros de su sitio.
- **Conflicto:** `C-19 Pivote hondo` — «el que llega al área no es el que baja a sacarla».
- **Motor:** extensión pequeña (C8 + C1 + C2).
- **Evidencia:** el equipo ataca y hay un centrocampista dentro del área; remates suyos desde dentro, que antes eran cero.

**C-26 · Cazagoles**
- **Fantasía:** dentro del área no piensa: tira.
- **Conducta:** en el tercio rival elige `Shoot` donde el resto elige `ShortPass`.
- **Condición:** una, geométrica.
- **Canal:** C1 + C2. · **Puesto:** delantero.
- **Coste:** mata jugadas que seguían vivas; con él, el equipo remata peor y más.
- **Conflicto:** `C-28 Sangre fría` — «o disparas lo primero o esperas un toque más; es el mismo balón».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** recibe de espaldas y se gira a rematar en vez de descargar; `shotsPerMatch` suyo muy por encima y precisión por debajo.

**C-27 · Hombre objetivo**
- **Fantasía:** todo el equipo juega para él.
- **Conducta:** sus compañeros lo eligen antes como receptor de `LongPass`/`ThroughPass`; él elige `ShortPass` para descargar.
- **Condición:** una, de identidad: llevarlo en el once.
- **Canal:** C6 (sesgo en el orden de receptores que `EvaluatePass` ya calcula) + C1. · **Puesto:** delantero.
- **Coste:** el ataque se vuelve previsible y depende de un cuerpo; si cae, el equipo se queda sin salida.
- **Conflicto:** `C-03 Hombre libre` — «no puedes ser al que nadie mira y al que todos buscan».
- **Motor:** extensión pequeña (C6 + C1).
- **Evidencia:** la pelota va del mismo al mismo, jugada tras jugada; su recuento de recepciones, el doble del segundo.

**C-28 · Sangre fría**
- **Fantasía:** con el portero delante no se precipita.
- **Conducta:** con el tiro tapado elige `Dribble` una casilla más y entonces `Shoot`.
- **Condición:** una, de situación que el motor ya calcula (`shootBlockedLanePenalty`).
- **Canal:** C1 + C2. · **Puesto:** delantero.
- **Coste:** un tick más con el balón es un tick más para que le entren.
- **Conflicto:** `C-26 Cazagoles` — «o la estrellas en el defensa o te la llevas: el tick es el mismo».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** en vez de estrellarla en el defensa, se la lleva un paso y define; tiros bloqueados suyos muy por debajo.

**C-29 · Cazador de rechaces**
- **Fantasía:** vive del balón que escupe el portero.
- **Conducta:** con balón suelto en el tercio rival elige `ChaseBall` en vez de `FindSpace`.
- **Condición:** una, de situación que el motor ya distingue (`chaseBallLooseBonus`).
- **Canal:** C1 + C2. · **Puesto:** delantero.
- **Coste:** deja de ofrecerse; el equipo pierde una opción de pase cada vez que el balón queda vivo.
- **Conflicto:** `C-38 Torre` — «el que va a por el rechace no es el que espera quieto a que le llegue».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** tras cada parada hay alguien ya en el rechace; goles suyos a menos de dos casillas.

**C-30 · Amenaza de larga distancia**
- **Fantasía:** si ve portería, dispara; la distancia le da igual.
- **Conducta:** el tiro **entra en su comparación** desde mucho más lejos, y lo elige sobre el pase largo.
- **Condición:** una, geométrica.
- **Canal:** C4 (`ShootRangeBonusCells`, escalar que ya escriben los rasgos; **es geometría, no cuota**: decide si el tiro se considera siquiera) + C1. · **Puesto:** centrocampista.
- **Coste:** regala posesión desde lejos; el rechace cae en campo propio.
- **Conflicto:** `C-16 Director` — «el que arma la pierna desde fuera no es el que la toca y la ofrece».
- **Motor:** extensión pequeña (C4 + C1).
- **Evidencia:** arma la pierna desde fuera del área mientras los demás buscan el pase; histograma de distancia de tiro con una segunda joroba.

## Tanda 3 (C-31 … C-46)

**C-31 · Socio de ataque**
- **Fantasía:** dos que se buscan a ciegas.
- **Conducta:** cada uno elige al otro como receptor antes que a nadie.
- **Condición:** una, decidida al alinear (`linked`, estático).
- **Canal:** C6. · **Puesto:** delantero + centrocampista.
- **Coste:** el ataque se vuelve legible y se apaga entero si uno de los dos cae.
- **Conflicto:** `C-27 Hombre objetivo` — «no puede haber dos destinos preferentes del mismo pase».
- **Motor:** extensión pequeña (C6).
- **Evidencia:** la pelota va del mismo al mismo; matriz de pases con una celda encendida.

**C-32 · Especialista a balón parado**
- **Fantasía:** sus faltas acaban dentro.
- **Conducta:** es **él** quien reanuda cuando hay saque de falta en campo rival, aunque no sea el más cercano.
- **Condición:** una: la falta señalada, que ya existe como reanudación (AZ tanda 2).
- **Canal:** selección del lanzador en la reanudación (extensión pequeña) + el canal de tiro que ya existe. · **Puesto:** cualquiera.
- **Coste:** ocupa un slot en algo que pasa pocas veces por partido; es el candidato con menos frecuencia del catálogo.
- **Conflicto:** ninguno limpio; su precio es la rareza del suceso.
- **Motor:** extensión pequeña (gancho de lanzador en la reanudación).
- **Evidencia:** el mismo jugador se acerca al balón en todas las faltas; goles de falta, que hoy serían anecdóticos.

**C-33 · Saque largo**
- **Fantasía:** no saca en corto: la manda arriba.
- **Conducta:** al reanudar desde su área elige `LongPass` en vez de `ShortPass`.
- **Condición:** una, de situación (reanudación propia).
- **Canal:** C1 + C2. · **Puesto:** portero.
- **Coste:** cede el balón la mitad de las veces; el equipo no sale jugando nunca.
- **Conflicto:** `C-22 Central constructor` — «el central pide la pelota al pie y el portero la manda por encima de su cabeza».
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** cada saque de puerta cruza el medio campo; cadena de pases del equipo más corta y más alta.

**C-34 · Manos de cepo** *(única excepción admitida de oficio; ver «Corrección 1»)*
- **Fantasía:** lo que le llega, se queda.
- **Conducta:** la parada **atrapa** en vez de escupir, así que la segunda jugada no llega a existir.
- **Condición:** ninguna.
- **Canal:** rama de resultado de la parada (no la cuota de `save`): extensión pequeña. · **Puesto:** portero.
- **Coste:** ninguno conductual; se paga con rareza. Es el candidato más flojo de la lista y entra solo porque **la ausencia del rechace es un suceso visible**.
- **Conflicto:** `C-29 Cazador de rechaces` del rival — «uno vive del rechace que el otro no concede».
- **Motor:** extensión pequeña.
- **Evidencia:** tras sus paradas no hay segunda jugada; rechaces concedidos, cero.

**C-35 · Portero líbero**
- **Fantasía:** un portero que juega de último defensa.
- **Conducta:** con una zona de acción mucho mayor, elige `ChaseBall` fuera del área en vez de `Retreat` a su línea.
- **Condición:** una, geométrica.
- **Canal:** `modifyLeash` (**existe**) + C1. · **Puesto:** portero.
- **Coste:** la portería queda vacía, y el precio se cobra en el mismo partido.
- **Conflicto:** `C-36 Guardameta clásico` — «la misma correa no puede ser larga y corta».
- **Motor:** **existente** en la correa, extensión pequeña en la intención. *(Ampliación de `sweeper_keeper`, hoy el único perk verdaderamente conductual del juego y dura una jugada.)*
- **Evidencia:** sale a la frontal a despejar un pase en profundidad; dos minutos después el mismo pase entra por el otro lado y la portería está vacía.

**C-36 · Guardameta clásico**
- **Fantasía:** de su línea no se mueve.
- **Conducta:** su zona se **estrecha** y elige `CoverSpace` siempre, así que `ChaseBall` fuera del área queda descartado por la precondición dura.
- **Condición:** ninguna.
- **Canal:** `modifyLeash` **negativo** (**PD-1**) + C1. · **Puesto:** portero.
- **Coste:** los balones a la espalda de la defensa no los sale a cortar nadie.
- **Conflicto:** `C-35 Portero líbero` — «es la misma cuerda tirada en dos direcciones». **Es la anti-sinergia más limpia y legible del catálogo y no necesita maestro, solo una exclusión por jugador.**
- **Motor:** **existente** + PD-1.
- **Evidencia:** nunca sale y nunca lo pillan fuera; distancia media a su línea, la menor del catálogo.

**C-37 · Red de seguridad**
- **Fantasía:** se coloca según dónde esté su defensa, no según dónde esté el balón.
- **Conducta:** su `CoverSpace` toma como referencia al defensa más retrasado, y sube y baja con él.
- **Condición:** una, de identidad.
- **Canal:** C8 con referencia a un tercero. · **Puesto:** portero.
- **Coste:** si la defensa se equivoca de altura, el portero se equivoca con ella.
- **Conflicto:** `C-36 Guardameta clásico` — «o te colocas por tu línea o por tu defensa».
- **Motor:** extensión pequeña (C8). *(Rediseño de `safety_net`, hoy con condición de proximidad en el tick: opaca e inútil.)*
- **Evidencia:** la defensa sube y el portero sube con ella; correlación de su posición con la de la línea, no con la del balón.

**C-38 · Torre**
- **Fantasía:** no se mueve a buscar el balón; el balón viene a él.
- **Conducta:** **rehúsa** `FindSpace` con balón y elige `OfferSupport` clavado donde está.
- **Condición:** una, de estado (con balón).
- **Canal:** C1 + C2 con rama por debajo del cien (**PD-1**) + C6. · **Puesto:** delantero (`positionOnly`).
- **Coste:** el ataque pierde movilidad; si lo marcan bien, el equipo ataca con seis.
- **Conflicto:** `C-39 Desmarque profundo` — «no puedes esperar quieto y estar corriendo a la espalda».
- **Motor:** extensión pequeña (C1 + C2 + C6) + PD-1. *(Casilla dominada por excelencia: `FindSpace` lidera su estado con mucha diferencia, así que subir `OfferSupport` no bastaría.)*
- **Evidencia:** mientras todos se mueven, uno se queda quieto en el punto de penalti esperando.

**C-39 · Desmarque profundo**
- **Fantasía:** vive al borde del fuera de juego que no existe.
- **Conducta:** su `FindSpace` acepta casillas por detrás de la última línea rival, que hoy el motor le prohíbe.
- **Condición:** una, de estado (transición ofensiva).
- **Canal:** C8, abriendo un techo que ya existe (`findSpaceLineMarginCells`). · **Puesto:** delantero.
- **Coste:** juega solo y de espaldas al juego; si el pase no llega, no participa.
- **Conflicto:** `C-38 Torre` — «el que espera y el que corre a la espalda son dos ataques distintos».
- **Motor:** extensión pequeña (C8). **La capacidad existe hoy como límite: el perk la abre.**
- **Evidencia:** cuando el equipo recupera, ya está corriendo a la espalda del central; goles suyos tras `ThroughPass`.

**C-40 · Pegado a banda**
- **Fantasía:** no se despega de la cal.
- **Conducta:** su zona se estrecha en columna y su `FindSpace` solo encuentra hueco pegado al lateral.
- **Condición:** una, decidida al alinear (en qué columna arranca).
- **Canal:** C8. · **Puesto:** extremo o carrilero.
- **Coste:** nunca aparece dentro; el equipo ataca con un hombre menos en el área.
- **Conflicto:** `C-41 Interior` — «la misma pierna no puede vivir en la cal y en el pasillo de dentro».
- **Motor:** extensión pequeña (C8). *(Absorbe `flank_specialist` y la mitad de `wing_overlap`.)*
- **Evidencia:** el campo se abre porque hay alguien que nunca entra al centro; dispersión lateral suya, la menor del equipo.

**C-41 · Interior**
- **Fantasía:** juega entre líneas, ni en la banda ni en el centro.
- **Conducta:** su hogar se desplaza al semiespacio y su `FindSpace` solo acepta ese corredor.
- **Condición:** una, decidida al alinear.
- **Canal:** C8. · **Puesto:** centrocampista.
- **Coste:** no da amplitud ni ocupa el centro: si el rival cierra ese corredor, desaparece.
- **Conflicto:** `C-40 Pegado a banda` — «o abres el campo o lo estrechas».
- **Motor:** extensión pequeña (C8).
- **Evidencia:** siempre aparece en el mismo hueco, entre el lateral y el central rivales.

**C-42 · Sombra**
- **Fantasía:** juega a la sombra del que va delante.
- **Conducta:** su `FindSpace` solo acepta casillas por detrás de su vinculado, así que nunca están a la misma altura.
- **Condición:** una, decidida al alinear (`linked`).
- **Canal:** C8 con referencia a un tercero. · **Puesto:** centrocampista.
- **Coste:** su altura la decide otro; si el vinculado se queda atrás, él también.
- **Conflicto:** `C-25 Llegador` — «no puedes llegar al área y quedarte siempre por detrás de otro».
- **Motor:** extensión pequeña (C8). *(Rediseño de `covering_shadow`.)*
- **Evidencia:** nunca están los dos a la misma altura; diferencia de posición vertical entre ambos, siempre del mismo signo.

**C-43 · Relevo**
- **Fantasía:** cuando uno sube, el otro cubre. Nunca los dos a la vez.
- **Conducta:** si su vinculado está por delante del medio campo, él elige `CoverSpace`; y al revés.
- **Condición:** una, decidida al alinear; lo que la enciende es la **posición** del otro, no su decisión (a diferencia de `diagonal_press`, que depende de lo que el otro elija en el mismo tick).
- **Canal:** C1 + C2 con lectura de un tercero. · **Puesto:** pareja de lateral o de centro.
- **Coste:** el equipo nunca ataca con los dos; renuncia a una superioridad que otra pareja sí tendría.
- **Conflicto:** `C-12 Presión tras pérdida` — «no puedes saltar todos si uno tiene orden de quedarse».
- **Motor:** extensión pequeña (C1 + C2). *(Es lo que `wing_overlap` y «Último hombre» querían ser.)*
- **Evidencia:** suben y bajan alternándose; correlación negativa entre sus posiciones verticales.

**C-44 · Raíces** *(racial, conservar sin cambios)*
- **Fantasía:** no lo mueven.
- **Conducta:** las cargas que derribarían a otro no lo mueven de su casilla (`Immovable`).
- **Condición:** ninguna.
- **Canal:** `immunity` (**existe**). · **Puesto:** cualquiera.
- **Coste:** ninguno; es una habilidad racial y su precio está en elegir el club.
- **Conflicto:** ninguno.
- **Motor:** **existente**.
- **Evidencia:** lo cargan y no se va.

**C-45 · Correa curtida** *(rediseño de `long_leash_legacy`)*
- **Fantasía:** cada partido que termina de pie le ensancha el sitio.
- **Conducta:** su zona de acción crece un poco por cada partido que **él** termina sin lesionarse, así que empieza a elegir acciones que antes le quedaban fuera.
- **Condición:** una, de carne acumulada por **este** jugador (hoy el contador sube por partido del equipo, y no se ve).
- **Canal:** `modifyLeash` + `addCounter` (**ambos existen**) + C11. · **Puesto:** cualquiera.
- **Coste:** tarda media run en notarse: es un perk que se compra pronto o no vale nada.
- **Conflicto:** `C-10 Ancla` — «una correa que crece y un puesto que no se abandona se estorban».
- **Motor:** **existente** salvo el contador de carne (C11).
- **Evidencia:** **el contador ya es visible sin trabajo de interfaz**: la pantalla de Equipo dibuja la zona de acción, así que el jugador *ve* crecer el efecto entre partidos. *(DERIVADO, y corrige a la auditoría: un contador es visible si la pantalla dibuja su consecuencia, no solo si dibuja el número.)*

**C-46 · Cazador de porteros**
- **Fantasía:** no deja sacar al portero rival.
- **Conducta:** elige `PressCarrier` contra el portero rival en su salida —un objetivo que el motor **ya contempla**— en vez de replegar con el bloque.
- **Condición:** una, de situación (el rival reanuda desde su área).
- **Canal:** C1 + C2. · **Puesto:** delantero.
- **Coste:** cuarenta metros de carrera cada saque de puerta, y el bloque sin su primera línea.
- **Conflicto:** `C-11 No vuelve` — «el que presiona al portero rival es exactamente el que no vuelve, y por eso no puede hacer las dos cosas mal a la vez». *(Aviso: puede ser sinergia, no conflicto; ver Fase C.)*
- **Motor:** extensión pequeña (C1 + C2).
- **Evidencia:** el portero rival la manda arriba en vez de sacarla jugada; saques de puerta cortos del rival, casi cero.

## Tanda 4 (C-47 … C-61)

**C-47 · Los cuatro letales** *(`iron_studs`, `marrow_thirst`, `second_wound`, `skullsplitter`; conservar sin cambios)*
- **Fantasía:** mata.
- **Conducta:** su entrada puede terminar con un rival que no se levanta nunca más.
- **Condición:** una cada uno, y las cuatro distintas (material de la bota, sed, herida abierta, remate).
- **Canal:** `lethal` (**existe**). · **Puesto:** cualquiera.
- **Coste:** el árbitro, la expulsión y la espiral; y desde la ADR 0048, la muerte es consecuencia de **una entrada**, nunca del saque.
- **Conflicto:** `C-58 Curtido` y todo `C-56..C-61` en el nivel de familia, no de perk — ver Fase C.
- **Motor:** **existente**. **Son la única regla que el motor rompe de verdad y la identidad declarada del juego.**
- **Evidencia:** alguien no se levanta. No hace falta más.
- *(Cuatro ficheros, una ficha: comparten canal y conducta y solo se distinguen por la condición. Escribirlos como cuatro candidatos sería inflar la cuenta, que es justo lo que este documento denuncia.)*

**C-48 · Sed de sangre** *(`blood_tithe`, conservar; candidato a maestro)*
- **Fantasía:** un equipo de brutos hace más daño del que debería.
- **Conducta:** todo el equipo lesiona más, y el rival cae con lesiones más graves.
- **Condición:** una, previsible al alinear (cuántos brutos hay en el once).
- **Canal:** `modifyProbability` de alcance equipo (**existe**). · **Puesto:** cualquiera.
- **Coste:** los brutos son lentos y torpes por `styles.json`, así que la plantilla que lo enciende juega peor al fútbol. **Es el único perk actual cuyo coste está fuera del perk y es real.**
- **Conflicto:** ver Fase C, par «la carne».
- **Motor:** **existente**.
- **Evidencia:** `injuriesPerMatch` y `deathsPerRun` del equipo que lo lleva.

**C-49 · Olfato de sangre**
- **Fantasía:** va a por el que ya está tocado.
- **Conducta:** elige `Tackle` contra el rival que llegó lesionado o con amarilla, no contra el que tiene el balón.
- **Condición:** una, **previsible en el ojeo** (el estado físico y las tarjetas del rival son legibles).
- **Canal:** C7. · **Puesto:** defensa o centrocampista.
- **Coste:** deja de entrar al portador: el balón sigue, el rival no.
- **Conflicto:** `C-01 Perro de presa` — «o vas al mejor o vas al más roto: solo tienes un hombre al que perseguir».
- **Motor:** extensión pequeña (C7). *(Rediseño de `shadow_marker`, cuya condición de proximidad en el tick es invisible.)*
- **Evidencia:** un rival sale renqueando y el mismo jugador va derecho a él en la jugada siguiente. **Es «carnicería administrada» hecha conducta.**

**C-50 · Sed acumulada**
- **Fantasía:** cada rival que deja en el suelo lo vuelve más peligroso.
- **Conducta:** por cada lesión **que él ha causado** en la run, su entrada hace más daño.
- **Condición:** una, de carne acumulada por él, con el contador en el retrato.
- **Canal:** `addCounter` + `injure` (**existen**) + C11. · **Puesto:** cualquiera.
- **Coste:** el criterio del árbitro se le gasta en la misma proporción; cuanto más sube el contador, antes lo expulsan.
- **Conflicto:** `C-62 Cara de inocente` — «no puedes acumular fama y pasar desapercibido» *(y por eso son buena pareja de tensión, no de exclusión)*.
- **Motor:** **existente** salvo el contador visible (C11).
- **Evidencia:** el retrato lleva la cuenta y se ve subir; la curva de `injuriesCaused` de ese jugador a lo largo de la run.

**C-51 · Sangre caliente** *(`hot_blooded`, conservar)*
- **Fantasía:** al que tira, lo deja en el suelo más tiempo.
- **Conducta:** sus derribos duran más, así que el rival tarda en volver a la jugada.
- **Condición:** ninguna.
- **Canal:** `modifyKnockdownTicks` (**existe**). · **Puesto:** cualquiera.
- **Coste:** ninguno propio; se paga en rareza.
- **Conflicto:** ninguno.
- **Motor:** **existente**.
- **Evidencia:** los rivales que él tumba tardan visiblemente más en levantarse.

**C-52 · Se levanta solo**
- **Fantasía:** lo tiran y ya está de pie otra vez.
- **Conducta:** sus propios derribos duran menos: vuelve a la jugada mientras los demás siguen en el suelo.
- **Condición:** ninguna.
- **Canal:** `modifyKnockdownTicks` sobre sí mismo (**existe**, hoy solo se usa contra el rival). · **Puesto:** cualquiera.
- **Coste:** ninguno propio; se paga en rareza. Es el espejo exacto de `C-51` y, por eso mismo, **está al límite del criterio [E4]**: mismo canal, misma magnitud, distinto sujeto. Se conserva porque el sujeto cambia quién se ve de pie en pantalla.
- **Conflicto:** ninguno.
- **Motor:** **existente**.
- **Evidencia:** en la misma jugada, todos en el suelo menos uno.

**C-53 · Rabia**
- **Fantasía:** si le hacen una falta, se la devuelve.
- **Conducta:** tras recibir falta, elige `Tackle` y su objetivo es **quien se la hizo**.
- **Condición:** una, de suceso del partido que el jugador reconoce al instante.
- **Canal:** C7 + C1. · **Puesto:** cualquiera.
- **Coste:** la devuelve donde no toca, y la amarilla se la lleva él.
- **Conflicto:** `C-09 Cobarde con ojo` — «el que no salta nunca no puede devolverla».
- **Motor:** extensión pequeña (C7 + C1).
- **Evidencia:** lo derriban y, dos jugadas después, va a por el mismo. **Es la conversión de «estado» en «conducta» más limpia de la lista del revisor.**

**C-54 · Pagar el hierro**
- **Fantasía:** cada rival que rompe se lo cobra su propio cuerpo.
- **Conducta:** cuando una entrada suya lesiona a alguien, **él** juega el resto del partido con más riesgo de romperse.
- **Condición:** una, de suceso propio.
- **Canal:** C4 (`InjuryChanceBonus`, escalar que ya existe por jugador). · **Puesto:** cualquiera.
- **Coste:** carne propia, y es el candidato que **crea la moneda que hoy no existe**: *MEDIDO/DERIVADO en `decision-lineas-de-perks.md` §6, hoy la carnicería solo gasta carne del rival*.
- **Conflicto:** `C-56 Superviviente` — «no puedes cobrarte carne y conservar la tuya: las dos gastan la plantilla».
- **Motor:** extensión pequeña (C4).
- **Evidencia:** el que más lesiona es el que acaba en la clínica; correlación dentro de la run entre `injuriesCaused` y `ownInjuries` de ese jugador.

**C-55 · Puerta de hierro** *(`iron_gate`, conservar sin cambios)*
- **Fantasía:** una lesión por partido, sencillamente, no le ocurre.
- **Conducta:** se levanta de una lesión que habría sido baja.
- **Condición:** ninguna.
- **Canal:** `cancelEvent` (**existe**). · **Puesto:** cualquiera.
- **Coste:** un slot raro.
- **Conflicto:** ninguno.
- **Motor:** **existente**. **Pasa los diez puntos del Perk Design Test y es el patrón a imitar.**
- **Evidencia:** cae, y se levanta.

**C-56 · Superviviente** **[carne]**
- **Fantasía:** cuantos más compañeros pierde, más difícil es matarlo a él.
- **Conducta:** por cada compañero muerto en la run, aguanta mejor la lesión grave.
- **Condición:** una, de carne de la run, con el contador en el retrato.
- **Canal:** `addCounter` + `severeInjury` (**existen**) + C11. · **Puesto:** cualquiera.
- **Coste:** su escalada la pagan otros cuerpos: **el contador solo sube si la run va mal**, y eso lo hace un seguro, no una apuesta.
- **Conflicto:** `C-54 Pagar el hierro` — ver Fase C.
- **Motor:** **existente** salvo C11.
- **Evidencia:** sobrevive a una entrada que mató a otro; es el único superviviente del once de la primera acta.

**C-57 · Veterano de cicatrices** **[carne]**
- **Fantasía:** cada lesión superada le quita un poco de miedo.
- **Conducta:** por cada lesión **propia** superada, elige `Tackle` más y `Retreat` menos.
- **Condición:** una, de carne propia, con el contador en el retrato.
- **Canal:** C1 + C11 (hoy: `+3` de fuerza por partido **del equipo**, invisible y sobre el atributo que menos mueve la decisión). · **Puesto:** cualquiera.
- **Coste:** el que se ha roto dos veces es el que más se expone a la tercera. Es una espiral y **está anunciada**.
- **Conflicto:** `C-09 Cobarde con ojo` — «el que ha vuelto de dos lesiones no es el que no salta».
- **Motor:** extensión pequeña (C1 + C11).
- **Evidencia:** el central que volvió de dos lesiones entra a todo, y ya no repliega como antes. **La escalada cuenta una historia que el jugador ha vivido.**

**C-58 · Curtido** **[carne]**
- **Fantasía:** se cura solo lo que a los demás les cuesta dinero.
- **Conducta:** vuelve sano del partido siguiente sin pasar por la clínica.
- **Condición:** una: haber terminado con lesión leve.
- **Canal:** estado de run (no toca el partido). · **Puesto:** cualquiera.
- **Coste:** ninguno en el campo; lo que compra es **oro y tiempo**, que son los recursos escasos de la run. Y ocupa un slot irreversible en un jugador que puede morir igual.
- **Conflicto:** `C-59 A media pierna` — «o esperas a estar sano o juegas roto; no puedes hacer las dos cosas con el mismo cuerpo».
- **Motor:** **existente** (el estado físico y la clínica ya están).
- **Evidencia (en un cuerpo):** la factura de clínica que no se paga. **MEDIDO como métrica: el gasto de clínica por run ya se registra.**

**C-59 · A media pierna** **[carne]**
- **Fantasía:** juega roto y casi no se le nota.
- **Conducta:** alineado con lesión leve, no arrastra la penalización que arrastraría otro.
- **Condición:** una, y es **una decisión del jugador**: alinearlo sabiendo que está tocado.
- **Canal:** estado de run + atributos efectivos. · **Puesto:** cualquiera.
- **Coste:** el riesgo de que la leve se vuelva grave sigue intacto. **Es la apuesta del juego en una frase.**
- **Conflicto:** `C-58 Curtido` — «uno ahorra el cuerpo, el otro lo gasta antes de tiempo».
- **Motor:** **existente**.
- **Evidencia (en un cuerpo):** juega el partido que no debía jugar, y a veces sale entero.

**C-60 · Hierro en la pierna** **[carne]**
- **Fantasía:** lo que le pusieron en lugar del hueso no se rompe.
- **Conducta:** con una prótesis puesta, las entradas contra él pierden fuerza y los derribos no le duran.
- **Condición:** una, previsible al alinear: llevar prótesis (`RunProsthesis` existe en el estado de la run).
- **Canal:** `immunity`/`modifyKnockdownTicks` condicionado (**existen**). · **Puesto:** cualquiera.
- **Coste:** solo vale en un cuerpo ya mutilado: es un perk que **premia haber perdido algo**, y hasta entonces está apagado.
- **Conflicto:** `C-58 Curtido` — «no se cura solo el que ya no tiene qué curar».
- **Motor:** **existente** (falta la condición sobre prótesis en el lenguaje de condiciones: extensión mínima).
- **Evidencia (en un cuerpo):** el manco es el último que queda de pie.

**C-61 · Carroñero** **[carne]**
- **Fantasía:** se queda con las botas del muerto.
- **Conducta:** cuando un compañero muere, hereda **una** de sus piezas de equipo sin pasar por el inventario.
- **Condición:** una: la muerte de un compañero, que ya devuelve el equipo al inventario (ADR 0048).
- **Canal:** estado de run. · **Puesto:** cualquiera.
- **Coste:** repugnante y gratuito a la vez: **es el candidato con peor coste de la lista**, porque su condición es una desgracia que el jugador no provoca. Entra a discusión, no a catálogo.
- **Conflicto:** `C-56 Superviviente` — «los dos cobran por el mismo muerto».
- **Motor:** **sistema nuevo pequeño** (gancho de herencia en la muerte).
- **Evidencia (en un cuerpo):** el acta dice que murió uno y el inventario no se mueve. **HIPÓTESIS: puede leerse como premio por perder, que es lo contrario de lo que el juego quiere; se incluye para que el revisor lo mate o lo apruebe, no como recomendación.**

## Tanda 5 (C-62 … C-76)

**C-62 · Cara de inocente** *(rediseño de `home_ref`; RF-064f lo pide literalmente)*
- **Fantasía:** hace la entrada y pone cara de no haber roto un plato.
- **Conducta:** sus faltas mueven el criterio del árbitro la mitad que las de cualquiera.
- **Condición:** ninguna.
- **Canal:** `modifyBias` (**existe**). · **Puesto:** el que más entra.
- **Coste:** ninguno propio; su precio es que solo vale en un equipo que hace faltas, y ese equipo ya paga en otro sitio.
- **Conflicto:** `C-50 Sed acumulada` — «la fama y el disimulo no caben en el mismo hombre».
- **Motor:** **existente**.
- **Evidencia:** **la barra de criterio es visible todo el partido (RF-062)**: hace tres faltas y la barra baja lo que bajaría con una y media. **Observabilidad garantizada por una pantalla que ya existe.**

**C-63 · Teatrero**
- **Fantasía:** se cae antes de que lo toquen y el árbitro se lo cree.
- **Conducta:** las faltas **contra él** se señalan más a menudo.
- **Condición:** ninguna.
- **Canal:** `modifyBias` con sujeto invertido (extensión mínima del canal existente). · **Puesto:** cualquiera; brilla en el que más recibe.
- **Coste:** ninguno propio. Es un perk de **ventaja limpia**, y hay que decidir si eso basta.
- **Conflicto:** `C-64 Provocador` — «no puedes ser la víctima y el que provoca al que la comete» *(el árbitro no cree las dos cosas del mismo hombre)*.
- **Motor:** extensión pequeña.
- **Evidencia:** el equipo saca faltas en sitios donde antes no las sacaba; faltas a favor por jugador, con una barra que se sale.

**C-64 · Provocador**
- **Fantasía:** a su alrededor todo el mundo acaba cometiendo una falta.
- **Conducta:** los rivales cercanos a él cometen más faltas y se llevan más amarillas.
- **Condición:** una, de proximidad… **y ahí está su problema**: la proximidad en el tick es exactamente la condición opaca que la auditoría midió como causa de los siete perks de valor cero. Solo pasa [E3] si la condición se reescribe como «el rival que lo marca a él», que es un emparejamiento **estable** y legible.
- **Canal:** C4 (`FoulChanceBonus`) aplicado al rival que lo marca. · **Puesto:** delantero.
- **Coste:** ninguno propio; lo paga el rival.
- **Conflicto:** `C-63 Teatrero`.
- **Motor:** extensión pequeña (C4 + C5 invertido).
- **Evidencia:** el que lo marca acaba amonestado; tarjetas del rival concentradas en un solo emparejamiento.

**C-65 · Instigador** *(rediseño de `mob_instigator`)*
- **Fantasía:** cuando el árbitro se va, es su momento.
- **Conducta:** en la turba, él y sus vecinos eligen `Tackle`/`Block` sobre todo lo demás.
- **Condición:** una, de suceso del partido, inconfundible.
- **Canal:** C1 + C2. · **Puesto:** cualquiera.
- **Coste:** la turba es donde se muere; el que la lidera es el que más se expone.
- **Conflicto:** `C-09 Cobarde con ojo`.
- **Motor:** extensión pequeña (C1 + C2). *(Hoy anula faltas donde ya casi no se pitan: **valor medido −7, el peor del catálogo**.)*
- **Evidencia:** empieza la turba y tres se lanzan mientras el resto sigue jugando al fútbol.

**C-66 · Manada** *(`pack_mentality`, conservar y reorientar)*
- **Fantasía:** cuantos más de los suyos hay, más se envalentonan todos.
- **Conducta:** con suficientes compañeros de su etiqueta, el equipo entero entra más.
- **Condición:** una, **previsible en la pantalla de Equipo** (`teammatesWithTag` ya lo resuelve el previsualizador).
- **Canal:** C1 (hoy: `modifyAttribute` de fuerza, el atributo que casi no entra en la decisión). · **Puesto:** cualquiera.
- **Coste:** exige una plantilla monotemática, y esa plantilla juega peor al fútbol.
- **Conflicto:** `C-70 Solitario` — «no puedes montar una manada y montar un equipo de uno».
- **Motor:** extensión pequeña (C1) para reorientarlo; **existente** si se deja como está.
- **Evidencia:** el número de brutos del once cambia la forma del partido, y el aviso de la pantalla de Equipo lo dice antes de jugar.

**C-67 · Gigante amable** *(`gentle_giant`, conservar)*
- **Fantasía:** el grande le tapa los pasillos al pequeño.
- **Conducta:** su vinculado intercepta más mientras él esté por delante.
- **Condición:** una, decidida al alinear (`linked`, con orientación).
- **Canal:** `modifyProbability` sobre un tercero (**existe**). · **Puesto:** defensa.
- **Coste:** la alineación queda atada: mover a uno apaga al otro.
- **Conflicto:** ninguno.
- **Motor:** **existente**.
- **Evidencia:** la pareja aparece siempre en el mismo orden vertical. *(Aviso: es oficio sobre un tercero, así que vive del lado flojo de la «Corrección 1»; se conserva por la decisión de alineación que crea, no por el efecto.)*

**C-68 · Pareja de centrales**
- **Fantasía:** dos centrales que no se despegan de la línea mientras el otro siga en pie.
- **Conducta:** mientras el vinculado esté en el campo, los dos eligen `MarkOpponent` en vez de `Retreat`; si uno cae, el otro repliega como todos.
- **Condición:** una, decidida al alinear.
- **Canal:** C1 + C2. · **Puesto:** defensa (pareja).
- **Coste:** el efecto **se apaga con una lesión**, y eso ata el perk a la carnicería: es el único candidato de pareja cuyo precio lo cobra la enfermería.
- **Conflicto:** `C-12 Presión tras pérdida`.
- **Motor:** extensión pequeña (C1 + C2). *(Rediseño de `back_to_back`: **MEDIDO, 0 % de activación en 21 partidos**.)*
- **Evidencia:** dos defensas clavados en la frontal tras un córner; cuando uno se lesiona, se ve al otro empezar a replegar.

**C-69 · Capitán de juego**
- **Fantasía:** los que juegan a su lado se atreven a más.
- **Conducta:** los compañeros con hogar adyacente eligen `Dribble` y `ThroughPass` más a menudo.
- **Condición:** una, decidida al alinear (quién está a su lado).
- **Canal:** C1 sobre terceros. · **Puesto:** centrocampista.
- **Coste:** su lado del campo pierde más balones que el otro.
- **Conflicto:** `C-16 Director` — «el que ordena el juego y el que suelta a los de al lado piden dos equipos distintos».
- **Motor:** extensión pequeña (C1 con alcance a adyacentes). **Es el «Capitán» del revisor convertido en algo que el rasgo `Leader` no hace**: hoy `Leader` sube **todo por igual**, y un multiplicador uniforme no cambia qué acción gana.
- **Evidencia:** la zona donde él está juega distinto a la otra; histogramas de acción por banda, separados.

**C-70 · Solitario**
- **Fantasía:** no se fía de nadie que no sea como él, y cuando está solo lo da todo.
- **Conducta:** si es el único de su etiqueta en el once, elige las acciones de riesgo (`Dribble`, `Tackle`) mucho más.
- **Condición:** una, **previsible al alinear** y es el reverso exacto de `C-66`.
- **Canal:** C1. · **Puesto:** cualquiera.
- **Coste:** fichar un segundo de su etiqueta **lo apaga**, y eso pone precio a una decisión de mercado. *(Es la única condición del catálogo que un fichaje futuro puede romper: hay que avisarlo en la tienda, o rompe la regla 11.)*
- **Conflicto:** `C-66 Manada`.
- **Motor:** extensión pequeña (C1).
- **Evidencia:** el elfo del equipo de orcos juega como un poseído; y deja de hacerlo el día que llega otro elfo.

**C-71 · Héroe**
- **Fantasía:** cuando el equipo va perdiendo, se echa el partido a la espalda.
- **Conducta:** yendo por detrás en el marcador, elige `Shoot` y `Dribble` en vez de pasar.
- **Condición:** una, de estado de partido que el jugador ve en pantalla.
- **Canal:** C3 + C1. · **Puesto:** delantero o centrocampista.
- **Coste:** cuando el equipo necesita orden, él sube la varianza. Y si va ganando, no hace nada.
- **Conflicto:** `C-72 Verdugo` — «no puedes ser otro jugador cuando pierdes y cuando ganas».
- **Motor:** extensión pequeña (C3 + C1). **Bloqueado hoy: MEDIDO, `UtilityContext` no tiene ni reloj ni marcador.**
- **Evidencia:** 0-1 y uno empieza a intentarlo todo él solo; su histograma cambia de forma con el marcador, cosa que hoy no le pasa a nadie.

**C-72 · Verdugo**
- **Fantasía:** con ventaja no se sienta: va a por el segundo.
- **Conducta:** yendo por delante, sigue eligiendo `PressCarrier` y `Shoot` donde el equipo elige `Retreat`.
- **Condición:** una, de marcador.
- **Canal:** C3 + C1. · **Puesto:** delantero.
- **Coste:** con 1-0 deja el equipo partido; el empate llega por donde él no estaba.
- **Conflicto:** `C-71 Héroe`.
- **Motor:** extensión pequeña (C3 + C1).
- **Evidencia:** el 1-0 se convierte en 2-0 o en 1-1 más veces que con cualquier otro once.

**C-73 · Arranque**
- **Fantasía:** sale a comerse el partido y luego se apaga.
- **Conducta:** en el primer tramo elige `PressCarrier` y `ChaseBall`; después vuelve a lo suyo.
- **Condición:** una, de reloj.
- **Canal:** C3 + C1. · **Puesto:** cualquiera.
- **Coste:** el tramo final lo juega un hombre gastado. **Es el único candidato que reparte su propio valor a lo largo del partido en vez de sumarlo.**
- **Conflicto:** `C-71 Héroe` — «el que se vacía al principio no está para héroes al final».
- **Motor:** extensión pequeña (C3 + C1).
- **Evidencia:** los primeros minutos se juegan en campo rival y los últimos no; su histograma, partido en dos mitades distintas.

**C-74 · Revulsivo**
- **Fantasía:** sale del banquillo con hambre.
- **Conducta:** cuando entra como sustituto, elige las acciones de ataque mucho más durante un tramo.
- **Condición:** una, **y es una decisión del jugador**: llevarlo al banquillo y no al once. La sustitución forzada ya es parte del estado inicial del partido (ADR 0094) y hay ventana en `/Game`.
- **Canal:** C1 + un indicador de «ha entrado desde el banquillo». · **Puesto:** cualquiera.
- **Coste:** **no juega**. Es el primer perk del catálogo que paga con el once titular, que es el recurso más escaso de la alineación.
- **Conflicto:** cualquier perk de arranque (`C-73`) — «el que espera en el banquillo no puede salir a comerse el primer tramo».
- **Motor:** extensión pequeña (C1 + un `bool` por jugador).
- **Evidencia:** el partido cambia de ritmo cuando entra; **y hace que el banquillo sea una decisión, que es donde vive el desgaste de la plantilla**.

**C-75 · Muro improbable** *(`unlikely_bulwark`, conservar)*
- **Fantasía:** cubre más campo del que un cuerpo así debería cubrir.
- **Conducta:** su zona de acción crece, así que llega a entradas que tenía descartadas.
- **Condición:** ninguna.
- **Canal:** `modifyLeash` + `tackle` (**existen**). · **Puesto:** cualquiera.
- **Coste:** ninguno propio. Se conserva porque **es uno de los tres perks que la auditoría midió como conductuales**, no porque su diseño sea ejemplar.
- **Conflicto:** `C-10 Ancla`.
- **Motor:** **existente**.
- **Evidencia:** llega donde no llegaba.

**C-76 · Distancia de tiro** *(`killing_range`, conservar sin cambios — **PD-5: congelado**)*
- **Fantasía:** cerca de la portería, este equipo no perdona.
- **Conducta:** **todo el equipo** remata mejor dentro de una distancia de la portería rival.
- **Condición:** una, geométrica.
- **Canal:** `modifyProbability` de alcance equipo (**existe**). · **Puesto:** cualquiera; es de equipo.
- **Coste:** hoy, el bloqueo de la línea contraria. Ver Fase D.
- **Conflicto:** ver Fase D.
- **Motor:** **existente**.
- **Evidencia:** **MEDIDO: 312 de 532 maestros tomados (59 %), valor 209**, y sostiene los 104 puntos de crédito de arco de las siete piezas de su línea (ADR 0072).

---

## Cuenta de la Fase A

**76 fichas** (`C-01` … `C-76`), que cubren **79 ficheros** de `/data` porque `C-47` agrupa los cuatro
letales. De ellas: **21 existen hoy** y se conservan o se rediseñan conservando el fichero; **55 son
nuevas o son fusiones**. Las nueve habilidades raciales que no aparecen con ficha propia (`elf_touch`,
`brute_boots` y compañía) se tratan aparte: no se ofrecen en el pool, van con el club, y su rediseño es un
encargo distinto —salvo `numb`, que está en la lista de descartados por PD-2.

**Cobertura por puesto** *(DERIVADO, contando la ficha `Puesto` de cada candidato)*: portero **7**
(`C-33..C-37`, más los transversales) · defensa **16** · **centrocampista 17** · delantero **14** ·
transversales **22**. El hueco medido —*el centrocampista no tiene un solo perk propio*— queda cerrado, y
no por decreto: salió solo, porque es el puesto con el reparto de acciones más rico y por tanto el que más
conductas distintas admite.

---

## Descartados, con motivo

Cada uno indica **qué criterio suspende**. La mayoría vienen de las ~45 ideas del revisor citadas en la
biblia o del catálogo actual.

### Suspenden [E4] — son otro candidato con otro nombre

| idea | es el mismo que | por qué |
|---|---|---|
| Oportunista | `C-26 Cazagoles` | mismo canal (`Shoot`), misma condición (cerca de portería), misma conducta |
| Cerebro | `C-16 Director` | ninguno de los dos nombra una conducta que el otro no produzca |
| Marcador | — | «marca mejor» **es** `MarkOpponent`, que **MEDIDO** ya es la acción líder de su estado para un defensa. Sin un **criterio de objetivo** distinto no hay perk |
| Último hombre | `C-10 Ancla` | misma geometría, mismo canal, misma imagen. Lo salvable de la idea es `C-43 Relevo` |
| Matón | `C-48 Sed de sangre` | los dos son «entra más y hace daño»; el que se queda es el que tiene el canal `injure` |
| Sobrevive | `C-56 Superviviente` | son la misma idea escrita dos veces en la misma lista |
| Capitán | rasgo `Leader` | **MEDIDO**: el rasgo ya da bono a los adyacentes y ya entra en la fórmula. Lo que el rasgo no hace es `C-69 Capitán de juego` |
| **Carrilero** | `C-40 Pegado a banda` | **crítica propia a la biblia**, que lo admitía «al límite»: una zona más larga y más estrecha es **una magnitud del mismo mando** (C8), y el criterio [E4] dice que una magnitud distinta no es un perk distinto |
| **El central que sube** | `C-25 Llegador` | mismo canal (C8 por estado), misma conducta, distinto puesto. El puesto no es un eje de diferenciación: es un filtro |
| **Saque en corto** | `C-33 Saque largo` | mismo canal, misma condición, dirección opuesta. Se conserva **uno** y el otro pasa a ser su conflicto |

### Suspenden [E2] o [E3] — oficio puro, o nada que ver en una jugada

`Anticipador` (cuota de `intercept`), `Manos seguras` (cuota de `save` — sobrevive **solo** reescrito como
`C-34 Manos de cepo`), `Puntería aprendida`, `Memoria del pasillo`, `Pegajoso`, y con ellos los **13
acumuladores de oficio** del catálogo actual. Motivo común: la acción es la misma y cambia el dado; no hay
verbo de conducta y no hay jugada que señalar. **Propuesta de reubicación: `/data/items`** (Corrección 1).

`quick_learner` — da experiencia: no toca el partido y no se ve. Su sitio es un **rasgo de club o raza**;
como perk ocupa un slot irreversible para nada.

`iron_lungs` y los **`modifyAttribute` planos de +10** — **MEDIDO: los cinco que existen tocan fuerza y
resistencia, y la resistencia no aparece ni una vez en `Utility.cs`**. Son un objeto disfrazado de perk, y
encima en los atributos que menos mueven la decisión. Incluye `brute_boots`.

`Especialista en finales` («en el último tramo sube lo que ya hace») — **contradice a la propia biblia**:
un multiplicador uniforme sobre lo que el jugador ya elegía **no cambia el `argmax`** y por tanto no cambia
ninguna conducta. Es un aumento de potencia con horario.

### Suspenden el criterio 8 (una sola condición) o la regla 11 (previsibilidad)

`Trampa` («deja pasar y cierra la puerta detrás») — dos condiciones encadenadas y la segunda no es una
decisión del jugador, sino un movimiento del rival. Además, **HIPÓTESIS**: en pantalla es indistinguible de
un defensa mal colocado que acierta.

`Diagonal` (`diagonal_press` rediseñado) — depende de **qué elige otro jugador en el mismo tick**: exige
orden de resolución explícito (RT-041), y en pantalla no se puede separar de la casualidad. *(`C-43 Relevo`
es la versión que sí pasa: lee la **posición** del otro, no su decisión.)*

`Nervios` («cuanto más importante, peor decide») — bajo **PD-1** una penalización es admisible **si expresa
una identidad**; «decide peor» no es una identidad, es un malus. Su sitio, si se quiere, es un **rasgo**,
que es la capa que el jugador no elige.

`nearAlly` / `nearOpponent` con radio en el tick, como clase — **MEDIDO: los siete perks opacos del
catálogo valen de −1 a 5 con activaciones del 0 % al 9 %**. `C-64 Provocador` solo entra porque su
condición se reescribe sobre un emparejamiento estable.

### Fuera de la capa de perks

`Comodín` («puede jugar en cualquier puesto») — es una regla de **plantilla y alineación**, no una conducta
en el campo. Buena idea, capa equivocada.

`Cotizado`, `Ojeador` y todo lo que toca precio o mercado — no es conducta ni es carne; es economía, y la
economía ya tiene sus palancas (ADR 0096-0100).

### Bloqueados por una capacidad que no toca construir ahora

`Incansable` / `Último esfuerzo` / `Segundo aire` — **MEDIDO: la resistencia no aparece ni una vez en
`Utility.cs`**. Meter la fatiga en la decisión es caro y desbloquea dos o tres candidatos. **No ahora.**

`Segundo palo`, `Centrado` — exigen la acción **centro** y su duelo aéreo: acción nueva, vuelo nuevo,
resolución nueva, y presión sobre `injuriesPerMatch`, que ya está en el techo de RT-056. **No.** Las
fantasías de banda se cubren con geometría (`C-40`, `C-24`, `C-43`).

`numb` (mitad de duelo) — **PD-2 deja RF-100..106 fuera del alcance**, así que su inmunidad no tiene
consumidor y su descripción generada promete lo que el motor no hace (choca con RT-035 y con la regla 11).
**O se retira esa mitad del texto, o el perk sale.**

---

# FASE B — Clustering: las familias aparecen

**Método.** Se agrupa por **la conducta que producen**, nunca por canal, nombre ni puesto. El criterio de
corte es la frase de la prueba ciega: *«un revisor que mira el partido sin saber la alineación describiría
a estos jugadores con la misma frase»*. Y cada grupo tiene que contestar que sí a: **«¿representa una
identidad que un jugador querría perseguir?»**. Si no, no es familia.

**PD-4 aplicado:** 5-6 miembros **funcionalmente distintos**. Ningún grupo se rellena para llegar a ocho,
y ningún grupo sobrevive con cuatro.

## Las nueve familias que salen

| # | familia | la frase de la prueba ciega | miembros | canales que recluta |
|---|---|---|---|---|
| **F1** | **El Bloque Bajo** | «ese equipo no sale de su campo» | `C-14` Bloque bajo · `C-10` Ancla · `C-04` Cobertura · `C-36` Guardameta clásico · `C-23` Despejador · `C-43` Relevo | correa (negativa), intención, geometría, pase |
| **F2** | **La Presión** | «esos saltan encima en cuanto pierden» | `C-12` Presión tras pérdida · `C-15` Línea adelantada · `C-06` Recuperador · `C-46` Cazador de porteros · `C-35` Portero líbero · `C-07` De área a área | correa, intención, hogar |
| **F3** | **La Presa** | «ese tío va a por la gente, no a por la pelota» | `C-01` Perro de presa · `C-02` Guardaespaldas · `C-49` Olfato de sangre · `C-53` Rabia · `C-08` Carnicero ciego | objetivo de marca, objetivo de entrada, intención |
| **F4** | **El Toque** | «esos tocan y no la pierden» | `C-16` Director · `C-17` Primer toque · `C-18` Conductor · `C-19` Pivote hondo · `C-31` Socio de ataque · `C-69` Capitán de juego | intención, hogar, receptor, efecto sobre terceros |
| **F5** | **El Envío** | «esos la mandan arriba en cuanto pueden» | `C-20` Rompe-líneas · `C-21` Cambio de orientación · `C-22` Central constructor · `C-33` Saque largo · `C-38` Torre · `C-39` Desmarque profundo | intención, receptor, geometría, techo de línea |
| **F6** | **El Remate** | «ese acaba las jugadas, y le da igual cómo» | `C-76` **Distancia de tiro** (maestro) · `C-26` Cazagoles · `C-28` Sangre fría · `C-30` Amenaza de larga distancia · `C-25` Llegador · `C-24` Extremo vertical · `C-29` Cazador de rechaces | intención, rango de tiro (geometría), hogar, cuota de equipo |
| **F7** | **La Carne Ajena** | «a ese equipo se le van quedando rivales por el suelo» | `C-48` Sed de sangre (maestro) · `C-47` los cuatro letales · `C-50` Sed acumulada · `C-51` Sangre caliente · `C-65` Instigador · `C-13` Kamikaze | letal, lesión, derribo, intención, riesgo propio |
| **F8** | **La Carne Propia** | «a ese equipo no se le rompe nadie» | `C-54` Pagar el hierro · `C-56` Superviviente · `C-57` Veterano de cicatrices · `C-58` Curtido · `C-59` A media pierna · `C-60` Hierro en la pierna | estado de run, contadores, inmunidad, riesgo propio |
| **F9** | **El Pacto** | «esos dos juegan siempre juntos» | `C-68` Pareja de centrales · `C-42` Sombra · `C-67` Gigante amable · `C-66` Manada · `C-70` Solitario | vínculo estático, composición, geometría, efecto sobre terceros |

**53 candidatos en familia. 23 fuera.** *(DERIVADO)*

## Los dos grupos que **no** llegan a familia

**El Árbitro** (`C-62` Cara de inocente · `C-63` Teatrero · `C-64` Provocador, y `C-65` Instigador, que está
en F7): la frase existe y es buena —«a ese le da igual la pelota: juega al árbitro»—, el recurso es **la
barra de criterio, que el jugador ve todo el partido (RF-062)**, y la identidad es apetecible. Pero con
PD-4 son **tres o cuatro miembros distintos y no hay un quinto que no sea relleno**. Veredicto: **no es una
familia hoy; son reglas rotas sueltas.** Se convierte en familia el día que el árbitro dispute algo más que
la expulsión.

**El Momento** (`C-71` Héroe · `C-72` Verdugo · `C-73` Arranque · `C-74` Revulsivo): la identidad es real y
**nueva** —«tengo un hombre para cada momento del partido»— y **MEDIDO: está bloqueada entera porque
`UtilityContext` no tiene ni reloj ni marcador**. Cuatro miembros. Veredicto: **familia en potencia**, y el
precio de abrirla son dos enteros en un contexto.

## Los 23 fuera de familia

- **Universales** (sirven en cualquier once y no arrastran doctrina): `C-44` Raíces · `C-45` Correa curtida · `C-52` Se levanta solo · `C-55` Puerta de hierro · `C-75` Muro improbable.
- **De puesto** (su valor lo fija el puesto, no una doctrina): `C-34` Manos de cepo · `C-37` Red de seguridad · `C-40` Pegado a banda · `C-41` Interior · `C-05` Cortacircuitos · `C-27` Hombre objetivo · `C-32` Especialista a balón parado.
- **De personalidad** (el jugador cambia con el momento): `C-71` · `C-72` · `C-73` · `C-74`.
- **De regla rota / árbitro**: `C-62` · `C-63` · `C-64`.
- **Contrarios declarados** (existen para negar una doctrina, y por eso no pertenecen a ninguna): `C-09` Cobarde con ojo · `C-11` No vuelve · `C-03` Hombre libre.
- **A discusión**: `C-61` Carroñero.

**El catálogo total no se fija en 36.** Sale **76 fichas / 79 ficheros** *(DERIVADO de PD-4)*, y el número
no es un objetivo: es lo que quedó al exigir que cada ficha produjera una conducta distinta. Si el revisor
recorta, lo que hay que recortar son **fichas enteras**, no miembros de familia hasta cuadrar una cifra.

## ¿Confirman estos grupos el diagnóstico del revisor?

**Sí, y con una corrección.** *(DERIVADO, contrastando con `/data`.)*

- **`aim` era una hoja de cálculo.** **MEDIDO: sus ocho miembros escriben `shotOnTarget` y nada más.** Al
  agrupar por conducta, «rematar» sí produce una familia (F6), pero **con otros miembros**: seis maneras
  distintas de acabar una jugada (tirar lo primero, esperar un toque, tirar de lejos, llegar de segunda
  línea, encarar y tirar, cazar el rechace) que producen **seis partidos distintos** aunque las seis acaben
  en gol. La familia no era el problema; el problema era que sus miembros eran el canal.
- **`wall` era una hoja de cálculo, y además era tres familias.** Sus 7 de 8 `tackle` se reparten en mi
  agrupación entre **F1** (`C-10` Ancla, `C-04` Cobertura), **F3** (`C-01`, `C-08`) y **F7** (`C-13`
  Kamikaze). *«Robar el balón» no es una identidad: es un resultado que comparten tres identidades
  distintas.* Ese es el hallazgo más limpio de la fase.
- **`butchery` era un prototipo accidental, y el proceso lo confirma y lo mejora.** Recluta de tres canales
  alrededor de una conducta, y al agruparlo por conducta **se parte en dos familias limpias** —F7 (carne
  ajena) y F8 (carne propia)— que además **estrenan un recurso disputado**. Era un prototipo de lo que se
  quiere, sí; le faltaba su otra mitad.
- **`craft` era un prototipo accidental y además estaba de más: eran DOS identidades bajo un nombre.**
  «Ganar sin tocar al rival» junta a quien conserva el balón (F4 El Toque) con quien lo manda lejos (F5 El
  Envío), que son **conductas opuestas** —y, en el motor, **el mismo `argmax` resuelto al revés**. Por eso
  reclutaba tres canales sin forzar sinónimos: porque había dos familias dentro. *(Es una corrección al
  informe de líneas, que lo daba por prototipo único.)*
- **La portería no es una familia.** Los cinco candidatos de portero **caen en cinco sitios distintos**:
  `C-35` en F2 (va al balón), `C-36` en F1 (no sale de casa), `C-33` en F5 (la manda arriba), `C-37` y
  `C-34` fuera de familia. *(DERIVADO, y contradice a la biblia §3.1-3, que pedía «Portería» como familia
  para que no le siguieran diseñando perks de defensa. El remedio es peor: una familia por puesto garantiza
  que el puesto vuelva a ser el eje. El puesto es un **filtro** —`positionOnly`— y funciona mejor así.)*

---

# FASE C — Conflictos: qué merece un bloqueo

Las tres pruebas del informe de líneas, aplicadas **para clasificar, no para descartar** (PD-3):
**(1)** la frase «no puedes X e Y porque las dos gastan **Z**», con `Z` nombrable en una palabra y la misma
para las dos · **(2)** `Z` es algo que el motor **dispute de verdad** · **(3)** la diferencia se vería en el
histograma de acción elegida. Solo **VALIDADA** y **VALIDABLE** merecen `blocksPerks`.

| par | frase | prueba 2 | prueba 3 | veredicto |
|---|---|---|---|---|
| **F1 Bloque Bajo ↔ F2 Presión** | «no puedes meter el bloque atrás y presionar arriba, porque las dos gastan **la posición vertical del equipo**» | **sí, hoy**: `modifyLeash` y `EffectiveHome` son **precondición dura** —`Utility` descarta la acción entera fuera del límite exterior— y un equipo no puede vivir arriba y abajo a la vez *(MEDIDO)* | `Retreat`/`CoverSpace` contra `PressCarrier`/`ChaseBall`: es la distancia L1 más grande que puede producir el catálogo | **VALIDADA** |
| **F3 La Presa ↔ F2 La Presión** | «no puedes ir al hombre y al balón, porque las dos gastan **el tick del jugador**» | **todavía no**: `Utility` es un `argmax` puro, así que dos órdenes sobre el mismo conjunto legal **sí** se disputan, pero **MEDIDO: ningún perk escribe ahí** | `MarkOpponent`/`Tackle` contra `ChaseBall`, sobre el mismo jugador y el mismo estado | **VALIDABLE** — falta **C1 + C2** |
| **F7 Carne Ajena ↔ F8 Carne Propia** | «no puedes cobrarte carne y conservar la tuya, porque las dos gastan **la plantilla**» | **a medias**: la carne es el recurso central declarado y ya es agotable, pero **MEDIDO: hoy la violencia solo gasta carne del rival**; falta que ser violento cueste cuerpos propios | `deathsPerRun`, `ownInjuries` y el gasto de clínica, que ya están en `runs.csv` | **VALIDABLE** — falta **C4 sobre `InjuryChanceBonus`**, que es **la capacidad más barata de las tres** |
| **F4 El Toque ↔ F5 El Envío** | «no puedes conservarla y mandarla, porque las dos gastan **el tick del portador**» | **todavía no**, mismo motivo; pero es el par más limpio que existe: **mismo jugador, mismo instante, mismo conjunto de acciones** (`ShortPass`/`Dribble` contra `LongPass`/`ThroughPass`) | histograma del portador, que es donde la diferencia es máxima | **VALIDABLE** — falta **C1 + C2** |
| **F6 El Remate ↔ F4 El Toque** | «no puedes acabar la jugada y seguirla, porque las dos gastan **el tick del portador**» | igual que el anterior: **es el mismo `Z`** | igual | **REDUNDANTE** con el anterior. El tick del portador admite un corte, no dos: tres familias no pueden excluirse por turnos sin que la run se quede sin nada que comprar |
| `aim` ↔ `wall` **(hoy)** | «no puedes rematar y robar» | **no**: canales distintos (`shotOnTarget` / `tackle`), puestos distintos, momentos distintos; no hay `Z` *(MEDIDO)* | los dos histogramas cambian, pero **en jugadores distintos**: no hay renuncia | **FALSA** |
| `craft` ↔ `butchery` **(hoy)** | «no puedes ganar sin tocar al rival y ganar tocándolo» | la frase insinúa `Z` = el tick, pero el mecanismo no existe: `dribble`/`pass` e `injure` son tiradas independientes que no compiten por nada | — | **REDUNDANTE** con **F3 ↔ F2** (que dice exactamente eso y con el mecanismo nombrado); **FALSA** mientras no exista C1/C2 |
| **F9 El Pacto ↔ nada** | — | no tiene contrario: montar el equipo alrededor de parejas no excluye ninguna otra forma de jugar | — | **no es doctrina.** Es una familia sin exclusión, y está bien que lo sea |

**Conclusión de la fase** *(DERIVADO)*: **una exclusión validada hoy, tres validables, dos falsas y dos
redundantes.** El número de doctrinas no lo fija el catálogo: lo fija cuántos recursos disputa el motor.
Hoy hay **un par real**; con C1+C2 hay **tres**; y el tercero —la carne— es **el más barato de los tres** y
el único que ya es la identidad declarada del juego. Eso invierte el orden de trabajo que proponía la
biblia: la carne antes que la geometría.

## La exclusión barata que nadie usa

**MEDIDO:** `blocksPerks.perks` —cerrar perks concretos **por id**— está implementado, validado y **usado
por cero perks**, porque el cargador lo prohíbe sin `requiresPerks`. Esa prohibición es correcta para el
bloqueo **de run** (cerrar algo para siempre sin haber demostrado nada es una trampa) pero deja sin cubrir
la exclusión **por jugador**: *«estos dos no pueden ir en el mismo cuerpo»*. Es barata, reversible por
colocación, previsible en la pantalla de Equipo, no necesita línea, ni maestro, ni oro — y el catálogo la
pide a gritos:

| pareja | frase | estado |
|---|---|---|
| `C-35` Portero líbero ↔ `C-36` Guardameta clásico | «es la misma cuerda tirada en dos direcciones» | **VALIDADA hoy** (la correa existe) |
| `C-10` Ancla ↔ `C-07` De área a área | «una correa que crece y un puesto que no se abandona» | **VALIDADA hoy** |
| `C-66` Manada ↔ `C-70` Solitario | «o montas una manada o montas un equipo de uno» | **VALIDADA hoy** (composición, previsible al alinear) |
| `C-17` Primer toque ↔ `C-18` Conductor | «la misma pelota no se suelta al instante y se conduce» | **VALIDABLE** (C1) |
| `C-38` Torre ↔ `C-39` Desmarque profundo | «el que espera quieto no es el que corre a la espalda» | **VALIDABLE** (C1) |
| `C-09` Cobarde con ojo ↔ `C-13` Kamikaze | «uno paga con su cuerpo lo que el otro se ahorra no saltando» | **VALIDABLE** (C1) |
| `C-22` Central constructor ↔ `C-23` Despejador | «desde la frontal, o sales jugando o la echas fuera» | **VALIDABLE** (C1) |
| `C-71` Héroe ↔ `C-72` Verdugo | «no puedes ser otro jugador cuando pierdes y cuando ganas» | **VALIDABLE** (C3) |

> **Recomendación: dos niveles de exclusión.** La de **run** (maestro, cara, irreversible, **pocas**: hoy
> una, mañana tres) y la de **jugador** (barata, muchas, decidida al colocar y deshecha al recolocar). La
> segunda no existe hoy y **es la que hace que 76 perks no sean intercambiables**, porque convierte la
> pregunta «¿cuál suma más?» en «¿qué es este jugador?».

---

# FASE D — Dónde cae `killing_range`

**PD-5: congelado.** No se propone moverlo, degradarlo ni retocarlo. Solo se contesta dónde caería su
fantasía y si esa familia sostiene su función.

**Cae en F6, El Remate, y cae como maestro.** *(DERIVADO)* Su fantasía —«cerca de la portería, este equipo
no perdona»— es de remate, y dentro de F6 es el **único miembro de alcance equipo**: los otros seis son
conductas de un hombre (tirar lo primero, esperar un toque, tirar de lejos, llegar de segunda línea,
encarar, cazar el rechace) y él es la **consecuencia colectiva** de haberlas juntado. Esa asimetría —seis
conductas individuales y una regla de equipo que las corona— es exactamente la forma que debería tener un
maestro, y es la que hoy **no** tiene ninguna de las cuatro líneas.

**¿Puede F6 sostener su función de crédito de arco?** **HIPÓTESIS con experimento asignado, y el
experimento hay que hacerlo antes de tocar nada.**

- El riesgo está **MEDIDO** y es el punto más frágil de todo el rediseño: `killing_range` es el **59 % de
  los maestros tomados** y **sostiene 104 puntos de crédito de arco** por pieza; la ADR 0072 midió que sin
  ese crédito `spearpoint` y `forward_line` caen por debajo del listón del slot y `mastersReached` se hunde
  de 25,2 a 14,8. **Quitarle el crédito a su línea no reubica un perk: borra siete.**
- Pero ese crédito existía **porque las siete piezas de `aim` no valían por sí solas**: ocho ficheros
  escribiendo `shotOnTarget`. **DERIVADO:** el crédito no medía el valor del maestro, medía el **vacío de
  sus miembros**. Los seis miembros de F6 son conductas distintas con valor propio previsible, así que la
  hipótesis es que **necesitan menos crédito, no más**.
- **El experimento que lo decide, y es el mismo que la ADR 0072 ya sabe hacer:** medir cada miembro de F6
  contra su control emparejado (ADR 0087) **con el crédito de arco a cero**. Si despejan el listón del slot
  sin crédito, F6 sostiene a `killing_range` y el maestro deja de ser una muleta. Si no lo despejan, el
  problema no es `killing_range`: son sus miembros, otra vez.
- **Medición pendiente de PD-5 que hay que hacer en el mismo lote:** el **59 % de runs con maestro que
  siguen comprando perks de la línea que cerraron** (media 1,06). Si lo que compran son periféricos, el
  bloqueo funciona; si son los que definen la fantasía, **el bloqueo es cosmético** y la exclusión de run
  vale menos de lo que creemos — lo que subiría el valor de la exclusión **por jugador** de la Fase C.
- **Nota sobre su exclusión actual:** `killing_range` cierra `wall`, y la Fase C clasifica ese par como
  **FALSA**. PD-5 congela el perk, así que la recomendación no es quitarle el bloqueo, sino **reapuntarlo**
  el día que la taxonomía se descongele (PD-3): F6 no tiene contrario legítimo, así que el maestro de F6
  debería cerrar **por ids** (`blocksPerks.perks`) las conductas que de verdad se contradicen con acabar la
  jugada cuanto antes —`C-16` Director, `C-17` Primer toque— y no una familia entera.

---

# Hoja de ruta de motor, agregada

Capacidades ordenadas por **cuántos de los 76 candidatos desbloquea cada una** *(DERIVADO, contando el
campo `Motor` de las fichas; un candidato cuenta en cada capacidad que necesita)*.

## Existe hoy, y basta usarlo

| capacidad | candidatos | nota |
|---|---|---|
| `modifyLeash` como **precondición dura** | **7** (`C-07`, `C-14`, `C-35`, `C-36`, `C-45`, `C-75`, y la mitad de `C-10`) | el mejor efecto del sistema y el único recurso que el motor **ya** disputa. **Lo que falta no es motor: es la decisión PD-1 sobre el signo negativo** |
| `lethal` · `cancelEvent` · `immunity` · `modifyBias` · `modifyKnockdownTicks` | **10** | la única familia de reglas rotas de verdad; `C-52` y `C-63` solo invierten el sujeto de un canal existente |
| estado de run (clínica, prótesis, muerte) | **4** (`C-58`, `C-59`, `C-60`, y el gancho de `C-61`) | **la clase [carne], y está entera sin tocar `/Sim`** |
| condiciones previsibles + `LineupPerkPreview` | todos los que se deciden al alinear | se conserva tal cual; es la mejor pieza de comunicación del sistema |

## Extensión pequeña, por orden de desbloqueo

| # | capacidad | candidatos | por qué es pequeña |
|---|---|---|---|
| 1 | **C1 · `modifyUtility(acción,%)`** — overlay de enteros sobre `_actionMultipliers` | **42 de 76** | el motor **ya compone** un segundo multiplicador mutable en la misma línea (`LeaderBonusPercent`, escrito en tiempo de partido). Pasar de escalar a `int[14]` es la misma pieza. **Es la única variable que decide qué intenta hacer un jugador, y hoy ningún perk puede tocarla** |
| 2 | **C2 · vocabulario cerrado de situación dentro de la decisión** (tercio del actor, fase de posesión, con/sin balón, balón suelto, primer tick tras recibir, turba) | **27** | enteros y comparaciones en C#; **nada de NCalc por tick** (`CompiledCondition` guarda contexto en la instancia y no es reentrante). Sin ella, C1 solo sabe decir «siempre» |
| 3 | **C8 · desplazamiento de hogar y forma de zona por jugador** | **9** | `EffectiveHome` ya se mueve cada tick con el desplazamiento de bloque; falta un desplazamiento **por jugador**. Es la capacidad que da toda la geometría sin una acción nueva |
| 4 | **C5 · sesgo de objetivo en el reparto de marcas** | **4** | la función de coste de `Marking.Assign` **ya tiene** un término de preferencia por rol: se añade un sesgo por jugador |
| 5 | **C4 · escritura sobre los trece escalares de rasgo** | **4** (`C-13`, `C-30`, `C-54`, `C-64`) | mismo patrón que el delta de atributo ya existente. **Incluye `InjuryChanceBonus`, que es la capacidad que abre el par «la carne» de la Fase C**: pocos candidatos, el desbloqueo más valioso por línea de código |
| 6 | **C11 · contadores de carne por jugador, visibles** | **4** | el volcado de contadores entre partidos ya existe; falta **qué** se cuenta (cicatrices propias, muertos del equipo, lesiones causadas) y **enseñarlo** |
| 7 | **C3 · reloj y marcador en `UtilityContext`** | **3** (+`C-74`) | **dos enteros**, y desbloquean la familia entera de «El Momento», que hoy no existe |
| 8 | **C6 · preferencia de receptor de pase** | **3** | `EvaluatePass` ya ordena receptores; se añade un sesgo |
| 9 | **C7 · criterio de objetivo de entrada** | **2** | `Utility` ya elige entre portador y marcado; las tarjetas y el estado físico son legibles |
| — | **C10 · techos por acción y por puesto en el cargador** | **0 propios, prerrequisito de C1** | un `×3` a `Shoot` en un portero tiene que ser un **error de datos**, no una anécdota (RT-032) |
| — | **C9 · la activación como evento en el flujo** | **0 mecánicos, todos perceptivos** | **MEDIDO: durante los 60-90 s el jugador no ve dispararse ni un perk.** `/Sim` no decide presentación (RT-014): deja de esconder lo que ya calcula. Es lo más barato y lo que más cambia la percepción |
| — | **C16 · descripciones generadas desde la intención** | prerrequisito de que 76 ofertas sean legibles | RT-035 se conserva entero; lo que cambia es que hay un efecto **conductual** del que generar una frase de conducta en vez de una suma |

## Sistema nuevo

| capacidad | candidatos | veredicto |
|---|---|---|
| gancho de lanzador en la reanudación | 1 (`C-32`) | pequeño, pero solo sirve a un candidato raro. **Aplazar** |
| rama de resultado de la parada (atrapar / escupir) | 1 (`C-34`) | pequeño y con efecto visible; **hacerlo solo si se mantiene la excepción de oficio** |
| herencia de equipo en la muerte | 1 (`C-61`) | **decidir primero si el candidato debe existir**; el motor es lo de menos |
| fatiga en la decisión | 0 en esta lista | **MEDIDO: la resistencia no aparece ni una vez en `Utility.cs`.** Caro, y sus candidatos ya están descartados. **No** |
| acción **centro** + duelo aéreo | 0 en esta lista | acción nueva, vuelo nuevo, resolución nueva, y presión sobre `injuriesPerMatch`, que ya está en el techo de RT-056. **No** |
| vínculos RF-100..106 | 0 | **fuera del alcance por PD-2.** Los nueve candidatos de vínculo usan `linked`, el vínculo **estático de alineación**, que sí existe |

## Orden de trabajo que se deduce

**Tanda 0 — HECHA (17 sep 2026, `docs/analisis/tanda-0-histograma-de-accion.md`).** El **histograma de
acción elegida** por jugador y partido, y su distancia L1 contra el control emparejado de la ADR 0087,
implementado en `Sim.Tests/Analysis/ActionHistogramTests.cs`. Sin código de motor tocado. La calibración
prevista (`bulwark_stance`/`own_third_anchor` contra `sweeper_keeper`/`iron_gate`) salió con un matiz que
el plan no anticipaba: `bulwark_stance` activa en solo 2 de 40 partidos (su condición de etiqueta rara vez
la cumple un portador al azar, el mismo límite AT-C de la ADR 0087) así que su L1=0 no prueba nada por
baja potencia; `own_third_anchor` sí activa en los 40 y su L1=0 exacto **sí** es la confirmación fuerte.
`sweeper_keeper` (L1=0,0013, 35 activaciones) confirma que el instrumento distingue un efecto real
pequeño de un cero. La prueba 3 de la Fase C ya tiene instrumento; sigue pendiente aplicarlo a un
candidato real de C1 (Tanda 2).

**Tanda 1 — C4 sobre `InjuryChanceBonus`, y nada más.** Es la capacidad más barata que abre una exclusión
nueva, y abre precisamente **la que ya es la identidad del juego**: dos candidatos (`C-13` Kamikaze,
`C-54` Pagar el hierro) convierten la violencia en un gasto de plantilla propia. **Se mide con lo que ya
hay** (`deathsPerRun`, `ownInjuries`, gasto de clínica) y no toca la IA. *(Esto invierte el orden de la
biblia, que ponía `modifyUtility` primero: es más honesto probar la identidad declarada del juego con la
palanca barata antes de recalibrar el motor entero.)*

**Tanda 2 — C1 + C10 + C9, con dos candidatos y nada más.** Uno que no necesita decisión del revisor
(`C-26` Cazagoles) y uno que sí (`C-10` Ancla, con rama por debajo del cien, PD-1). Métricas de corte,
todas hoy en banda y sin presupuesto libre: `injuriesPerMatch`, `shotsPerMatch`, `ballThirdMaxShare`, la
cadena de pases y `runWinRate`. **Si dos candidatos conductuales sacan de banda tres de esas cinco, lo que
falta es techo por puesto (C10), no menos ambición.**

**Tanda 3 — C2 y la capa de intención con C16.** Es la que convierte `modifyUtility` en mecanismo en vez de
idioma y la que hace legibles las ofertas de una run. *(El diseñador no debería escribir porcentajes en
`/data`: debería escribir «prefiere A sobre B cuando S» y que el cargador calcule el entero contra la tabla
del rol. Es la diferencia entre 76 perks y 76 sumas.)*

**Tanda 4 — C8, C5, C11, C3, C6, C7.** Geometría, objetivos, contadores de carne y reloj. Aquí entra el
grueso del catálogo, y aquí se abre «El Momento».

---

# Riesgos materiales

1. **El crédito de arco de `killing_range`** es el riesgo medido más grande del rediseño y está en la Fase
   D: hay que hacer el experimento **antes** de descongelar nada (PD-5).
2. **C1 no es un parche, es un recalibrado.** Un ×1,6 a `Tackle` cambia **cuántas entradas hay**, y las
   entradas son la fuente de `injuriesPerMatch`, que ya está en el techo de RT-056. Igual con
   `ballThirdMaxShare`, la cadena de pases y los tiros. Hay que presupuestarlo como un recalibrado, no como
   una tanda de datos.
3. **La clase [carne] (`C-58`, `C-59`, `C-60`) toca la economía de la clínica**, que las ADR 0099-0100
   acaban de rehacer y cuyas métricas llevan dos lotes en banda. Cualquiera de los tres mueve el gasto de
   clínica por run: se miden juntos o no se miden.
4. **`C-70` Solitario tiene una condición que un fichaje futuro puede apagar.** Eso choca con la regla 11 si
   la tienda no lo avisa. O la tienda lo avisa, o el candidato cae.
5. **`C-03` Hombre libre es el único candidato que toca la IA del rival.** Se mide aparte o contamina todo
   lo demás.
6. **La prueba 3 de la Fase C no se puede ejecutar hoy.** Tres de los ocho veredictos (los VALIDABLES)
   están apoyados en la prueba 1 y la 2 y **la 3 está en blanco** hasta que exista el histograma. Son
   VALIDABLES, no VALIDADAS, y la diferencia es exactamente esa.
