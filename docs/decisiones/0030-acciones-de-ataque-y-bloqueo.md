# 0030. Acciones de ataque diferenciadas y bloqueo sin balón

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor). **Matiza RF-057 y adelanta el árbitro de la fase 3.**
**Requisitos afectados:** RF-051, RF-057, RF-061..064g, RT-090..096, RF-022c

## Contexto

El revisor pide un fútbol más reconocible: pase corto por defecto y pase largo solo si el jugador tiene con qué; regate en lugar de pase cuando el jugador es habilidoso; tiro lejano solo para quien lo tiene; y entradas agresivas **también en ataque**, para quitar rivales de en medio y abrir espacio a los compañeros, al estilo de Blood Bowl.

Buena parte ya existe, pero en el sitio equivocado del modelo. Hoy los atributos modulan el **resultado** de una acción, no la **decisión** de intentarla: la utilidad puntúa una única acción `Pass` y después elige receptor, así que un jugador torpe y uno brillante *deciden* pasar con la misma frecuencia y solo se diferencian en si el pase llega. Eso produce partidos donde todos juegan igual y el atributo se nota como estadística, no como estilo.

## Decisión

### 1. La decisión depende del jugador, no solo el resultado

El pase se separa en dos acciones que compiten entre sí en la tabla de utilidad:

- **`ShortPass`**: receptor a 3 casillas o menos. Peso base alto para todo el mundo.
- **`LongPass`**: receptor entre 4 y 8. Peso base bajo, escalado por técnica y por rasgos de visión. Un centrocampista técnico abre el juego; uno torpe casi nunca lo intenta, y cuando lo intenta lo pierde.

`Dribble` y `Shoot` ya existen; lo que cambia es que sus pesos escalan con los atributos y rasgos del jugador con más pendiente que los del pase, de modo que el habilidoso **prefiere** encarar y el goleador **prefiere** rematar. El tiro pasa de un corte binario dentro/fuera de rango a una penalización continua por distancia, modulada por el rasgo `LongShot`: nadie tiene prohibido tirar de lejos, simplemente casi nadie debería querer.

### 2. Bloqueo sin balón (`Block`)

Acción nueva: derribar a un rival **que no tiene el balón** para abrir espacio. Es la mecánica que convierte la violencia en herramienta ofensiva y no solo en defensa, y es lo que el documento llama "carnicería administrada".

**Límite que impone RF-057** (*"solo hay contacto físico entre jugadores que disputan el balón o que se encuentran en la trayectoria de la jugada activa; no hay peleas paralelas sin relación con el juego"*): el objetivo debe estar **dentro de la jugada activa**, definido como un radio alrededor del balón o en el corredor entre el balón y la portería atacada. No se puede ir a partir a un rival al otro lado del campo. RF-057 se matiza en ese sentido (R-8).

Con los cuerpos de la ADR 0020, derribar a alguien abre hueco **de verdad**: el rival deja de ocupar espacio y de estorbar.

### 3. El árbitro se adelanta de la fase 3

Un bloqueo sin balón es falta casi segura. Sin árbitro funcional, la violencia no tiene contrapeso y domina, que es justo lo que RF-064e previene (*"el árbitro es el contrapeso de las builds de violencia"*). Hoy el árbitro es neutro con criterio fijo en 0, y el criterio estaba planificado para la fase 3.

**Se adelanta**: criterio (RF-062), desplazamiento por acción sucia se pite o no (RF-063) y efectos sobre falta, tarjeta y penalti (RF-064) entran en este bloque, junto con las acciones nuevas. Los rasgos de árbitro y los sobornos siguen en la fase 3.

## Alternativas descartadas

- **Solo modular el resultado** (statu quo): es lo que produce que todos los equipos jueguen igual.
- **Bloqueo libre por todo el campo**: contradice RF-057 y convierte el partido en una pelea con un balón de fondo.
- **Bloqueo sin adelantar el árbitro**: la violencia quedaría sin contrapeso y dominaría todas las builds.

## Consecuencias

- El espacio de acciones pasa de 9 a 12 (`ShortPass`, `LongPass`, `Block` sustituyendo a `Pass`), lo que **multiplica el coste de balanceo**. Es el motivo de agrupar este cambio con el resto del rediseño y hacer un único reajuste al final, en lugar de dos.
- `data/ai/weights.json` gana las acciones nuevas y sus escalados por atributo, que hasta ahora eran fijos por posición y rasgo.
- El volcado de utilidad (RT-098) sigue siendo la herramienta para depurar por qué un jugador eligió encarar en vez de pasar; con doce acciones es más necesario, no menos.
- Riesgo a vigilar: con `LongPass` disponible, el balón puede empezar a volar de área a área y romper las métricas de cadena de pases y de tiempo por tercio (RT-056). Los pesos deben dejar el pase largo como recurso, no como norma.
