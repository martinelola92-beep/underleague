# Modelo de datos

Concreta RT-030 a RT-035, RT-060, RT-061b. El esquema del estado de la run se define **antes** de implementar sistemas (RT-030) y está versionado. Versión actual: **0** (borrador, sin código).

## Estado de la run (`Run`)

Corrige tres desajustes del bloque de RT-030 respecto al resto del documento (ver `pendientes.md` I-1 a I-3): cinco atributos, un único slot de equipo, vínculos solo positivos.

```
Run
  schemaVersion         int
  seed                  ulong
  division              third | second | first | continental | world   (RF-128)
  club                  id de club inicial (RF-004)
  act                   1..3
  currentNode           id
  gold                  int
  nodeHistory[]         (nodeId, kind, result)
  map                   grafo del acto (nodos, aristas, rival asignado, modificador de jefe oculto/revelado)
  referees[]            6-8 árbitros de la run: id, name, trait, bribesReceived (RF-061b, RF-064c)
  rerollsUsed           int  (RF-071b, coste creciente)
  dataSnapshot          copia de /data congelada al empezar (RT-061b)
  Roster
    Player
      id                int, asignado en orden de creación
      name              generado por raza (RF-020b)
      race              id
      position          goalkeeper | defender | midfielder | forward   (RF-022b)
      rarity            common | rare | legendary                        (RF-023)
      level             1..8 (resurrección: máximo -2, RF-096)
      experience        int
      attributes        { strength, speed, technique, stamina, leash }  1..99  (RF-022)
      traits[]          1..3 ids  (RF-022c)
      tags[]            raza + posición + rasgos + adquiridas (Scrap, Automaton, Rotting, Stranger)  (RF-022d)
      perks[]           ids, tamaño máximo = slots por rareza
      item              id de objeto o null  (RF-076: un único objeto)
      physicalState     healthy | minorInjury | severeInjury | dead  (RF-090)
      minorInjuries     int (acumulables, RF-091)
      prostheses[]      (slot, effect)  (RF-095)
      wage              int, 0 salvo mercenarios  (RF-111)
      isMercenary       bool
      isYouth           bool  (+33% experiencia, RF-114c)
      matchesBenched    int  (mercenarios, RF-111)
      bonds[]           (otherPlayerId, kind: partnership | bloodDebt | stonewall)  máximo 2  (RF-101, RF-102)
      mourning          partidos restantes, 0 si no aplica  (RF-104)
      counters{}        acumuladores de perks entre partidos  (RF-070)
      bondProgress{}    contadores parciales (asistencias A->B, partidos sin encajar como pareja)
  Lineup
    assignments[]       (playerId, column, row)  con portero en casilla fija  (RF-041)
    doubleSize[]        jugadores que ocupan 2 casillas  (RF-033)
  Consumables
    equipped[]          (id, mode: manual | conditional, trigger)  máximo 3, mínimo 1 manual  (RF-080..082)
  Achievements
    progress{}          contadores de logros de desbloqueo (RF-125b)
```

Fuera de la run, en el perfil del jugador: razas desbloqueadas, divisiones ganadas por raza (RF-128b), perks/objetos/consumibles desbloqueados (RF-126), compendio de modificadores de jefe descubiertos (RF-014b), memorial acumulado.

## Estado del partido (`MatchState`)

Es lo que recibe `Simulator.Run`. Se construye desde `Run` y no vuelve a ella salvo a través de los eventos.

```
MatchState
  teams[2]              plantilla en campo, alineación, consumibles equipados, es local
  referee               id, rasgo, criterio inicial (0 salvo objetos como "Amigo de la federación")
  ruleModifiers[]       0..3 (jefes, RF-001b/c, RF-128)
  pitch                 16x5 (RF-040); en turba 16x3 útil, filas invadidas fijas (RF-055b)
  manualActivations[]   (consumableId, tick)   ver arquitectura.md
```

## Ficheros de `/data`

Todos con esquema JSON en `/data/schemas/` y validados por `tools/DataValidator` (RT-032, RT-083).

| Directorio | Contenido | Requisitos |
|---|---|---|
| `data/perks/` | Un perk por fichero | RF-065..072, RT-033 |
| `data/items/` | Equipamiento, con arquetipo maldito/frágil/restringido (`cursed`/`fragile`/`restricted`) | RF-075..078 |
| `data/consumables/` | Familias médico/táctico/sucio/sobrenatural (`medical`/`tactical`/`dirty`/`supernatural`), con tabla de resultados para sobornos | RF-080..085, RF-064b |
| `data/races/` | Sesgo poblacional, etiqueta de especie, distribución de etiquetas de estilo, habilidad racial, radio de cuerpo, disciplina, dimensiones de sprite, generador de nombres | RF-030..035, RF-031b, RF-020b |
| `data/tags/styles.json` | Etiquetas de estilo con su descripción y su sesgo de atributos | RF-022d (v0.9.1, ADR 0024) |
| `data/clubs/` | Raza, plantilla inicial, oro, regla especial | RF-004 |
| `data/traits/` | Rasgos de jugador y de portero, con modificadores de pesos de IA | RF-022c, RF-057e, RT-094 |
| `data/referees/` | Rasgos de árbitro y sus efectos sobre criterio y sobornos | RF-061, RF-064 |
| `data/ai/` | Pesos base por posición y por estado táctico | RT-093, RT-096 |
| `data/rivals/` | Equipos rivales diseñados a mano por acto y división | RF-015 |
| `data/bosses/` | Modificadores de regla | RF-001b, RF-014 |
| `data/economy/` | Oro por acto, multiplicadores, precios, objetivos de partido excelente | RF-114g..k |
| `data/balance/` | Configuraciones de equipos de referencia para `/Balance` | RT-052 |
| `data/l10n/` | Plantillas de descripción y textos, es/en | RT-035, RT-073 |

## Formato de perk (RT-033)

```json
{
  "id": "bloodlust",
  "name": { "es": "Sed de sangre", "en": "Bloodlust" },
  "rarity": "rare",
  "kind": "conditional",
  "trigger": "TACKLE",
  "condition": "hasTag(actor, 'Brute') && bias() < 0",
  "effect": { "type": "modifyAttribute", "target": "actor", "attribute": "strength", "value": 3, "duration": "play" },
  "limit": { "per": "match", "times": 2 },
  "accumulatesAcrossMatches": false,
  "lethal": false,
  "positionOnly": null
}
```

- `kind` (tipo de perk): `filler` (relleno, 60%), `conditional` (condicional, 30%), `ruleBreaker` (rompe-reglas, 10%) (RF-069). `/Balance` informa de la distribución real del catálogo.
- `trigger` (disparador): uno del catálogo RF-066.
- `condition` (condición): expresión NCalc. Vacía = siempre.
- `effect.type` (tipo de efecto): catálogo cerrado, cada tipo con su plantilla de descripción. Conjunto inicial de fase 1: `modifyAttribute`, `modifyLeash`, `modifyBias`, `modifyProbability` (falta, tarjeta, lesión, parada), `cancelEvent`, `repeatEvent`, `heal`, `injure`, `addCounter`, `gold`. Se amplía por ADR ligero (entrada en `decisiones/` de una línea).
- `effect.target` (objetivo): `actor` (ejecutor), `target` (receptor), `opponent` (rival), `adjacent` (adyacentes), `team` (equipo), `opposingTeam` (equipo rival), `withTag:<Tag>` (con etiqueta).
- `effect.duration` (duración): `instant`, `play`, `match`, `run`.
- `limit.per` (límite por): `play` (jugada), `match` (partido), `mob` (turba), `run`. (El ejemplo original decía `parte`; no existen partes, RF-055.)
- `lethal` (letal): `true` obliga a destacar el perk en el informe de ojeo (RF-013) y es la única vía, junto a lesión grave sin tratar, de muerte (RF-093).

## Funciones NCalc propias (RT-034)

| Función | Devuelve |
|---|---|
| `hasTag(who, 'Tag')` | bool. `who` es `actor` (ejecutor), `target` (receptor) u `opponent` (rival) |
| `isMob()` | bool, partido en gol de oro |
| `bias()` | int, -100..100 (criterio del árbitro) |
| `zone(who)` | `'Own'` (propia), `'Middle'` (centro), `'Opposing'` (rival) |
| `vinculado(who, 'beside'\|'ahead'\|'behind'\|...)` | bool, existe vínculo direccional en esa relación, resuelto **antes** del partido (RF-044 v0.9.1, ADR 0021) |
| `distanceToGoal()` | int, casillas |
| `scoreDiff()` | int, diferencia de goles desde el punto de vista del equipo del ejecutor |
| `tick()` | int |
| `counter('name')` | int, contador del ejecutor (RF-070) |

Las expresiones se compilan una vez al cargar. Un identificador desconocido es error de validación, no error en partido.

## Descripciones generadas (RT-035)

Cada `effect.type` tiene una plantilla en `data/l10n/<lang>/templates.json`, con parámetros del efecto y de la condición:

```
modifyAttribute: "{target} gana {value:+} de {attribute} durante {duration}{conditionText}{limitText}"
```

Las condiciones NCalc se traducen con un pequeño *pretty-printer* por función (`hasTag(actor,'Brute')` -> "si es Bruto"). Nunca hay campo `description` en el JSON; si aparece, el validador lo rechaza.

## Objetos y consumibles

Comparten `effect`, `condition` y `limit` con los perks. Diferencias:

- Objeto: `archetype` (arquetipo: `cursed` (maldito) con `drawback` (contrapartida), `fragile` (frágil) con `uses` o `breaksOnInjury`, `restricted` (restringido) con `requiresTag`), `rarity`, `sellValue` (valor de venta) (RF-076b, RF-077).
- Consumible: `family` (familia), `allowedTriggers` (disparadores permitidos) (RF-083), `isBribe` (es soborno) con `outcomeTable[]` (tabla de resultados) de `(outcome, baseProbability)` ajustada por rasgo del árbitro (RF-064b).

## Versionado

- `Run.schemaVersion` y `data/schemas/version.json` suben con cualquier cambio de forma. Una run guardada con versión anterior carga con su snapshot de `/data` y una migración explícita, o se rechaza con mensaje claro. Nunca se migra en silencio.
