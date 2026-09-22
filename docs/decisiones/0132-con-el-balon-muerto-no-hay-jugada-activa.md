# 0132 — Con el balón muerto no hay jugada activa

Estado: **Aceptada** (23 sep 2026). Corrige una regla de motor que incumplía RF-057; cambia
comportamiento, así que pasa por `balance-measure`. Sin cambios en `/data`.

## Problema

`Utility.IsInActivePlay` —el criterio que RF-057 impone a todo contacto sin balón— era **pura geometría**:
cerca del balón o en el pasillo hacia la portería. No sabía si el balón estaba **en juego**.

Durante la cuenta atrás de una reanudación el balón está aparcado en el punto de saque (`ParkBall`), así
que esa geometría declaraba «en jugada activa» a todo el que estuviera cerca. Resultado: **al que iba a
sacar le entraban y le cargaban mientras esperaba**, y el empuje acumulado lo apartaba del punto.

No es una hipótesis: estaba escrito en el propio test que lo cubre —*«una entrada o un bloqueo sin balón
contra él (ADR 0030 §2, **no comprueba si el balón está en juego**)»*— y su tolerancia era el empuje de
**un** tick. Al abrir la entrada sin balón al centrocampista (ADR 0133) dejó de bastar: en la semilla 34 el
sacador se desplazaba 1,66 casillas.

RF-057 dice que solo hay contacto «entre jugadores que disputan el balón o que se encuentran en la
trayectoria de la jugada activa». Con el balón muerto **no hay jugada activa** y por tanto no hay contacto
legítimo con nadie.

## Decisión

`IsInActivePlay` devuelve `false` cuando `ctx.BallDead`.

Acota las **dos** acciones que usan ese criterio —la entrada al marcado sin balón (ADR 0105) y el bloqueo
sin balón (ADR 0030 §2)—, que es donde estaba la causa común. No hace falta tocar ninguna de las dos por
separado.

## Las diez preguntas, en corto

1. **Qué experimenta**: nadie reparte leña mientras el balón está parado esperando un saque. Antes, al que
   iba a sacar lo empujaban hasta sacarlo de su sitio.
2. **Qué decide hoy**: nada; era contacto que el jugador no podía prever ni evitar.
3. **Qué debería decidir**: nada tampoco. Esto no añade una mecánica, quita una que no debía existir.
4. **Qué regla representa**: RF-057, literal. RF-012d: nada malo sin ser previsible.
5. **Sistemas**: `/Sim` (`Utility.IsInActivePlay`). Nada en `/data` ni en `/Game`.
6. **Alternativas**: (a) comprobar `BallDead` en cada acción por separado —dos sitios que se desincronizan;
   (b) subir la tolerancia del test —esconder el síntoma; (c) donde vive la regla, que es lo que se hace.
7. **Trade-off**: menos contacto total. Se paga con menos faltas y menos lesiones, que es presupuesto que
   se recupera, no que se pierde.
8. **Estrategias**: ninguna nueva. Quita una fuente de daño que no dependía de ninguna decisión.
9. **Degeneración**: ninguna previsible; el cambio solo resta contacto.
10. **Cómo se demuestra**: el lote de referencia, y el test de AZ-A que lo destapó, en verde.

## Lo medido (500 partidos, conjunto de referencia, sobre la ADR 0133 ya aplicada)

| | `tacklesPerMatch` | sin balón | `injuriesPerMatch` | `foulsPerMatch` |
|---|---|---|---|---|
| semilla 1, sin el arreglo | 7,56 | 4,94 | 0,77 | 7,74 |
| **semilla 1, con el arreglo** | 7,55 | 4,91 | **0,73** | 7,46 |
| semilla 7, sin el arreglo | 8,59 | 3,36 | 0,83 | 6,16 |
| **semilla 7, con el arreglo** | 8,81 | 3,13 | **0,81** | 5,57 |

Las disputas del balón no se mueven —el arreglo no toca la jugada viva— y **devuelve presupuesto de
lesión** justo donde más escaso es: cuatro centésimas en la plantilla más expuesta, que es lo que hizo
asumible abrir la entrada al centrocampista.

## Efecto colateral que conviene anotar

`EndToEndProtocolDemoTests` empezó a fallar por el **suelo** de `injuriesPerMatch` (0,20 contra 0,30). No
era el arreglo: era que esa demostración comprobaba una banda de RT-056 sobre **40 partidos**, donde el
error típico de esa métrica es ~0,08 y el suelo no significa nada. Su muestra sube de 20 a 60 plantillas en
el mismo commit, y el test pasa a decir **qué** métrica rompe cuando rompe, en vez de un booleano.

## Hermanos

ADR 0030 §2 (el bloqueo sin balón, que comparte el criterio) · ADR 0105 (la entrada sin balón) ·
ADR 0133 (la que lo destapó) · AZ-A / `TheRestartTakerStandsStillDuringTheDeadBall` (el test que lo cubre).
