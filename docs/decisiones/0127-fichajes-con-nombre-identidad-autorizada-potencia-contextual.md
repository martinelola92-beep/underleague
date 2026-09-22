# 0127 — Los fichajes tienen nombre: identidad autorizada, potencia contextual

Estado: **Propuesta** (22 sep 2026, decisión del revisor sobre el qué). Hermana de la **ADR 0126**.
Toca `/data`, `/Sim` y la economía del mercado, así que exige `game-design-review` (esta nota),
`balance-measure` (Regla D) e `independent-reviewer` (Regla E).

## Lo que pide el revisor

> «En cuanto a los fichajes (como no pertenecen a ningún clan) puede que también los hiciera "no
> aleatorios". Podemos crear un set de fichajes lo suficientemente amplio como para que no se repitan mucho
> pero que sean variados y con personalidad. Creo que la sensación para el player de acordarse de que 3
> runs antes fichó a un jugador que al final resultó ser su estrella, y volver a encontrárselo en el
> mercado, es positiva. Hace que la curva de aprendizaje merezca la pena y además poder coger cariño al
> jugador.»

## Qué existe ya, y qué no

*(LEÍDO, verificado 22 sep 2026.)*

- **La plantilla inicial del jugador YA es canónica.** `data/clubs/*.json` define un `roster` de **9
  jugadores con nombre propio, puesto y rareza** por club. `RunSetup.Roster` lo acepta; solo se genera con
  `TeamGenerator` cuando es `null`, que es lo que hacen los tests y `/Balance` porque no cargan clubes.
  **La mitad de lo que pide el revisor ya está construida.**
- **Los fichajes del mercado NO.** Salen de `GeneratedPlayers` → `PlayerGenerator.Generate`, con nombre
  del generador por raza (RF-020b) y atributos repartidos con presupuesto por nivel y rareza.
- **`playerOffers: 3`** por mercado *(LEÍDO, `data/economy/economy.json`)*.

## El riesgo de diseño, que es el motivo de esta nota

**Hoy el mercado es una prueba de evaluación. Un conjunto fijo lo convierte en una prueba de memoria.**

Hoy el jugador lee atributos, rasgos y precio y **juzga**. Con un elenco fijo, un jugador veterano
reconoce el nombre y la decisión colapsa en **recordar**: «ah, es Thrainn, ese es bueno». Las dos son
pruebas legítimas, pero **no son el mismo juego**, y el proyecto ya tiene instrumental construido para la
primera (informe de ojeo, `LineupPerkPreview`, el tooltip de composición de BB-J).

Es además la misma objeción que el análisis de la ADR 0126 levantó contra «reduce el RNG», y hay que
aplicarse el mismo criterio aquí en vez de aceptarlo por venir en otro envase.

## Decisión propuesta: el mismo principio que la 0126

> **La identidad se autoriza. La potencia es contextual.**

- **Fijo y autorizado**: nombre, raza, puesto, rasgos y estilo. *Quién es.* Es lo que permite reconocerlo
  tres runs después y encariñarse.
- **Variable por contexto**: nivel, rareza, reparto exacto de atributos dentro de su presupuesto, perks que
  trae y precio. *Cuánto vale hoy.*

Así el jugador **reconoce a la persona y sigue teniendo que juzgar la oferta**: «es Thrainn otra vez, pero
esta vez llega común y en el acto 1, y me piden lo mismo que por un raro». Conserva la decisión, añade el
apego, y **hace que la curva de aprendizaje pague** — que es exactamente lo que el revisor busca — sin
convertir el mercado en un examen de memorización.

Es, palabra por palabra, la misma arquitectura que la ADR 0126 aplica a los clanes. *Un solo principio
gobernando rivales, clanes y fichajes.*

## Lo que hay que medir antes de escribir el elenco

1. **Cuántos fichajes ve una run.** `playerOffers` es 3, pero el número de mercados por run no está medido
   aquí. De esa cifra sale el tamaño del elenco: si una run ve ~30 ofertas, un elenco de ~120-150 hace que
   el jugador vea el 20-25 % por run y se reencuentre con uno concreto cada cuatro o cinco partidas — que
   es la frecuencia que produce «anda, otra vez este» sin producir «siempre los mismos».
2. **Cuánto se repite hoy un mismo perfil generado**, como línea base.

**No se escribe ni un nombre hasta tener la primera cifra.** Es el mismo error que la ADR 0126 estuvo a
punto de cometer.

## Riesgos

1. **Balance.** Hoy los atributos de un fichaje salen de un presupuesto por nivel y rareza. Un elenco
   escrito a mano es **superficie de balance nueva**. *Mitigación, la misma que en la 0126: el elenco fija
   la forma, el presupuesto fija la suma.* Así ningún fichaje escrito a mano puede salirse de la curva.
2. **Variedad entre runs.** Es el riesgo real y no está medido: si el elenco es pequeño, tres runs seguidas
   se parecen. La cifra del punto 1 lo gobierna.
3. **RF-020b** («los nombres se producen con un generador por raza») pasa a aplicarse solo donde siga
   habiendo generación. Si clubes, rivales y fichajes son canónicos, **el generador se queda casi sin uso**
   — hay que decidir si se conserva para los canteranos y mercenarios (RF-110..114) o se retira. **No se
   retira en esta ADR.**
4. **El elenco crece con cada raza futura.** Si un día entran los vampiros, el elenco hay que ampliarlo.
   Es argumento a favor de un elenco por raza en vez de uno global.

## Lo que esta ADR NO decide

- El tamaño del elenco (depende de la medición).
- Si los canteranos y mercenarios entran en el elenco o siguen generados.
- Si un fichaje puede pertenecer al elenco **y** aparecer como jugador de un clan rival. *Recomendación
  provisional: no.* Un nombre, una identidad — la misma regla que la ADR 0126 impone a los clanes, y
  romperla aquí reintroduciría el problema que aquella arregla.

## Hermanos

ADR 0126 (clanes canónicos, mismo principio) · ADR 0123 §D9 (la meta desbloquea posibilidades, nunca
estadísticas) · RF-005, RF-020, RF-020b, RF-110..114

---

## Censo de ofertas (22 sep 2026) — MEDIDO, y dimensiona el elenco

Instrumento temporal `Sim.Tests/Analysis/_MarketOfferCensusTests.cs`, 300 runs completas jugadas de
verdad con la doctrina contextual, misma población que `matchesPerFullRun` (19,32).

| cifra | media | mín | máx |
|---|---|---|---|
| **mercados visitados por run** | **8,66** | 3 | 14 |
| — por acto (1/2/3) | 3,92 / 3,08 / 1,66 | | |
| **ofertas de fichaje por run** (sin canteranos ni mercenarios) | **25,97** | 9 | 42 |
| ofertas de mercenario por run *(aparte, RF-110..114)* | 8,66 | 3 | 14 |

Rareza: común 60,1 % · poco común 31,8 % · rara 8,1 % · **legendaria 0 %**.
Puesto: **portero 33,3 % (exacto)** · defensa 22,5 % · delantero 22,2 % · centrocampista 22,0 %.

**Son siempre 3 por mercado** — confirmado por lectura de código, no por muestreo: el bucle de
`MarketOfferGenerator` es incondicional y los filtros de oro y hueco actúan solo al comprar. Así que
«ofertas por run» = 3 × mercados, exacto.

*El desglose por rareza y puesto es una estimación declarada como tal por quien la midió: el surtido se
regeneró sobre un nodo representativo por capa en vez de reconstruir el nodo exacto visitado. El sesgo
afecta a cuántas capas contribuyen, nunca a la distribución dentro de ellas.*

### Dos hallazgos que cambian el dimensionado

**1. Los fichajes son de la raza del club** *(LEÍDO: `GeneratedPlayers.Recruit` toma la raza; solo el
mercenario toma `foreignRace`)*. **El elenco hay que multiplicarlo por cinco.**

**2. Un tercio de las ofertas son porteros, y solo hace falta uno.** `market.goalkeeperOffers: 1` fija que
el primero de los tres fichajes sea siempre portero — decisión deliberada de la ADR 0080, porque antes el
42 % de los mercados no ofrecía ninguno. Consecuencia: **8,66 ofertas de portero por run para un puesto
del que se necesita uno**. *Al escribir el elenco NO hay que replicar esa proporción: es una regla de
surtido por mercado, no una necesidad de plantilla.*

### El elenco, dimensionado

Con la regla del revisor —la mitad de los fichajes sale dos veces por run y la otra mitad una sola, «si no
lo compras en ese momento pierdes el tren»— las 25,97 ofertas equivalen a **~17,3 jugadores distintos por
run** (25,97 / 1,5).

| | ofertas/run | distintos/run | elenco por raza para reencuentro cada ~4-5 runs |
|---|---|---|---|
| campo | 17,3 | ~11,5 | **50-55** |
| portero | 8,66 | ~5,8 | **8-12** *(se repiten más, y conviene: se aprenden)* |
| **total por raza** | 25,97 | ~17,3 | **~60-65** |

**Orden de magnitud del contenido: 300-325 jugadores con nombre** (5 razas × ~62). Sumado a los 150-250 de
los clanes rivales de la ADR 0126, el proyecto pasa a tener del orden de **500 personajes escritos**. Es
asumible como trabajo delegable, pero **es el coste real de la propuesta y conviene verlo junto**.

*Palanca para reducirlo, si hiciera falta: bajar la frecuencia del portero garantizado, u ofrecerlo solo
cuando al jugador le falte uno. Es decisión aparte y no se toma aquí.*

---

## Enmienda (22 sep 2026) — el elenco es por RAZA, no por clan; y la rareza se desplaza con el acto

Aclaración del revisor:

> «Los fichajes no tienen clan asignado. Todos los clanes de una raza comparten el mismo set de fichajes.
> Solo el equipo inicial del clan es característico. […] En el mercado, a medida que avanzas, a los comunes
> los sustituyen los de rareza superior y mejores stats.»

### Dos consecuencias

**1. El elenco es por raza y lo comparten los dos clanes de esa raza.** El dimensionado medido arriba
(~60-65 por raza, ~300-325 en total) **no cambia** —ya estaba calculado por raza—, pero se confirma que no
hay que multiplicarlo por clan. Lo característico de un clan es **su plantilla inicial**, que ya existe en
`data/clubs/*.json`.

**2. La distribución de rareza del mercado debe desplazarse con el acto, y hoy NO lo hace.** *(MEDIDO)*
`GeneratedPlayers.RecruitWeights` es una constante `(60, 32, 8)` **igual en los tres actos**, y el censo lo
confirma: 60,1 / 31,8 / 8,1 agregado. Para que «a los comunes los sustituyan los de rareza superior» hace
falta **una tabla de pesos de rareza por acto** en `/data`, que hoy no existe.

*Riesgo declarado: mover esa tabla cambia el poder adquisitivo efectivo del mercado y toca `runWinRate`,
`affordableShareAtMarket` y `brokeMarketRunShare`. Es un cambio de balance, no de contenido, y va con lote.*

### El coste real de esta ADR, ya cerrado

**~300-325 jugadores con nombre** (5 razas × ~62). Es **el grueso del contenido** de las dos ADR juntas:
la 0126 son ~110-120. *Si hubiera que recortar, la palanca es aceptar más repetición dentro de una run —
el elenco por raza, no los clanes.*
