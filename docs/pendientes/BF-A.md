# BF-A — `elf_brawler`, una build mala a propósito, gana el 46,6 % contra su referencia

Estado: **abierta, medida**. Aparece como el **único rojo verdadero** de las 43 puertas tras la ADR 0131,
que hizo que la puerta promediara ocho plantillas en vez de una.

## Síntoma

`badBuildsLoseToNone_elf_brawler` = **46,64** contra un techo de **45** (media de 8 bases de semilla,
sd 1,61, error típico 0,57). El rango dice que **una build mal construida a propósito** —`elf_brawler`,
perks de pelea puestos sobre elfos técnicos— debe quedarse entre el 45 y el 55 % contra la misma plantilla
sin perks: *un perk mal puesto no hace nada, ni resta ni suma*. Gana un punto y medio más de lo permitido.

**No es ruido de semilla**: con una sola salía 7 de 8 veces fuera; con las ocho promediadas la media
también está fuera. Es lo primero que la puerta nueva afirma con solvencia.

## Por qué importa

Es la afirmación de fase 1 sobre que **las decisiones de construcción importan**: si equivocarse eligiendo
perks sale casi neutro, el jugador no está decidiendo nada al elegirlos. No es una métrica de sensación de
partido, es la del bucle de meta.

## Lo que NO se sabe

- Si los perks de `elf_brawler` son «malos» de verdad para un elfo o si el catálogo tiene perks tan
  genéricos que casi nunca estorban. *(Sospecha principal, sin medir.)*
- Si el techo de 45 sigue siendo el número correcto con el catálogo actual de 102 perks: se fijó cuando el
  catálogo era más pequeño. **No se toca sin medir la distribución** — y el precedente de la ADR 0131 es
  justo el contrario: medir mejor antes de mover el rango.
- Cuánto de esos 1,64 puntos viene de uno o dos perks concretos. `perks.csv` del modo `--builds` da la
  activación por perk y build: es la medición barata que discrimina.

## Antes de tocar nada (Regla A)

1. **Un perk concreto lleva la ventaja** — *verificación:* `--builds elf_brawler --vs elf_none` con
   `perks.csv` y mirar activaciones y tasa por perk.
2. **El catálogo es demasiado genérico** (ningún perk estorba de verdad fuera de su sitio) — *verificación:*
   la misma medida sobre las otras dos builds malas (`elf_out_of_zone` 44,53 IN, `orc_misplaced`).
   Que una esté dentro y otra fuera discrimina entre 1 y 2.
3. **El techo de 45 es lo que está mal** — la más cara de descartar y la única que exige ADR.

## Hermanos

`docs/decisiones/0131-*.md` (la puerta que lo destapó) · ADR 0033 y `docs/fase1-diseno.md` §8 (de dónde
sale el 45-55) · `docs/pendientes/BE-A.md` (la otra métrica de diferenciación, `buildsWinDifferently_injuries`
en 1,29, que es deuda de diseño del mismo color: la build de contacto solo lesiona un 29 % más que la técnica)
