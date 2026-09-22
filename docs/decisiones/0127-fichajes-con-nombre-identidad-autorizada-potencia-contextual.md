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
