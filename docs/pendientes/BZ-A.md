# BZ-A — el portero puede sacar rasgos de jugador de campo que en él no hacen nada

**Observación** (anotada en `docs/project-state.md` antes del 11 oct, sin ficha). Un portero generado puede llevar
«goleador». `PlayerGenerator.PickTraits` sortea de los pesos de la raza sin mirar el puesto.

## Hipótesis (11 oct 2026)

- **H1, hay rasgos muertos en el portero.** **CONFIRMED** por lectura de `data/traits/traits.json` y
  `data/ai/weights.json`.
  - El portero tiene `Shoot` y `OfferSupport` a 0 y nunca tira, así que el multiplicador de «goleador» (Shoot,
    OfferSupport, `shotQualityBonus`) y el de «tiro lejano» (Shoot, `shootRangeBonusCells`) no hacen nada en él.
  - Los demás rasgos tocan acciones que el portero sí puntúa (ChaseBall, Tackle, Retreat, pases) o bonos generales
    (resistencia, líder).
- **H2, activación.** **CONFIRMED.** Sonda sobre 2000 plantillas de referencia (`TestMatches.Reference`): 1278 de 4000
  porteros (32 %) llevaban uno de los dos.
- **H3, la nota «cambiarlo mueve el RNG de todas las plantillas».** **REJECTED.**
  - Filtrar el pool cambia qué rasgo sale, no cuántos números se consumen. `Pcg32.Range` repite el sorteo sólo con
    probabilidad ~límite/2³².
  - Medido en esas 2000 plantillas: 0 de 36 000 jugadores de campo cambian.
  - Cambian los rasgos del 89 % de los porteros (3565 de 4000), y no sólo del 32 %: quitar entradas del pool desplaza
    el sorteo de los demás.

## Revisión de diseño (`game-design-review`)

1. **Qué ve el jugador:** un portero «goleador» en el ojeo (RF-012b) y en su plantilla. Es una promesa que no se cumple
   nunca.
2. **Qué decide con eso:** nada útil. Peor: un jugador que se lo crea puede preferir a ese portero.
3. **Qué debería ver:** rasgos que hagan algo en el puesto del jugador. RF-024c: «dos jugadores de la misma raza y
   posición no deben sentirse intercambiables»; un rasgo muerto no diferencia nada.
4. **Regla:** RF-024c (de 1 a 3 rasgos) y RF-057e (rasgos propios de portero). Ninguna cambia: sigue habiendo de 1 a 3
   rasgos sorteados, y el rasgo propio de portero sale aparte, como antes. Es la simétrica de `goalkeeperTraits`.
5. **Sistema:** `/Sim`. `Catalog.TraitActsOn` decide desde la tabla de pesos si un rasgo hace algo en un puesto, y
   `PlayerGenerator.PickTraits` filtra el pool con ella. Sin dato nuevo (`traits.json` sólo cambia su `_doc`) y sin
   cambio en `/Game`.
6. **Alternativas:**
   - (A) **elegida**, derivar de la tabla de pesos qué rasgo está muerto en cada puesto y filtrar el pool;
   - (A') una marca `outfieldOnly` en `traits.json`. Fue la primera versión; la revisión la tumbó por la Regla G: el
     repositorio ya resuelve esto contra la tabla de pesos (el techo C10 de los perks, `PerkLoader`), y una lista escrita
     a mano se desfasa en silencio cuando la tabla cambie;
   - (B) dejar el rasgo y quitarlo después. Rompe el «de 1 a 3» cuando era el único;
   - (C) volver a sortear. Consume RNG extra y cambia a los jugadores de campo que vienen detrás;
   - (D) una lista en código. Va contra RT-096 y tiene el mismo defecto que (A').
7. **Trade-off:**
   - los porteros llevan más rasgos vivos: más «rápido», «agresivo», «sucio», «cobarde»… Lo mide el lote;
   - «sucio» en el portero tiene `foulChanceBonus`, que actúa en sus entradas.
8. **Estrategia:** el ojeo del portero vuelve a decir algo.
9. **Degeneración:** ninguna esperable. Son rasgos que ya existían en el portero, con otra frecuencia.
10. **Demostración:**
    - test permanente `GeneratorTests.AGoalkeeperNeverDrawsATraitThatDoesNothingInHim` (1200 porteros, con control en
      el delantero). Fija también los casos conocidos: en el portero, los muertos son exactamente goleador y tiro lejano;
      en el delantero, ninguno. Falla sin el cambio;
    - `GeneratorTests.AGoalkeeperNeverShoots`, la premisa;
    - lote de balance y puertas.

Lo que no cambia: los jugadores de las plantillas guardadas (el rasgo ya sorteado se queda) y los rasgos impuestos a
mano por `/Balance` (`extraTraitsBySlot`), que son instrumento.

## Revisión independiente (Regla E, 11 oct)

**CONFIRMED:**
- el test discrimina;
- H3 vale en las cinco razas y en el mercado (40 000 jugadores de campo con `TeamGenerator` y 15 000 con
  `GeneratedPlayers.Recruit`, 0 cambiados);
- los datos validan;
- ningún perk, objeto, evento ni rival da efecto a un portero por las etiquetas `Scorer` o `LongShot`;
- el esquema de guardado no cambia.

Cambios que pidió, ya hechos:
- **La premisa «el portero nunca tira» era LIKELY, no CONFIRMED.**
  - El peso base de `Shoot` es 0, pero el contexto del tiro no, y tiro lejano entra en ese contexto.
  - Medido: 0 de 6720 tiros en 1000 partidos, antes y después del cambio. Queda fijado en `AGoalkeeperNeverShoots`.
- **Regla G: la marca `outfieldOnly` era una lista a mano** y el repositorio ya tiene la abstracción. Se deriva de la
  tabla de pesos (`Catalog.TraitActsOn`). La conducta es byte a byte la misma que con la marca: las huellas de
  `MobNarrowingTests` no se mueven entre las dos versiones.
- **`Range(0, 0)` lanzaba si el pool filtrado pesaba 0** (una raza con peso sólo en rasgos muertos para el puesto). Ahora
  el bucle para, igual que con el pool vacío.

Anotado, sin cambio:
- **Rasgos parcialmente muertos en el portero:** «agresivo» (`MarkOpponent` y `Block` a 0) y «vago» (`MarkOpponent` y
  `OfferSupport` a 0). Hacen algo, así que se sortean, pero el ojeo enseña también efectos que en él no existen. Es la
  clase de problema de las descripciones generadas desde el efecto (RT-035), sin ficha.
- **Una run en curso:** los rivales y los jefes se regeneran desde la semilla, así que los porteros de los nodos futuros
  cambian de rasgos tras actualizar, aunque ya se hubieran ojeado. La carga es compatible.
- **El portero sobrante que se alinea de defensa** (`RunLineup`) ya no puede sacar goleador, que ahí sí haría algo. **El
  portero de emergencia** (un jugador de campo en la portería) conserva el suyo. Los dos casos son menores.
- **Los datos a mano no se validan contra el puesto:** un portero de rival o de jefe con un rasgo muerto, o un jugador de
  campo con un rasgo de portero. Hoy no hay ninguno en `/data`.

## Balance (`--runs 3000`, semillas 1 y 2, base = el lote de BX-10b)

- Ninguna métrica cambia de estado.
- La que más se mueve, `share_over5goals` (INFO), un 6 % y un 22 % de su banda.
- `injuriesPerMatch`: −0,01 y +0,03.
- Fuera de banda sólo `betterTeamWinRate_human_60_vs_human_40`, como en la base.

**Puertas** (`Category=Gate`): rojas `elf_brawler` (45,39), `orc_violence` (57,71) y `TheThreeDoctrinesBuyDifferently`
(BU-A), las tres ya rojas u oscilantes en la base de BX-10b. `betterTeamWinRate` y `MatchOrderTests` salen en verde en
esta lectura.

**Suite** (`Category!=Gate`): 2038 de 2038 en verde, con la versión final.
