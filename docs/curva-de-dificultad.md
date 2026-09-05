# Curva de dificultad, azar y retención

Investigación aplicada a Underleague. Los datos son de fuentes públicas sobre roguelikes consolidados; las conclusiones son propuestas concretas para este juego, con lo que hay que cambiar.

## 1. Los datos de referencia

| Dato | Fuente | Valor |
|---|---|---|
| Tasa de victoria global de Slay the Spire | 18 millones de runs (2020) | **9%** |
| Tasa de victoria de Slay the Spire 2 | 240 millones de sesiones | **25%** de media |
| Victoria en la ascensión más baja frente a la más alta (StS 2) | oficial | **16% (A0) y 17% (A10)** |
| Duración de una run **ganada** | StS | **~64 minutos** |
| Duración de una run **perdida** | StS | **~23 minutos** |
| Mortalidad del jefe final | StS | **48-53%** de las runs que llegan |
| Mortalidad de un enemigo normal peligroso | StS, Masked Bandits | **>10%** de las runs |
| Duración considerada punto dulce | análisis del género | **30-60 minutos**, con 20-30 como ideal para muchos |

## 2. Siete lecciones, y qué significan aquí

### 2.1 La tasa de victoria apenas cambia entre dificultades

En Slay the Spire 2, la ascensión 0 gana el 16% y la ascensión 10 el 17%. **No es un fallo de balance: es el objetivo.** El jugador mejora al mismo ritmo que sube la dificultad, así que la sensación de reto se mantiene constante mientras su competencia crece.

Confirma el criterio de la ADR 0039 —la tasa de victoria en una división superior debe ser igual o menor que en la anterior— y da el número: **igual**, no menor. Si la división Mundial se gana mucho menos que la Tercera, la progresión meta no está compensando bien.

### 2.2 Una run perdida debe costar mucho menos tiempo que una ganada

23 minutos frente a 64. **Perder es barato**, y por eso se reintenta. Es el mecanismo de retención más importante del género y no es un accidente: la mayoría de las derrotas ocurren pronto.

Underleague tiene aquí un riesgo estructural: RF-003 fija runs de 75-100 minutos, más largas que el punto dulce del género. Si además las derrotas llegan tarde, cada fracaso cuesta una hora y el bucle de "una más" se rompe.

**Propuesta**: que la mayoría de las derrotas ocurran en el jefe del acto 1 —a un tercio de la run, unos 25-30 minutos— y medir la duración real de una run perdida como métrica de retención, no solo la de una ganada.

### 2.3 Los roguelikes empiezan siendo fáciles, a propósito

Slay the Spire, Ring of Pain y compañía dejan al jugador nuevo ver mucho contenido en su primera partida. *"Nada es peor que un jugador nuevo abandonando tras morir en los primeros minutos."*

Esto **matiza** la directriz de que un jugador mediocre pierda en el primer acto: hay que separar **novato** de **mediocre**. El novato debe llegar al acto 2 o 3 en su primera run, ver el juego y perder al final; el jugador que después de muchas partidas sigue sin construir bien es el que se estanca en el acto 1.

Underleague ya tiene la herramienta: RF-123, la primera run guiada. **Propuesta**: que esa primera run sea claramente generosa, y que la dificultad real muerda a partir de la segunda.

### 2.4 Azar de entrada frente a azar de salida

La distinción central del diseño de azar: **el azar de entrada** ocurre *antes* de que decidas —qué cartas robas, qué hay en la tienda— y se percibe justo, porque decides sabiendo. **El azar de salida** ocurre *después* —tiras el dado y tu buena jugada se deshace— y se percibe injusto.

**Underleague es, por naturaleza, un juego de azar de salida**: colocas el equipo, pulsas empezar y el partido se resuelve solo. Es el riesgo de diseño más serio del proyecto, y explica por qué RF-012d —*todo lo malo debe haber sido previsible*— no es un requisito más, sino la pieza que hace viable el género elegido.

La estrategia del documento es correcta y conviene reconocerla como lo que es: **convertir azar de salida en azar de entrada dando la información antes**. El informe de ojeo completo y gratuito (RF-012b), el indicador de riesgo de lesión por jugador (RF-012c), la tabla de probabilidades del taller (RF-095) y del soborno (RF-064b) hacen exactamente eso. Cada vez que se añada una mecánica con azar, la pregunta obligatoria es: *¿el jugador conocía la probabilidad antes de decidir?*

### 2.5 Los picos de éxito están detrás de los jefes

En Slay the Spire la probabilidad de victoria sube de golpe justo después de cada jefe de acto, por la recompensa que sueltan. El jefe es una barrera **y** un trampolín.

Underleague tiene la barrera (ADR 0033) pero **no el trampolín**: hoy la recompensa es la misma tras cualquier victoria. Es la carencia que ya señalaba el análisis de Rune Dice, y los datos la refuerzan: sin recompensa diferenciada, superar un jefe no cambia la trayectoria de la run y el acto siguiente empieza igual de apretado.

### 2.6 Un enemigo normal debe poder matarte

Los Masked Bandits, que no son un jefe, acaban con más del 10% de las runs de algunos personajes. Eso mantiene la tensión entre jefes y hace que el mapa importe.

Underleague tiene el nodo de **partido de élite** para ese papel, pero hoy no está diferenciado ni en riesgo ni en recompensa. Debería ser la decisión de ruta más interesante del mapa: más peligro, más premio.

### 2.7 La duración de la run

El punto dulce del género está en 30-60 minutos. Underleague fija 75-100 (RF-003), y la aritmética actual da: 20 partidos × 60-90 s ≈ 20-30 minutos de simulación, más las decisiones. Con el presupuesto de 20 segundos entre partidos de UI-003 y unos minutos en cada nodo de servicio, sale una run de **40-60 minutos**, más cerca del punto dulce que del requisito.

**Propuesta**: medir la duración real cuando exista la interfaz y, si se confirma, corregir RF-003 a la baja en vez de alargar el juego para cumplirlo. Una run más corta es mejor producto.

## 3. Qué cambiar, en orden

1. **Recompensas diferenciadas por tipo de nodo** (§2.5 y análisis de Rune Dice): élite paga más y jefe da un salto real. Es lo que convierte el mapa en decisiones y da los picos de progresión.
2. **Abrir la separación entre perfiles de jugador** (§2.3): hoy la política mediocre gana el 12,2% y la buena el 17,8%. Con una diferencia de 5 puntos, jugar bien casi no importa. Es el problema de balance más urgente.
3. **Que las derrotas lleguen pronto** (§2.2): medir la duración de una run perdida y comprobar que la mayoría se decide en el primer jefe.
4. **Primera run generosa** (§2.3): concretar RF-123 para que el novato vea el juego antes de que le muerda.
5. **Auditar el azar de salida** (§2.4): revisar cada mecánica aleatoria y comprobar que su probabilidad es visible antes de decidir. Es una revisión de diseño, no de código.

## Fuentes

- [Slay the Spire statistical analysis (Fox Row, 18 M de runs)](https://foxrow.com/slay-the-spire-statistical-analysis)
- [Estadísticas oficiales de Slay the Spire 2 (240 M de sesiones)](https://www.gamemeca.com/en/view.php?gid=1775591)
- [Designing Fair RNG in Roguelikes](https://medium.com/@JeongHyeonUk/designing-fair-rng-in-roguelikes-balancing-luck-and-skill-7b967230e961)
- [Input vs. Output Randomness](https://entrogames.substack.com/p/019-input-vs-output-randomness-a-couple-of-words-out-of-order-and-more)
- [Cogmind: Adjustable Difficulty (Grid Sage Games)](https://www.gridsagegames.com/blog/2017/02/adjustable-difficulty/)
- [Roguelite restart: length of a perfect run](https://medium.com/@todorovicnik2/video-games-roguelite-restart-length-of-a-perfect-run-ef8078c76495)
