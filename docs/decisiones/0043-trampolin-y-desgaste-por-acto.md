# 0043. Cada acto tiene su función: taller, gestión y examen

**Fecha:** 2026-09-05
**Estado:** Aceptada (directriz del revisor)
**Requisitos:** RF-071, RF-090..094, RF-011, RF-114g..k
**Complementa:** ADR 0033 (curva revisada) y `docs/curva-de-dificultad.md`

## Decisión

Los tres actos dejan de ser el mismo juego con números más altos y pasan a tener **funciones distintas**:

| Acto | Función | Cómo se consigue |
|---|---|---|
| **1** | **Taller**: montar una build decente | Dificultad baja, poco desgaste, recompensas frecuentes. Su jefe solo castiga a quien lo ha hecho muy mal |
| **2** | **Gestión**: administrar lo que tienes | Más dificultad, **más lesiones**, decisiones con coste. Aquí se decide si tu build era buena de verdad |
| **3** | **Examen**: demostrarlo | Build ya completa; el margen lo pone lo bien que hayas construido |

## El trampolín del jefe

Superar un jefe no solo abre el acto siguiente: **cambia la trayectoria de la run**. En los roguelikes consolidados la probabilidad de victoria da un salto justo después de cada jefe, por la recompensa que sueltan (`curva-de-dificultad.md` §2.5); hoy Underleague da lo mismo tras cualquier victoria, así que el jefe es barrera sin ser trampolín.

**Recompensas escalonadas por tipo de nodo:**

| Nodo | Recompensa |
|---|---|
| Partido de liga | Oro base + 1 elección de 3 (o rechazar) |
| Partido de élite | Más oro + 1 elección de 3 con rareza mejorada |
| **Jefe de acto** | Mucho más oro + **dos perks** en vez de uno + curación de la plantilla |

Los **dos perks del jefe** son la propuesta del revisor y son la pieza que convierte el final del acto 1 en el trampolín que permite afrontar el acto 2: se sale del taller con la build ya armada.

La **curación tras el jefe** cierra el ciclo de desgaste de cada acto: permite exprimir la plantilla durante un acto sabiendo que habrá alivio, en lugar de administrar una ruina uniforme durante toda la run.

## Desgaste creciente

Hoy la probabilidad de lesión es la misma en los tres actos. Pasa a **escalar por acto**: poco en el 1, notable en el 2, alto en el 3. Es lo que hace que la gestión de plantilla —clínica, suplentes, mercenarios, jugar en inferioridad— sea un problema real a partir del acto 2 y no antes.

Se implementa como multiplicador por acto sobre las probabilidades de lesión, en datos, sin tocar las fórmulas.

## Consecuencias

- `data/economy/` gana las recompensas por tipo de nodo; el generador de recompensas deja de ser uniforme.
- **Rechazar la recompensa** pasa a ser posible (RF-071 obliga hoy a elegir una de las tres): con perks irreversibles y slots limitados, coger el menos malo puede ser peor que no coger nada.
- El nodo de **élite** adquiere por fin su función: más riesgo y más premio, que es lo que hace del mapa una decisión y no un pasillo.
- Hay que recalibrar los tres jefes contra la curva revisada de la ADR 0033, sobre todo el del acto 1, que estaba ajustado para ser un examen y ahora es un taller.
- Las métricas de run cambian de forma esperable: más victorias en el acto 1, y la mayoría de las derrotas desplazadas al acto 2, que es donde el jugador debe descubrir si construyó bien.
