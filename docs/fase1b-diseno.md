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

- **`value` en unidades enteras redondas, con escala propia por tipo de efecto.** La escala `5, 10, 15, 20, 25, 50` es de **puntos porcentuales** y aplica **solo a `modifyProbability`** (el cargador multiplica por 100 para la base interna de 10.000). Los demás efectos no son porcentajes y tienen su propia escala, porque un `+20` de fuerza sobre un presupuesto total de ~290 puntos sería enorme y un `+5` de correa es, según `docs/balance/fase1-perks.md`, el efecto más potente del juego:

| Tipo de efecto | Unidad | Escala permitida |
|---|---|---|
| `modifyProbability` | puntos porcentuales | 5, 10, 15, 20, 25, 50 |
| `modifyAttribute` | puntos de atributo (1-99) | 3, 5, 8, 10 (y sus negativos) |
| `modifyLeash` | casillas de extensión de zona | 1, 2 |
| `modifyBias` | puntos de criterio (−100..100) | 10, 15, 20 |
| `setState` | ticks | 5, 10, 15 |
| `addCounter` | unidades de contador | 1, 2 |

El validador rechaza valores fuera de la escala de su tipo.
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

2. **La conversión ×100 y la escala 5/10/15/20/25/50 se aplican solo a `modifyProbability`.** Es el único canal que vive en base 10.000. Los puntos de atributo, las casillas de correa, los puntos de criterio del árbitro, los ticks de derribo y el porcentaje de experiencia son unidades propias y el cargador no las toca. La escala se comprueba también en el esquema, con un `if/then` sobre el tipo de efecto, para que un dato malo caiga en el validador antes que en el cargador.

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
