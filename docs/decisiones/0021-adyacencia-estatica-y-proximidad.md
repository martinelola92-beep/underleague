# 0021. Adyacencia resuelta antes del partido, y proximidad dinámica como familia aparte

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor; implementación pendiente)
**Requisitos:** RF-044, RF-041, RF-065..068, UI-003, UI-020

## Contexto

En la fase 1 los perks de sinergia posicional evalúan `adjacent(who, 'Tag')` **en cada evento**, comparando casillas-hogar con distancia Chebyshev 1. Eso tiene tres problemas:

1. Obliga a apiñar el equipo para que dos casillas-hogar sean contiguas, y el coste táctico de apiñarse supera cualquier bonus (ver ADR 0020).
2. Es confuso para el jugador: la condición se comprueba sobre casillas-hogar, no sobre dónde están los jugadores en ese momento, así que el efecto no coincide con lo que se ve en el campo.
3. Cuesta recalcularlo en cada evaluación de condición cuando el resultado no cambia nunca durante el partido.

## Decisión

Dos familias distintas y explícitas:

- **Sinergia de colocación (estática).** Se resuelve **una sola vez al construir el partido**, a partir de las casillas-hogar de la alineación, y produce **relaciones fijas entre pares de jugadores** que duran todo el partido, aunque los implicados no estén cerca en el momento de la acción. Ejemplo: un perk que mejora la precisión de pase hacia los jugadores adyacentes hace que su portador pase mejor **a esos jugadores concretos** durante los 90 segundos. Es la lectura literal de RF-044 ("la adyacencia entre casillas-hogar es la base de los perks de sinergia posicional"), es barata (coste cero en partido) y es legible: el jugador la decide en la pantalla de alineación y la ve dibujada.
- **Sinergia de proximidad (dinámica).** Condición nueva evaluada en el momento del evento sobre las posiciones reales, con radio en casillas. Es la que produce las historias ("estabas ahí cuando hacía falta") y encaja con los cuerpos de la ADR 0020.

Ambas conviven: hay perks de casilla inicial y perks de proximidad, y el catálogo debe dejar claro cuál es cuál en su descripción generada.

**Los vínculos son direccionales, no "los más cercanos".** Con radio Chebyshev ≤ 2 sobre un campo de 5 filas casi cualquier formación produce vínculos —la alineación por defecto ya tiene seis pares adyacentes—, así que vincular a todo el que cumpla el radio haría el bonus gratuito y la colocación dejaría de ser una decisión. En su lugar, **cada perk declara la relación geométrica que necesita**, en coordenadas relativas al sentido de ataque del equipo:

| `link` | Significado | Lectura futbolística |
|---|---|---|
| `beside` | misma columna, fila contigua | pareja de centrales, doble pivote |
| `ahead` / `behind` | columna contigua en sentido de ataque / en sentido contrario, fila igual o contigua | lateral y extremo, pivote y mediapunta |
| `left` / `right` | fila contigua hacia una banda concreta | el compañero de tu banda |
| `diagonalAhead` / `diagonalBehind` | columna y fila contiguas | apoyos en diagonal |

Un perk puede declarar una o varias (`links: ["beside"]`, `links: ["ahead", "behind"]`). Si hay más de un candidato para la misma relación, se toma el más cercano y se desempata por id ascendente. **Si no hay candidato, no hay vínculo**: el perk no aplica (o aplica sus `elseEffects`), que es lo que convierte la formación en una decisión con coste.

Convención de orientación, fijada aquí para evitar errores sutiles: "adelante" es siempre hacia la portería rival, e "izquierda" y "derecha" se toman desde el punto de vista de un jugador que mira hacia esa portería. El equipo visitante refleja columnas y bandas, de modo que un mismo perk describe la misma estructura para ambos equipos.

Efecto de diseño buscado: los perks dejan de hablar de proximidad genérica y pasan a describir **estructuras de equipo** —una pareja de centrales, un carril de banda, un eje—, que es el vocabulario del deporte que el juego parodia (RA-025) y encaja con las líneas y el bloque de la capa táctica (RT-089).

El radio de la relación estática es el de la ADR 0011 (Chebyshev ≤ 2 entre casillas-hogar): esa decisión sigue vigente y define **qué pares se vinculan**; lo que esta ADR cambia es **cuándo se evalúa** (una vez, al construir el partido) y **sobre qué actúa** (el par, no el portador aislado).

## Alternativas descartadas

- **Solo dinámica**: RF-044 pide explícitamente la adyacencia de casillas-hogar, y perder la sinergia estática vaciaría de contenido la pantalla de alineación (UI-020: es donde se toman las decisiones).
- **Solo estática con radio mayor**: resuelve el coste táctico pero no aporta la lectura de "jugar juntos".
- Mantener la evaluación por evento de la adyacencia estática: mismo resultado, más coste y peor legibilidad.

## Consecuencias

- Los efectos de la familia estática son **dirigidos a pares**: hay que poder expresar "hacia el jugador X" en el motor de efectos, no solo "al portador". Requiere un tipo de modificador por par (por ejemplo, un bono de pase aplicable cuando el receptor es uno de los vinculados).
- Las descripciones generadas (RT-035) necesitan plantillas para ambas familias, y la interfaz debe dibujar las relaciones estáticas en la pantalla de alineación, junto a las líneas de vínculo (RF-106).
- El catálogo de perks de la fase 1 se revisa: los que hoy usan `adjacent*` pasan a la familia estática con efecto por par, y se añaden perks nuevos de proximidad.
