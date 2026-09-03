# Simulación del partido

Concreta RF-040..060, RF-057b..e, RF-061..064, RT-089..098. Es el contenido de la fase 0 (sin perks) y la base de la 1.

## Campo y colocación

- Cuadrícula 16 columnas x 5 filas (RF-040). Cada equipo coloca en su mitad (columnas 0-7 / 8-15); portero en casilla fija (RF-041). Posición restringe filas y columnas permitidas (RF-022b). Razas grandes ocupan 2 casillas contiguas (RF-033).
- **Correa**: radio en casillas desde la casilla-hogar (RF-042). Fuera del radio el jugador no persigue el balón; actúa como filtro previo de la utilidad (RT-095). El portero tiene su correa contenida en el área (RF-057b).
- Estado táctico desplaza las casillas-hogar de todo el bloque (RT-089 capa 2). La correa se evalúa respecto a la casilla-hogar **desplazada**.

## Las tres capas (RT-089)

### 1. Máquina de estados del partido

`Saque -> JuegoAbierto <-> Reanudacion | Penalti -> ... -> FinReglamentario -> (empate) Turba -> Fin`

- Saque inicial: primer contacto en los 2 primeros segundos (30 ticks) (RF-052).
- Reanudaciones instantáneas con animación de 1 s superpuesta que no detiene el reloj (RF-053). Solo penalti y tarjeta roja detienen el reloj (RF-054).
- Reglamentario: una sola fase, 100% con reglas normales (RF-055).
- Turba (RF-055b): solo si hay empate. Transición `ARBITRO_SE_VA`: sin faltas, sin tarjetas, criterio deja de aplicarse; campo 16x3 (filas 0 y 4 invadidas, siempre las mismas); velocidad global +15% (se aplica como multiplicador entero a los costes en ticks, no cambiando el tick); primer gol termina.
- Incomparecencia: menos de 5 jugadores en campo = derrota inmediata (RF-059). Si el equipo del usuario baja de 5 disponibles, la run termina (RF-002b).

### 2. Estado táctico del equipo

`EnPosesion | SinPosesion | TransicionOfensiva | TransicionDefensiva`, derivado de la posesión. Desplaza el bloque de casillas-hogar; el tamaño y la velocidad del desplazamiento (en `data/ia/`) expresan estilos: tiki-taka, contragolpe, balón largo, presión alta.

### 3. Máquina de estados del jugador

`Colocandose, Persiguiendo, Conduciendo, Pasando, Tirando, Entrando, Derribado, Lesionado, Celebrando`. Cada estado tiene duración en ticks y lista de acciones legales. `Puede(estado, accion)` es una tabla explícita en código en fases 0-1 (RT-089c). Un jugador `Derribado` no decide nada hasta expirar.

## IA de utilidad (RT-090..098)

En cada tick en que su estado lo permite, el jugador puntúa las acciones legales y ejecuta la de mayor puntuación.

- Acciones mínimas (RT-092): perseguir balón, marcar rival, ofrecer apoyo, cubrir espacio, pasar, conducir, tirar, entrar, replegar a casilla-hogar.
- Puntuación = suma de términos enteros: `pesoBase[posicion][accion]` (RT-093) x `modificadorRasgo` (RT-094) x `modificadorEstadoTactico`, más términos de contexto (distancia al balón, a portería, rivales cerca, correa restante). Todo en `data/ia/pesos.json` (RT-096).
- Rasgos (RT-094): `agresivo` sube entrar; `goleador` sube tirar; `tiro lejano` amplía la distancia útil de tiro; `cobarde` baja duelos; `vago` baja replegar; `cerebral` sube pasar; `rapido` sube perseguir; `sucio` sube entrar dura y baja el coste percibido de falta; `lider` sube pesos de compañeros adyacentes; `resistente` reduce el decaimiento por fatiga.
- Correa como filtro previo (RT-095). Empates por id ascendente (RT-097).
- **Volcado de tabla** (RT-098): `Simulador.Ejecutar` con `config.VolcarUtilidad = (idJugador, tick)` añade al informe la tabla `accion -> puntuacion, términos`. Es la herramienta principal de depuración; `/Balance --dump-utility` la expone.

## Jugada (RF-051)

Secuencia desde recuperación hasta tiro o pérdida, en cuatro tramos: recuperación, progresión, último tercio, definición. Publica `INICIO_JUGADA`/`FIN_JUGADA`. La jugada mejor puntuada del partido es la que se ofrece como repetición (RF-120).

## Contacto y violencia (RF-057)

Solo hay contacto entre jugadores que disputan el balón o están en la trayectoria de la jugada activa. Una entrada (`ENTRADA`) resuelve: éxito/fallo (técnica vs técnica+velocidad), falta o no (rasgos, criterio), tarjeta (umbral por criterio), lesión (fuerza del ejecutor vs resistencia del receptor, probabilidad en `data/`), muerte solo si RF-093. Toda probabilidad es entera sobre 10000.

## Portero (RF-057b..e)

- Nunca sale del área. Puede recibir cargas dentro de ella (objetivo legítimo de builds de violencia).
- Parada = `50 + mediaPonderada(atributos relevantes al tipo de tiro) - calidadTiro`, en porcentaje entero. Fuerza: tiros lejanos/potentes y aguante a cargas; velocidad: reflejos en tiros cercanos y uno contra uno; técnica: colocación y penaltis; resistencia: evita el decaimiento tras tiros consecutivos y en turba. Sin atributos exclusivos de portero.
- Rasgos propios: `Gato`, `Muro`, `Sale mucho` (RF-057e).

## Árbitro (RF-061..064g)

- Uno por partido, con nombre, retrato y **un rasgo**: estricto, permisivo, casero, tuerto (lado ciego indicado), cobarde, corrupto, incorruptible. Grupo de 6-8 por run que recuerdan sobornos (RF-061b).
- **Criterio** -100..+100, 0 neutral, positivo favorable al usuario. Visible siempre. Cada acción sucia lo desplaza contra quien la comete, se pite o no (RF-063). Efectos (RF-064): probabilidad de señalar falta, umbral de tarjeta, probabilidad de penalti, tolerancia a invasiones en turba. A |60| el árbitro es decisivo.
- Soborno (RF-064b..d): consumible con tabla de resultados visible; denuncia +10 puntos por soborno previo al mismo árbitro; efecto de denuncia: criterio -60 el resto del partido y expulsión del portador elegido.
- Mitigaciones (RF-064f): perk "Cara de inocente", objeto "Amigo de la federación", consumible "Protesta del banquillo", rasgo "Sucio pero discreto". `/Balance` verifica RF-064e y RF-064g.

## Duración y velocidad

- 60-90 s a x1 = 900-1350 ticks + turba. La velocidad de reproducción (RF-050) es cosa de `/Game`. La legibilidad a x4 (RF-050b) es criterio de fase 3.

## Fuera de la fase 0

Perks y bus de efectos (fase 1), lesiones persistentes y taller (fase 2), turba, árbitro con rasgos, sobornos y vínculos (fase 3). En fase 0 el árbitro existe con rasgo neutro y criterio fijo en 0, para que la máquina de estados ya tenga faltas y tarjetas.
