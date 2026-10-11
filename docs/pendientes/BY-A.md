# BY-A — Las descripciones de perk enseñaban contadores internos y un disparador al revés

**Observación (11 oct 2026, captura `recompensa`):** «En una muerte, +1 al contador lifeInsuranceClaims», «Al terminar
el partido, +1 al contador boxOfficeMatches». **Estado: cerrada, con hermanos abiertos abajo.**

## Causas (CONFIRMED por lectura del consumidor, Regla I)

1. Seis perks tienen como único efecto `addCounter`, y lo que pagan vive en `data/economy`: oro por unidad de contador
   (`counter-gold.json`, ADR 0113), oro por muerte del portador (`death-gold.json`), mínimo de reventa
   (`perk-resale.json`) y herencia (`inheritance.json`). El generador (RT-035) sólo veía el contador.
2. `triggersReceived.INJURY` decía «cuando le lesionan», pero `INJURY` lleva a la víctima de actor y al causante de
   rival (`MatchEngine`, `EmitCancellable(Injury, …, victim, opponent: tackler)`): con alcance `opponent` el portador
   es quien lesiona. Sed acumulada, Pagar el hierro y Máquina de publicidad se describían al revés (RF-012d).
3. Dos contadores sin sustantivo (`survivorLosses`, `gatheringThirstVictims`).

## Arreglo

`PerkRunEffects` (Sim/Perks) + `PerkRunEffectsBuilder` (Sim/Run/Systems/Economy), parámetro opcional del generador,
como `PerkCatalog` para los maestros. Revisión independiente: cifras, inversión y cableado correctos; corrigió tres
frases que prometían otra cosa (Herencia nombraba «su vinculado» y recibe el enlace del perk, ahora derivado de
`links`; Préstamo es un mínimo sobre el estado físico, no «sin la rebaja»; Seguro de vida es la muerte del portador,
«si muere»). Tests: `CatalogDescriptionTests` (ningún identificador camelCase en el catálogo descrito, es/en).

## Hermanos abiertos (revisión independiente)

- ~~La condición de Taquillero se lee «0 bajas propias»~~: ahora «0 lesiones o muertes» (estadístico `down`).
- ~~Seis perks de `modifyTraitScalar` sin verbo~~ (Pagar el hierro, Kamikaze, Cañón…): «el portador suma +10 de…».
- ~~`DEATH` no distinguía alcance~~: secciones opcionales `triggersOwn` («si muere») y `triggersTeam` («cuando muere
  un compañero»), con vuelta a `triggers` si falta la clave.
- ~~El detector de fugas sólo ve camelCase~~: **hecho (11 oct)**. Ahora también busca snake_case, UPPER_SNAKE y, en
  español, ids de perk de una palabra, y cubre también los consumibles.
  - Medido: ninguna fuga en el catálogo real.
  - El detector ve (Regla J): sin `PerkRunEffects` salen las doce fugas de los contadores, y cada patrón tiene su caso
    conocido en `TheLeakDetectorSeesEachKindOfIdentifier`.
- Las cifras de `counter-gold.json` y `death-gold.json` están marcadas «a ojo, pendientes de medición» y ahora se
  enseñan como promesa (Regla H): el texto sigue al dato, el dato sigue sin medir.
- Sin run detrás (equipo de pruebas) `TeamState.RunEffects` es null y vuelve el texto con el contador.
