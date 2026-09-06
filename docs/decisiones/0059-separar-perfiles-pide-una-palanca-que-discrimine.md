# 0059. Separar perfiles pide una palanca que discrimine, no una que suba el listón

**Fecha:** 2026-09-06
**Estado:** Aceptada; **puntos 1, 2 y 4 ejecutados; el punto 3 queda falsificado por la medición del punto 2**
y lo releva la **ADR 0060** (`fase2-diseno.md` §28). Los dos hallazgos: el instrumento estaba **borroso, no
sesgado** —a 384 partidos por perk la mitad de la varianza de la tabla era ruido— y al afinarlo **las tablas
no se mueven** (build buena 52,43 → 52,28 en el acto 2); y **la build mediocre concentra más que la buena**
(4,32 perks de su línea frente a 3,17; 87,2% frente a 68,8% con tres o más), así que un pago por coherencia
le pagaría a quien no debe. Sí se confirma que concentrar satura, pero por la **base del canal** y no por el
techo del 2%-98%. La palanca que sí discrimina es la asimetría entre premio y castigo (ADR 0060), y con ella
el hueco del acto 2 pasa de 6,27 a **10,03** puntos: el objetivo central de la ADR 0056, alcanzado por
primera vez
**Corrige:** la palanca elegida en la **ADR 0058**, falsificada por su propio criterio
**Requisitos:** RT-055, RT-057, RF-032
**Relacionada con:** ADR 0033, ADR 0050, ADR 0056 (objetivos), ADR 0057, ADR 0058

## Tres paquetes, tres palancas falsificadas

| paquete | palanca | hueco buena/mediocre, acto 2 |
|---|---|---|
| ADR 0055 | oro y precios del mercado | — |
| ADR 0057 → P1 | lo que vale un perk (cuotas) | 9,8 → **6,8** |
| ADR 0058 | rareza del perk + build del rival | 6,8 → **6,3** |

Ninguna abrió la separación entre perfiles. La tercera trae, por fin, las dos razones **medidas**:

**El techo por rareza no puede separar.** Una build buena se compone de 42,8/45,5/11,4/0,4 de común/poco común/raro/legendario; una mediocre, 56,2/35,5/8,2/0,1. Son un **6,9%** de diferencia de multiplicador medio: a la sensibilidad medida, **una décima de punto** de tasa de victoria. Aislado, el hueco va de 5,82 a 5,79. Las dos builds se surten del mismo mostrador y salen con casi la misma mezcla de rarezas; la rareza **no es** lo que distingue una build buena de una mediocre.

**La fuerza del rival comprime en vez de abrir, y es geometría.** La tasa de victoria es una sigmoide con pendiente máxima en el 50%. Las dos builds estaban a caballo de ese punto (56,8 y 50,0), así que subir al rival las empuja a las dos hacia la parte plana: con 11 perks de rival en el acto 3 el hueco se hundía de 7,88 a **3,37**.

De ahí el enunciado que ordena lo que queda:

> **Los objetivos 1 y 2 de la ADR 0056 son incompatibles entre sí mientras la palanca sea la fuerza del rival.** "Build buena al 60%" pide un rival más débil y "build mediocre al 42-45%" lo pide más fuerte, y es un solo número.

## Lo que sí movió algo

**El suelo sin build: 12,08% → 10,08%** (−2,00, error típico 1,28). Es el único número que ha respondido a una palanca en tres paquetes, y deja medido el reparto: **dar build al rival vale dos puntos de suelo; los otros diez son nivel y atributos.** Eso es exactamente la conversación de la P3 que la ADR 0057 suspendió, ahora con una cifra al lado en vez de una sospecha.

## La sospecha que hay que medir antes de tocar nada

Una build buena **concentra** perks en su línea; una mediocre los **reparte**. Con la escala de cuotas, repartir puede estar saliendo mejor que concentrar:

- El canal se acota al 2%-98% (`tuning.resolution`, ADR 0050 P4). Concentrar tres perks legales en `pass` (base 77%) lo lleva al 96,4%: el cuarto perk de esa línea ya casi no compra nada.
- Repartir la misma cuota entre `pass`, `tackle`, `intercept` y `dribble` —bases bajas— no choca con ningún techo, y cada perk cobra entero.

Si eso es así, **la aritmética está premiando incoherencia**, y ninguna palanca de fuerza lo va a arreglar porque el problema no es cuánto vale un perk sino cuánto vale *el segundo perk de la misma línea*.

## Decisión

**Primero el instrumento, luego el diagnóstico, y sólo entonces la palanca.**

1. **`data/economy/perk-values.json` lleva dos escalas de retraso** y no se regeneró ni en la P1 ni en la 0058. La doctrina contextual —o sea, la que define lo que en todas estas tablas se llama "build buena"— elige sus perks con valores de antes de multiplicar cuotas. **Todas las cifras de build buena de las ADR 0057 y 0058 están medidas con un instrumento desafinado**, y hay que regenerarlo y volver a medir antes de sacar una conclusión más.

2. **Medir si concentrar satura.** Es una pregunta con respuesta empírica barata y decide la palanca siguiente.

3. **La palanca candidata, si la sospecha se confirma, es el pago por coherencia fuera del canal saturado**: que completar una línea de `data/build/arcs.json` pague en un canal **distinto** del que la línea satura. Discrimina por construcción —una build mediocre nunca completa una línea, así que nunca cobra— y esquiva el techo del 2%-98%, que es lo que hoy castiga a quien concentra.

4. **Devolver la build buena a su banda.** Hoy está en 52,4/42,1 en los actos 2/3 con la run al 15,9%, por debajo de la banda 20-30%. La directriz del revisor es explícita: *"es frustrante tenerlo todo planeado y no ganar"*. La capa del rival se retoca hacia abajo hasta que la build buena vuelva al 60% de los objetivos, aceptando que el suelo suba algo — el suelo se ataca con el punto 3, no apretando a todo el mundo.

## Qué falsificaría esta decisión

- **Concentrar no satura**: los perks de una línea siguen cobrando enteros hasta el cuarto o el quinto. Entonces la incoherencia no está premiada por la aritmética y el punto 3 no tiene base; la conversación pasa a la P3 (peso de los atributos frente a la build), con los dos puntos de suelo del rival ya en el bolsillo.
- **El instrumento afinado cambia las tablas de sitio.** Si con `perk-values.json` regenerado la build buena sube sola, parte de lo que estos tres paquetes han estado persiguiendo era el desafine y no el diseño.
