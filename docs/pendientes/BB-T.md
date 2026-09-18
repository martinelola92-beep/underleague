# BB-T — La puerta de impacto de equipamiento cae a 0,5 puntos al cuadrar el catálogo

**Estado:** Abierta. Regresión CONFIRMADA y acotada; no se arregla aquí porque el arreglo es recalibrar
objetos (ADR 0033), que es una decisión de balance aparte.

## Observación

`EquipmentImpactTests.EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` pasaba antes de cuadrar el
catálogo por raza y rasgo (19 sep 2026) y ahora falla:

> *"equipar a los siete titulares solo aporta 0.5 puntos de tasa de victoria: con eso el escalón 'muy
> buena' de la ADR 0033 no tiene contenido y los objetos están mal calibrados"*

## Causa, confirmada

La puerta **no usa ninguna build de `data/balance/builds/`**: construye sus dos equipos con
`PerkAssignment.AssignInitial(ref rng, team.Players, Catalog)` (`EquipmentImpactTests.Build`), es decir
reparte perks **del catálogo entero** y mide cuánto añaden los objetos encima de eso.

Por tanto es sensible al **tamaño y composición del catálogo**, no a las sustituciones en builds. El
catálogo pasó de 94 a 102 perks (+12 creados, −1 `unlikely_bulwark`, −3 `pack_mentality`/`shadow_marker`/
`fine_orchestra`), y con más perks repartidos el margen que dejan los objetos se estrecha.

**Verificación parcial ya hecha**: en el paso intermedio —12 perks creados, ninguno borrado— esta puerta
**pasaba** y las rojas eran las 3 de CAT-J. Fue el borrado el que la cruzó. (Antes de eso, con solo 8 de
los 12 creados y sin asignarlos a builds, también falló; se recuperó al asignarlos. Es decir: la puerta se
mueve en los dos sentidos con la composición del catálogo, lo que confirma la sensibilidad.)

## Lo que NO es

- No es un fallo de las sustituciones en builds: se corrigieron aparte dos colisiones reales (un jugador
  no puede llevar el mismo perk dos veces, `elf_brawler`), y con ellas arregladas la puerta sigue roja.
- No es CAT-J: `elf_brawler=48,96` y `passChain=1,10` siguen exactamente donde estaban.

## Qué hay que decidir

Los objetos no han cambiado; lo que ha cambiado es la referencia contra la que se miden. Opciones:

1. **Recalibrar los objetos** para que el escalón "muy buena" de la ADR 0033 vuelva a tener contenido con
   el catálogo nuevo. Es un cambio de balance con su propia ADR (RT-057: nunca un ajuste silencioso).
2. **Revisar el umbral de la puerta**, si se acepta que un catálogo más grande diluye por diseño lo que
   aporta el equipamiento. Ojo: eso es exactamente "ajustar el test para que pase", y solo vale si se
   argumenta que el umbral viejo asumía un catálogo de 94.

No se toca ninguna de las dos sin decisión del revisor.

## Hermanos

- `docs/pendientes/CAT-J.md` — las otras tres puertas rojas, preexistentes y de otra causa.
