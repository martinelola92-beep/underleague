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

**Algoritmo de renormalización implementado** (`Sim.Generation.PlayerGenerator.GenerateAttributes`, paquete Q): el reparto inicial por `positionShare` usa el método del resto mayor (*largest remainder*) para que la suma antes de aplicar sesgos sea exactamente el presupuesto: se asigna `budget × share / 100` (división entera) a cada atributo y el resto sin repartir se entrega, de uno en uno, a los atributos con mayor resto, con empate resuelto por el orden fijo `Strength, Speed, Technique, Stamina, Leash`. Sobre ese reparto se suman `attributeBias` de raza y de estilo y la desviación individual (un dado `[-dev, dev]` por atributo, mismo orden fijo), y cada atributo se acota a `[floor, cap]` con `floor = max(attributeFloor, rangeByRarity[rareza].min, positionFloors[posición][atributo] si existe)` y `cap = min(attributeCap, rangeByRarity[rareza].max)`. Acotar aleja la suma del presupuesto cuando los sesgos empujan un atributo contra su tope; la renormalización final reparte esa diferencia de 1 en 1 en el mismo orden fijo, sumando a los atributos con hueco bajo su techo (si la suma quedó corta) o restando a los que tienen hueco sobre su suelo (si quedó larga), pasada tras pasada, hasta agotar la diferencia o comprobar que una pasada completa no mueve nada (salvaguarda de terminación para baremos mal configurados que no darían margen suficiente). Con los valores de partida de esta sección la suma final iguala siempre el presupuesto exacto, muy por debajo de la tolerancia de ±2 puntos.

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

- **`value` en unidades enteras redondas, con escala propia por tipo de efecto.** `modifyProbability` va en **porcentaje de cuota con signo** desde la **ADR 0050 P1** (`±15, ±30, ±50, ±100`, con el negativo como inverso exacto del positivo); el cargador lo convierte al multiplicador interno en base 10.000. La escala es **única para todos los canales** y la tabla de escalones por canal de la ADR 0035 queda retirada. Los demás efectos no son porcentajes y tienen su propia escala, porque un `+20` de fuerza sobre un presupuesto total de ~290 puntos sería enorme y un `+5` de correa es, según `docs/balance/fase1-perks.md`, el efecto más potente del juego:

| Tipo de efecto | Unidad | Escala permitida |
|---|---|---|
| `modifyProbability` | puntos porcentuales | escalón del canal × 1, 2, 3, 5, 10 (tabla abajo) |
| `modifyAttribute` | puntos de atributo (1-99) | 3, 5, 8, 10 (y sus negativos) |
| `modifyLeash` | casillas de extensión de zona | 1, 2 |
| `modifyBias` | puntos de criterio (−100..100) | 10, 15, 20 |
| `setState` | ticks | 5, 10, 15 |
| `addCounter` | unidades de contador | 1, 2 |

*(Retirada por la ADR 0050 P1: ya no hay escalón por canal. La tabla se conserva como registro de lo que hubo.)* Escalones por canal (ADR 0035; el escalón se fijaba para que un paso valiera aproximadamente lo mismo en impacto **relativo** sobre la base del canal):

| Canal | Base (10.000) | `step` | Valores legales |
|---|---|---|---|
| `intercept` | 250 | 1 | 1, 2, 3, 5, 10 |
| `injure` / `injury` | 40 / 40 | 1 | 1, 2, 3, 5, 10 |
| `foul` | 320 | 1 | 1, 2, 3, 5, 10 |
| `card` | 250 | 1 | 1, 2, 3, 5, 10 |
| `interceptEvasion` | contra 250 | 1 | 1, 2, 3, 5, 10 |
| `tackle` | 2.800 | 3 | 3, 6, 9, 15, 30 |
| `tackleEvasion` | contra 2.800 | 3 | 3, 6, 9, 15, 30 |
| `severeInjury` | 3.000 | 3 | 3, 6, 9, 15, 30 |
| `shotOnTarget` | 4.625 | 5 | 5, 10, 15, 25, 50 |
| `save` | 5.000 | 5 | 5, 10, 15, 25, 50 |
| `dribble` | 7.200 | 5 | 5, 10, 15, 25, 50 |
| `pass` | 7.700 | 5 | 5, 10, 15, 25, 50 |

El validador rechaza valores fuera de la escala de su tipo. La comprobación de `modifyProbability` la hace el **cargador** (`Sim.Perks.PerkLoader`, contra los ocho valores de `Sim.Perks.ProbabilityScale`), no el esquema JSON, que solo pone la cota de cordura -100..100.
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

## 4. Decisiones de implementación del paquete R

Lo que el paquete R (cuerpos, zona de acción y comportamiento sin balón) decidió por su cuenta porque §2 no lo fijaba. Ficheros: `Sim/Engine/{ActionZone,BodySeparation,Marking}.cs` (nuevos), `Sim/Engine/{MatchPlayer,Utility,MatchEngine,MatchReport,MatchPhase,StateMachine}.cs`, `data/ai/weights.json`, `data/schemas/ai-weights.schema.json`, `Sim.Tests/Engine/*`.

1. **Orden de resolución del tick.** `estado táctico → bloque → cachés → marcaje → vaciado del buffer de empuje → decisiones y movimiento de los jugadores → separación de cuerpos → balón → fuera de banda`. La separación va **antes** de `UpdateBall` para que el balón, que sigue al poseedor, vea las posiciones definitivas del tick. El buffer se vacía **al principio**, no dentro de la separación, porque el empuje de una entrada se acumula durante el bucle de jugadores y tiene que caer en el mismo buffer.

2. **La separación no usa el recorrido alterno por paridad de tick.** Los pares se recorren siempre `(i, j)` con `i < j` por índice ascendente (= id ascendente). Con acumulación en buffer la suma de un tick es conmutativa, así que el orden no puede influir; usar el recorrido alterno solo habría añadido ruido. Hay un test que comprueba que el desplazamiento de cada jugador es exactamente la suma de sus contactos por pares.

3. **Dos cuerpos exactamente en el mismo punto** no tienen dirección de separación: se separan a lo largo del eje X, el de menor índice hacia las columnas bajas. Es arbitrario, pero depende solo del orden de los índices.

4. **Un cuerpo inamovible se lleva la parte del otro.** Si uno de los dos tiene `Immovable`, el otro absorbe el solape **entero**, no solo su parte por masa: contra un cuerpo con Raíces se rebota, no se atraviesa. Si los dos lo son, no se mueve ninguno.

5. **`MatchPlayer.Immovable` se siembra desde la habilidad racial, no desde la raza.** El motor compara `race.ability` con la constante `"roots"` una sola vez, al construir el jugador, y a partir de ahí solo lee la propiedad. El paquete de perks puede encenderla o apagarla por efecto sin que aparezca un `if` por raza en medio de la separación. Hay un test que la apaga en un enano y comprueba que pasa a ser empujable.

6. **El empuje de una entrada usa el contacto pleno**, la suma de los dos radios, no el solape real: una entrada llega desde más lejos de lo que se solapan dos cuerpos, así que con el solape real el empuje habría sido casi siempre cero. Y **el tope del receptor sube en el mismo `tacklePushMultiplier`**: con el tope normal (60 milicasillas) el multiplicador no se notaría, porque el empuje de una entrada lo supera con creces.

7. **El empuje no toca `Velocity` ni se acota a la zona.** `Velocity` es el desplazamiento propio del jugador y la lee la anticipación del pase; que a uno lo empujen no significa que vaya hacia allí. Y que un empujón te saque de tu zona es justo lo que tiene que poder pasar: la utilidad ya paga por volver.

8. **La zona vive en un marco local** `u = (X − hogar.X) × direcciónDeAtaque`, `v = Y − hogar.Y`. El mismo dato de forma describe la misma geometría para los dos equipos y el visitante refleja columnas sin datos propios. Las extensiones se guardan en **milicasillas enteras** y `-1` (sin límite) se propaga por el escalado.

9. **La distancia fuera de la zona es la distancia al rectángulo** (longitud del vector de exceso), la generalización natural del radio que había antes. Se mide sobre el punto **ya acotado** al límite duro, que es adonde el jugador iría de verdad.

10. **La penalización lee la disciplina como porcentaje**: `outsidePenaltyPerCell × distanciaFuera × disciplina/100 × disciplineWeightPercent/100`. Multiplicar por la disciplina cruda (0-100) habría dado penalizaciones de miles de puntos contra pesos base de centenares. Todo entero.

11. **El límite duro descarta con el mismo criterio que la correa de la fase 0**: la acción se descarta solo si, acotada al límite exterior, ya no avanza al menos 0,25 casillas. Descartar por el mero hecho de apuntar fuera dejaba sin acciones a un jugador arrinconado.

12. **`Retreat` mide su bono sobre la posición actual, no sobre el objetivo.** El objetivo de replegar es la casilla-hogar y por definición está dentro de la zona; el bono tiene que crecer con lo lejos que está el **jugador**.

13. **`CoverSpace` recorta el segmento contra la zona.** El corte circular contra el radio de correa se sustituye por un recorte por franjas (slabs) que devuelve el punto por el que la recta balón→portería propia **entra** en la zona, es decir, el punto más adelantado que ese jugador puede cubrir. Si la recta no cruza la zona, se cubre el punto de la recta acotado a la zona.

14. **`LeashCells` sobrevive como la extensión lateral de la zona.** Es la única dirección finita en las cuatro posiciones de la tabla de formas, así que sirve de escalar representativo de "cuánta correa tiene" y hace que el efecto `modifyLeash` siga sumando exactamente lo que dice. Las constantes puente `minCells = 1` y `cellsPer99 = 8` que dejó el paquete Q se retiran.

15. **`FindSpace` puntúa dieciséis puntos**: las ocho direcciones a una y a dos casillas. El "8 candidatos" de §2.3 son las ocho direcciones. Las diagonales llevan el factor 0,70711 para que "a una casilla" signifique una casilla de distancia real en las ocho. Los candidatos se acotan a la zona **blanda**, así que buscar hueco nunca es la acción que saca a un jugador de su zona. Empate por índice de candidato ascendente.

16. **`OfferSupport` no se retira.** RT-092 la exige como acción evaluable mínima y los rasgos la modulan desde `data/traits/traits.json`. `FindSpace` la sustituye **de hecho**, con pesos base mucho mayores; `OfferSupport` queda como el apoyo geométrico barato. Retirarla habría obligado a tocar `data/traits` y `Sim/Data`, fuera de las fronteras del paquete.

17. **Las dos acciones nuevas van al final del enum `PlayerAction`.** El desempate de utilidad es por orden de declaración (RT-097): ponerlas al final deja intactas las prioridades relativas de las nueve anteriores.

18. **El marcaje se asigna por avaricia con preferencia por rol.** Defensores por id ascendente; cada uno se queda con el rival libre de menor coste, siendo el coste la distancia menos 2 casillas si el rol es el preferente (defensa↔delantero, centrocampista↔centrocampista); empate por id de rival ascendente. Si no quedan rivales libres se permite doblar el marcaje. Se rehace entero al cambiar la posesión y, el resto de ticks, solo se rellenan los huecos que deja un jugador que sale del campo. Si todavía no hay asignación, `MarkOpponent` cae al rival más cercano, que es el comportamiento de la fase 0.

19. **`UtilityRow.LeashFiltered` pasa a `Rejected` + `OutsideZone` + `OutsideCentiCells`.** `Rejected` es "quedó fuera de la elección" (por su propio criterio o por el límite duro) y las otras dos son cuánto se sale la acción, en centésimas de casilla. Se conserva una propiedad `LeashFiltered => Rejected` **solo** porque `Balance/Program.cs` imprime esa columna y queda fuera de las fronteras del paquete R; el paquete U sustituye la columna y la propiedad desaparece.

20. **Los coeficientes de contexto de `FindSpace` y `PressCarrier` están en código, no en datos.** Añadir claves a `data/ai/weights.json.context` exige tocar `AiContext` y `DataLoader` (`Sim/Data`), fuera de las fronteras del paquete. Están agrupados en `Utility.cs` con el nombre exacto que tendrán como clave, para que el paquete que abra `Sim/Data` los mueva de un tirón. **Es la única deuda deliberada que deja el paquete R.**

21. **Presionar vale menos que entrar dentro del alcance de una entrada.** Con los valores iniciales (`PressCarrierBonus` 260) presionar ganaba siempre y las entradas por partido se hundían de 13,0 a 1,0, y con ellas las lesiones (0,82 → 0,05) y la mitad del contacto del juego. Presionar es acercarse al que lleva el balón; quitárselo sigue siendo entrar. Los valores de partida quedan en 120 / 60 por casilla / 200 de bono contra el portero en su salida, y los pesos base de `PressCarrier` por debajo de los de `Tackle`.

22. **El contraste táctico se amplía sobre todo en el par nuevo.** `FindSpace` va de 210 (con balón) a 15 (sin balón) y `PressCarrier` de 15 a 165: catorce y once veces. Las acciones que ya existían se mueven menos (`MarkOpponent` 30↔150, `CoverSpace` 60↔140) porque ampliarlas al mismo nivel canibalizaba `Tackle`. La diferencia entre atacar y defender se lee en el volcado de utilidad y hay un test que la exige mayor que un factor 2.

23. **Rendimiento** (600 partidos, semilla 1, `data/balance/reference.json`, misma máquina): **435,1 → 227,7 partidos/s**. La estimación de la ADR 0020 era 520 → 380 solo por los cuerpos; el resto lo pone la evaluación de los dieciséis candidatos de `FindSpace`. Sigue muy por encima de los 167 partidos/s que exige RT-051 (10.000 partidos en 60 s).

## 5. Decisiones de implementación del paquete S

Vínculos direccionales, efectos por par, funciones de condición nuevas y habilidades raciales. Frontera del paquete: `Sim/Perks/*`, `Sim/Progression/*`, `Sim.Tests/{Perks,Progression}/*`, `data/l10n/**`, `data/schemas/perks.schema.json` y los cinco ficheros de habilidad racial de `data/perks/`.

1. **El formato de perk se muda de `Sim/Data/DataLoader.cs` a `Sim/Perks/PerkLoader.cs`.** Es la única excepción de frontera del paquete, y es un traslado, no una reescritura: en `DataLoader` queda una línea (`PerkLoader.Parse(path, content)`) donde había doscientas. El motivo es que el formato del perk es el contrato del **motor de perks**: quien añade un tipo de efecto, un objetivo o una función de condición tocaba antes dos paquetes y ahora toca uno. `PerkLoader` lleva su propio lector de JSON con ruta porque el de `DataLoader` es un `private struct` de esa clase; duplicar cien líneas de lector es el precio de que el formato viva junto al motor que lo ejecuta. La otra excepción, de una línea, es la lista `TemplateSections` de `DataLoader`, que enumera las secciones legales de `data/l10n`: hay cinco nuevas.

2. **La conversión ×100 y la escala de puntos porcentuales se aplican solo a `modifyProbability`.** *(la escala única de este punto la sustituye la escala por canal de la ADR 0035; ver la tabla de §1.4)* Es el único canal que vive en base 10.000. Los puntos de atributo, las casillas de correa, los puntos de criterio del árbitro, los ticks de derribo y el porcentaje de experiencia son unidades propias y el cargador no las toca. La escala se comprueba también en el esquema, con un `if/then` sobre el tipo de efecto, para que un dato malo caiga en el validador antes que en el cargador.

3. **La etiqueta de especie se rechaza sobre el AST, no sobre el texto.** `ConditionCompiler.TagLiterals` recorre la condición ya analizada y devuelve los literales que ocupan una posición de etiqueta en la firma de la función, así que no se puede colar por espaciado, comillas ni por una función nueva que reciba etiquetas. Se comprueba también en `tagsRequired`, `tagsForbidden` y el objetivo de cada efecto. El conjunto de etiquetas de especie son los nombres de `Model.Race`, que es exactamente lo que `data/races/*.json` pone en `speciesTag`.

4. **Los vínculos los resuelve `EffectEngine`, no `MatchEngine`.** `IPerkWorld` la implementa el motor de partido y está fuera de la frontera del paquete, así que las funciones nuevas cuelgan de una interfaz aparte, `IPerkLinks`, que implementa el propio motor de efectos. Sale mejor de lo que parece: los vínculos, las estadísticas de perk y los modificadores por par son estado del **motor de perks**, y con cero perks no existe ninguno de los tres (§3, coste cero). La tabla no llega a construirse si ningún perk en campo declara relaciones.

5. **Geometría de las relaciones**, en coordenadas relativas al ataque (`avance = Δcolumna × sentido`, `derecha = Δfila × (equipo 0 ? +1 : −1)`), acotadas al radio Chebyshev ≤ 2 de la ADR 0011: `beside` = avance 0 y |derecha| 1; `ahead`/`behind` = avance ±1 y |derecha| ≤ 1; `left`/`right` = derecha ∓1 con la columna libre dentro del radio; `diagonalAhead`/`diagonalBehind` = avance ±1 y |derecha| 1. Un candidato por relación: menor distancia al cuadrado entre casillas-hogar (entera, sin raíces) y, a igual distancia, id ascendente —que sale gratis recorriendo el array de jugadores, que ya está ordenado por id (RT-041)—.

6. **El visitante refleja columnas y bandas, y eso cambia de vecino.** El motor refleja las **columnas** de la alineación pero no las filas, así que dos jugadores colocados en la misma casilla relativa tienen el mismo `ahead` en los dos equipos pero **distinto** `left`: la izquierda es siempre la del que mira a la portería que ataca, que es la convención que fija la ADR 0021 y la que corresponde a la banda izquierda real de cada equipo.

7. **El modificador por par se resuelve contra el evento en curso.** `modifyProbability` con objetivo vinculado no entra en la tabla plana `jugador × probabilidad`: entra en una lista de pares `(portador → vinculado, canal, delta)`. Cuando el motor pregunta `Modifiers.Probability(sujeto, canal)`, el par suma **solo si** el sujeto es el portador y el vinculado es una de las partes del evento que se está resolviendo. `EffectEngine.Publish` fija ese contexto en cada publicación, y funciona porque el motor ya publica `PASS_ATTEMPTED`, `TACKLE` y `SHOT` **antes** de tirar sus dados (semántica pre-resolución, `PublishBeforeResolving`). Con eso, "mejora el pase hacia el compañero de su columna" mejora ese pase y no los demás, sin tocar la firma que llama `MatchEngine`.
   - Qué evento gobierna cada canal es una **tabla explícita y cerrada** (`Modifiers.PairEventFor`), no "el último evento publicado". Si el paquete R cambia el orden y deja de publicar antes de resolver, el bono por par **desaparece**; nunca se aplica a la disputa equivocada. Es la degradación segura y es lo que hay que vigilar si algún día la semántica pre-resolución se mueve.
   - Un efecto **no** probabilístico con objetivo vinculado (atributo, correa, inmunidad) actúa sobre el vinculado directamente: ahí no hay par que resolver.

8. **`stat` lee los contadores del propio motor.** Goles, pases completados, entradas ganadas y tiros salen de `MatchPlayer`, que es de donde sale el informe post-partido (RF-119), así que no hay una segunda contabilidad que desincronizar. Las **paradas** son la excepción: el motor no lleva un total por portero (solo `ConsecutiveSaves`, que se reinicia), así que el motor de efectos las cuenta del propio flujo de eventos `SAVE`. Consecuencia documentada: `stat` devuelve el contador **en el instante en que se evalúa el perk**, y si la acción que dispara el perk ya está contada o no depende del orden interno del motor (los tiros y los goles se cuentan antes de emitir; los pases completados y las entradas ganadas, después).

9. **`nearAlly`/`nearOpponent` miden posiciones reales**, no casillas-hogar, con radio en casillas de 1 a 8 y comparación al cuadrado. Es la familia dinámica de la ADR 0021 y es lo que el jugador ve en el campo. El radio es un literal entero validado al cargar, que es la primera vez que la gramática de condiciones admite un argumento numérico.

10. **Las habilidades raciales se suscriben en el constructor de `EffectEngine`**, leyendo `race.ability` del catálogo. No están en `PlayerDefinition.Perks`, así que **no ocupan slot** (`Progression.PerkSlots`) y no hay que tocar la validación de `Simulator.Run`. Comparten motor de efectos, límites, orden de RT-041 y descripción generada, que es justo lo que pedía la ADR 0026.
    - **Consecuencia visible**: el informe post-partido trae ahora una activación de habilidad racial por titular. Es correcto (RT-043) pero cambia el recuento de activaciones que ven `/Balance` y cualquier test que contara `PerkActivations` en bruto.
    - **Hueco conocido, para el paquete R o U**: `MatchEngine` solo construye el motor de efectos si algún titular lleva perks en su lista. Un equipo sin ningún perk asignado **no** recibe su habilidad racial de partido. La corrección es una línea en esa condición (`anyPerks` debe contemplar también que la raza declare `ability`); las habilidades que actúan fuera del partido (`quick_learner`, `numb`) no se ven afectadas, y `roots` tampoco, porque el motor siembra `MatchPlayer.Immovable` desde `race.ability`.

11. **Un perk exclusivo de raza exige la etiqueta de especie para aplicarse** (ADR 0023 §4): si el portador no la lleva, el motor de efectos no lo suscribe y `Progression` no lo cuenta. No es un error de carga —el perk es legal—, simplemente no hace nada, que es lo que rompe las sinergias raciales del mercenario (RF-110/111) sin mecánica adicional. La habilidad racial se concede por la **raza** del jugador, no por la etiqueta, para que un mercenario conserve la suya.

12. **Tres tipos de efecto nuevos, todos como canal y no como caso especial.** `modifyKnockdownTicks` (Sangre caliente) abre `Modifiers.KnockdownTicks(jugador)`, en ticks; `immunity` (Raíces, No sienten nada) enciende un bit por jugador y, en el caso de `push`, además `MatchPlayer.Immovable`; `modifyExperience` (Adaptables) no hace nada dentro del partido y lo lee `Progression`. Se añaden también dos canales de probabilidad, `tackleEvasion` e `interceptEvasion` (Toque), que son la resistencia del conductor y la del pasador: los que faltaban para poder expresar "esquivar" sin invertir el signo de un canal ajeno.

13. **Estado de las cinco habilidades raciales.** Completas de extremo a extremo: **Adaptables** (humanos, en `Progression.AwardExperience` y `ExperiencePercent`), **Raíces** (enanos: el paquete R ya expone `MatchPlayer.Immovable` y lo siembra desde `race.ability`; el efecto `immunity: push` lo enciende también como dato) y **No sienten nada** (no-muertos: las dos inmunidades se consultan con `Progression.HasImmunity`; los sistemas que las consumirán —el duelo de RF-104 y la penalización de lesión leve de RF-035— son de la capa de campaña y todavía no existen). Pendientes de una línea del paquete R o U, con el canal ya abierto, probado y descrito: **Sangre caliente**, que necesita `+ Modifiers.KnockdownTicks(tackler)` en las dos entradas a `PlayerState.KnockedDown` de `ResolveTackle`; y **Toque**, que necesita `− Probability(carrier, TackleEvasion)` en el `win` de `ResolveTackle` y `− Probability(_ball.Passer, InterceptEvasion)` en el tiro de intercepción de `TryIntercept`.

14. **Las descripciones no nombran unidades internas.** El derribo más largo se dice "sus entradas dejan al rival derribado más tiempo" y no en ticks (dos claves de plantilla, una por signo); la experiencia se dice en porcentaje relativo ("gana un 25% más"), que es lo único honesto para un multiplicador; y el modificador por par tiene plantilla propia (`modifyProbabilityPaired`), porque describirlo con la de siempre —"el compañero de su columna: probabilidad de pase +10%"— diría que mejora el pase **del** vinculado, que es justo lo contrario de lo que hace.

15. **El catálogo de perks se comprueba fichero a fichero.** El test de describibilidad recorre `data/perks/` con `PerkLoader.Parse` en vez de cargar el catálogo entero, porque el paquete T lo está migrando al formato de §1.4 en paralelo; los ficheros que aún no lo cumplen se listan aparte en lugar de tumbar la carga sin contexto. La garantía fuerte la da un segundo test que exige que **toda** clave que el generador puede llegar a pedir —cada tipo de efecto, objetivo, probabilidad, inmunidad, relación, función de condición y comparación— exista en los dos idiomas, aunque todavía no haya ningún perk que la use. Los tests de perks trabajan sobre un catálogo del que se descarta `data/perks/` salvo las cinco habilidades raciales (`TestPerks.CatalogWith`): prueban el motor, no el catálogo.

16. **El valor por contador se extiende a `modifyLeash` y `modifyProbability`.** Estaba limitado a `modifyAttribute` desde la fase 1 y dejaba fuera al eje de acumulación en dos de los tres canales que suman un número: un perk que mejora la intercepción partido a partido es tan natural como uno que sube la fuerza. En `modifyProbability` lo que va en puntos porcentuales —y por tanto lo que el cargador multiplica por 100— son el incremento por unidad y el tope. Sigue prohibido en los canales que encienden un interruptor (`cancelEvent`, `immunity`), disparan un estado o mueven al árbitro, y también combinado con un objetivo vinculado: escalar un modificador por par con un contador es dos ideas en un efecto y la descripción resultante ya no cabe en una frase.

17. **`distanceToGoal` pasa a recibir un jugador**, como el resto de funciones que hablan de alguien. El implícito era siempre el actor del evento y no dejaba preguntar por el portador ni por el rival, que es lo que hace falta para un perk de portero o de marcaje. La condición se escribe ahora `distanceToGoal(actor) < 3` y las seis plantillas de comparación llevan `{who}`.

18. **Dos ficheros del catálogo del paquete T describen efectos que el motor no puede cumplir** y el cargador los rechaza, con razón: `home_ref` pone `duration` en un `modifyBias` (el criterio del árbitro se desplaza y se queda desplazado: no hay nada que expirar) y `iron_gate` usa `cancelEvent` con disparador `GOAL` (el motor solo publica de forma cancelable `FOUL`, `CARD` e `INJURY`; anular un gol exigiría una regla nueva del simulador, no un perk). Son errores de dato, no huecos del formato.

## 6. Decisiones de implementación del paquete V

Acciones de ataque diferenciadas, bloqueo sin balón, criterio del árbitro y las tres integraciones que el paquete S dejó pendientes (ADR 0030; RF-051, RF-057, RF-061..064, RT-090..098). Ficheros: `Sim/Engine/*`, `Sim/Data/{Catalog,DataLoader}.cs`, `data/ai/weights.json`, `data/sim/tuning.json`, `data/traits/traits.json`, `data/schemas/{ai-weights,tuning,traits}.schema.json`, `Balance/Program.cs`, `Sim.Tests/Engine/*` (más tres retoques de una línea fuera de frontera, decisión 14).

1. **`Pass` desaparece del enum; `ShortPass`, `LongPass` y `Block` van al final.** El desempate de utilidad es por orden de declaración (RT-097), y lo que ordena es la posición **relativa** de las acciones que quedan: quitar `Pass` del centro no altera ninguna prioridad entre las demás, y añadir las tres nuevas al final deja intactas las diez anteriores. Efecto secundario aceptado: en un empate exacto, conducir y tirar ganan ahora al pase, que antes ganaba a los dos.

2. **Las dos bandas de pase son disjuntas y exhaustivas.** Corto es `distancia <= shortPassMaxCells` (3) y largo es `> shortPassMaxCells` y `<= longPassMaxCells` (8). Ningún compañero puntúa en las dos acciones y ninguno se pierde. El portero conserva la excepción que ya tenía: no tiene tope superior en el pase largo —un saque de puerta llega a donde llega—, pero sí la misma banda inferior, porque un pase suyo de dos casillas es un pase corto como el de cualquiera.

3. **La pendiente por atributo es un término de contexto, no un multiplicador.** `pendiente × (atributo − 50)`, con signo, sumado al contexto. El pivote 50 es la definición de "jugador medio" que ya usa todo el motor (las fórmulas de `tuning.json` restan 50 en todas partes), así que vive en código como constante y no en datos: no es un valor de balance. Que sea aditivo y con signo es lo que hace que la pendiente **reparta** en vez de inflar: el torpe paga exactamente lo que cobra el brillante.

4. **El pase largo exige línea despejada; el corto no.** Es la contención declarada contra el riesgo de la ADR 0030 —el partido de balonazos de área a área—, y es la que hace el trabajo: un pase corto se cuela entre cuerpos, uno de siete casillas no. Con ella, la cadena de pases del lote de referencia **no se mueve** (1,56 antes, 1,58 después) y el reparto por tercios sigue en rango.

5. **`LongShot` modula la rampa del tiro moviendo dónde empieza, no cambiando su pendiente.** El corte binario dentro/fuera de alcance se sustituye por `shootBeyondRangePenaltyPerCell` por casilla de exceso, y el alcance sigue siendo `shootBaseRangeCells + ShootRangeBonusCells`, que es lo que el rasgo aporta desde `data/traits/traits.json` (RT-094). Así el rasgo sigue siendo dato puro y no aparece ningún `if` por rasgo en la utilidad.

6. **Criterio operativo de "jugada activa" (RF-057, matizado por la ADR 0030 §2).** Un punto está en la jugada si cumple **una** de estas dos, que es la lectura literal del requisito: está a menos de `blockActiveRadiusCells` (5) del balón —"disputa el balón"—, o a menos de `blockCorridorHalfWidthCells` (2) del **segmento** que une el balón con la portería que ataca el equipo que lo tiene —"está en la trayectoria de la jugada activa"—. Con el balón suelto no hay equipo atacante ni corredor: queda solo el radio, que es la lectura conservadora. El corredor es un segmento y no una recta infinita a propósito: por detrás del balón no hay jugada que proteger. Los dos números están en `data/ai/weights.json.context`.

7. **`Block` reutiliza el evento `TACKLE` con Detail propio (`block`, `blockFoul`, `blockMissed`) y los canales de probabilidad de la entrada.** Un bloqueo **es** una entrada sin balón: separar el tipo de evento habría dejado a todos los perks de contacto ciegos ante la mitad de los golpes del partido, y añadir un `EventType` estaba fuera de frontera. Lo que **no** hace es contar en `report.Tackles`: esa métrica de RT-056 mide disputas del balón. Los bloqueos se cuentan en `report.Blocks`, nuevo.

8. **El bloqueo comparte enfriamiento con la entrada.** Las dos son la misma carga, con y sin balón; sin un tope común, un jugador alternaba las dos y repartía golpes cada dos ticks. El precio está medido y es real: las entradas por partido bajan de 2,38 a 1,65 en el lote corto (de 4,37 a 2,31 en la puerta de 1.000). Si el reajuste necesita recuperar entradas, separar los dos contadores es una línea en `MatchEngine.Decide` —y la decisión de no hacerlo es de diseño, no de implementación—.

9. **Un bloqueo sin derribo y sin falta no tira lesión**, igual que una entrada a destiempo sin falta: el que carga se frenó a tiempo y no hubo contacto. Sin esta simetría el bloqueo era la única forma de lesionar gratis.

10. **El criterio se mueve antes de tirar las tarjetas, no después.** Dentro de una misma falta: desplazamiento del criterio (RF-063) → tirada de roja → tirada de amarilla → penalti. Es la cadena causal legible —el árbitro se enfada y **por eso** saca la tarjeta— y hace que la falta que colma el vaso sea la que se castiga, no la siguiente. Es determinista en cualquiera de los dos órdenes; se elige este por legibilidad.

11. **Tres desplazamientos del criterio que no vienen de una falta señalada** (RF-063, "el árbitro toma nota aunque no pite"): el bloqueo que no se pita (`biasShiftFoulUnseen + biasShiftBlockExtra`), la falta que un perk anula con `cancelEvent` —ocurrió, aunque no se castigue— y **toda** lesión provocada (`biasShiftInjuryExtra`), haya habido falta o no. La gravedad se compone sumando los `biasShift...` de `tuning.referee`, que es lo que pide "la magnitud depende de la gravedad".

12. **`BiasRollShift` unifica los tres efectos del criterio sobre la simulación** (RF-064: falta, tarjeta y penalti). Es la fórmula que ya estaba en `ResolveTackle` extraída a un método, con su división simétrica explícita intacta: `Math.DivRem` trunca hacia cero y por eso `-(a/b) == (-a)/b`, que es lo que hace que el sesgo tenga la misma magnitud para los dos equipos y para criterio positivo y negativo.

13. **La habilidad Sangre caliente se aplica solo al derribo que el jugador provoca a un rival**, no a las veces que se cae él mismo. §5.13 pedía sumarla "en las dos entradas a `KnockedDown` de `ResolveTackle`", pero de esas dos, una derriba al rival (entrada ganada) y la otra derriba **al propio orco** cuando falla. Aplicarla ahí habría hecho que un orco que falla una entrada tarde más en levantarse: un castigo que la descripción generada del perk —"sus entradas dejan al rival derribado más tiempo" (§5.14)— no anuncia, y por tanto un daño no previsible (RF-012d, regla 11 de `CLAUDE.md`). El canal se aplica en los dos sitios donde un jugador tumba a **otro**: la entrada ganada y el bloqueo. Está encapsulado en `MatchEngine.KnockdownTicksCausedBy`, que es el único punto que hay que tocar si aparece un tercero.

14. **El motor de efectos existe ahora en todos los partidos.** Al contemplar `race.ability` en `anyPerks` (el hueco de §5.10), y como las cinco razas de `data/races` declaran habilidad, la propiedad "coste cero con cero perks" de §3 deja de darse en la práctica. Está medido y no cuesta nada: 220,1 partidos/s en el lote de 600 (semilla 1, `data/balance/reference.json`), contra los 227,7 del paquete R y muy por encima de los 167 que exige RT-051; la diferencia la explica el bloqueo, no el motor de efectos. El test que defendía esa propiedad se reescribe para decir lo que ahora es cierto: con cero perks asignados el motor se construye igual y lo único que lleva dentro son las catorce habilidades raciales de los titulares.

15. **Tres ficheros fuera de frontera, con retoques mecánicos forzados.** `data/traits/traits.json` y `data/schemas/traits.schema.json` nombraban la acción `Pass`, que ya no existe: sin tocarlos, `/data` no carga. Se aprovecha el mismo cambio para dar a `Cerebral` sus dos multiplicadores de pase (corto 125, largo 165: es el rasgo de visión de RT-094) y a `Aggressive`, `Dirty` y `Coward` el suyo de `Block`, que es el canal de datos por el que la ADR 0030 pide "peso alto para `Aggressive`". El tercero es `Sim.Tests/Data/DataLoaderTests.cs`, que comprobaba un peso de `Pass`.

16. **La etiqueta `Brute` entra por un término de contexto, no por una tabla de etiquetas.** Un `tagMultipliers` en `data/ai/weights.json` habría sido el sitio natural, pero exige maquinaria nueva en `AiWeights` y en `MatchPlayer` para un solo consumidor. `blockBruteTagBonus` hace el mismo trabajo con una clave de contexto y deja la puerta abierta a generalizarlo cuando haya un segundo caso.

17. **`tuning.json` gana la sección `block` además de la de `referee`.** El encargo solo preveía `referee`, pero las constantes de **resolución** del bloqueo (ticks, tirada de derribo, duración del derribo, tirada de falta) son hermanas de `tackle` y `dribble` y su sitio es `tuning.json`; `data/ai/weights.json` es la tabla de **decisión**. Separar decisión y resolución es la misma frontera que ya respeta el resto del motor.

18. **La deuda 20 del paquete R queda saldada** y los seis coeficientes de `FindSpace` y `PressCarrier` viven en `data/ai/weights.json.context` con exactamente el nombre que el paquete R les había reservado. `AiContext` crece con los parámetros nuevos **con valor por defecto 0**: el cargador los pasa todos por nombre y `EnsureKnownKeys` sigue exigiendo la clave en el JSON, así que un dato ausente sigue siendo un error explícito y el valor por defecto solo lo ve el contexto sintético de los tests. `UtilityRow.LeashFiltered` desaparece y `Balance/Program.cs` imprime `rejected` y `fueraCenti`.

19. **Valores de partida, deliberadamente conservadores, y lo que costó llegar a ellos.** El bloqueo es la acción más sensible del paquete: con los primeros valores (base 300/240/200, `blockTargetBonus` 600) el lote corto daba **25 bloqueos, 22 faltas, 2,5 rojas y 37 incomparecencias de 40 partidos**. Bajando los pesos base a 80/60/55, `blockTargetBonus` a 200 y `foulBase` a 4.500 queda en 3,65 bloqueos, 2,92 faltas, 0,40 rojas y 0 incomparecencias, contra un partido de referencia que tenía 1,20 faltas y 0,10 rojas. La lección para el reajuste: el bloqueo escala **muy** rápido, porque su término de contexto compite con `FindSpace` y `MarkOpponent`, que son las acciones más frecuentes del partido.

20. **El peso base del pase largo es el mando de los tiros por partido.** Con el pase largo demasiado barato para el jugador medio (base 120 en el centro del campo, pivote de pendiente en 50), la mitad de la plantilla se queda sin pase de media distancia y conduce o remata en su lugar: `shotsPerMatch` se iba a 16,3, fuera de rango, y era la única métrica que este paquete rompía. La curva medida en el lote corto de 60 partidos, moviendo la base (portero/defensa/centro/delantero): 260/90/120/70 → 17,87 tiros; 300/140/200/110 → 17,27; 340/190/280/150 → 16,78. Se cierra en 320/170/250/135, que devuelve `shotsPerMatch` al rango y sube la cadena de pases, sin que el pase largo deje de valer la mitad que el corto en la tabla.

21. **Estado de las métricas al cerrar** (puerta de 1.000 partidos, semilla 1; entre paréntesis el valor en `63e0fe3`): `passChainAvgLength` 1,56 (1,56), `tacklesPerMatch` 2,31 (4,37), `betterTeamWinRate_60_vs_40` 47,59 (47,59), `shotsPerMatch` y el resto en rango. En el lote de `/Balance` de 600 partidos con los mismos valores: `shotsPerMatch` 15,46 IN, `ballThirdMaxShare` 36,53 IN, `injuriesPerMatch` 0,53 IN, `possessionChanges` 23,27 IN, `passChainAvgLength` 1,54 OUT, `tacklesPerMatch` 2,33 OUT, `betterTeamWinRate_60_vs_40` 61,00 OUT. Siguen rojas exactamente las mismas cuatro pruebas que ya lo estaban y ninguna nueva. La cadena de pases y el reparto por tercios (`ballThirdMaxShare` 36,96) confirman que el pase largo **no** convirtió el partido en balonazos, que era el riesgo declarado de la ADR 0030; las entradas por partido, ya bajas, bajan más por la decisión 8 y son el primer trabajo del reajuste.

## 7. Decisiones de implementación del paquete U

Reajuste único, puertas y cierre. Frontera: `data/**`, `Sim/{Analysis,Engine,Generation,Model,Perks}`,
`Balance/*`, `Sim.Tests/*`, `docs/*`. La medición completa está en **`docs/balance/fase1b-resultados.md`**;
aquí quedan solo las decisiones que este documento no fijaba.

1. **`quality` vuelve a significar lo que decía `reference.json`: la media objetivo de atributos.** El
   modelo de presupuesto de §1.3 no tenía dial de calidad y el paquete Q lo tradujo a `nivel = quality/10`.
   Con eso, calidad 60 contra calidad 40 eran dos niveles —16 puntos de presupuesto sobre 290— y
   `betterTeamWinRate` medía la varianza de la plantilla: el equipo "mejor" de calidad 60 ganaba el 40,8%
   contra uno de calidad 50. El dial pasa a desplazar a la vez el **presupuesto**
   (`+ q × AttributeCount`, cinco puntos por punto de calidad) y la **banda** de suelo y techo (`+ q` en los
   dos), de modo que un equipo de calidad 60 es exactamente uno de calidad 40 con veinte puntos más en cada
   atributo. `PlayerGenerator.QualityPivot = 50` vive en código, como el pivote 50 de las pendientes (§6.3):
   es la definición de jugador medio, no un valor de balance. `attributeFloor` y `attributeCap` son cotas
   absolutas de cordura y **no** se desplazan.

2. **Nivel y rareza son diales propios de los datos de `/Balance`.** `reference.json.teams[]` y las builds
   admiten `level` (1-8, por defecto **1**) y `rarity` (rareza uniforme para los diez jugadores; ausente
   deja la composición de RF-005, un raro entre diez). Sin ellos no se pueden expresar las métricas de la
   ADR 0027, que son comparaciones de rareza y nivel a calidad constante. El defecto de `level` pasa de 5
   (lo que daba `quality/10`) a 1: una campaña empieza en el nivel 1.

3. **Tres instrumentos de medida más en los datos, que no son mecánica de juego.** `styles` y `traits` por
   slot en una build imponen la etiqueta de estilo y añaden rasgos: sin ellos, una build que prueba
   `unlikely_bulwark` (exige `Elf` + `Bulwark`) solo es válida el 12% de las veces y el lote se cae al
   validar. `attributeBonus` en un equipo de `reference.json` suma puntos a un atributo **después** de
   generar la plantilla, que es como se mide el valor marginal de cada atributo. El dado de estilo se tira
   igual cuando la etiqueta se impone: el flujo de RNG no puede depender de si hay imposición (RT-021).

4. **Los tercios de `startsIn()` se miden sobre las columnas de colocación, no sobre el campo.** Una
   casilla-hogar vive en las columnas 0-7 relativas a la portería propia, pero `ZoneOfHome` dividía las 16
   del campo: el tercio atacante caían en las columnas 11-15, donde nadie puede colocarse, y
   `startsIn(owner,'AttackingThird')` era una condición imposible. Con `Pitch.PlacementColumns` = 8 los tres
   tercios son 0-2, 3-5 y 6-7.

5. **La alineación por defecto pasa a un 2-3-1 con columnas contiguas**: GK (0,2); DEF (2,1),(2,3); MID
   (3,2),(4,1),(4,3); FWD (6,2). La anterior dejaba a todo el mundo en columnas pares y a los compañeros de
   línea a dos filas, así que **ninguna** de las siete relaciones direccionales de la ADR 0021 encontraba
   candidato nunca: los seis perks del eje de colocación eran maluses puros. Con esta forma se resuelven
   `ahead`, `behind`, `left`, `right` y las dos diagonales, y los tres tercios de inicio quedan ocupados.
   `beside` (misma columna, filas contiguas) sigue sin resolverse con ninguna forma razonable de 7 jugadores
   en 5 filas: `pivot_duo` pasa a declarar `left`/`right`, que es lo que "el compañero de al lado" quiere
   decir, y ningún perk del catálogo usa ya `beside`.

6. **Un perk de vínculo necesita `condition: linked(owner, '<relación>')` para poder castigar.** Los seis
   llevaban condición vacía —siempre cierta—, así que sus `elseEffects` no se aplicaban jamás y una mala
   colocación salía gratis. Es una propiedad del formato que conviene tener presente al escribir perks
   nuevos: `elseEffects` responde a la **condición**, no a que el objetivo del efecto exista.

7. **El criterio del árbitro tenía un bucle de realimentación.** `biasFoulShiftPer10` valía 400 sobre un
   `foulBase` de 1.200: con el criterio medio en 35,7 puntos y el 10% de los partidos saturando en 100, una
   falta movía el criterio, el criterio disparaba la siguiente falta y el partido terminaba con 1,04 rojas
   y una incomparecencia cada cuatro partidos. Los tres `...ShiftPer10` bajan a 120/100/100 y los siete
   `biasShift...` a la mitad. El criterio sigue teniendo efecto medible (RF-064) y el test que lo defiende
   se reescribe para medirlo sobre treinta semillas: con umbrales pequeños, un partido suelto puede no
   voltear ninguna tirada y salir idéntico.

8. **`noDeadPerks` se mide por perk, no por pareja (perk, build).** El criterio de `fase1-diseno.md` §8 es
   "se activa en >= 1% de los partidos de **alguna** build que lo lleve"; la implementación lo exigía en
   todas, incluidas las builds mal construidas a propósito, cuyo sentido es justamente que sus perks no se
   disparen. Las filas por pareja se conservan como `INFO`.

9. **La escala de `modifyAttribute`, `modifyLeash`, `modifyBias` y `addCounter` se comprueba en el
   esquema.** §1.4 decía que el validador rechazaba los valores fuera de escala, pero solo estaba escrito el
   `if/then` de `modifyProbability`: había perks con 15 puntos de atributo y con 5 y 10 **casillas** de
   correa en un campo de cinco filas. No se lleva al cargador a propósito: decenas de tests construyen perks
   sintéticos con valores arbitrarios y lo que hay que garantizar es el contenido de `/data`.

10. **`Vec2.ToString()` se escribe a mano.** El `PrintMembers` generado para un `record struct` recorre las
    propiedades públicas, incluida `Normalized`, que devuelve otro `Vec2`: el `ToString()` por defecto se
    llamaba a sí mismo hasta desbordar la pila. No era teórico —cualquier aserción fallida de xUnit que
    formateara un vector abortaba la ejecución de los tests a mitad, y durante meses la suite completa nunca
    llegó a terminar cuando algo fallaba—. Vale para cualquier `record` con una propiedad calculada de su
    propio tipo.

11. **Muestra de la puerta de fase 1: 40 plantillas × 12 partidos = 480 por celda, 30 s.** Antes eran
    80 × 20 = 1.600 por celda y 85 s, por encima del minuto que se le pide a una puerta. Con 480 partidos el
    error típico de una tasa de victoria es de 2,3 puntos y el margen más ajustado de la medición de cierre
    es de 9,7 puntos, así que la puerta es estable. Semilla 1 en las tres puertas.

12. **El jefe final se monta como rival de plantilla íntegramente legendaria y nivel máximo, sin perks.** No
    existe todavía como sistema de campaña; es la lectura literal de RF-001c y lo único que cambia respecto
    de un rival normal es el presupuesto que da la rareza (300 frente a 250) y el nivel. El umbral operativo
    de "tasa razonable" de la ADR 0027 se fija en **25%**. Medido: 57,9% con una build coherente de comunes
    de nivel máximo y 38,8% con comunes sin perks, así que la ADR 0027 no hay que revisarla — con el aviso
    de que la holgura la ponen **los perks**, no el nivel.
