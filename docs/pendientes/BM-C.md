# BM-C — Lo que `ankle_bite` paga no se ve (revisión de diseño de la paga)

Estado: **Abierta, revisada en diseño** (26 sep 2026). Es el «DESIGN CLAIM NOT PROVEN» que dejó
[BL-A](./BL-A.md) y el paso 3 del orden del revisor. **No se ha cambiado ningún dato**: la revisión termina
en una medición barata que decide entre las salidas, por la Regla A.

## Lo que promete el diseño y lo que hace el motor

`ankle_bite` (rara, `TACKLE`, `injure(opponent)`, dos por partido con 45 s de enfriamiento): *«no va a por el
balón, va a por la pierna»*. Paga declarada: **el árbitro aprende** (RF-063) y **la víctima guarda rencor**
(ADR 0145); el catálogo de actos añade *«la expulsión es cuestión de tiempo»*.

Leído en el código y en `data/sim/tuning.json` (26 sep):

1. **La lesión nunca se pita.** `injure` entra por `MatchEngine.ProvokeInjury` → `ResolveInjury(isFoul:
   true)`, que usa la fórmula de lesión de una falta pero **no pasa por `WhistleOrLetPlay`**: ni falta
   señalada, ni saque de falta, ni tarjeta. El acto más violento del catálogo es invisible para el árbitro.
2. **«El árbitro aprende» es, en números, casi nada.** Cada lesión mueve el criterio
   `biasShiftInjuryExtra = 2` puntos (escala −100..+100). El efecto sobre las tiradas es
   `biasFoulShiftPer10 = 120` y `biasCardShiftPer10 = 100` puntos base por cada 10 de criterio: **+0,24 pp
   de probabilidad de falta y +0,20 pp de tarjeta** en las entradas siguientes del equipo, y como mucho el
   doble con las dos activaciones del partido. *(Cálculo sobre los datos, no medida.)* Es **consistente**
   con que en BL-A apagar el sesgo no moviera el valor (celda «sin sesgo»): **LIKELY** que H1 no tenga
   efecto porque su magnitud es de décimas de punto, no por un fallo del instrumento.
3. **El rencor dura 10 s** (`GrudgeTicks = 150`) y sólo lo toman los compañeros de la víctima que lo vieron
   de cerca (`RaiseGrudgeAgainst`, radio de testigo). Cuánto le cuesta al portador **no está medido**: no
   hay censo de entradas ni lesiones recibidas por el que lesiona.
4. **El medidor de valor no ve la paga de campaña**: no arrastra lesiones entre partidos, así que una
   represalia que lesiona a un jugador propio es gratis en la tabla aunque en la run sea desgaste.

## Las diez preguntas

1. **Qué experimenta el jugador.** Su jugador entra, el rival cae lesionado, y **no pasa nada más**: no hay
   silbato, no hay tarjeta, el partido sigue. La paga prometida no aparece en pantalla.
2. **Qué decide.** Si lleva el perk o no, y a quién se lo pone. Hoy la decisión es gratis en el partido
   (valor ≈ +10 ± 5, LIKELY no negativo) y su coste declarado es invisible.
3. **Qué debería decidir.** *«¿Me compensa arriesgar a este jugador —su tarjeta, su expulsión, la
   represalia— para quitar a uno de los suyos?»*. Es la decisión del pilar de la carnicería administrada:
   el daño tiene precio y el precio se ve.
4. **Regla.** RF-063 (toda acción sucia mueve el criterio, se vea o no), RF-012d y la regla 11 (lo malo,
   previsible), ADR 0145 (rencor), RF-069b (el soporte modula el acto **del mismo perk**). **No hace falta
   una regla nueva**: la salida recomendada usa la falta que ya existe.
5. **Sistemas.** `/data` (`ankle_bite.json`); `/Sim` sólo si se elige la salida B.
6. **Alternativas.**
   - **A. La mordida arrastra la falta** (sólo datos): añadir a `ankle_bite` un `modifyProbability(foul)`
     de duración `play` sobre la misma entrada, para que el acto vaya casi siempre con falta y el árbitro
     la pite al 80 % (`whistlePercent`) con su tarjeta por la fórmula de siempre. Soporte que modula el
     acto del mismo perk: es literalmente RF-069b. **Ojo (Regla I)**: los multiplicadores de jugada se
     componen como producto; con un `per: match` y 45 s de enfriamiento sólo hay una por jugada, pero hay
     que leer `Modifiers.AddProbability` antes de fijar la cifra. Y comprobar el invariante de
     `PerkAuditTests` «un perk no mezcla categorías de efecto», que podría impedirlo.
   - **B. Una lesión provocada es siempre falta** (motor): `ProvokeInjury` pasa por el silbato. Afecta
     también a `dirty_play` (que ya se dispara en `FOUL`, así que sería una doble falta) — más amplio y más
     caro de razonar.
   - **C. Aceptar que la paga es de campaña** (rencor → lesiones propias → desgaste) y **arreglar el
     instrumento** para que la vea. Es la más fiel al pilar pero la paga seguiría sin verse en el partido.
   - **D. Quitar la paga declarada del `_doc`** y dejar `ankle_bite` como carnicería gratis con precio de
     rara (ADR 0038: el valor alto se paga con precio). Honesto, pero contradice «la expulsión es cuestión
     de tiempo».
7. **Trade-off de A.** Da la escena que falta (mordida → silbato → tarjeta) y convierte el perk en una
   apuesta legible: quitas a uno de los suyos, arriesgas quedarte con diez. Quita valor: hay que medirlo.
8. **Estrategias.** Con A, `silver_tongue` y los perks que cancelan `CARD`/`FOUL` pasan a ser la pareja
   natural de `ankle_bite`: una combinación con sentido (*el que muerde y el que convence al árbitro*), no
   una degeneración, porque cada cancelación cuesta un slot.
9. **Degeneración.** A con una cuota de falta excesiva hace del perk un generador de penaltis en contra si
   se activa en el área propia: el acto dispara en `TACKLE` sin condición de zona. Considerar
   `zone(actor) != 'Own'` o aceptar el penalti como parte de la paga (es previsible).
10. **Cómo se demuestra.** Primero el censo de abajo; después, con A, la tabla de valores (ADR 0087, cuatro
    lotes por la ADR 0150 porque la fila tiene varianza alta) y un censo de tarjetas del portador.

## Recomendación y siguiente paso

**Recomendación: A**, por ser datos, reutilizar el silbato y la tarjeta que ya existen y hacer visible la
paga en el mismo instante que el acto (`eventos explícitos > transiciones invisibles`).

**Antes de tocar el dato (Regla A)**: un censo de `/Sim` puro, con el patrón de
`PerkSituationCensusTests`, que cuente **por portador** y con/sin `ankle_bite`: faltas pitadas, amarillas,
rojas, entradas recibidas y lesiones recibidas mientras tiene rencor encima. Si el rencor ya le cuesta
lesiones al portador en cantidad apreciable, la paga existe y el problema es sólo de visibilidad (A sigue
valiendo, con menos cuota); si no, la paga declarada es decorativa y A es obligatoria.
