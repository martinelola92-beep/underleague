# Dirección de UI de Underleague

**Estado (19 sep 2026): fases A–D.3 hechas con el revisor; nada implementado en `/Game`.** Este documento
fija lo acordado para que una sesión nueva pueda seguir sin la conversación. Lo marcado *provisional* se
ajustará; lo marcado *pendiente de ADR* contradice un requisito o ADR vigente y no se implementa hasta
registrarlo (RT-057). Etiquetas de evidencia: [HECHO] medido, [DECISIÓN] acordada, [HIPÓTESIS] por validar.

Capturas de referencia en `capturas/` (prototipo en gris y cápsulas, **no arte final**). Cómo reproducirlas
y las mediciones: `prototipo/README.md`.

---

## 1. Dirección visual

**Gramática de retransmisión · materia de fiesta popular medieval · voz de pregón.**
*Presentación vibrante, contenido brutal.*

- De la retransmisión deportiva se toma la **gramática** (marcador compacto, rótulos, anuncios de estado,
  repetición, patrocinadores), no su materia: nada de cristal, degradados ni tipografía corporativa.
- La materia es la de una fiesta popular (referencia ancla: el *calcio storico* florentino y el Palio):

  | Material | Función |
  |---|---|
  | Tela y heráldica | identidad de equipo: paños con orla, escudos partidos (color **y** forma, UI-002) |
  | Madera | estructura persistente: tablero del marcador, botones |
  | Papel y pergamino | información que se lee: tiras, sellos, bandos, esquela, bandeja |
  | Voz de pregón | acontecimientos: «Se hace saber…», trompeta de heraldo, sello de lacre |

- **El cómic se descartó para los acontecimientos** (revisor, D.2–D.3): el estallido con onomatopeya leía
  infantil. Los acontecimientos se proclaman como un pregonero con imprenta.
- Color: heráldica profunda — nuestro equipo azur y oro, el rival gules y sable; pergamino y tinta parda para
  lo impreso; el rojo sangre solo para daño y muerte; el oro también para economía.
- Dos voces tipográficas (fuentes libres, provisionales): **IM Fell** (imprenta del XVII, romana, nunca
  gótica) para proclamar; **Barlow Condensed** para datos. Cifras del marcador en **Cinzel**. Texto esencial a
  **20 px lógicos** como mínimo (C15: sale a ~13 px efectivos en 1280×800; UI-004 pide ≥11).
- Irregularidad en el **marco** (bordes rasgados, placas torcidas 2–4°), nunca en el contenido que se lee.
- Diferenciación de Blood Bowl: tela/madera/papel en vez de metal/piedra, día y color en vez de *grimdark*,
  heráldica y pregón en vez de calaveras y pinchos, humor en los textos.

## 2. Principios

1. El mundo cuenta la acción; la UI cuenta los cambios de estado (marcador, plantilla, partido).
2. Una sola voz alta a la vez.
3. Lo persistente es quieto y periférico; lo transitorio, breve.
4. Volumen proporcional a la rareza.
5. Todo acontecimiento deja un residuo persistente (nada desaparece sin explicación).
6. Presentación vibrante, contenido brutal.
7. Material en el marco, plano en el contenido.
8. Color y forma siempre juntos.
9. Nada exige interacción durante el partido salvo una decisión real (sustitución forzada, consumible).
10. La velocidad degrada la presentación, nunca la información.
11. Todo funciona con marcadores de posición: nada depende de retratos.
12. El riesgo se explica antes del partido (RF-012d): el HUD puede ser ligero solo si el ojeo cumple.

## 3. Arquitectura conceptual

Tres ejes independientes:

- **Plano** (dónde vive): *Mundo* (3D: jugadores, balón, sangre RA-027, vallas, grada) · *Marcas* (ancladas a
  un jugador o punto, tamaño fijo en pantalla: carteles de perk, sellos) · *Tablero* (bordes: marcador,
  progreso, estado, tiras, velocidad) · *Escena* (presentación grande: N3, N4, bandeja).
- **Modo**: juego · inspección (pausa, ficha expandida) · depuración (lo que hoy es `MatchScreen`: vista 2D,
  tick a tick, log, leyenda; fuera del juego final).
- **Comportamiento**: persistente · transitorio · interrupción.

**Evento ≠ presentación.** Cadena: motor → sucesos → **agrupador de momentos** → director (importancia →
frase → canales → duración → ¿pausa?) → residuo. El director es un **agrupador**, no un planificador
complejo (fase A: los conflictos son 0,02 por partido).

**Regla de reproducción [HECHO, C.2]:** el motor cambia el estado en el tick del suceso (tras un gol recoloca
para el saque; al lesionarse, el jugador desaparece). Toda presentación que congela muestra **el fotograma
anterior al suceso**. Simulación ≠ reproducción ≠ presentación: la presentación puede alargar el tiempo de
reproducción (el partido ya está simulado).

## 4. Gramática de eventos

| Nivel | Qué | Duración 1× (*provisional*) | ¿Pausa? |
|---|---|---|---|
| N0 | juego corriente: solo mundo | — | no |
| N1 | detalle: marca o sello pequeño + residuo | ~1 s | no |
| N2 | notable: sello grande + gesto de cámara + residuo | ~1,5 s | no |
| N3 | mayor: estandarte de pregón + gesto de cámara + residuo | ~3 s | según el momento |
| N4 | capital: bando / acta a pantalla parcial | ~4 s o hasta decidir | sí |

Catálogo:

| Momento | Nivel | Frase |
|---|---|---|
| pase, regate, entrada limpia, tiro, parada | N0 | animación |
| perk activado en juego | N1 | cartel de pergamino sobre el jugador + destello de su tira |
| falta · falta no vista | N1 | tampón «Falta» sobre recorte de papel · «No lo ha visto» con emblema del árbitro |
| amarilla · lesión leve sin decisión | N2 | sello + residuo en tira/marcador |
| gol | N3 | **estandarte del equipo que marca**: «Se hace saber · Gol · de X, al minuto N · ¡Viva …!»; congela ~2 s |
| roja | N3 | estandarte del equipo del expulsado: «Expulsado» |
| lesión grave | N3 | estandarte: «Herido»; si hay decisión, bandeja en la misma pausa |
| turba / el árbitro se va | N3 | **banda de pregón** a lo ancho, sin congelar |
| muerte | N4 | dos tiempos: (1) el campo cuenta la muerte (gesto de cámara, cuerpo y mancha); (2) **bando con lacre** «Se hace saber el fallecimiento de…» + bandeja |
| final / gol de oro | N4 | **acta del encuentro** con los dos escudos y el resultado; transición al informe |
| saque inicial | N3 | presentación breve de los equipos; los 14,5 perks del saque **no** generan carteles (P1) |

Reglas: fusión de sucesos del mismo momento (ventana de 1 s) y fusiones fijas (turba + árbitro se va; gol de
oro + final); una voz alta a la vez; un N3/N4 no presentado a tiempo queda en residuo; saltar una
presentación nunca salta su residuo; **las decisiones tienen prioridad narrativa y pausan a cualquier
velocidad**; los gestos de cámara anticipan una **clase** de situación, nunca un resultado (no «zoom = gol»).

## 5. Medición del partido (fase A, 11.176 partidos de run, dos semillas)

- [HECHO] 8,6 momentos narrables por partido: N1 3,79 · N2 0,85 · N3 2,80 · N4 1,16. Mediana de 4,8
  presentaciones por minuto a 1×; 7,1 s entre presentaciones; 12,7 s entre voces altas.
- [HECHO] 14,5 de los 19 perks por partido saltan en el primer segundo (habilidades raciales y formación):
  estado inicial, no acontecimiento. Solo 0,9 carteles de perk propios durante el juego; el 65 % de los
  partidos, ninguno (**P2 → `game-design-review`**: ¿cómo se hace visible la build?).
- [HECHO] El 87 % de nuestras lesiones leves provocan sustitución: la presentación de la lesión y la decisión
  son una unidad (**C4 abierta**: ¿pausa, automática o diferida?).
- [HECHO] Tras un gol hay 1 s de balón muerto natural: la N3 congela la reproducción.

## 6. Velocidades [DECISIÓN, C7]

| Velocidad | Qué se presenta | Real por partido | Congelado |
|---|---|---|---|
| x1 | todo | ~99 s | 11 % |
| x4 «retransmisión comprimida» | solo el gol (1 s), N4 comprimida y decisiones | ~26 s | 14 % |
| x16 «ir al resultado» | solo N4 comprimida y decisiones | ~7 s | — |

Descartado: «x4 = bajar un nivel» (14,3 presentaciones por minuto real, el triple que x1).

## 7. Composición [DECISIÓN, B/B.1/C]

A 1920×1080, campo 16×7 **entero** con la cámara táctica fija (ortográfica, 60°, ancho del campo + 0,6
casillas por lado; `Size` 9,68 en 16:9 y 10,75 en 16:10). 1280×800 usa el lienzo lógico de 1920×1200
escalado ×0,667.

| Zona | 1080p | Contenido |
|---|---|---|
| Franja superior | y 12–132 | tablero de madera con paños heráldicos, placas del resultado, residuo del rival junto a su nombre (C3), progreso, velocidad |
| Campo | y 251–928 (bajado 30 px) | mundo y marcas; cápsulas ~85 px |
| Fila inferior | y 990–1062 | 7 tiras del equipo propio (232×72) + placa de banquillo |
| Escena | variable | N3 desde el borde contrario al protagonista (~15 % del campo); N4 a la izquierda; la **bandeja de decisión sustituye a las tiras** (campo visible al 100 %) |

Tira: escudo, dorsal, nombre, puesto y raza, recuento de perks, estado físico por color **y** forma (sano ●,
tocado ▼, grave ■). Expandible (inspección, pausa). Suplentes solo en la bandeja. La bandeja dice el puesto y
la casilla del que sale y marca el candidato recomendado (BA-I, BB-F).

Sellos de la fila superior: se apartan lateralmente hacia el centro (no suben al marcador) y llevan fondo de
papel propio (se leen sobre las vallas y en grises).

## 8. Entorno (prototipo procedural, no arte)

Césped gastado (franjas de siega, calvas en el círculo y delante de las porterías, hierba seca en las bandas),
vallas con patrocinadores de parodia (RA-025: «Funeraria El Último Saque», «Seguros Ay Madre»…) y grada
escalonada con público. **La regla 10 de `CLAUDE.md` (sin arte hasta cerrar la fase 2) sigue vigente**: esto
es prototipo para validar la UI sobre un fondo realista, no un encargo.

[HECHO] En grises los dos equipos son indistinguibles **en el campo** (azul y rojo de luminancia parecida;
la UI sí aguanta). Propuesta para el arte: el visitante con contraste fuerte de luminancia y anillo con otro
dibujo (afecta a UI-002).

## 9. Decisiones abiertas

- **C3** qué residuos del rival y cuánto duran · **C4** lesión leve con decisión · **C5** posición exacta de la
  N3 · **C6** cierre automático de cada presentación · **C8** cerrar la ficha expandida · **C9** árbitro
  persistente o contextual · **C10** barra de progreso con o sin minuto · **C12** nombres largos en las tiras.
- Tamaños, ángulos, tiempos de entrada y salida: *provisionales* hasta un prototipo animado con arte.
- Fuentes definitivas (licencias y personalidad).

## 10. Requisitos y ADR que esta dirección contradice (pendiente de ADR)

| Vigente | Propuesta |
|---|---|
| RA-025 «cultura futbolística real» y RA-026 | espectáculo deportivo popular medieval con gramática de retransmisión; RA-026 se mantiene en espíritu (sin gótico ni calaveras como adorno) |
| ADR 0114 (cámara de cuatro estados) | cámara táctica fija + gestos (sacudida, *punch-in*) |
| RF-116 (máx. 2 highlights) | N3 en cada gol, roja, lesión grave y turba (~3 por partido); el highlight cinemático de RA-020 se replantea |
| UI-021 (Equipo primero) | se empezó por el partido |
| UI-013 / UI-011 (tiras reactivas de 24 px, ambos equipos) | tiras solo del equipo propio, 72 px |
| Tabla de pantallas: «log bajo el campo» | el log pasa a depuración y a la crónica del informe |
| ADR 0112 (cartel de perk de 1 s) | se mantiene la idea; sin carteles en el saque inicial (P1) y revisión de visibilidad de la build (P2) |

## 11. Siguiente paso

Fase E (arquitectura): pasar por la skill `architecture-review` el director (agrupador + niveles), la
separación de capas en `/Game` (Theme de Godot con los tokens de §1, componentes: tira, sello, estandarte,
bando, bandeja, tablero) y el modo depuración; después las ADR de §10 y la implementación con marcadores de
posición, verificada con `visual-review` contra `capturas/`.
