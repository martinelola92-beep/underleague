# BH-B — El nodo de jefe se presenta con el nombre de un clan de liga, en el mapa y en el ojeo

**Estado:** **RESUELTA (29 sep 2026)** en el mapa y el ojeo, y en un tercer sitio que la ficha no nombraba (el
nombre del equipo del jefe en el partido). **CONFIRMED con experimento** antes y después. Encontrada por la
revisión independiente del paquete BE (23 sep 2026), buscando por `OpponentId` donde la auditoría de
[BE-F](./BE-F.md) había buscado por `IsMatch` — y ese fue justo el error de método.

## Síntoma

El nodo de jefe guarda un `OpponentId` **fantasma** (ver [BE-F](./BE-F.md)): sintácticamente válido,
semánticamente falso, porque el equipo del jefe lo construye `BossRunSystems` desde `data/bosses/` y ese
id no lo juega nadie. `/Sim` ya no lo lee —BE-F separó las dos preguntas—, pero **`/Game` sí**, en dos
sitios que no pasan por `NodeKinds.IsMatch` y que por eso la auditoría no vio:

- `Game/Screens/MapScreen.cs:357` — resuelve el clan y añade su nombre a la fila del nodo **sin excluir
  `Boss`**, dos líneas antes de añadir «Jefe del acto N».
- `Game/Screens/ScoutScreen.cs:81` — pinta nombre y descripción del clan **antes** de la guarda
  `if (node.Kind != NodeKind.Boss)`, que sólo protege las líneas de rivalidad. Y al jefe **se entra por el
  ojeo** (`MapScreen.cs:390`).

## Medido

Tres semillas, los tres actos, **nueve de nueve** resuelven a un clan real del catálogo:

| semilla | acto | `OpponentId` del nodo de jefe | se pinta como |
|---|---|---|---|
| 1 | 1 | `act1_undead_graveshift` | Turno de Ultratumba |
| 12345 | 3 | `act3_human_allstars` | Selección de Puerto Claro |
| 99991 | 3 | `act3_dwarf_ironkings` | Reyes de Hierro |

## Por qué importa más que BE-F

BE-F hablaba de «una mentira en el historial del jugador». Aquí no es historial: es **el nombre y la
descripción que el jugador lee encima de la plantilla del jefe**, justo antes del partido más importante
del acto. Y con la ADR 0126 (clanes canónicos) y la 0127 en camino, ese nombre es identidad, no adorno.

Choca además con dos principios del proyecto a la vez: *consecuencias legibles > datos internos* e
*identidad memorable > bonus genéricos*. Un jefe que se presenta con el nombre de un equipo de liga no es
memorable: es confuso.

## Resolución (29 sep 2026)

**Causa CONFIRMED con captura** (`CapturasOjeo.tscn`, semilla 7, acto 1): el mapa decía «Academia Meridiana ·
jefe del acto 1» y el ojeo encabezaba la plantilla con «Academia Meridiana — Un equipo de manual: sin
estrellas…», encima de diez enanos que son **Los Cañones de Grimhold**. Las dos pantallas resolvían
`node.OpponentId` contra el catálogo de clanes sin mirar el tipo de nodo.

**Arreglo, en dos commits porque cruza la frontera:**

- `/Sim`: `OpponentView.For(node, bosses, rivals, language)` decide la ficha del rival **por el tipo de nodo** —
  el jefe con `BossDefinition.NameIn(language)`, el partido de liga o élite con su clan, nada en lo que no se
  juega— y `RunController.Opponent` la sirve a las pantallas. Tests: `OpponentViewTests` (tres semillas × tres
  actos: el nombre es el del jefe y no es el de ningún clan).
- `/Game`: `MapScreen` y `ScoutScreen` piden la ficha en vez de leer el id. El ojeo pone bajo el nombre
  «jefe del acto N», porque `data/bosses/` no trae descripción de ojeo (no se inventa texto de jefe).

**El tercer sitio, hallado buscando por el concepto (Regla G) y no por el identificador**:
`BossTemplate.ToTeamSetup` construía el equipo con `new TeamSetup(teamId, teamId, …)`, así que el nombre visible
del equipo del jefe era **su id de datos** («the_hunt»): lo que leen el marcador, el pregón, el log y el informe
(`MatchPlayback.RivalName`). Es el mismo defecto que RF-015 ya arregló para los rivales de catálogo
(`RivalTeamBuilder`). Ahora recibe el nombre del jefe (`TheBossTeamCarriesTheBossNameNotItsDataId`); el `Id` del
equipo no cambia.

**Lo que no se toca**: el nodo sigue **guardando** el `opponentId` fantasma (BE-F, opción 1, mueve el cursor de
`MapGenerator`). Ahora ninguna pantalla lo lee para presentar a nadie; `NemesisCaptureRunner` lo usa sólo en un
partido de liga.

**Sin resolver, fuera del encargo**: el ojeo del jefe no enseña su modificador de regla, y es a propósito
(RF-014: oculto hasta llegar; el compendio de RF-014b no está implementado). Las claves `ui.scout.boss` y
`ui.map.bossHidden` están huérfanas desde el primer commit de la UI.

## Antes de tocar nada (histórico)

Es `/Game`, así que **no puede ir en el mismo commit que un arreglo de `/Sim`** (hay hook). Y antes de
declarar nada resuelto, `visual-review`: el dato está CONFIRMED, pero lo que se ve en pantalla no se ha
capturado.

La pregunta de diseño que hay detrás no es «cómo lo oculto» sino **qué debería leer el jugador ahí**. El
jefe tiene identidad propia en `data/bosses/` —nombre, descripción—, así que lo natural es que la pantalla
pregunte por ella en vez de resolver un id que no le corresponde. Eso es contenido que ya existe y no se
está usando, no una carencia.

## Hermanos

- [BE-F](./BE-F.md) — la misma causa raíz, ya resuelta **en `/Sim`**. El dato fantasma sigue guardándose:
  mientras siga ahí, cada consumidor nuevo puede tropezar con él.
- [BH-A](./BH-A.md) — el otro pendiente nacido de una revisión independiente, también por mirar la clase
  en vez del caso.
