# BH-B — El nodo de jefe se presenta con el nombre de un clan de liga, en el mapa y en el ojeo

**Estado:** Abierta, **CONFIRMED con experimento**. Encontrada por la revisión independiente del paquete
BE (23 sep 2026), buscando por `OpponentId` donde la auditoría de [BE-F](./BE-F.md) había buscado por
`IsMatch` — y ese fue justo el error de método.

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

## Antes de tocar nada

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
