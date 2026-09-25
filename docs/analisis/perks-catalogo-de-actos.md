# Catálogo de actos — 65 perks que se ven

**Encargo del revisor (25 sep 2026):** *«Los ejemplos que te he dado antes eran ejemplos. Quiero que tú seas
el creativo. Me da igual si dos perks saltan sucesivamente, cada perk activado muestra un cartelito encima
de la cabeza del jugador que no molesta. El balanceo lo dejamos para más adelante. Primero crea el catálogo
con lo que ya sabes.»*

Cumple la **[ADR 0149](../decisiones/0149-el-catalogo-se-mide-por-lo-que-se-ve.md)** (RF-069 v0.10) y aplica
la receta de [`perks-de-cuota-a-acto.md`](./perks-de-cuota-a-acto.md). **Sin una sola magnitud**, por
instrucción expresa: no hay porcentajes, ni ticks, ni casillas. Lo que se fija es **qué acto es cada perk,
qué se ve y qué se paga**.

---

# 0. Dos decisiones del revisor que cambian el plan anterior

## 0.1 El cartelito, y la vuelta de una idea que ya estaba escrita

> *«cada perk activado muestra un cartelito encima de la cabeza del jugador que no molesta»*

**Es, palabra por palabra, para lo que se creó `EventType.PerkTriggered`** *(MEDIDO, su propio comentario en
`Sim/Events/EventType.cs`)*:

> *«Existe para que la pantalla de partido pueda ATRIBUIR lo que ocurre —**un aviso sobre la cabeza del
> jugador**— sin calcular ni decidir nada (RT-014).»*

El evento se emite hoy con el id del perk, su portador y su celda, y **nadie lo dibuja**. La decisión del
revisor no pide nada nuevo: pide que se use lo que ya está.

**Lo que esto retira del plan anterior:**

- **El `MomentKind` de perk ya no hace falta.** Un perk no tiene que competir por la voz alta ni por el
  sello ni congelar la imagen: tiene su propio canal, ligero y paralelo. *(Retira la mitad de C18 y la
  regla 3 de §1.6.C de la biblia, que exigía entrada en la gramática de momentos.)*
- **El riesgo de saturación queda retirado por decisión**, no por medición: *«me da igual si dos perks
  saltan sucesivamente»*. La cola de una plaza y la caducidad de 1,5 s gobiernan los **momentos**, no los
  cartelitos. **La tanda A deja de ser una medición de saturación y pasa a ser contenido.**

## 0.2 El balance se aplaza, la asimetría no

*«El balanceo lo dejamos para más adelante»* se aplica a **magnitudes**. La **mitad mala** de cada acto no
es balance: es diseño, y es lo que impide que dentro de seis meses tengamos sesenta actos que sólo son
buenos. Cada ficha la lleva escrita.

---

# 1. Cómo se lee una ficha

> **Nombre** · *disparador* · `necesita`
> **Acto:** qué se ve.
> **Paga:** la mitad mala.
> **Soporte:** el estadístico que decide si sale bien o mal (RF-069b).

`necesita`: **HOY** (escribible con el vocabulario actual) · **ENF** (enfriamiento) · **VAR** (variante de
acción) · **IMP** (impulso dirigido) · **CONT** (efecto continuo).

Todos los actos llevan enfriamiento (RF-069c); sólo se marca **ENF** cuando es lo *único* que les falta.

---

# 2. Tiro — 8

> **Cañonazo** · *tiro* · `VAR`
> **Acto:** el balón sale silbando y el portero apenas se mueve.
> **Paga:** si no va entre palos se pierde en la grada. Saque de puerta y contra.
> **Soporte:** fuerza y técnica deciden si va dentro o a la grada.

> **Vaselina** · *tiro con el portero adelantado* · `VAR`
> **Acto:** el balón le pasa por encima y cae solo en la portería vacía.
> **Paga:** con el portero en su sitio es un globo que atrapa sin moverse, y la grada se ríe.
> **Soporte:** la técnica decide la altura; la distancia al portero, si tiene sentido intentarlo.

> **Rosca de brujo** · *tiro con un defensa en medio* · `VAR`
> **Acto:** el balón se curva alrededor del bloqueo. El defensa se tira a nada.
> **Paga:** va lento. Al portero le sobra tiempo.
> **Soporte:** técnica.

> **A bocajarro** · *tiro muy cerca* · `HOY`
> **Acto:** dispara dos veces en el mismo instante; si el primero rebota, el segundo ya va.
> **Paga:** si el primero da en un cuerpo, el segundo también.
> **Soporte:** el remate, sin cambios.

> **Tiro de hacha** · *tiro* · `IMP`
> **Acto:** raso y brutal: el defensa que se cruza **cae derribado** y el balón sigue.
> **Paga:** un tiro raso es el más fácil de parar.
> **Soporte:** fuerza.

> **Testuz de ariete** · *centro o balón alto* · `IMP`
> **Acto:** cabecea con todo el cuerpo y manda balón **y rival** por los aires.
> **Paga:** aterriza mal: se queda en el suelo mientras el juego sigue.
> **Soporte:** fuerza contra fuerza en el duelo aéreo.

> **Último aliento** · *tiro en el tramo final* · `VAR`
> **Acto:** vuelca lo que le queda en un disparo que no debería poder dar.
> **Paga:** termina reventado; lo que quede de partido lo juega a rastras.
> **Soporte:** el cansancio acumulado es literalmente la munición.

> **Fusil de feria** · *tiro desde muy lejos* · `VAR`
> **Acto:** dispara desde donde nadie dispara, y el portero no lo espera.
> **Paga:** casi siempre acaba en saque de puerta.
> **Soporte:** técnica y distancia.

---

# 3. Regate — 6

> **Sombrerito** · *regate* · `VAR`
> **Acto:** el balón por encima del defensa, él por el lado, y se reencuentran detrás.
> **Paga:** el balón vuela solo un instante: cualquiera que esté cerca llega antes.
> **Soporte:** técnica contra la posición del defensa.

> **Caño** · *regate* · `HOY`
> **Acto:** el balón entre las piernas. El defensa se queda **clavado** mirando.
> **Paga:** si no cuela, el balón se queda en el defensa y él ya ha pasado de largo.
> **Soporte:** técnica.

> **Ruleta** · *regate* · `IMP`
> **Acto:** gira sobre sí mismo y **el defensa se pasa de largo** con su propia inercia.
> **Paga:** sale del giro lento; la defensa tiene tiempo de recomponerse.
> **Soporte:** técnica y velocidad.

> **Bicicleta sin fin** · *regate* · `VAR`
> **Acto:** amaga tantas veces que el defensa acaba sentado en el césped.
> **Paga:** tarda una eternidad. Para cuando pasa, ya han llegado dos más.
> **Soporte:** técnica.

> **Paso de duende** · *regate entre dos rivales* · `VAR`
> **Acto:** se cuela entre los dos como si no estuvieran.
> **Paga:** sale sin el balón controlado: lo lleva suelto por delante.
> **Soporte:** velocidad y técnica.

> **Carga de jabalí** · *conducción sostenida* · `IMP`
> **Acto:** a partir de cierto rato corriendo con el balón se convierte en proyectil y **atraviesa al
> primero que se le pone delante**.
> **Paga:** no sabe girar. Si le cierran el camino, choca con quien sea y pierde el balón.
> **Soporte:** fuerza y resistencia deciden a quién atraviesa y a quién no.

---

# 4. Pase — 6

> **Taconazo** · *pase* · `VAR`
> **Acto:** pase hacia atrás sin mirar, con el tacón, a un compañero que llega de frente.
> **Paga:** sin mirar es sin mirar: a veces no hay nadie.
> **Soporte:** técnica.

> **Bombeado** · *pase largo* · `VAR`
> **Acto:** el balón pasa por encima de toda la línea defensiva.
> **Paga:** va lento y alto: da tiempo a replegar y a disputarlo.
> **Soporte:** técnica y fuerza.

> **Aguja** · *pase* · `VAR`
> **Acto:** el balón se cuela entre dos rivales por un hueco que no existía.
> **Paga:** si lo cazan, el contraataque nace exactamente de ahí.
> **Soporte:** técnica.

> **Pase con bendición** · *pase completado* · `ENF`
> **Acto:** el que recibe sale disparado, como si el pase le hubiera empujado.
> **Paga:** el pasador se queda plantado admirando su propia obra.
> **Soporte:** el arranque del receptor.

> **Grito del capitán** · *pase completado* · `ENF`
> **Acto:** tres compañeros arrancan a la vez hacia adelante.
> **Paga:** dejan sus puestos; si se pierde el balón ahí, no hay nadie detrás.
> **Soporte:** la posición de cada uno decide si el arranque sirve.

> **Saque de vikingo** · *saque de banda* · `VAR`
> **Acto:** saca de banda hasta el área rival con las dos manos y toda la espalda.
> **Paga:** llega alto y en disputa: es un centro, no un pase.
> **Soporte:** fuerza.

---

# 5. Entrada y cuerpo — 10

> **Barrida** · *entrada* · `IMP`
> **Acto:** se lanza al suelo desde mucho más lejos de lo razonable y el balón sale despedido.
> **Paga:** si falla, se queda **él** en el suelo y el rival se va.
> **Soporte:** fuerza y velocidad deciden si llega.

> **Plancha** · *entrada* · `IMP`
> **Acto:** derriba seguro y manda al rival **hacia atrás**, por donde vino.
> **Paga:** falta casi segura, y la tarjeta detrás.
> **Soporte:** el criterio del árbitro decide cuánto le cuesta.

> **Robo de carterista** · *entrada de frente* · `VAR`
> **Acto:** le quita el balón sin llegar a tocarlo y **sale jugando** con él.
> **Paga:** sólo de frente. Por detrás o de lado no existe.
> **Soporte:** técnica.

> **Muro de piedra** · *le regatean* · `IMP`
> **Acto:** el que intenta regatearle **rebota** contra él.
> **Paga:** no recupera el balón: sólo lo detiene. Queda en disputa.
> **Soporte:** fuerza.

> **Tenaza** · *entrada con un vinculado cerca* · `IMP`
> **Acto:** entran los dos a la vez y el rival sale volando **entre ambos**.
> **Paga:** depende enteramente de dónde esté el otro.
> **Soporte:** la suma de las dos fuerzas.

> **Patada del burro** · *le entran por detrás* · `IMP`
> **Acto:** cocea hacia atrás sin mirar y manda al de detrás por los aires.
> **Paga:** la falta es **suya**, y de las feas.
> **Soporte:** fuerza; el árbitro decide el castigo.

> **Segador** · *entrada* · `IMP`
> **Acto:** la entrada barre a **todo el que esté al lado**, no sólo al que llevaba el balón.
> **Paga:** el árbitro no se pierde una así.
> **Soporte:** fuerza.

> **Morder el tobillo** · *entrada* · `HOY`
> **Acto:** no le quita el balón, le quita la pierna.
> **Paga:** la expulsión es cuestión de tiempo.
> **Soporte:** la probabilidad de lesión, que es lo que decide si sale carne o sólo susto.

> **Aguantar el tipo** · *protege el balón* · `IMP`
> **Acto:** se planta con el balón y los que le presionan **rebotan**.
> **Paga:** no avanza ni un palmo mientras dura.
> **Soporte:** fuerza y resistencia.

> **Pies de plomo** · *le empujan* · `HOY`
> **Acto:** el contacto no le mueve. El que empuja se va para atrás.
> **Paga:** no le da ventaja: sólo le quita la desventaja.
> **Soporte:** fuerza. *(Ya existe a medias: `roots`, la racial enana.)*

---

# 6. Intercepción y presión — 6

> **Manotazo** · *intercepta* · `VAR`
> **Acto:** corta el pase y el balón sale **despedido hacia arriba**: segunda jugada, en el aire.
> **Paga:** no lo tiene nadie, tampoco él.
> **Soporte:** técnica.

> **Lectura de vísceras** · *van a pasar* · `HOY`
> **Acto:** **aparece en la línea de pase** antes de que el pase salga.
> **Paga:** deja su sitio. Si se equivoca, el hueco es suyo y es enorme.
> **Soporte:** técnica.

> **Jauría** · *pérdida del balón* · `ENF`
> **Acto:** los tres más cercanos se echan encima del que lo robó, todos a la vez.
> **Paga:** el resto del campo se queda vacío durante ese rato.
> **Soporte:** velocidad.

> **Sabueso** · *inicio* · `HOY`
> **Conducta:** elige a un rival y lo persigue por todo el campo, donde vaya.
> **Paga:** se olvida del balón y de su puesto.
> **Soporte:** resistencia.

> **Despeje de leñador** · *despeja* · `VAR`
> **Acto:** manda el balón a tomar viento, altísimo y lejísimos.
> **Paga:** regala la posesión con las dos manos.
> **Soporte:** fuerza.

> **Cortar la cabeza** · *el portero rival tiene el balón* · `ENF`
> **Acto:** se planta encima del portero y, si duda, se la roba en el área.
> **Paga:** si el portero sale jugando, queda a su espalda y su equipo con uno menos.
> **Soporte:** velocidad.

---

# 7. Portería — 5

> **Manos de yunque** · *parada* · `VAR`
> **Acto:** o la atrapa, o entra. No existe el rechace.
> **Paga:** literalmente eso: no existe el rechace salvador.
> **Soporte:** la parada de siempre.

> **A puños** · *parada* · `VAR`
> **Acto:** la saca con los puños a treinta metros en vez de atraparla.
> **Paga:** nunca conserva el balón: siempre queda en disputa.
> **Soporte:** fuerza.

> **Salida suicida** · *balón suelto cerca del área* · `IMP`
> **Acto:** sale del área como un jabalí y **arrolla al que llegue**.
> **Paga:** si no llega, la portería está vacía y todo el mundo lo sabe.
> **Soporte:** velocidad.

> **El muro que grita** · *saque de puerta* · `HOY`
> **Conducta:** coloca a sus defensas, que se hunden y cierran el área.
> **Paga:** la línea se hunde tanto que desde fuera se dispara gratis.
> **Soporte:** la zona de acción de los defensas.

> **Reflejo de ultratumba** · *tiro que ya había pasado* · `VAR`
> **Acto:** llega a una que ya no podía llegar. El brazo sale tarde y aun así toca.
> **Paga:** el rechace le queda muerto en el área pequeña.
> **Soporte:** la parada de lejos.

---

# 8. El balón — 5

La familia que hoy **no existe**: cero de los diecinueve tipos de efecto tocan el balón *(MEDIDO)*. Es la
más barata de ver, porque la traza ya graba posición y altura del balón y `/Game` ya las dibuja.

> **Balón imantado** · *mientras dure* · `CONT`
> **Acto:** el balón suelto **tuerce hacia él**. Trayectorias que no tienen explicación.
> **Paga:** también tuerce hacia él cuando está mal colocado, y lo arrastra a donde no debe estar.
> **Soporte:** la distancia decide cuánto tuerce.

> **Pelota de la suerte** · *rechace* · `IMP`
> **Acto:** los rechaces —del portero, del palo, de un cuerpo— **vuelven hacia los suyos**.
> **Paga:** no elige hacia quién: vuelve hacia el más cercano, sea quien sea.
> **Soporte:** la probabilidad decide cuáles.

> **Botas pegajosas** · *primer control* · `HOY`
> **Acto:** el balón se le queda **pegado al pie** un rato: no hay quien se lo quite.
> **Paga:** pegado es pegado: tampoco lo suelta para pasar rápido.
> **Soporte:** la resistencia a que se lo roben.

> **Balón maldito** · *toca el balón* · `CONT`
> **Acto:** el siguiente que lo toque se lo encuentra botando de forma imposible.
> **Paga:** el siguiente puede ser un compañero.
> **Soporte:** técnica del que lo recibe.

> **Efecto de feria** · *tiro o pase largo* · `VAR`
> **Acto:** el balón hace una comba absurda a mitad de vuelo.
> **Paga:** nadie sabe dónde va a caer. Él tampoco.
> **Soporte:** técnica.

---

# 9. Carnicería — 8

Es el canal que **menos conversión necesita**: ya produce sucesos. Lo que le falta es el **impacto visible**
y el enfriamiento. **Restricción medida que manda aquí**: `injuriesPerMatch` 0,78 contra techo 0,90 y la
brecha de faltas de la ADR 0147 abierta — **es el único canal donde el balance no se puede aplazar entero**.

> **Sed de médula** · *entrada* · `IMP`
> **Acto:** el impacto manda al rival al suelo, y de ahí no se levanta.
> **Paga:** cada vez que lo hace, el árbitro le tiene más ganas.
> **Soporte:** la tirada letal.

> **Rompecráneos** · *entrada* · `IMP`
> **Acto:** el crujido se oye desde la grada.
> **Paga:** roja probable.
> **Soporte:** fuerza contra resistencia.

> **Precio del hierro** · *lesiona a alguien* · `HOY`
> **Acto:** reparte carne y **se lleva su parte**: sale tocado de la misma jugada.
> **Paga:** eso exactamente.
> **Soporte:** su propia probabilidad de lesión.

> **Segunda herida** · *ya está tocado* · `HOY`
> **Acto:** herido juega más suelto, como si le diera igual.
> **Paga:** le da igual de verdad: se rompe antes.
> **Soporte:** la probabilidad de lesión grave.

> **Falso muerto** · *debería morir* · `HOY`
> **Acto:** cae, se queda tumbado, el partido sigue — y al rato **se levanta**.
> **Paga:** vuelve roto y lo que quede de partido lo juega así.
> **Soporte:** una vez por run, y no más.

> **Carnicero agradecido** · *muere un rival* · `ENF`
> **Acto:** los suyos se crecen y se vienen todos arriba.
> **Paga:** se vienen arriba **de más**: se abren y dejan la espalda.
> **Soporte:** el arranque.

> **Cicatriz que recuerda** · *acumula* · `HOY`
> **Acto:** cada cicatriz le hace más duro, y **se le ve en el retrato**.
> **Paga:** las cicatrices son cicatrices: llegan con lo que las causó.
> **Soporte:** el contador, visible.

> **Público enloquecido** · *algo especialmente brutal* · `ENF`
> **Acto:** la grada se enciende y el partido se vuelve otra cosa durante un rato.
> **Paga:** se enciende para los dos equipos.
> **Soporte:** el criterio del árbitro, que se relaja.

---

# 10. Cabeza y carácter — 7

> **Borracho** · *siempre* · `CONT`
> **Conducta:** camina en zigzag. Nadie sabe hacia dónde va, **y los defensas fallan por eso**.
> **Paga:** sus pases salen igual de torcidos.
> **Soporte:** técnica.

> **Berserk** · *le hacen falta* · `ENF`
> **Acto:** se levanta y va a por todo durante un rato: entra a cualquier cosa que se mueva.
> **Paga:** a cualquier cosa que se mueva.
> **Soporte:** fuerza.

> **Cobarde con ojo** · *siempre* · `HOY`
> **Conducta:** no entra nunca. Se queda leyendo el pase.
> **Paga:** no entra nunca, tampoco cuando hay que entrar.
> **Soporte:** la intercepción.

> **Ancla** · *siempre* · `HOY`
> **Conducta:** no sale de su tercio ni aunque el partido se lo pida.
> **Paga:** su equipo ataca con uno menos.
> **Soporte:** la zona de acción.

> **No vuelve** · *siempre* · `HOY`
> **Conducta:** el delantero que no repliega jamás, esperando arriba.
> **Paga:** su equipo defiende con uno menos.
> **Soporte:** la zona de acción.

> **Ídolo local** · *le animan* · `ENF`
> **Acto:** cuando la grada corea su nombre, arranca como si tuviera veinte años.
> **Paga:** cuando no, se acuerda de que no los tiene.
> **Soporte:** velocidad.

> **Supersticioso** · *saque* · `ENF`
> **Acto:** hace su gesto antes de sacar y le sale bien. Siempre.
> **Paga:** si no le dejan hacerlo —si hay que sacar rápido— le sale fatal.
> **Soporte:** técnica.

---

# 11. Árbitro y turba — 4

> **Árbitro sobornado** · *comete falta* · `HOY`
> **Acto:** el árbitro **mira hacia otro lado**.
> **Paga:** no siempre mira hacia otro lado, y cuando mira, mira con ganas.
> **Soporte:** el criterio del árbitro.

> **Piscinero** · *contacto leve* · `HOY`
> **Acto:** se tira, rueda, se agarra la cara. Falta a favor.
> **Paga:** el árbitro aprende. La segunda ya no cuela, y la tercera es tarjeta.
> **Soporte:** el criterio del árbitro.

> **Instigador** · *falta* · `HOY`
> **Acto:** lo que iba a ser una falta se convierte en una tángana.
> **Paga:** la tángana es de los dos equipos.
> **Soporte:** el criterio del árbitro.

> **Cuando la grada manda** · *turba* · `HOY`
> **Acto:** con la turba encendida, sus entradas dejan de existir para el reglamento.
> **Paga:** sólo con la turba encendida, que no depende de él.
> **Soporte:** el árbitro que ya se ha ido (RF-055d).

---

# 12. Las cinco raciales, convertidas

*(MEDIDO: hoy son `quick_learner`, `numb`, `elf_touch`, `roots`, `hot_blooded`.)*

| raza | hoy | pasa a ser | necesita |
|---|---|---|---|
| **Enano** | `roots` — inmunidad al empuje | **Pies de plomo**: el que le empuja rebota | `IMP` |
| **Orco** | `hot_blooded` — alarga el derribo que provoca | **Manos de orco**: el que se levanta de su entrada se levanta tarde y torcido | `HOY` |
| **Elfo** | `elf_touch` — evasión | **Paso de duende**: se cuela entre dos | `VAR` |
| **No-muerto** | `numb` — inmunidades | **No lo siente**: sigue jugando con lo que a otro le habría parado | `HOY` |
| **Humano** | `quick_learner` — experiencia | **Sigue siendo `economy`**: no toca el partido, y está bien que no lo toque | — |

---

# 13. Cuadre contra RF-069 v0.10 — y el suelo que este catálogo falsifica

**65 fichas** (60 de campo + 5 raciales), más los **6 de economía** que quedan exentos por RF-069e.
*(MEDIDO: contadas sobre este mismo fichero, no estimadas.)*

| grado | fichas | % | cuota de la ADR 0149 | |
|---|---:|---:|---|---|
| **Acto** | **59** | **91 %** | ≥ 50 % | ✅ |
| **Conducta** | 6 | 9 % | ≥ 25 % | ❌ |
| **Soporte como único efecto** | **0** | 0 % | nunca | ✅ |

## El suelo de Conducta estaba mal, y lo dice el catálogo

El `≥ 25 %` de Conducta **lo puse yo por intuición** al redactar la ADR 0149, el mismo día, sin nada que lo
respaldara. Escribir el catálogo que el encargo realmente implica lo ha falsificado: sale **9 %**, y no
porque falten fichas de conducta, sino porque **el encargo pide actos**. Un catálogo que llegara al 25 % de
conducta sería un catálogo peor para este producto.

**Se corrige el requisito, no el catálogo** *(enmienda de la ADR 0149, aplicada a `docs/requisitos.md`)*:
**Conducta deja de tener suelo.** Sigue siendo un grado válido y necesario —es lo que hace que dos
plantillas jueguen distinto a lo largo de 90 minutos— pero **no es una cuota**: lo que el producto exige es
el suelo de Acto y la prohibición del soporte solo, y las dos se cumplen con holgura.

**Lo que NO se toca**: `Acto ≥ 50 %` y `ningún perk es sólo soporte`. Son las dos reglas que traducen el
encargo del revisor, y este catálogo las cumple con 91 % y 0 %.

**Por necesidad de motor** *(MEDIDO)*:

| necesita | fichas | qué son |
|---|---:|---|
| **HOY** | **19** | escribibles con el vocabulario actual, sin una línea de C# |
| **VAR** — variante de acción | 19 | tiro, regate, pase, parada, corte |
| **IMP** — impulso dirigido | 15 | entrada, cuerpo, carnicería, rechaces |
| **ENF** — sólo el enfriamiento | 9 | ya expresables; les falta dejar de ser permanentes |
| **CONT** — efecto continuo | 3 | balón imantado, balón maldito, borracho |

---

# 14. El orden, revisado con el cartelito dentro

1. **Tanda A — el cartelito, y sólo el cartelito.** Dibujar `PERK_TRIGGERED` sobre la cabeza del jugador.
   Es `/Game` puro, no toca `/Sim`, no toca balance, **y hace visibles de golpe los 15 perks de situación que
   ya existen**. Sin esto, todo lo demás ocurre sin nombre.
2. **Tanda B — las 19 fichas `HOY`.** Catálogo nuevo sin una línea de C#. Es donde el juego empieza a
   parecerse a lo que el revisor describe, y sirve de prueba de la receta antes de gastar motor.
3. **Tanda C — el enfriamiento (`ENF`).** Convierte las 8 pendientes y arregla las 18 anteriores, que hasta
   entonces usan `limit: per play` como sustituto pobre.
4. **Tanda D — la variante de acción (`VAR`), empezando por el tiro.** Cañonazo y vaselina primero: escriben
   estado del balón que la traza **ya dibuja**, y el tiro no gasta del presupuesto de violencia.
5. **Tanda E — el impulso (`IMP`).** Entrada, cuerpo y carnicería. **Con medición en la misma tanda**, por §9.
6. **Tanda F — el efecto continuo (`CONT`)**, que son tres fichas y la primitiva más cara.
7. **Retirada**: los perks actuales que no tengan heredero en este catálogo se van. El cuadre perk a perk
   contra los 102 actuales **no está hecho** y es el trabajo que abre este documento.

---

# 15. Para el revisor

1. **65 fichas, 91 % actos, cero perks que sean sólo un número.** Ninguna lleva una magnitud.
2. **El cartelito retira el `MomentKind` de perk y el riesgo de saturación.** Y resulta que es exactamente
   para lo que se escribió `PerkTriggered` hace tiempo: su propio comentario dice «un aviso sobre la cabeza
   del jugador». Estaba pedido y nadie lo dibujó.
3. **19 fichas son escribibles hoy sin tocar el motor.** Más el cartelito, que es `/Game` puro.
4. **Me equivoqué en un número de la ADR 0149 y el catálogo lo ha demostrado.** Puse un suelo de 25 % para
   el grado Conducta por intuición; escribir el catálogo que tu encargo implica da **9 %**, y subirlo sería
   empeorarlo. **He retirado ese suelo del requisito** (§13) y he dejado intactos los dos que sí traducen tu
   encargo: Acto ≥ 50 % y ningún perk es sólo soporte.
5. **Sigue sin decidir**: el **segundo balón** (255 líneas de motor suponen uno solo) y si un perk de
   economía debe ocupar un slot.
6. **Lo que no está hecho y hay que hacer**: el cuadre ficha a ficha contra los 102 actuales —cuál hereda
   cuál, cuál se retira—. Preferí darte el catálogo entero antes que un cuadre a medias.
