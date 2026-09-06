# Estilo de las descripciones

Aplica a todo lo que el jugador lee para tomar una decisión: perks, objetos, consumibles, habilidades raciales, razas, clubes y etiquetas de estilo. Cumple RT-035 (las descripciones de efectos se generan, nunca se escriben) y RF-012d (todo lo que puede pasar debe ser previsible).

## La regla

**Una frase. Efecto observable. Nada de implementación.**

El jugador tiene que poder decidir, no auditar el código. Debe entender qué va a pasar en el campo; no necesita saber sobre qué variable se suma ni en qué orden se resuelve.

| Bien | Mal | Por qué |
|---|---|---|
| "Mejora el pase hacia el compañero de su columna" | "+800 a `pass` sobre el objetivo `linked:ahead` durante `match`" | La segunda expone el modelo de datos |
| "Multiplica por 1,5 sus opciones de causar una lesión grave" | "+2000 puntos base a `severeInjury`" | Puntos base sobre 10.000 no significan nada para nadie |
| "Sus entradas dejan al rival derribado más tiempo" | "+12 ticks al estado `KnockedDown` del objetivo" | Los ticks son una unidad interna |
| "El primer pase de cada jugada no puede interceptarse" | "Anula el chequeo de intercepción si `passIndexInPlay == 0`" | La primera se puede ver ocurrir; la segunda hay que creérsela |

## Qué no aparece nunca

Ticks, puntos base, nombres de canales o de campos JSON, identificadores, fórmulas, umbrales internos, orden de resolución, y cualquier número que el jugador no pueda verificar mirando el partido o la ficha.

## Convención de cuota

**Un perk multiplica la cuota de su canal, y la descripción escribe la multiplicación** (ADR 0058). El
JSON declara `"value": 30` y eso significa `cuota × 1,3`; el jugador lee *"multiplica por 1,3 sus opciones
de interceptar"* / *"multiplies their interception odds by 1.3"*. El negativo es el **inverso exacto** del
positivo de la misma magnitud, así que se lee con la **misma cifra y el verbo contrario**: `-30` es
*"divide por 1,3 sus opciones de interceptar"*. Nunca hay dos números que memorizar.

Los catorce valores legales son `±15, ±30, ±50, ±100, ±200, ±300, ±500`, es decir
`k ∈ {1,15 · 1,3 · 1,5 · 2 · 3 · 4 · 6}` y sus inversos, iguales en **todos** los canales. Lo que no es
igual para todos es el **techo**, que depende de la rareza (ADR 0058):

| Rareza | Techo | k | Techo con contador |
|---|---|---|---|
| Común | 100 | ×2 | 50 (×1,5) |
| Poco común | 200 | ×3 | 100 (×2) |
| Raro | 300 | ×4 | 200 (×3) |
| Legendario | 500 | ×6 | 300 (×4) |

La rareza **compra cuota**: es lo que se paga en el mercado y lo que sueltan los jefes, así que es donde
vive la decisión. Un perk cuyo valor pase del techo de su rareza es un error de carga, con la rareza, el
valor y el techo en el mensaje. El techo de un efecto **con contador** es un escalón más bajo porque ahí
el multiplicador se aplica hasta `n` veces y el total es `k^n`.

**Cuota, no proporción de probabilidad.** Es el cambio respecto de la convención que trajo la P1 —"un 30%
más de probabilidad de interceptar"— y el motivo es que aquella **mentía** en los canales de base alta. El
aumento relativo real de la *probabilidad* es `(k−1)(1−p)/(1+(k−1)p)`, que depende de la base:

| Canal | Base | Decía | Era |
|---|---|---|---|
| `intercept` | 2,5% | 30% más | 29,2% más |
| `injure` | 2,0% | 30% más | 29,4% más |
| `tackle` | 28% | 30% más | 19,5% más |
| `pass` | 77% | 30% más | **5,6% más** |

No existe ninguna frase corta en proporción de probabilidad que sea exacta para una multiplicación de
cuotas en toda la escala de bases. Escribir la multiplicación sí lo es —en los cuatro canales de la tabla
y en cualquier otro— y además enseña el modelo mental correcto: **dos perks se multiplican, no se suman**,
que es exactamente lo que hace el motor. La escala por canal de la ADR 0035 sigue retirada:
multiplicando cuotas, la misma cifra vale lo mismo en cualquier canal por construcción, y
`tuning.probabilityChannels` ya no existe.

La base 10.000 se mantiene **solo** dentro del motor, donde hace falta precisión para probabilidades
pequeñas (una lesión del 2,4% no se puede expresar en enteros sobre 100). El multiplicador vive en esa
misma base: `10000` es "no hacer nada".

**Cuando el efecto se acumula**, cada unidad del contador vale exactamente **una copia más** del mismo
multiplicador, y el tope es cuántas copias como mucho: *"multiplica por 1,5 sus opciones de regatear por
cada regate ganado, hasta cinco veces"*. Escribir el tope como copias y no como un multiplicador máximo es
lo que deja al eje de acumulación (RF-070) crecer más allá del techo de su rareza sin salirse de la escala.

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
