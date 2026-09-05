# Estilo de las descripciones

Aplica a todo lo que el jugador lee para tomar una decisión: perks, objetos, consumibles, habilidades raciales, razas, clubes y etiquetas de estilo. Cumple RT-035 (las descripciones de efectos se generan, nunca se escriben) y RF-012d (todo lo que puede pasar debe ser previsible).

## La regla

**Una frase. Efecto observable. Nada de implementación.**

El jugador tiene que poder decidir, no auditar el código. Debe entender qué va a pasar en el campo; no necesita saber sobre qué variable se suma ni en qué orden se resuelve.

| Bien | Mal | Por qué |
|---|---|---|
| "Mejora el pase hacia el compañero de su columna" | "+800 a `pass` sobre el objetivo `linked:ahead` durante `match`" | La segunda expone el modelo de datos |
| "20% más de probabilidad de lesionar gravemente a un rival" | "+2000 puntos base a `severeInjury`" | Puntos base sobre 10.000 no significan nada para nadie |
| "Sus entradas dejan al rival derribado más tiempo" | "+12 ticks al estado `KnockedDown` del objetivo" | Los ticks son una unidad interna |
| "El primer pase de cada jugada no puede interceptarse" | "Anula el chequeo de intercepción si `passIndexInPlay == 0`" | La primera se puede ver ocurrir; la segunda hay que creérsela |

## Qué no aparece nunca

Ticks, puntos base, nombres de canales o de campos JSON, identificadores, fórmulas, umbrales internos, orden de resolución, y cualquier número que el jugador no pueda verificar mirando el partido o la ficha.

## Convención de porcentajes

**Un perk multiplica la cuota de su canal, y la descripción lo dice en proporción** (ADR 0050 P1). El
JSON declara `"value": 30` y eso significa `cuota × 1,3`; el jugador lee *"un 30% más de probabilidad de
interceptar"*. Los ocho valores legales son `±15, ±30, ±50, ±100`, iguales en **todos** los canales: el
negativo es el **inverso exacto** del positivo de la misma magnitud —`-30` divide por 1,3, no resta el
30%— y por eso se escribe con la cifra verdadera de la reducción, que no es la misma: `-15` se lee "un
13% menos", `-30` "un 23% menos", `-50` "un 33% menos" y `-100` "un 50% menos".

**Proporciones, no puntos.** Es el cambio respecto de la convención anterior, y es consecuencia directa
de la fórmula: los efectos ya no suman puntos porcentuales, multiplican cuotas. La escala por canal de la
ADR 0035 —que existía porque un punto porcentual no valía lo mismo sobre `intercept` (base 2,5%) que sobre
`pass` (base 77%)— **queda retirada**: multiplicando cuotas, la misma cifra vale lo mismo en cualquier
canal por construcción, y `tuning.probabilityChannels` ya no existe.

La base 10.000 se mantiene **solo** dentro del motor, donde hace falta precisión para probabilidades
pequeñas (una lesión del 2,4% no se puede expresar en enteros sobre 100). El multiplicador vive en esa
misma base: `10000` es "no hacer nada".

**Cuando el efecto se acumula**, cada unidad del contador vale exactamente **una copia más** del mismo
multiplicador, y el tope es cuántas copias como mucho: *"un 50% más de probabilidad de regate por cada
regate ganado, hasta cinco veces"*. Escribir el tope como copias y no como un multiplicador máximo es lo
que deja al eje de acumulación (RF-070) crecer más allá del ×2 de la escala sin salirse de ella.

### El límite de precisión de esta convención, y por qué se acepta

Multiplicar cuotas es exacto; describirlo como una proporción de **probabilidad** no lo es. El aumento
relativo real de la probabilidad es `(k−1)(1−p)/(1+(k−1)p)`: coincide con la cifra escrita cuando la base
es pequeña y se queda por debajo cuando es grande. Con `k = 1,3`:

| Canal | Base | Dice | Es |
|---|---|---|---|
| `intercept` | 2,5% | 30% más | 29,2% más |
| `injure` | 2,0% | 30% más | 29,4% más |
| `tackle` | 28% | 30% más | 19,5% más |
| `pass` | 77% | 30% más | 5,6% más |

**Esto es una desviación de "la descripción no puede mentir" y está anotada como tal**: no existe ninguna
frase corta en proporción que sea exacta para una multiplicación de cuotas en toda la escala de bases, y
la convención anterior ("+25% de probabilidad de pase" entendido como puntos) tenía el defecto simétrico y
peor —la misma cifra significaba cosas que se diferenciaban en dos órdenes de magnitud según el canal—.
La alternativa exacta es hablar de **cuota** ("multiplica por 1,3 sus opciones de interceptar"), que es
verdad literal y encaja con la ficción del juego; queda sobre la mesa del revisor y anotada en
`pendientes.md`. Lo que **no** se hace es describir un efecto multiplicativo como si sumara puntos: eso sí
mentiría siempre.

## Qué se genera y qué se escribe

- **Perks, objetos, consumibles y habilidades raciales**: descripción **generada** desde el efecto (RT-035), con las plantillas de `data/l10n/<idioma>/templates.json`. No existe campo `description` y el validador lo rechaza. Es la única forma de garantizar que el texto y el efecto no divergen nunca.
- **Razas, clubes y etiquetas de estilo**: no son efectos, son conceptos, así que su descripción se **escribe a mano** en `data/l10n/`, una frase, con las mismas reglas de estilo. Su habilidad asociada, en cambio, sí se genera, porque es un perk.

## Nombres propios: el chiste se localiza, no se traduce

Todo nombre que ve el jugador —clubes, rivales, jugadores generados, legendarios— existe en **español e inglés**. Y la regla no es traducir: es **encontrar el equivalente en la cultura futbolística de ese idioma**.

`Fénix Concursal` / `Newco Athletic` es el estándar: *newco* es el término inglés real para la sociedad que sucede a un club en concurso de acreedores. No es una traducción de "concursal", es el mismo chiste contado a otro público. Traducirlo literalmente no habría significado nada.

| Tipo de nombre | Qué se hace |
|---|---|
| **Nombre de pila de fantasía** (`Grok`, `Aelar`, `Borin`) | No cambia. No significa nada en ningún idioma, y ahí está la gracia |
| **Apellido parlante** (`Rompehuesos`, `Hojaplata`) | Se busca el equivalente con el mismo registro: `Bonebreaker`, `Silverleaf`. No una traducción literal que suene a manual |
| **Nombre de club y de rival** | El chiste equivalente, no la traducción. Puede alejarse del original todo lo que haga falta |
| **Legendario** (parodia de futbolista) | El guiño tiene que funcionar en los dos idiomas, y no siempre es el mismo. Si un nombre solo funciona en uno, se cambia por otro futbolista que funcione en ambos |

La prueba: **un jugador inglés no debe poder notar cuál era el idioma original**. Si el nombre en inglés suena a traducción, está mal.

## Longitud

Una frase de línea y media como máximo a 1280x800 (RT-070) en el tamaño de texto pequeño (UI-004). Si no cabe, el problema es el efecto: un efecto que necesita dos frases para explicarse combina demasiados ejes (`perks-ejes.md`, regla de legibilidad) y hay que partirlo o simplificarlo.

## Ejemplos por tipo

| Elemento | Descripción |
|---|---|
| Raza (Enanos) | "Bajos, tercos y difíciles de mover. No llegan lejos, pero donde plantan el pie se quedan." |
| Habilidad racial (Enanos) | "No pueden ser desplazados por empujones." |
| Etiqueta de estilo (`Brute`) | "Busca el contacto. Gana duelos y reparte daño." |
| Club (Los Carniceros de Kharg) | "Orcos. Empiezan con poco oro y un delantero legendario con antecedentes." |
| Perk | "Mejora el pase hacia el compañero de su columna." |
| Objeto maldito | "Mucha más fuerza. El portador termina cada partido lesionado." |
