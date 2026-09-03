# Documento de requisitos funcionales y técnicos

**Proyecto:** roguelite de fútbol autojugado con mecánicas de deporte brutal
**Versión:** 0.9 (borrador de trabajo)
**Plataforma objetivo:** PC (Steam), premium
**Estado:** preproducción, sin código escrito

---

## 1. Resumen del producto

Roguelite de gestión y autobatalla en el que el jugador dirige un equipo de fútbol 7 formado por criaturas fantásticas. Los partidos se resuelven solos en 60-90 segundos sobre un campo compacto en cuadrícula. Toda la decisión ocurre entre partidos: colocación, perks, equipamiento, fichajes, tratamiento de lesionados y preparación de consumibles.

La identidad del juego no es el fútbol, es la **carnicería administrada**: los jugadores se lesionan, mueren, se sustituyen por prótesis y desarrollan vínculos entre ellos. El desgaste de la plantilla es el recurso central de la partida.

### Referencias de diseño

| Referencia | Qué se toma |
|---|---|
| Despot's Game | Estilo visual, densidad de unidades, tono cínico |
| Super Auto Pets | Estructura de autobatalla, sinergias por etiquetas |
| Slay the Spire | Mapa ramificado, desgaste como recurso, bosses con modificadores |
| Blood Bowl | Violencia como mecánica, razas con identidad de juego |
| Balatro | Modificadores de regla en el jefe que fuerzan diversidad de builds |

### No objetivos

- No es un simulador de fútbol. No hay reglamento realista ni 90 minutos.
- No es un juego de cartas. No hay mazo, mano ni descarte.
- El jugador **nunca** controla a un jugador durante el partido.
- No hay multijugador, servidor ni componente online más allá de Steam.
- No hay soporte móvil en el alcance actual.

---

## 2. Glosario

| Término | Definición |
|---|---|
| **Run** | Partida completa, desde el equipo inicial hasta el jefe final o la derrota |
| **Acto** | Segmento del mapa terminado en jefe. Una run tiene 3 actos |
| **Nodo** | Punto seleccionable del mapa (partido, tienda, clínica, evento...) |
| **Casilla** | Unidad de la cuadrícula de colocación |
| **Correa** | Radio máximo de desplazamiento de un jugador respecto a su casilla-hogar |
| **Jugada** | Secuencia continua de juego desde una recuperación hasta un tiro o pérdida |
| **Fase** | Uno de los tres tramos en que se divide un partido |
| **Perk** | Efecto permanente ligado a un jugador |
| **Etiqueta (tag)** | Descriptor de un jugador que los perks consultan |
| **Vínculo** | Relación positiva o negativa entre dos jugadores de la plantilla |

---

## 3. Requisitos funcionales

### 3.1 Estructura de la partida

- **RF-001** Una run se compone de 3 actos. Cada acto contiene entre 10 y 12 nodos y termina en un nodo de jefe obligatorio.
- **RF-001b** Los jefes de los actos 1 y 2 aplican **un** modificador de regla cada uno.
- **RF-001c** El jefe del acto 3 es el **jefe final**: dificultad marcadamente superior. Plantilla íntegramente legendaria, **dos** modificadores de regla activos a la vez y una condición de derrota adicional propia.
- **RF-002** La run termina en victoria al derrotar al jefe del acto 3.
- **RF-002b** La run termina en derrota por dos vías únicamente: perder un partido de **jefe**, o que el número de jugadores disponibles baje de **5 en cualquier momento**, incluido durante un partido. Una lesión grave o una muerte en pleno partido con solo 5 en campo termina la run al instante.
- **RF-002c** Perder un partido ordinario no termina la run. No otorga recompensa de perk ni oro, pero aplica lesiones y experiencia con normalidad.
- **RF-002d** Se puede jugar en **inferioridad numérica**, con 5 o 6 jugadores, dejando casillas vacías en la cuadrícula. Es una decisión legítima frente a desviarse hacia un mercado, y el jugador la toma sabiendo que con 5 en campo una sola baja termina la run. La interfaz lo advierte de forma explícita antes de confirmar la alineación.
- **RF-002e** El contador de jugadores disponibles frente al mínimo es visible permanentemente en el mapa. Los nodos de mercado se distinguen a golpe de vista, porque son los nodos que salvan runs.
- **RF-003** Duración objetivo de una run completa: 75-100 minutos.
- **RF-003b** Con 30-36 nodos por run, no más del 60% pueden ser partidos. El resto son tienda, mercado, clínica, taller, entrenamiento y eventos. Un mapa mayoritariamente de partidos agota al jugador y diluye las decisiones.
- **RF-004** El jugador elige el club inicial antes de empezar. **Todos los jugadores del club inicial pertenecen a una única raza.** Cada club define esa raza, la plantilla inicial, el oro de partida y una regla especial.
- **RF-004b** La sinergia no procede de mezclar razas, sino de las **etiquetas** que portan individuos de una misma raza, que varían entre ellos (RF-024b). Dos orcos del mismo club pueden tener perfiles opuestos y habilitar builds distintas.
- **RF-004c** La única vía para incorporar jugadores de otra raza durante la run es el fichaje de mercenarios (RF-110).
- **RF-005** La plantilla inicial consta de 7 titulares y 3 suplentes. Uno de los 10 es de rareza superior a común.
- **RF-006** Al terminar una run, con victoria o derrota, se conservan todos los logros conseguidos durante ella. Los logros **no son incrementales**: desbloquean contenido (RF-125) pero nunca otorgan ventaja en la run siguiente.
- **RF-007** El jugador puede abandonar una run desde el mapa en cualquier momento. Se aplica RF-006.

### 3.2 Mapa

- **RF-010** El mapa de cada acto es un grafo dirigido con múltiples caminos, avance en una sola dirección y sin retroceso.
- **RF-011** Tipos de nodo: partido de liga, partido de élite (mayor riesgo y recompensa), mercado, clínica, taller de implantes, entrenamiento, evento aleatorio, jefe.
- **RF-011b** Hay un nodo de mercado cada **3-4 nodos**, y desde cualquier punto del mapa debe existir un mercado alcanzable en dos saltos como máximo. Es lo que convierte jugar en inferioridad en una decisión y no en una trampa.
- **RF-015** Los **rivales son estáticos por acto y división**: cada acto tiene un conjunto fijo de equipos rivales, diseñados a mano con una build reconocible cada uno. Lo aleatorio es el mapa, la posición de los nodos y qué rivales aparecen en qué nodo. Los rivales son personajes que el jugador aprende, y el informe de ojeo describe una build real, no un bloque de estadísticas.
- **RF-015b** Los rivales usan consumibles y sobornos. Ambos aparecen en el informe de ojeo.
- **RF-012** Cada nodo de partido muestra un **distintivo de dificultad** legible de un vistazo: 5 niveles, cada uno con color e icono propios, que resume la amenaza del rival sin necesidad de abrir nada.
- **RF-012b** Antes de seleccionar un nodo de partido, el jugador puede abrir el **informe de ojeo completo**: plantilla íntegra del rival, formación, posiciones, atributos numéricos, rasgos, etiquetas y la totalidad de sus perks. Es opcional y gratuito. El distintivo sirve a quien no quiera profundizar; el informe, a quien optimice.
- **RF-012c** En la pantalla de alineación, cada jugador propio muestra un **indicador de riesgo de lesión** calculado contra la plantilla rival concreta y la colocación actual, con explicación textual ("Alto: dos Brutos con Verdugo en tu banda derecha"). El indicador se recalcula al mover jugadores en la cuadrícula.
- **RF-012d** Principio rector: **todo lo malo que ocurra en un partido debe haber sido previsible con la información disponible antes de empezarlo.** El jugador conoce las condiciones, prepara la estrategia y asume los riesgos a sabiendas. Ningún sistema puede introducir un daño que no estuviera anunciado.
- **RF-013** Los perks rivales capaces de causar la muerte de un jugador propio aparecen destacados en el informe. No se admiten muertes sin telegrafiar.
- **RF-014** El nodo de jefe se muestra desde el principio del acto, pero **su modificador de regla permanece oculto hasta llegar a él**.
- **RF-014b** Una vez descubierto, el modificador de ese jefe queda registrado en el compendio y pasa a ser visible desde el inicio del acto en todas las runs posteriores. La sorpresa se paga una sola vez y el conocimiento acumulado se convierte en progresión meta legítima.

### 3.3 Plantilla y jugadores

- **RF-020** Tamaño de plantilla: mínimo 7, máximo 12 jugadores.
- **RF-020b** Los nombres se producen con un **generador por raza** definido en `/data`. Ningún jugador se llama "Jugador 3".
- **RF-021** Cada jugador posee: nombre, raza, **posición**, rareza, nivel, experiencia, atributos numéricos, **rasgos**, etiquetas, lista de perks, slots de equipamiento, estado físico, vínculos y salario.
- **RF-022** Atributos numéricos, en rango 1-99: **fuerza, velocidad, técnica, resistencia y correa**. La precisión se integra en técnica; la agresividad se expresa mediante rasgos, no como cifra.
- **RF-022b** **Posición**: portero, defensa, centrocampista o delantero. Determina el comportamiento del jugador en la simulación (RT-090) y restringe las filas y columnas de la cuadrícula donde puede colocarse.
- **RF-022c** **Rasgos**: descriptores cualitativos que alteran el comportamiento, no solo las cifras. Conjunto inicial: agresivo, rápido, goleador, tiro lejano, cerebral, sucio, resistente, cobarde, líder y vago. Cada jugador porta entre 1 y 3.
- **RF-022d** Los rasgos son funcionalmente etiquetas consultables. Raza, posición y rasgo comparten el mismo sistema de etiquetado, de modo que un solo mecanismo alimenta todas las sinergias.
- **RF-023** Rareza determina el **punto de partida y el techo de perks**, nunca el techo de nivel. Todos los jugadores pueden alcanzar el nivel 8.

| Rareza | Perks iniciales | Slots de perk | Nivel máximo | Slots de equipo |
|---|---|---|---|---|
| Común | 0 | 2 | 8 | 1 |
| Raro | 1 | 3 | 8 | 1 |
| Legendario | 2 | 4 | 8 | 1 |

- **RF-023b** Un jugador común que sobrevive toda la run debe seguir siendo competitivo ante el jefe final. Las decisiones tempranas no caducan por diseño.

- **RF-024** Un jugador común de nivel máximo con buenos perks debe poder superar en rendimiento a un legendario de nivel bajo. El balanceo debe verificarlo (ver RT-062).
- **RF-024b** La raza fija un **sesgo poblacional** que desplaza la media de sus atributos, pero cada jugador generado recibe además un **sesgo individual** que puede contradecirlo. Debe ser posible un orco técnico y lento, o un elfo agresivo y torpe. La raza describe a la población, nunca al individuo.
- **RF-024c** La generación de jugadores combina: media de raza, desviación individual, posición y de 1 a 3 rasgos. Dos jugadores de la misma raza y posición no deben sentirse intercambiables.
- **RF-025** La experiencia se reparte tras cada partido: 100% a los que jugaron, 45% a los suplentes.
- **RF-026** El nodo de entrenamiento permite asignar experiencia dirigida al jugador que el usuario elija.
- **RF-027** Subir de nivel incrementa atributos base. No otorga perks.

### 3.4 Razas

- **RF-030** El juego contempla 9 razas jugables: 5 en el lanzamiento y 4 reservadas para expansión o DLC.
- **RF-031** Cada raza aporta un sesgo poblacional de atributos, un conjunto de etiquetas propias y una regla o afinidad exclusiva.
- **RF-032** Como cada raza equivale a un **club inicial completo** (RF-004), debe sostener por sí sola al menos tres builds viables y distintas. Este requisito encarece cada raza de forma sustancial frente a un diseño de plantilla mixta.
- **RF-033** Las razas de tamaño grande ocupan **2 casillas contiguas** en la cuadrícula de colocación.
- **RF-034** Las razas distintas a la inicial se desbloquean **al completar logros concretos**, nunca por acumulación de partidas jugadas.
- **RF-035** No-muertos y vampiros disponen de mecánicas de retorno o drenaje (RF-096).

| Raza | Lanzamiento | Sesgo poblacional | Etiqueta | Nota de diseño |
|---|---|---|---|---|
| Humanos | Sí | Equilibrado | `Neutral` | Club de referencia y tutorial implícito |
| Orcos | Sí | Fuerza, agresividad | `Bruto` | Núcleo de las builds de violencia |
| Elfos | Sí | Técnica, precisión | `Fino` | Muy vulnerables a agresividad |
| Enanos | Sí | Resistencia, defensa | `Muro` | Correa corta, tamaño reducido |
| No-muertos | Sí | Recuperación | `Frío` | Resurrección, inmunes a moral y duelo |
| Elfos oscuros | DLC | Velocidad, juego sucio | `Ponzoña` | Faltas que no se señalan |
| Demonios | DLC | Fuerza extrema | `Enorme` | Tamaño grande, ocupan 2 casillas |
| Vampiros | DLC | Drenaje | `Sanguijuela` | Se curan con las lesiones que provocan |
| Lagartos | DLC | Regeneración | `Escamas` | Convierten lesiones graves en leves |

### 3.5 Cuadrícula y colocación

- **RF-040** El campo se divide en una cuadrícula de 16 columnas por 5 filas.
- **RF-041** Cada jugador se asigna a una casilla-hogar antes del partido. La colocación es libre dentro de la mitad propia, salvo el portero, que ocupa una casilla fija.
- **RF-042** Cada jugador tiene una **correa**: un radio en casillas dentro del cual puede desplazarse durante el partido. Fuera de ese radio no persigue el balón.
- **RF-043** La correa es un atributo modificable por perks, equipamiento y consumibles.
- **RF-044** La adyacencia entre casillas-hogar es la base de los perks de sinergia posicional.
- **RF-045** En la pantalla de colocación se muestran todas las correas simultáneamente. Durante el partido solo se muestra la del jugador seleccionado o señalado.

### 3.6 Simulación del partido

- **RF-050** Un partido dura entre 60 y 90 segundos a velocidad x1. Velocidades disponibles: x1, x1.5, x2, x4 y saltar al resultado.
- **RF-050b** La velocidad x4 debe ser legible. Si a x4 el partido no se entiende, es un fallo de legibilidad del núcleo, no un problema de opciones.
- **RF-051** El partido se estructura en **jugadas**. Cada jugada resuelve en fases: recuperación, progresión, último tercio, definición.
- **RF-052** El saque inicial coloca a ambos equipos de forma que el primer contacto ocurra en los 2 primeros segundos.
- **RF-053** Las reanudaciones (banda, córner, saque de puerta) son instantáneas, con una animación superpuesta de 1 segundo que **no** detiene el reloj.
- **RF-054** Solo detienen el partido: penalti y tarjeta roja. Son los únicos puntos de pausa dramática.
- **RF-055** El tiempo reglamentario es **una sola fase** con reglas normales durante el 100% del partido. La variación en el arbitraje procede exclusivamente del rasgo y el criterio del árbitro (3.6b), no del reloj.
- **RF-055b** **Gol de oro de la turba.** Solo si el partido termina en empate, se juega una prórroga a gol de oro: el primer gol decide. Al empezar, **el árbitro abandona el campo**: no se señala ninguna falta ni se muestra ninguna tarjeta, y el criterio deja de aplicarse. El campo se estrecha 1 fila por lado, el público invade **casillas fijas y anunciadas** (siempre las mismas filas exteriores), y la velocidad global sube un 15%. Al ser las casillas conocidas, la prórroga es un problema de colocación anticipable, no un castigo aleatorio (RF-012d).
- **RF-055d** La turba es el único tramo del partido sin árbitro. Las builds de violencia tienen ahí su ventana natural y las builds técnicas su mayor riesgo, lo que da a ambos bandos un motivo para buscar o evitar el empate.
- **RF-055c** Los empates no existen como resultado final. Todo partido tiene ganador.

- **RF-056** La entrada en la turba se anuncia visualmente de forma inequívoca, incluida la salida del árbitro como highlight. El estado del partido (reglamentario o turba) es visible en todo momento en la interfaz.
- **RF-057** Solo hay contacto físico entre jugadores que disputan el balón o que se encuentran en la trayectoria de la jugada activa. No hay peleas paralelas sin relación con el juego.
- **RF-058** Condición de victoria principal: más goles al final del partido.
- **RF-059** Condición de victoria secundaria: si un equipo queda con menos de 5 jugadores en campo, pierde por incomparecencia inmediata.
- **RF-060** El simulador debe ser reproducible: la misma semilla y el mismo estado inicial producen exactamente el mismo partido.

### 3.6c El portero

- **RF-057b** El portero **nunca abandona el área**. Su correa define cuánto del área cubre, y siempre queda contenida en ella.
- **RF-057c** La parada se resuelve como un **porcentaje**: base del 50%, más la media ponderada de los atributos relevantes al tipo de tiro, menos la calidad del tiro. Fuerza gobierna tiros lejanos y potentes y el aguante ante cargas; velocidad, los reflejos en tiros cercanos y uno contra uno; técnica, la colocación y los penaltis; resistencia, que el porcentaje no decaiga tras tiros consecutivos ni en la turba. No hay atributos exclusivos de portero.
- **RF-057d** El portero **puede recibir cargas** dentro del área. Es un objetivo legítimo de las builds de violencia y un motivo para que las builds defensivas protejan su área.
- **RF-057e** Existen rasgos propios de portero ("Gato", "Muro", "Sale mucho") visibles en el ojeo, y perks y objetos que solo aplican a la posición.

### 3.6b El árbitro

- **RF-061** Cada partido lo dirige un **árbitro con nombre, retrato y un rasgo**. El rasgo es visible en el informe de ojeo (RF-012b) y en el distintivo del nodo. Rasgos iniciales: estricto, permisivo, casero (favorece al equipo local), tuerto (no ve las faltas en un lado del campo, indicado), cobarde (nunca muestra roja), corrupto (sobornos más baratos y menos arriesgados) e incorruptible (no admite sobornos).
- **RF-061b** Existe un grupo de **6-8 árbitros por run**. Se repiten a lo largo de los actos y **recuerdan** los sobornos recibidos (RF-064c).
- **RF-062** **Criterio**: valor entre -100 y +100, con 0 como neutral. Positivo es favorable al jugador. Es visible en el HUD durante todo el partido, en la pantalla de alineación y en el informe post-partido.
- **RF-063** Cada acción sucia (falta, entrada dura, simulación) desplaza el criterio **en contra del equipo que la comete**, se haya señalado o no. El árbitro toma nota aunque no pite. La magnitud depende de la gravedad y del rasgo del árbitro. El desplazamiento se muestra como texto flotante.
- **RF-064** Efectos del criterio sobre la simulación: probabilidad de señalar falta contra cada equipo, umbral de tarjeta, probabilidad de penalti en falta dentro del área y, en el gol de oro, tolerancia ante las invasiones de la turba. Un criterio de ±60 o más convierte al árbitro en un factor decisivo y visible.
- **RF-064b** **Soborno**: consumible de la familia sucia, utilizable antes del partido o manualmente durante él. Antes de confirmar, el jugador ve la **tabla completa de resultados posibles con sus probabilidades**, que dependen del rasgo del árbitro. Ejemplo de tabla base: +30 de criterio (55%), anular la próxima tarjeta propia (20%), penalti a favor en la próxima falta rival en el área (15%), no ocurre nada y se pierde el oro (7%), **denuncia** (3%).
- **RF-064c** La probabilidad de denuncia sube **10 puntos por cada soborno previo al mismo árbitro** dentro de la run. Sobornar una vez es táctica; sobornar tres veces al mismo es avaricia, y el jugador lo ve venir.
- **RF-064d** Efecto de la denuncia: criterio a -60 durante el resto del partido y expulsión inmediata del jugador designado como portador del sobre, que el jugador elige al usar el consumible.
- **RF-064e** El árbitro es el **contrapeso de las builds de violencia** y el soborno es su sostén. Métrica obligatoria en `/Balance`: la build de violencia debe ser viable con acceso a sobornos e inviable sin él.
- **RF-064f** Existen perks, objetos y consumibles cuyo propósito explícito es **mitigar el efecto del árbitro** sobre las builds de violencia, de modo que el soborno no sea su única vía. Ejemplos: perk "Cara de inocente" (las faltas propias desplazan el criterio la mitad), objeto "Amigo de la federación" (el partido empieza con +15), consumible "Protesta del banquillo" (anula el último desplazamiento negativo del criterio), rasgo "Sucio pero discreto" (las entradas de este jugador no desplazan el criterio si no se señalan).
- **RF-064g** Métrica en `/Balance`: la build de violencia debe alcanzar una tasa de victoria de referencia combinando sobornos con al menos dos de estas mitigaciones, sin depender de ninguna en exclusiva.

### 3.7 Sistema de perks

- **RF-065** Un perk es un dato, no código. Su estructura es: `disparador`, `condición`, `efecto`, `alcance`, `límite`.
- **RF-066** Los perks se suscriben a eventos emitidos por el simulador. Catálogo mínimo de eventos:

```
INICIO_PARTIDO      FIN_PARTIDO       INICIO_TURBA      ARBITRO_SE_VA
INICIO_JUGADA       FIN_JUGADA
PASE_INTENTADO      PASE_COMPLETADO   PASE_FALLADO
REGATE_INTENTADO    REGATE_GANADO     REGATE_PERDIDO
DUELO_AEREO         ENTRADA           RECUPERACION
TIRO                GOL               PARADA
FALTA               TARJETA           LESION      MUERTE
SUSTITUCION         CONSUMIBLE_USADO
```

- **RF-067** Cada evento transporta contexto: ejecutor, receptor, rival implicado, casilla, zona del campo, estado del partido (reglamentario o turba), criterio del árbitro y distancia a portería.
- **RF-068** Los perks consultan **etiquetas**, nunca jugadores concretos. Esto permite escalar el catálogo sin casos especiales.
- **RF-069** Distribución objetivo del catálogo de perks:

| Tipo | Proporción | Descripción |
|---|---|---|
| Relleno con condición | 60% | Modificadores numéricos condicionados. Dan grosor a las builds |
| Condicionales interesantes | 30% | Cambian el comportamiento en situaciones concretas |
| Rompe-reglas | 10% | Anulan o invierten una regla del simulador |

- **RF-070** Al menos 15 perks deben acumular efecto **entre partidos** dentro de la misma run, para crear curvas de escalado.
- **RF-071** Tras cada partido **ganado**, el jugador elige 1 recompensa entre 3 opciones aleatorias. Cada opción puede ser un **perk**, un **jugador** o un **objeto de equipamiento**; las tres pueden ser de tipos distintos. Si es un perk, elige además a qué jugador se lo asigna.
- **RF-071b** El jugador dispone de un **reroll** de las 3 opciones por nodo de recompensa. El primero cuesta poco oro y el coste crece con cada uso dentro de la run.
- **RF-072** Un perk asignado no puede retirarse ni transferirse.

### 3.8 Equipamiento

- **RF-075** El equipamiento es transferible entre jugadores en cualquier momento fuera de partido.
- **RF-076** Cada jugador lleva **un único objeto** equipado, sea cual sea su rareza. La decisión no es cuántos, sino cuál y en quién.
- **RF-076b** Los objetos pueden venderse en cualquier nodo de tienda por una fracción de su valor.
- **RF-077** Tres arquetipos obligatorios en el catálogo:
  - **Maldito**: efecto potente con contrapartida permanente.
  - **Frágil**: se destruye tras N usos o si el portador se lesiona.
  - **Restringido**: solo funciona sobre portadores con una etiqueta concreta.
- **RF-078** El equipamiento tiene rareza propia, con la misma escala de nomenclatura que los jugadores.

### 3.9 Consumibles

- **RF-080** Antes de cada partido el jugador equipa hasta 3 consumibles.
- **RF-081** Hasta 2 de ellos se configuran como **condicionales**: se asocian a un disparador y se ejecutan solos durante el partido.
- **RF-082** Al menos 1 slot es **manual**: el jugador lo activa cuando quiere durante el partido.
- **RF-083** Disparadores condicionales disponibles como mínimo: marcador por debajo, marcador empatado, últimos 20 segundos, entrada en la turba, lesión propia, tarjeta roja propia, N goles encajados, criterio del árbitro por debajo de un umbral.
- **RF-084** Familias de consumibles: médicos, tácticos, sucios y sobrenaturales.
- **RF-085** Los consumibles se consumen al usarse y no persisten entre partidos.

### 3.10 Lesiones, muerte y taller

- **RF-090** Estados físicos posibles: sano, lesión leve, lesión grave, muerto.
- **RF-091** **Lesión leve**: -15% a todos los atributos durante el siguiente partido. Acumulable. No impide jugar.
- **RF-092** **Lesión grave**: el jugador no puede alinearse hasta recibir tratamiento en un nodo de clínica o taller.
- **RF-093** **Muerte**: pérdida permanente del jugador. Solo puede producirse en dos casos:
  1. El jugador se alineó arrastrando una lesión grave sin tratar.
  2. Un perk rival explícitamente marcado como letal y visible en el ojeo (RF-013).
  Un jugador en estado sano **nunca** puede morir.
- **RF-094** **Clínica**: coste alto en oro, resultado garantizado, restaura al jugador a sano sin efectos secundarios.
- **RF-095** **Taller de implantes**: alternativa barata y arriesgada a la clínica. Antes de confirmar, el jugador ve los **tres resultados posibles con su probabilidad**:
  - **Curación completa**: el jugador vuelve a sano.
  - **Mejora**: se instala una prótesis con ventaja (atributo o correa) y la etiqueta `Chatarra`.
  - **Empeoramiento**: se instala una prótesis con desventaja y la etiqueta `Chatarra`.
- **RF-095b** El jugador puede **invertir oro adicional** para desplazar las probabilidades hacia los resultados favorables, con rendimiento decreciente. La apuesta sigue siendo apuesta, pero el jugador la asume conociendo las cifras exactas (RF-012d).
- **RF-095c** Con 3 prótesis el jugador pierde su etiqueta racial y adquiere `Autómata`, habilitando una familia de perks distinta.
- **RF-096** **Resurrección**: disponible mediante perk, objeto o consumible de la familia sobrenatural. El jugador resucitado vuelve con el nivel máximo reducido en 2 y la etiqueta `Descompuesto`, que aplica una penalización creciente por cada partido posterior.
- **RF-097** El estado físico persiste entre partidos dentro de la run y se muestra siempre en la pantalla de plantilla.

### 3.11 Vínculos

- **RF-100** Los vínculos se forman a partir de eventos compartidos concretos y verificables, nunca de forma aleatoria.
- **RF-101** Cada jugador soporta un máximo de 2 vínculos simultáneos.
- **RF-102** Tipos mínimos de vínculo positivo:
  - **Sociedad**: 3 asistencias de A que terminan en gol de B.
  - **Deuda de sangre**: A comete una falta defendiendo a B tras una entrada dura sobre B.
  - **Muro**: 2 partidos consecutivos como pareja defensiva sin encajar.
- **RF-103** No existen vínculos negativos en el lanzamiento. Las rivalidades quedan fuera del alcance y pueden incorporarse en una actualización si el sistema positivo funciona.
- **RF-104** Cuando un jugador con vínculo positivo muere, se vende o queda con lesión grave, sus vinculados entran en estado de **duelo** durante 3 partidos, con penalización de atributos.
- **RF-105** La formación de un vínculo se comunica con un highlight (RF-115).
- **RF-106** Los vínculos se visualizan como líneas entre retratos en la pantalla de alineación.

### 3.12 Mercenarios

- **RF-110** Los mercenarios son jugadores de raza distinta a la mayoritaria del club, disponibles en el mercado de fichajes con estadísticas superiores a la media de su rareza.
- **RF-111** Restricciones obligatorias del mercenario:
  - No aporta la etiqueta racial del club y rompe las sinergias raciales.
  - Cobra un salario por partido que se descuenta del oro disponible.
  - No puede formar vínculos y cuenta como `Extraño` para los perks de cohesión.
  - Abandona el club si acumula 3 partidos consecutivos sin jugar o si el equipo encadena 3 derrotas.
- **RF-112** Los mercenarios son la única vía de ver en juego razas que aún no se han desbloqueado, lo que los convierte también en escaparate del contenido pendiente.
- **RF-113** Las razas de tamaño grande contratadas como mercenarios ocupan 2 casillas, con el coste de colocación que eso implica.

### 3.12b Mercado, ventas y cantera

- **RF-114** El nodo de mercado es la **única tienda del juego** y ofrece cuatro categorías simultáneas, cada una con 3-4 artículos: **jugadores**, **perks**, **equipamiento** y **consumibles**. El surtido se genera al llegar al nodo y no se renueva. La estructura toma como referencia la tienda de Rune Dice, con sus dados, reliquias y runas como equivalentes de jugadores, equipamiento y perks.
- **RF-114e** Los perks comprados en el mercado se asignan al instante a un jugador de la plantilla con slot libre. Es la única forma de obtener un perk sin ganar un partido.
- **RF-114f** El jugador puede **vender jugadores** en el nodo de mercado. El precio de venta parte de un valor base por rareza y nivel, y aumenta por cada perk asignado y por cada vínculo activo.
- **RF-114b** Todo nodo de mercado ofrece, además de los fichajes de pago, entre 1 y 2 **canteranos**: jugadores comunes de la raza del club, **gratuitos**, con atributos muy bajos. Amortiguan una run mala, pero solo si el jugador pasa por el nodo. Quien no tenga oro se lleva un mal jugador; quien no pase por el mercado, no se lleva nada.
- **RF-114c** Los canteranos ganan experiencia un **33% más rápido** que el resto. Son malos hoy y potencialmente los mejores del acto 3 si se fichan pronto. Así el mercado interesa también a quien va bien, y no es solo un nodo de emergencia.
- **RF-114d** El canterano es un jugador completo: sube de nivel, recibe perks y forma vínculos. Un canterano que llega al jefe final es una historia que el juego debe permitir.

### 3.12c Economía

- **RF-114g** El oro se gana **en cada partido ganado**, con una cantidad fija por acto y un multiplicador por dificultad del rival. Los partidos de élite y de jefe pagan más. Perder no paga.
- **RF-114h** **Partido excelente**: bonus de oro por cumplir objetivos concretos y anunciados antes del partido, como ganar por 3 o más, portería a cero, ganar en inferioridad o ganar con un canterano goleador. Los objetivos se muestran en la pantalla del nodo.
- **RF-114i** El oro **nunca escala con el rendimiento** dentro del partido (goles, lesiones causadas, faltas). Las builds de economía existen, pero escalan mediante perks y objetos, no mediante el resultado.
- **RF-114j** Las otras fuentes de oro son la venta de jugadores y objetos, y determinados eventos. No hay más.
- **RF-114k** Sumideros: jugadores, perks, objetos y consumibles en el mercado; clínica; taller; sobornos; rerolls; salarios de mercenarios. `/Balance` debe verificar que el oro medio por acto permite usar dos o tres sumideros, nunca todos.

### 3.13 Presentación y feedback

- **RF-115** **Highlights**: paneles ilustrados que aparecen sobre el partido en eventos relevantes. Se componen por capas: fondo según tipo de evento, pose según raza y acción, y color o retrato del jugador implicado.
- **RF-116** El sistema puntúa cada evento por relevancia (rareza del perk implicado, momento del partido, primera aparición, ajuste del marcador) y muestra como máximo **2 highlights por partido**.
- **RF-117** Todo highlight es saltable con cualquier tecla. En opciones puede desactivarse por separado cada tipo de notificación: highlights, texto flotante de perks, desplazamientos de criterio y anuncio de la turba.
- **RF-118** Cada activación de perk durante el partido produce feedback visible: icono, número flotante y destello del color del perk.
- **RF-119** **Informe post-partido**: pantalla obligatoria que lista cada perk activado, número de activaciones y contribución medible (goles, lesiones causadas, recuperaciones), más un apartado del árbitro con la evolución del criterio y las faltas no señaladas a cada equipo. Es el principal vehículo de aprendizaje del jugador.
- **RF-120** **Repetición**: el jugador puede rever la jugada mejor puntuada del partido, reconstruida desde la semilla.
- **RF-121** **Log de eventos**: tres líneas de texto siempre visibles bajo el campo describiendo las acciones recientes. Es también la única ventana al simulador durante el desarrollo temprano.
- **RF-122** **Ceremonia de muerte**: la muerte de un jugador produce una pantalla de obituario con sus estadísticas de la run. Al terminar la run, un **memorial** lista a todos los caídos. Es el cierre del arco emocional que abre el apego, y la pantalla de fin de run incluye una imagen compartible.
- **RF-123** **Primera run guiada**: el primer acto de la primera run tiene mapa fijo y tres partidos guiados que presentan un sistema cada uno (cuadrícula y correas; perks y etiquetas; árbitro y consumibles). No vuelve a aparecer.

### 3.14 Progresión meta

- **RF-125** Cada raza adicional se desbloquea al completar un **logro específico y anunciado**. Ejemplos de la forma esperada: terminar una run sin perder ningún jugador desbloquea enanos; provocar 30 lesiones en una sola run desbloquea orcos; ganar el acto 1 con la plantilla mínima desbloquea elfos.
- **RF-125b** Los logros de desbloqueo son visibles desde el principio, con su progreso, para que el jugador pueda perseguirlos de forma deliberada en lugar de tropezarse con ellos.
- **RF-126** Se desbloquean perks, objetos y consumibles que se incorporan al conjunto disponible en runs posteriores.
- **RF-127** No hay progresión meta que otorgue poder puro acumulativo. Los desbloqueos añaden variedad, no ventaja.
- **RF-128** **Divisiones** como sistema de ascensión, al estilo de las apuestas de Balatro: niveles fijos y acumulativos con identidad temática. Una run recorre los tres actos de una división (C, B, A). Cada división añade una regla sobre las anteriores:

| División | Regla añadida |
|---|---|
| Tercera | Run base |
| Segunda | Los rivales traen consumibles y sobornan |
| Primera | No hay canteranos gratuitos; el mercado los vende |
| Continental | Cada jefe lleva dos modificadores; el final, tres |
| Mundial | Ningún árbitro es neutro: todos corruptos o incorruptibles |

- **RF-128b** Cada división se desbloquea ganando la anterior **con esa raza**. La rejilla razas por divisiones es el mapa de objetivos a largo plazo.
- **RF-128c** **Copa**: semilla diaria compartida con tabla de clasificación en Steam. Requiere determinismo entre plataformas (RT-023b), por lo que es contenido posterior al lanzamiento.

---

## 4. Requisitos técnicos

### 4.1 Stack

- **RT-001** Motor: Godot 4.6 o superior, rama .NET.
- **RT-002** Lenguaje: C# sobre .NET. Justificación: velocidad en simulación por lotes, tipado estático sobre un modelo de datos grande y acceso a NuGet.
- **RT-003** Integración con Steam mediante Steamworks.NET o Facepunch.Steamworks.
- **RT-004** Control de versiones: Git, con Git LFS para binarios de arte a partir de la fase 3.
- **RT-005** Editor de código: libre. Extensión `godot-tools` para VS Code o Rider.

### 4.2 Arquitectura

- **RT-010** Estructura de la solución:

```
/Sim            Librería .NET pura. Cero referencias a Godot
/Sim.Tests      Pruebas unitarias y estadísticas del simulador
/Balance        Aplicación de consola para simulación por lotes
/Game           Proyecto Godot. Referencia a /Sim
/data           JSON de perks, objetos, razas, formaciones, clubes
/tools          Validadores de datos y scripts auxiliares
```

- **RT-011** La dependencia es unidireccional: `/Game` conoce `/Sim`. `/Sim` **nunca** referencia `/Game`, Godot ni ninguna API de presentación. Esta regla no admite excepciones.
- **RT-012** `/Sim` no realiza entrada/salida, no accede a ficheros y no consulta el reloj del sistema.
- **RT-013** El simulador expone una única superficie pública: recibe un estado inicial y una semilla, y devuelve una secuencia ordenada de eventos más el estado final.
- **RT-014** La capa de render consume la secuencia de eventos. No calcula ni decide nada del partido.
- **RT-015** No se usa el motor de físicas de Godot para el partido. El movimiento se implementa en `/Sim` con vectores propios.

### 4.3 Determinismo

- **RT-020** El simulador avanza en ticks lógicos de frecuencia fija: 15 por segundo. La interpolación entre ticks ocurre solo en el render.
- **RT-021** Toda aleatoriedad procede de instancias explícitas de generador con semilla. Queda prohibido el uso de generadores globales o estáticos.
- **RT-022** Se mantienen flujos de aleatoriedad separados e independientes: uno para el partido, uno para la generación del mapa, uno para las recompensas. Cambiar las recompensas no debe alterar el desarrollo de un partido con la misma semilla.
- **RT-023** Atributos, probabilidades y contadores se calculan con **aritmética entera**. Las posiciones pueden usar `float`. El requisito de determinismo es "misma semilla, mismo binario, mismo resultado"; el determinismo entre plataformas distintas no es un requisito de lanzamiento.
- **RT-023b** Si en el futuro se desea compartir semillas entre plataformas, se migrará a punto fijo (Fix64). No se adopta de forma preventiva.
- **RT-024** Debe existir una prueba automatizada que ejecute el mismo partido dos veces con la misma semilla y verifique que las secuencias de eventos son idénticas. Se ejecuta en integración continua **en Windows y en Linux**; una divergencia entre ambos activa RT-023b.

### 4.4 Modelo de datos

- **RT-030** El estado de la run se define como un esquema explícito y versionado **antes** de implementar cualquier sistema. Contenido mínimo:

```
Run
  semilla, acto, nodoActual, oro, historialNodos, mapa
  Plantilla
    Jugador
      id, nombre, raza, rareza, nivel, experiencia
      atributos { fuerza, velocidad, tecnica, precision, agresividad, resistencia, correa }
      etiquetas[]
      perks[]            (ids)
      equipamiento[]     (ids, por slot)
      estadoFisico       (sano | leve | grave | muerto)
      protesis[]
      salario
      vinculos[]         (idOtroJugador, tipo, signo)
      contadores{}       (acumuladores de perks entre partidos)
  Alineacion
    asignaciones[]       (idJugador, casilla)
  Consumibles
    equipados[]          (id, modo, disparador)
```

- **RT-031** Los perks, objetos, razas, clubes y consumibles se definen en JSON externo, no en código.
- **RT-032** Todo fichero de datos se valida contra un esquema al arrancar. Un dato inválido produce un error explícito, nunca un fallo silencioso.
- **RT-033** Formato de perk:

```json
{
  "id": "sed_de_sangre",
  "nombre": "Sed de sangre",
  "rareza": "raro",
  "tipo": "condicional",
  "disparador": "ENTRADA",
  "condicion": "ejecutor.tiene('Bruto') && criterio < 0",
  "efecto": { "tipo": "modificar_atributo", "objetivo": "ejecutor", "atributo": "fuerza", "valor": 3, "duracion": "jugada" },
  "limite": { "por": "parte", "veces": 2 },
  "acumulaEntrePartidos": false
}
```

- **RT-034** Las condiciones se evalúan con **NCalc**, ampliado con funciones propias (`tiene`, `turba`, `criterio`, `zona`, `adyacente`). No se ejecuta código arbitrario ni se usa reflexión en tiempo de partido. Las expresiones se compilan una vez al cargar `/data`, no en cada evaluación.

- **RT-035** Las **descripciones de perks, objetos y consumibles se generan desde el efecto**, mediante plantillas por tipo de efecto. No existe texto descriptivo escrito a mano para efectos; se localizan las plantillas, nunca el texto final. Es imposible por construcción que la descripción y el efecto diverjan.

### 4.5 Motor de efectos

- **RT-040** Bus de eventos: el simulador publica, los perks activos se suscriben.
- **RT-041** El orden de resolución de perks simultáneos debe ser determinista y documentado. Criterio: rareza descendente, luego id de jugador ascendente, luego id de perk ascendente.
- **RT-042** Debe existir protección contra recursión: un efecto que dispara un evento que dispara el mismo efecto se corta a una profundidad máxima configurable.
- **RT-043** Cada activación de perk se registra con su contexto para alimentar el informe post-partido (RF-119).

### 4.6 Comportamiento de los jugadores

- **RT-089** El comportamiento se organiza en **tres capas** que interactúan:
  1. **Máquina de estados del partido**: saque, juego abierto, reanudación, penalti, gol de oro, fin. Define las reglas activas y las acciones existentes. La salida del árbitro (RF-055b) es una transición de esta máquina.
  2. **Estado táctico del equipo**: en posesión, sin posesión, transición ofensiva, transición defensiva. Se deriva de la posesión del balón y **desplaza las casillas-hogar** de todo el equipo (el bloque sube o baja). Es la capa que produce líneas, bloque y presión, y donde se expresan los estilos de juego (tiki-taka, contragolpe, balón largo, presión alta) mediante el tamaño y la velocidad de ese desplazamiento.
  3. **Máquina de estados del jugador**: colocándose, persiguiendo, conduciendo, pasando, tirando, entrando, derribado, lesionado, celebrando. Cada estado tiene **duración en ticks** y una **lista de acciones legales**. Un jugador derribado no decide nada hasta que expira el estado.
- **RT-089b** La utilidad (RT-090) opera **dentro** de la máquina de estados del jugador: solo puntúa las acciones legales del estado actual, y sus pesos se modulan por posición, rasgos y estado táctico del equipo. La máquina decide qué es posible; la utilidad decide qué es mejor.
- **RT-089c** Las tres máquinas se implementan **en código** en las fases 0 y 1, con transiciones explícitas y una función `Puede(estado, accion)`. Se migran a `/data` solo cuando el número de ajustes demuestre qué necesita ser configurable.
- **RT-090** Cada jugador ejecuta una IA de decisión basada en **utilidad**: en cada tick en que su estado lo permite, evalúa las acciones legales, puntúa cada una y ejecuta la de puntuación más alta.
- **RT-091** La IA **no** emplea aprendizaje automático ni modelos entrenados. Justificación: rompería el determinismo exigido por RT-020, impediría razonar sobre las causas de un desequilibrio, no existen datos de entrenamiento y convertiría cada ajuste de balanceo en un reentrenamiento. La utilidad ponderada es depurable y ajustable desde datos.
- **RT-092** Acciones evaluables mínimas: perseguir el balón, marcar a un rival, ofrecer apoyo, cubrir espacio, pasar, conducir, tirar, entrar, replegar a la casilla-hogar.
- **RT-093** La **posición** define los pesos base de la función de utilidad. Un defensa puntúa alto cubrir espacio y entrar; un delantero, tirar y ofrecer apoyo en el último tercio.
- **RT-094** Los **rasgos** modifican esos pesos. `agresivo` sube el peso de entrar, `goleador` el de tirar, `tiro lejano` amplía la distancia a la que tirar sigue siendo rentable, `cobarde` reduce el peso de disputar duelos, `vago` reduce el de replegar.
- **RT-095** La **correa** actúa como filtro previo: las acciones que exigirían salir del radio se descartan antes de puntuar.
- **RT-096** Todos los pesos por posición y por rasgo residen en `/data`, no en código, y son ajustables sin recompilar.
- **RT-097** Los empates de puntuación se resuelven de forma determinista por identificador de jugador ascendente. Nunca al azar.
- **RT-098** El sistema debe permitir volcar, para un tick concreto, la tabla de puntuaciones de un jugador. Es la herramienta principal de depuración de comportamiento.

### 4.7 Herramientas de balanceo

- **RT-050** `/Balance` es una aplicación de consola que ejecuta N partidos sin abrir Godot y vuelca resultados a CSV.
- **RT-051** Debe ejecutar 10.000 partidos en menos de 60 segundos en una máquina de desarrollo.
- **RT-052** Parámetros mínimos: número de ejecuciones, semilla base, configuración de equipos, filtro de perks.
- **RT-053** Métricas de salida: tasa de victoria por build, goles por partido, lesiones por partido, tasa de activación por perk, duración media de partido.
- **RT-054** Integración continua: el lote de balanceo se ejecuta en cada commit sobre `/Sim` o `/data`.
- **RT-055** El build falla si alguna build catalogada supera el 70% o baja del 30% de tasa de victoria contra el conjunto de referencia.
- **RT-056** **Métricas de sensación de fútbol**, con rango objetivo, comprobadas por `/Balance`. Son el criterio de salida de la fase 0 y el indicador permanente del equilibrio entre fútbol y agresividad:

| Métrica | Rango objetivo inicial |
|---|---|
| Alternancias de posesión por partido | 12-25 |
| Longitud media de cadena de pases | 2-4 |
| Tiros por partido (ambos equipos) | 8-16 |
| Distribución de resultados | Mayoría entre 1-0 y 3-2; menos del 5% por encima de 5 goles totales; menos del 15% de empates |
| Tiempo del balón por tercio | Ningún tercio por encima del 50% |
| Entradas por partido | 6-14 |
| Lesiones por partido | 0,3-0,8 |

- **RT-057** Los rangos son puntos de partida y se revisan con datos, pero un cambio de rango es una decisión explícita, nunca un ajuste silencioso.

### 4.8 Persistencia

- **RT-060** Guardado en JSON local, con número de versión de esquema.
- **RT-061** **Guardado ironman**: un único slot por run. Se guarda automáticamente al completar cada nodo, se borra al cargarse, y no puede copiarse ni restaurarse desde el juego. Salir a mitad de partido reproduce el partido desde la semilla al volver. Sin trampas por recarga y sin runs perdidas por cerrar el juego.
- **RT-061b** Cada run **congela una instantánea de `/data`** al empezar y la guarda consigo. Una actualización del juego nunca altera una run en curso ni invalida sus repeticiones.
- **RT-062** Debe existir un modo de depuración que cargue un estado predefinido (por ejemplo, acto 2 con una plantilla concreta) sin jugar los nodos previos.
- **RT-063** Sincronización con Steam Cloud.
- **RT-064** Los datos de perks y objetos se mantienen externos y versionados, sin cerrar la puerta a un futuro soporte de Steam Workshop.
- **RT-065** **Telemetría opcional y anónima** desde la demo: al terminar una run se envía build, acto alcanzado, causa de derrota y activaciones de perks. Desactivada por defecto en la primera pantalla, con explicación clara. El formato del evento se define en la fase 1 aunque el envío llegue en la 4.

### 4.9 Plataforma y rendimiento

- **RT-070** Resolución de diseño: 1280x800, escalable. Debe ser legible en Steam Deck.
- **RT-071** Navegación completa con mando, sin acciones exclusivas de ratón.
- **RT-072** Objetivo de rendimiento: 60 fps estables con 14 jugadores en campo, partículas y highlights activos.
- **RT-073** Idiomas iniciales: español e inglés. Todo el texto en ficheros de localización desde el primer día.

### 4.10 Pruebas

- **RT-080** Pruebas unitarias sobre `/Sim` con xUnit o NUnit.
- **RT-081** Pruebas estadísticas: ejecutar 1.000 partidos y comprobar que la distribución de resultados está dentro de los márgenes esperados. Estas pruebas valen más que las unitarias en este proyecto.
- **RT-082** Prueba de determinismo obligatoria (RT-024).
- **RT-083** Validación automática de todos los ficheros de `/data` contra esquema en cada commit.
- **RT-084** No se escriben pruebas de interfaz.

---

## 5. Especificación de arte

### 5.1 Sprites

- **RA-001** Vista cenital en tres cuartos. Escala de trabajo: 3x sobre píxel base.
- **RA-002** Cada raza tiene dimensiones propias. La diferenciación se produce por **silueta**, no por paleta. Toda raza debe ser reconocible en blanco y negro.

| Raza | Dimensiones aprox. | Rasgo firma |
|---|---|---|
| Enano | 13x16 | Barba que ocupa medio cuerpo, casco |
| Humano | 12x17 | Referencia neutra |
| No-muerto | 11x17 | Costillas, cuenca ocular vacía |
| Elfo | 11x20 | Altura, estrechez, melena |
| Orco | 16x18 | Hombros, brazos largos, colmillos |
| Elfo oscuro | 11x19 | Silueta élfica, paleta invertida |
| Vampiro | 12x19 | Capa, postura erguida |
| Lagarto | 14x17 | Cola, hocico |
| Demonio | 20x20 | Masa, cuernos, cabeza pequeña |

- **RA-003** Paleta máxima por sprite: 6 colores más equipación. Cada material define tono base, sombra y luz.
- **RA-004** El contorno es un tono muy oscuro **teñido según el material adyacente**, nunca negro puro.
- **RA-005** Fuente de luz: superior izquierda, constante en todas las razas.
- **RA-006** Anclaje del sprite: centro inferior sobre la posición lógica. No centro geométrico.
- **RA-007** Ordenación de dibujo por coordenada vertical descendente.
- **RA-008** Sombra elíptica en el suelo obligatoria para todos los sprites.
- **RA-009** Las razas pequeñas requieren un refuerzo de color de equipo (brazalete o tinte de contorno), porque su área de equipación es insuficiente para identificar el bando.
- **RA-010** Los brazos llevan manga del color de la equipación en las dos primeras filas.
- **RA-011** El desplazamiento vertical de animación es de **1 píxel**. Dos ya se lee como un salto.

### 5.2 Animación

- **RA-015** Dos orientaciones únicamente: derecha e izquierda por volteo horizontal. El sprite se orienta hacia el balón.
- **RA-016** Ciclos requeridos por raza:

| Ciclo | Frames | Duración por frame |
|---|---|---|
| Reposo | 4 | 200 ms |
| Carrera | 6 | 90 ms |
| Entrada | 5 | 80 ms, con pausa en el impacto |
| Caída | 4 | 110 ms, reutilizado para lesión |
| Celebración | 4 | 150 ms |

- **RA-017** Total: 23 frames por raza. 69 frames para las 3 razas del prototipo, 115 para las 5 del lanzamiento y 207 para las 9 completas.
- **RA-019** Cada raza necesita además variantes de posición reconocibles, como mínimo un distintivo de portero. No requieren ciclos propios, solo una capa superpuesta.
- **RA-019b** El árbitro requiere un sprite propio con ciclos de reposo, carrera y señalización (3 ciclos, unos 12 frames), y un retrato por rasgo (7). Una sola raza neutra con recolor.
- **RA-018** Variantes de prótesis: capas superpuestas sobre el sprite base, no sets de animación separados.

### 5.3 Highlights

- **RA-020** Composición por capas: fondo según tipo de evento, pose según raza y acción, identificador del jugador.
- **RA-021** Estilo cómic de alto contraste, paleta reducida, líneas cinéticas. No es pixelart.
- **RA-022** Cobertura mínima: 6 tipos de evento por 3 razas del prototipo.

### 5.4 Dirección visual

- **RA-025** El lenguaje visual es **cultura futbolística real** (marcadores de estadio, vallas publicitarias, pancartas de ultras, prensa deportiva) cruzada con **humor negro y gore**: sangre persistente sobre el césped, patrocinadores siniestros, comentaristas que celebran las lesiones, un mercado de fichajes que parece un matadero.
- **RA-026** Se evita explícitamente la iconografía de calaveras, huesos y marcos góticos, que remite a Blood Bowl y a un lenguaje ajeno. El gore es de estadio, no de mazmorra.
- **RA-027** La sangre es una calcomanía persistente durante el partido y se limpia entre partidos. Es feedback y es historia del partido.

---

## 6. Interfaz

### 6.1 Principios

- **UI-001** **Un solo patrón de inspección** en todas las pantallas. Ratón: clic expande. Mando: foco y un mismo botón. Aplica a fichas, perks, objetos, nodos, artículos de mercado y árbitro.
- **UI-002** **Estado por color y por forma.** Estado físico con cuatro colores y cuatro iconos; equipos por color y por grosor de contorno. El daltonismo queda cubierto sin modo especial.
- **UI-003** **Presupuesto de veinte segundos entre partidos** cuando no se cambia nada. Alineación y consumibles se conservan entre partidos. Del mapa al saque en dos pulsaciones.
- **UI-004** Dos tamaños de texto, ninguno menor de 11 píxeles a 1280x800. Fuente elegida para Steam Deck.
- **UI-005** Sobre el campo solo va **información transitoria** (activación de perk, cambio de estado). Nada permanente sobre los sprites: ni barras, ni iconos de mejora. Toda la información persistente vive en las fichas.
- **UI-006** Dos flujos de entrada completos desde el principio: ratón (arrastrar y soltar) y mando (seleccionar, mover cursor, confirmar). No se adapta uno al otro.

### 6.2 Componente de ficha de jugador

- **UI-010** La ficha tiene tres estados y es el mismo componente en Equipo, Alineación, Partido y Mercado.
- **UI-011** **Colapsada**: una tira de 24 píxeles con retrato, icono de posición, nombre y barra de color de estado físico. Nada más.
- **UI-012** **Expandida**: nivel, cinco atributos, rasgos, perks con icono, objeto, vínculos, estado y salario. Solo una expandida a la vez.
- **UI-013** **Reactiva**: cuando un perk se activa en el partido, la tira de su jugador destella a la vez que el sprite. Pulsar la tira resalta el sprite con corchetes y correa; seleccionar el sprite expande la tira. Es el índice del partido.
- **UI-014** Los iconos de mejora (perks, objeto, prótesis) viven en la ficha, no sobre el sprite.

### 6.3 Pantallas

| Pantalla | Componente clave | Fase |
|---|---|---|
| Mapa | Nodos con distintivo, contador de disponibles frente al mínimo, mercados destacados | 2 |
| **Equipo** | Fuera del partido. Fichas completas, cambio de alineación, posiciones, objetos entre jugadores, consumibles y sus disparadores. Es donde se toman las decisiones de plantilla | 1 |
| Nodo de partido / ojeo | Ficha de rival, árbitro con rasgo, riesgo de lesión por jugador, objetivos de Partido excelente, botón **Empezar partido** | 2 |
| Alineación previa | Solo la cuadrícula con la alineación de Equipo ya cargada. Se puede reposicionar o pulsar Empezar directamente | 1 |
| Partido | Marcador con criterio del árbitro, tiras laterales, campo, log bajo el campo, consumibles, velocidad, contador de disponibles | 1 |
| Recompensa | Tres cartas con reroll | 1 |
| Informe post-partido | Tres titulares y expansión; apartado del árbitro | 1 |
| Mercado | Cuatro columnas, ficha en modo compra, venta | 2 |
| Clínica / Taller | Tabla de probabilidades con inversión de oro | 3 |
| Fin de run / memorial | Estadísticas, caídos, imagen compartible | 3 |

- **UI-020** La pantalla de **Equipo** concentra todas las decisiones de plantilla. La alineación previa al partido es solo una confirmación con opción de reposicionar. Si el jugador no toca nada, "Empezar partido" es una pulsación.
- **UI-021** La pantalla de Equipo se diseña la primera y en detalle; las demás derivan de sus decisiones.

---

## 7. Plan de fases

| Fase | Contenido | Criterio de salida |
|---|---|---|
| **0** | `/Sim` sin gráficos: 7v7, cuadrícula, correas, portero, las tres máquinas de estado, utilidad básica, log de texto. Sin perks | Las métricas de RT-056 entran en rango y los equipos mejores ganan más con sorpresas creíbles |
| **1** | Motor de efectos con descripciones generadas, 20 perks, niveles, nombres, fichajes con rareza, pantalla de Equipo. Círculos de colores | Dos builds distintas ganan de formas distintas y se nota |
| **2** | Bucle de run completo: mapa, 8 partidos, 1 jefe, tienda, mercado con canteranos, lesiones, equipamiento, mercenarios. Máximo 30 perks y 12 objetos | El jugador dice "una run más" sin arte terminado |
| **3** | Pixelart definitivo, animaciones, highlights, vínculos, gol de oro con turba, taller de prótesis, árbitro con rasgos y sobornos | El partido se lee sin necesidad del log |
| **4** | Demo de Steam, sonido, localización, soporte de mando y Steam Deck | Wishlists suficientes para justificar el lanzamiento |

**Regla de fase:** no se produce arte hasta que el diseño de la fase 2 esté cerrado. El arte producido antes de fijar el diseño se descarta.

---

## 8. Riesgos identificados

| Riesgo | Impacto | Mitigación |
|---|---|---|
| El partido no se entiende y el jugador no sabe por qué perdió | Crítico. Rompe el género | RF-118, RF-119 y RF-121 son requisitos de núcleo, no de pulido |
| Alcance excesivo en un proyecto sin fecha | Alto. Prototipo eterno | Plan de fases con criterio de salida objetivo por fase |
| Muerte percibida como injusta | Alto. Reseñas negativas | RF-093: nunca desde estado sano, siempre telegrafiada |
| Exceso de azar post-acción negativo | Alto. Erosiona la agencia | RF-012d como principio rector; ojeo completo, indicador de riesgo, taller con cifras visibles, turba en casillas fijas |
| Runs perdidas por cerrar el juego o suspender la Deck | Alto. Reseñas negativas | RT-061 guardado ironman |
| Espiral de muerte: run perdida sin saberlo | Alto. Frustración larga | RF-114b canteranos en mercado, RF-002d inferioridad numérica, RF-071b reroll, RF-114 ventas, RF-007 abandono con logros |
| Decisiones tempranas que caducan | Medio | RF-023: sin techo de nivel por rareza |
| El jugador salta partidos y nunca ve el arte | Medio | RF-050b: x4 legible; slot manual de consumible como incentivo para mirar |
| El árbitro se percibe como azar puro | Medio | Rasgo visible en ojeo, criterio visible en HUD, tabla de soborno con cifras, riesgo escalonado y anunciado |
| Combo dominante que anula la variedad de builds | Alto | Jefes con modificadores de regla, RT-055 como red de seguridad |
| Balanceo inabordable con 150 perks | Alto | `/Balance` operativo desde la fase 1, no al final |
| Percepción de arte no propio | Medio. Penaliza en Steam | Assets libres solo como placeholder, arte encargado para el lanzamiento |
| 9 razas, cada una como club completo | Alto. Multiplica diseño, arte y balanceo | 5 en lanzamiento y 4 en DLC. Ninguna raza entra sin 3 builds viables demostradas en `/Balance` |
| Modificador de jefe oculto percibido como injusto | Medio | RF-014b: se revela para siempre tras el primer encuentro |
| 30-36 nodos alargan la run más de lo previsto | Medio. Fatiga y abandono | RF-003b: tope del 60% de nodos de partido |
| Mercado saturado y descubrimiento nulo | Alto | Demo y acumulación de wishlists como objetivo explícito de la fase 4 |

---

## 9. Anexo técnico: librerías

Criterio: `/Sim` no puede depender de Godot (RT-011), por lo que los addons de IA de Godot (LimboAI, Beehave, Takobi AI) quedan descartados para el simulador. Pueden usarse en `/Game` si la presentación lo necesita.

| Necesidad | Decisión | Motivo |
|---|---|---|
| Condiciones de perks | **NCalc** | Evaluador acotado, rápido, admite funciones propias. Sin ejecución de código arbitrario |
| Validación de `/data` | **JsonSchema.Net** | Cumple RT-032 en integración continua |
| Pruebas | **xUnit** | Aserciones básicas, sin librerías fluidas |
| Steam | **Steamworks.NET** o **Facepunch.Steamworks** | Ya decidido en RT-003 |
| Generador aleatorio | Propio (xoshiro256 o PCG32) | 20 líneas. `System.Random` no garantiza secuencia estable entre versiones de .NET |
| Bus de eventos | Propio | 30 líneas. Cualquier librería es más abstracción de la necesaria |
| IA de utilidad | Propia | Tabla de pesos y `argmax`. Unas 200 líneas, depurable línea a línea |
| Punto fijo | **Aplazado** (RT-023b) | Fork de FixedMath.Net por ForNeVeR si algún día hace falta |
| ECS | **Descartado** | 14 jugadores y un balón no lo justifican. Friflo o Arch si el alcance cambiara radicalmente |
| Generación de mapa | Propia | Un grafo por capas al estilo Slay the Spire son unas 100 líneas |

---

## 10. Decisiones pendientes

1. Nombre del proyecto y del universo.
2. Número exacto de nodos por acto y su distribución por tipo.
3. Modelo económico del salario de mercenarios frente al coste de tienda.
4. Si la fase 3 del partido introduce peligros de público como entidades o solo como estrechamiento del campo.
5. Qué 3 razas entran en el prototipo. Recomendación: humanos como referencia, orcos como extremo agresivo y elfos como extremo técnico. Son las tres que más separan el espacio de builds.
6. Si el jefe final admite reintento inmediato o exige nueva run.
7. Detalle de la tienda de Rune Dice que se quiere replicar (disposición, precios, rotación), si es algo más que la estructura por categorías.
8. Qué logro concreto desbloquea cada raza, y en qué orden se espera que el jugador los consiga.
9. Si el jefe final introduce una condición de derrota propia y cuál.
10. Distribución exacta de tipos de nodo dentro de los 10-12 de cada acto.
11. Precio de lanzamiento y alcance de la demo.
