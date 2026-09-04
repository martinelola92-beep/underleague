# Bloque de rediseño espacial: especificación de implementación

Cierra el diseño acordado en las ADR 0020-0029 y define el **contrato exacto** contra el que se programa. Los subagentes implementan contra este documento; lo que no esté aquí se decide con el criterio más simple y determinista y se anota. Convenciones de `fase0-diseno.md` vigentes (enteros, orden determinista, sin E/S en `/Sim`, identificadores en inglés).

Todos los valores numéricos de este documento son **valores de partida a calibrar** en el paso de reajuste, no verdades.

## 1. Contrato de datos

### 1.1 `data/races/<id>.json`

```json
{
  "id": "Elf",
  "name": { "es": "Elfos", "en": "Elves" },
  "speciesTag": "Elf",
  "styleTagWeights": { "Fine": 70, "Bulwark": 12, "Brute": 10, "Cold": 8 },
  "launch": true,
  "cellsOccupied": 1,
  "bodyRadius": 30,
  "discipline": 35,
  "attributeBias": { "strength": -12, "speed": 6, "technique": 14, "stamina": -6, "leash": 1 },
  "ability": "elf_touch",
  "description": { "es": "...", "en": "..." },
  "individualDeviation": 12,
  "traitWeights": { "...": 0 },
  "names": { "first": ["..."], "last": ["..."] }
}
```

- `speciesTag`: etiqueta fija de especie (ADR 0024). Sustituye al antiguo `tag`.
- `styleTagWeights`: distribución de la etiqueta de estilo, pesos enteros que suman 100. La dominante entre 60 y 85; obligatoriamente al menos una **opuesta** a la identidad de la raza.
- `bodyRadius`: en centésimas de casilla (30 = 0,30). Enano 30 · humano 32 · elfo 30 · orco 38 · demonio 55.
- `discipline`: 0-100, cuánto tira de vuelta a su zona (ADR 0028). Enano 80 · humano 55 · elfo 35 · orco 45.
- `ability`: id del perk de habilidad racial (ADR 0026), en `data/perks/`.
- `description`: texto a mano, una frase, reglas de `estilo-descripciones.md`.

### 1.2 `data/tags/styles.json` (nuevo)

```json
{
  "Brute":   { "name": {"es":"Bruto","en":"Brute"}, "description": {"es":"Busca el contacto. Gana duelos y reparte daño.","en":"..."},
               "attributeBias": { "strength": 10, "speed": -2, "technique": -8, "stamina": 4, "leash": -2 } },
  "Fine":    { "...": "..." },
  "Bulwark": { "...": "..." },
  "Cold":    { "...": "..." },
  "Neutral": { "...": "..." }
}
```

El `attributeBias` del estilo es lo que hace que un elfo `Brute` sea de verdad más fuerte que un elfo medio (ADR 0024).

### 1.3 `data/sim/tuning.json` — secciones nuevas

```json
"bodies": {
  "separationEnabled": true,
  "maxPushPerTickMilli": 60,
  "massStrengthWeight": 60,
  "massRadiusWeight": 40,
  "tacklePushMultiplier": 250
},
"actionZone": {
  "_doc": "Forma de la zona por posición, en casillas relativas a la casilla-hogar efectiva (ADR 0028). -1 = sin límite en esa dirección.",
  "shape": {
    "Goalkeeper":  { "forward": 1, "back": 0, "sides": 1 },
    "Defender":    { "forward": 3, "back": -1, "sides": 2 },
    "Midfielder":  { "forward": 5, "back": 4, "sides": 3 },
    "Forward":     { "forward": -1, "back": 1, "sides": 2 }
  },
  "scaleFromLeashPercent": { "at1": 60, "at99": 150 },
  "outerLimitMultiplier": 200,
  "outsidePenaltyPerCell": 120,
  "disciplineWeightPercent": 100,
  "retreatBonusOutsidePerCell": 90
},
"generation": {
  "budgetByRarity": { "common": 250, "rare": 275, "legendary": 300 },
  "budgetPerLevel": 8,
  "attributeFloor": 25,
  "attributeCap": 92,
  "rangeByRarity": { "common": { "min": 40, "max": 70 }, "rare": { "min": 45, "max": 78 }, "legendary": { "min": 50, "max": 86 } },
  "positionShare": {
    "Goalkeeper": { "strength": 22, "speed": 24, "technique": 22, "stamina": 22, "leash": 10 },
    "Defender":   { "strength": 26, "speed": 18, "technique": 16, "stamina": 22, "leash": 18 },
    "Midfielder": { "strength": 16, "speed": 20, "technique": 24, "stamina": 22, "leash": 18 },
    "Forward":    { "strength": 20, "speed": 24, "technique": 24, "stamina": 16, "leash": 16 }
  },
  "positionFloors": {
    "Goalkeeper": { "strength": 40, "speed": 40, "technique": 40, "stamina": 40 },
    "Defender":   { "strength": 40 }, "Midfielder": { "technique": 38 }, "Forward": { "speed": 38 }
  },
  "traitCountWeights": [50, 35, 15],
  "goalkeeperTraitChance": 5000
},
"progression": { "...": "sin cambios" }
```

**Reparto del presupuesto**: el presupuesto de la rareza más `budgetPerLevel × (nivel−1)` se reparte según `positionShare` (porcentajes que suman 100), se le suman `attributeBias` de raza y de estilo, se aplica la desviación individual, y el resultado se **renormaliza** al presupuesto y se acota a `rangeByRarity` y a `positionFloors`. El algoritmo exacto (renormalización iterativa con topes) lo fija el implementador y lo documenta; requisito: la suma final es igual al presupuesto ±2 y ningún atributo viola su rango.

Comprobación obligatoria (ADR 0027): común de nivel 8 (presupuesto 306) ≈ legendario de nivel 2 (308).

### 1.4 `data/perks/<id>.json` — formato revisado

```json
{
  "id": "wall_of_beef",
  "name": { "es": "Muro de carne", "en": "Wall of beef" },
  "rarity": "rare",
  "kind": "conditional",
  "axis": "alignment",
  "race": null,
  "trigger": "MATCH_START",
  "scope": "actor",
  "links": ["beside"],
  "condition": "",
  "effects": [ { "type": "modifyProbability", "target": "linked", "probability": "tackle", "value": 10, "duration": "match" } ],
  "elseEffects": [],
  "limit": null,
  "accumulatesAcrossMatches": false,
  "lethal": false,
  "positionOnly": null,
  "tagsRequired": [],
  "tagsForbidden": []
}
```

Cambios respecto a la fase 1:

- **`value` en puntos porcentuales enteros** de la escala `5, 10, 15, 20, 25, 50` (el cargador multiplica por 100 para la base interna). El validador rechaza valores fuera de la escala.
- **`race`**: `null` (universal) o id de raza (exclusivo, ADR 0023). Restricción de **aparición**, no de asignación.
- **`axis`**: uno de `identity`, `accumulation`, `alignment`, `startZone`, `geometry`, `matchState`, `composition`, `proximity` (`docs/perks-ejes.md`). El validador comprueba la distribución del catálogo.
- **`links`**: relaciones direccionales que el perk necesita (ADR 0021), de `beside`, `ahead`, `behind`, `left`, `right`, `diagonalAhead`, `diagonalBehind`. Resueltas **una vez** al construir el partido. Habilita `target: "linked"` y `target: "linkedWithTag:<Tag>"`.
- Se retira `hasTag` sobre etiqueta de **especie** en perks universales (el validador lo rechaza si `race` es `null`).

### 1.5 Funciones de condición nuevas

`startsIn(who,'OwnThird'|'Middle'|'AttackingThird')` · `startsOn(who,'LeftFlank'|'Center'|'RightFlank')` · `linked(who,'beside'|...)` · `nearAlly(who,'Tag',cells)` · `nearOpponent(who,'Tag',cells)` · `stat(who,'goals'|'passesCompleted'|'tacklesWon'|'shots'|'saves')`.

## 2. Motor

### 2.1 Cuerpos (ADR 0020)

Al final de cada tick, tras mover a todos:

1. Para cada par `(i, j)` con `i < j` por índice ascendente, si `dist < r_i + r_j` y ninguno está en `Injured`/`SentOff`: `overlap = r_i + r_j − dist`.
2. `mass = strength × massStrengthWeight/100 + bodyRadius × massRadiusWeight/100`. El desplazamiento se reparte inversamente a la masa: el ligero se lleva la parte mayor.
3. Los desplazamientos se **acumulan en un buffer** y se aplican **todos al final**, nunca sobre la marcha (Jacobi). Tope `maxPushPerTickMilli` por jugador y tick.
4. Al resolver `TACKLE`, el empuje sobre el receptor se multiplica por `tacklePushMultiplier`.
5. El balón no participa. La raza `Dwarf` (habilidad Raíces) recibe desplazamiento 0.

**Determinismo**: aplicar los empujes sobre la marcha haría que el orden del bucle cambiara el resultado. Es el error que produjo el sesgo por id en la fase 0 y hay un test que lo vigila.

### 2.2 Zona de acción (ADR 0028)

Sustituye al radio de RT-095. `Zone(player)` = rectángulo relativo a la casilla-hogar efectiva, con `forward/back/sides` de `actionZone.shape` escalados por `scaleFromLeashPercent` interpolado según el atributo `Leash`. `-1` = sin límite en esa dirección (hasta el borde del campo).

- **Blanda**: la utilidad ya no descarta acciones fuera de zona. Penaliza `outsidePenaltyPerCell × distanciaFuera × (discipline × disciplineWeightPercent/100)`, y `Retreat` gana `retreatBonusOutsidePerCell × distanciaFuera`.
- **Límite duro exterior**: `outerLimitMultiplier` sobre la zona; ahí sí se descarta y el movimiento se acota.
- `UtilityRow.LeashFiltered` pasa a `OutsideZone` con la distancia fuera.

### 2.3 Comportamiento sin balón (ADR 0022)

- Acciones nuevas `FindSpace` y `PressCarrier` en `PlayerAction`, con sus pesos en `data/ai/weights.json` y su entrada en la tabla de `StateMachine`.
- `FindSpace`: puntúa 8 candidatos (las 8 direcciones a 1 y a 2 casillas, acotados a la zona), con distancia al rival más cercano, avance hacia la portería rival y línea de pase abierta con el poseedor. Sustituye al punto fijo de `OfferSupport`.
- `PressCarrier`: objetivo el poseedor rival o, si el balón está en manos del portero rival, el portero.
- **Marcaje estable**: la asignación defensor→atacante se calcula una vez por posesión y se mantiene mientras sea válida. Preferencia por rol.
- Contraste por estado táctico ampliado hasta que la diferencia sea visible; valores a calibrar.

### 2.4 Vínculos y efectos por par (ADR 0021)

- Al construir `MatchEngine`, para cada jugador con perks que declaren `links`, se resuelven los vínculos según la geometría **relativa al sentido de ataque del equipo** (el visitante refleja columnas y bandas). Un candidato por relación: el más cercano, desempate por id ascendente.
- `target: "linked"` aplica a los vinculados; `modifyProbability` con objetivo vinculado se aplica **cuando el vinculado es el sujeto de la resolución** (por ejemplo, `pass` mejora cuando el portador **pasa a** un vinculado).
- Sin candidato para la relación, no hay vínculo: se aplican los `elseEffects`.

## 3. Paquetes de trabajo

Fronteras de ficheros **exclusivas**: ningún paquete toca los ficheros de otro.

| Paquete | Agente | Depende de | Ficheros |
|---|---|---|---|
| **P. Requisitos v0.9.1** | fast-worker | — | `docs/requisitos.md` |
| **Q. Datos y generación** | fast-worker | — | `data/races/*`, `data/tags/styles.json`, `data/sim/tuning.json`, `data/schemas/*`, `Sim/Model/*`, `Sim/Data/*`, `Sim/Generation/*`, `Sim.Tests/{Data,Generation}/*`, `tools/DataValidator/*` |
| **R. Cuerpos, zona y comportamiento** | deep-reasoner | Q | `Sim/Engine/*`, `data/ai/weights.json`, `Sim.Tests/Engine/*` |
| **S. Vínculos, efectos por par y funciones nuevas** | deep-reasoner | Q | `Sim/Perks/*`, `Sim.Tests/Perks/*`, `data/l10n/*` |
| **T. Catálogo y builds** | fast-worker | Q, S | `data/perks/*`, `data/balance/*` |
| **U. Reajuste y puertas** | deep-reasoner | R, S, T | `data/**` (valores), `Sim/Analysis/*`, `Balance/*`, `Sim.Tests/Analysis/*`, `docs/balance/*` |

Criterio de terminado de todo el bloque: RT-056 en rango, criterio de salida de fase 1 (`fase1-diseno.md` §8), métrica de RF-024 según ADR 0027, y un equipo sin legendarios capaz de ganar al jefe final.
