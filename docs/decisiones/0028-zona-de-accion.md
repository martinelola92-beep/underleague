# 0028. La correa deja de ser un radio y pasa a ser una zona de acción con forma

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor). **Modifica RF-042 y RT-095: exige subir `requisitos.md` de versión.**
**Requisitos afectados:** RF-042, RF-043, RF-045, RT-089, RT-095, RF-022

## Contexto

RF-042 define la correa como *"un radio en casillas dentro del cual puede desplazarse durante el partido. Fuera de ese radio no persigue el balón"*, y RT-095 la aplica como **filtro duro**: las acciones que exigirían salir se descartan antes de puntuar.

Un radio circular y duro produce dos problemas. El primero es de verosimilitud: en el fútbol real un centrocampista puede aparecer delante del portero rival o defendiendo su propia área, y moverse de banda a banda; con un radio, no. El segundo es de balance, y está medido: la correa es el atributo de mayor valor marginal del juego (+6,7 puntos de tasa de victoria por cada +10 en orcos, frente a +0,5 de la fuerza), porque subirla no mejora al jugador sino que **multiplica cuántas veces aplica todo lo demás**. Eso rompía el modelo de presupuesto de la ADR 0025.

## Decisión

La correa pasa a ser una **zona de acción con forma, asimétrica y blanda**, con tres componentes separados:

1. **Forma: la da la posición.** La zona no es un círculo sino una región relativa a la casilla-hogar, con límites distintos hacia delante, hacia atrás y a los lados. Valores de partida, en casillas:

| Posición | Adelante | Atrás | Lados |
|---|---|---|---|
| Portero | 1 | 0 | ±1 (siempre dentro del área, RF-057b) |
| Defensa | 3 | hasta su portería | ±2 |
| Centrocampista | 5 | 4 | ±3 |
| Delantero | hasta la portería rival | 1 | ±2 |

Un defensa al que se le permiten tres columnas hacia delante se mantiene cerca de la defensa por construcción, sin necesidad de prohibirle nada. El centrocampista es el que más recorrido tiene, como en el deporte real.

2. **Tamaño: lo escala el atributo `Leash`.** La posición da la forma; el atributo la agranda o la encoge. Un centrocampista con la correa alta cubre más de esa misma región, pero **no puede convertirse en otra posición** subiéndola. Esto es lo que devuelve el atributo a una escala comparable con los demás y permite que vuelva al presupuesto de la ADR 0025 (a remedir tras el rediseño; la propuesta de dejarla fuera queda retirada).

3. **Disciplina: cuánto tira de vuelta.** La zona deja de ser un muro y pasa a ser **una tendencia**: el jugador puede salir, pero su utilidad penaliza de forma creciente con la distancia fuera y la acción de replegar gana peso. La disciplina se deriva de la raza y los rasgos, no es un atributo nuevo: los enanos, cortos y lentos, no se desmadran; los elfos, rápidos, se mueven con mucha más libertad; `Lazy` no vuelve, `Leader` mantiene la posición.

Se conserva un **límite duro exterior** generoso (del orden del doble de la zona) para que un caso raro no deje a un defensa instalado en el área rival el resto del partido.

## Alternativas descartadas

- **Radio duro** (statu quo): produce un fútbol que no se parece al fútbol y convierte a la correa en el atributo dominante.
- **Sin correa**: RF-042 existe para que el partido no sea catorce jugadores persiguiendo el balón. La zona conserva esa función; lo que cambia es su forma y su dureza.
- **Zona dura pero asimétrica**: arregla la verosimilitud del recorrido, pero mantiene el corte artificial de que un jugador se detenga en una línea invisible.

## Consecuencias

- `requisitos.md` debe reescribir RF-042 y RT-095 (anotado como R-6 en `docs/pendientes.md`).
- La zona es relativa a la casilla-hogar **efectiva**, ya desplazada por el estado táctico (RT-089 capa 2), así que el bloque sigue subiendo y bajando como hasta ahora.
- **Riesgo a vigilar**: al ser blanda, el bloque puede desmadrarse y devolver el caos que la correa evitaba. Los controles son la disciplina, el coste creciente en la utilidad y el peso de replegar; y las métricas de RT-056 —tiempo del balón por tercio y alternancias de posesión— lo detectarían de inmediato.
- La visualización de RF-045 cambia: se dibuja una región con forma, no un círculo, y conviene distinguir la zona propia del margen exterior.
- Es un cambio del núcleo del comportamiento: entra en el bloque del rediseño espacial, junto a los cuerpos y a `FindSpace`, con el que comparte reajuste.
