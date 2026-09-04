# Simulación del partido

Concreta RF-040..060, RF-057b..e, RF-061..064, RT-089..098. Es el contenido de la fase 0 (sin perks) y la base de la 1.

## Campo y colocación

- Cuadrícula 16 columnas x 5 filas (RF-040). Cada equipo coloca en su mitad (columnas 0-7 / 8-15); portero en casilla fija (RF-041). Posición restringe filas y columnas permitidas (RF-022b). Razas grandes ocupan 2 casillas contiguas (RF-033).
- **Correa** (`Leash`): desde v0.9.1 (ADR 0028) no es un radio circular sino el tamaño de una **zona de acción con forma asimétrica dada por la posición** (RF-042): un delantero avanza sin límite y retrocede una columna; un defensa avanza tres. Salir de la zona no está prohibido, está **penalizado** de forma creciente (RT-095), con un límite duro exterior; cuánto tira de vuelta lo da la disciplina de la raza y los rasgos. El portero tiene su zona contenida en el área (RF-057b). Detalle de implementación en `fase1b-diseno.md` §2.2.
- Estado táctico desplaza las casillas-hogar de todo el bloque (RT-089 capa 2). La correa se evalúa respecto a la casilla-hogar **desplazada**.

## Las tres capas (RT-089)

### 1. Máquina de estados del partido

Fases del partido (`MatchPhase`):

`Kickoff -> OpenPlay <-> Restart | Penalty -> ... -> RegulationEnd -> (empate) MobGoldenGoal -> Finished`

- Saque inicial (`Kickoff`): primer contacto en los 2 primeros segundos (30 ticks) (RF-052).
- Reanudaciones (`Restart`) instantáneas con animación de 1 s superpuesta que no detiene el reloj (RF-053). Solo penalti (`Penalty`) y tarjeta roja detienen el reloj (RF-054).
- Reglamentario: una sola fase, 100% con reglas normales (RF-055).
- Turba (`MobGoldenGoal`, RF-055b): solo si hay empate. Transición `REFEREE_LEAVES`: sin faltas, sin tarjetas, criterio deja de aplicarse; campo 16x3 (filas 0 y 4 invadidas, siempre las mismas); velocidad global +15% (se aplica como multiplicador entero a los costes en ticks, no cambiando el tick); primer gol termina.
- Incomparecencia (`Forfeit`): menos de 5 jugadores en campo = derrota inmediata (RF-059). Si el equipo del usuario baja de 5 disponibles, la run termina (RF-002b).

### 2. Estado táctico del equipo

Estado táctico (`TacticalState`): `InPossession | OutOfPossession | OffensiveTransition | DefensiveTransition`, derivado de la posesión. Desplaza el bloque de casillas-hogar; el tamaño y la velocidad del desplazamiento (en `data/ai/`) expresan estilos: tiki-taka, contragolpe, balón largo, presión alta.

### 3. Máquina de estados del jugador

Estado del jugador (`PlayerState`): `Positioning, Chasing, Dribbling, Passing, Shooting, Tackling, KnockedDown, Injured, Celebrating`. Cada estado tiene duración en ticks y lista de acciones legales. `CanPerform(state, action)` es una tabla explícita en código en fases 0-1 (RT-089c). Un jugador derribado (`KnockedDown`) no decide nada hasta expirar.

## IA de utilidad (RT-090..098)

En cada tick en que su estado lo permite, el jugador puntúa las acciones legales y ejecuta la de mayor puntuación.

- Acciones mínimas (`PlayerAction`, RT-092): perseguir balón (`ChaseBall`), marcar rival (`MarkOpponent`), ofrecer apoyo (`OfferSupport`), cubrir espacio (`CoverSpace`), pasar (`Pass`), conducir (`Dribble`), tirar (`Shoot`), entrar (`Tackle`), replegar a casilla-hogar (`Retreat`).
- Puntuación = suma de términos enteros: `baseWeight[position][action]` (RT-093) x `traitModifier` (RT-094) x `tacticalStateModifier`, más términos de contexto (distancia al balón, a portería, rivales cerca, correa restante). Todo en `data/ai/weights.json` (RT-096).
- Rasgos (RT-094): agresivo (`Aggressive`) sube entrar; goleador (`Scorer`) sube tirar; tiro lejano (`LongShot`) amplía la distancia útil de tiro; cobarde (`Coward`) baja duelos; vago (`Lazy`) baja replegar; cerebral (`Cerebral`) sube pasar; rápido (`Fast`) sube perseguir; sucio (`Dirty`) sube entrar dura y baja el coste percibido de falta; líder (`Leader`) sube pesos de compañeros adyacentes; resistente (`Resilient`) reduce el decaimiento por fatiga.
- Zona de acción como penalización creciente, no como filtro (RT-095, ADR 0028). Empates por id ascendente (RT-097).
- **Volcado de tabla** (RT-098): `Simulator.Run` con `config.DumpUtility = (playerId, tick)` añade al informe la tabla `action -> score, términos`. Es la herramienta principal de depuración; `/Balance --dump-utility` la expone.

## Jugada (RF-051)

Secuencia desde recuperación hasta tiro o pérdida, en cuatro tramos (`PlayPhase`): recuperación (`Recovery`), progresión (`Progression`), último tercio (`FinalThird`), definición (`Finishing`). Publica `PLAY_START`/`PLAY_END`. La jugada mejor puntuada del partido es la que se ofrece como repetición (RF-120).

## Contacto y violencia (RF-057)

Solo hay contacto entre jugadores que disputan el balón o están en la trayectoria de la jugada activa. Una entrada (`TACKLE`) resuelve: éxito/fallo (técnica vs técnica+velocidad), falta o no (rasgos, criterio), tarjeta (umbral por criterio), lesión (fuerza del ejecutor vs resistencia del receptor, probabilidad en `data/`), muerte solo si RF-093. Toda probabilidad es entera sobre 10000.

## Portero (RF-057b..e)

- Nunca sale del área. Puede recibir cargas dentro de ella (objetivo legítimo de builds de violencia).
- Parada = `50 + weightedAverage(atributos relevantes al tipo de tiro) - shotQuality`, en porcentaje entero. Fuerza: tiros lejanos/potentes y aguante a cargas; velocidad: reflejos en tiros cercanos y uno contra uno; técnica: colocación y penaltis; resistencia: evita el decaimiento tras tiros consecutivos y en turba. Sin atributos exclusivos de portero.
- Rasgos propios: Gato (`Cat`), Muro (`Wall`), Sale mucho (`Rusher`) (RF-057e).

## Árbitro (RF-061..064g)

- Uno por partido, con nombre, retrato y **un rasgo**: estricto (`Strict`), permisivo (`Lenient`), casero (`Homer`), tuerto (`OneEyed`, lado ciego indicado), cobarde (`Cowardly`), corrupto (`Corrupt`), incorruptible (`Incorruptible`). Grupo de 6-8 por run que recuerdan sobornos (RF-061b).
- **Criterio** (`Bias`) -100..+100, 0 neutral, positivo favorable al usuario. Visible siempre. Cada acción sucia lo desplaza contra quien la comete, se pite o no (RF-063). Efectos (RF-064): probabilidad de señalar falta, umbral de tarjeta, probabilidad de penalti, tolerancia a invasiones en turba. A |60| el árbitro es decisivo.
- Soborno (`Bribe`, RF-064b..d): consumible con tabla de resultados visible; denuncia (`Report`) +10 puntos por soborno previo al mismo árbitro; efecto de denuncia: criterio -60 el resto del partido y expulsión del portador elegido.
- Mitigaciones (RF-064f): perk "Cara de inocente", objeto "Amigo de la federación", consumible "Protesta del banquillo", rasgo "Sucio pero discreto" (`Discreet`). `/Balance` verifica RF-064e y RF-064g.

## Duración y velocidad

- 60-90 s a x1 = 900-1350 ticks + turba. La velocidad de reproducción (RF-050) es cosa de `/Game`. La legibilidad a x4 (RF-050b) es criterio de fase 3.

## Fuera de la fase 0

Perks y bus de efectos (fase 1), lesiones persistentes y taller (fase 2), turba, árbitro con rasgos, sobornos y vínculos (fase 3). En fase 0 el árbitro existe con rasgo neutro y criterio fijo en 0, para que la máquina de estados ya tenga faltas y tarjetas.
