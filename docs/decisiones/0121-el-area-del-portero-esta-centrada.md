# 0121 — El área del portero está centrada

Estado: **Aceptada** (20 sep 2026, **petición del revisor** sobre una captura de la retransmisión: «el área
de portero en tu diseño está descentrado porque estás usando el grill para colocarlo, pero no debería ser
así»). Cambia una regla de `/Sim`, así que pasa por `game-design-review` (esta nota) y por `balance-measure`
(hecho: ver «Lo medido»).

## Problema

El área ocupa la banda continua **y ∈ [1, 5]** de un campo que va de 0 a 7 (`Pitch.IsInArea`,
`Utility.ClampToArea`): **una fila libre por arriba y dos por abajo**. No es una decisión de diseño, es el
resto de haber pasado de seis filas a siete (ADR de la fila impar, BA-C): la constante `AreaRows = 4` se
mantuvo con su comentario —4 de 7 conserva el ~57 % histórico— pero nadie volvió a mirar **dónde** caía esa
banda. Consecuencias reales, no estéticas:

- El portero **podría** acercarse a la banda de arriba más que a la de abajo (`ClampToArea`). *(Medido
  después, revisión independiente: en 300 partidos el portero vive en y ∈ [2,41; 4,73] y **nunca toca ningún
  borde**, así que este recorte no ataba. La consecuencia es real en el código y **sin evidencia de
  activación** en el juego: no es lo que arregla esta ADR.)*
- Una falta idéntica es **penalti** arriba y no lo es abajo (`MatchEngine`, falta en el área).
- La casilla-hogar del portero admite filas 1..4, no 1..5: **colocarlo abajo es ilegal y arriba no**.

## Las diez preguntas

1. **Qué experimenta el jugador**: ve el área pintada pegada a la banda de arriba y al portero cubriendo
   mejor ese lado; al colocar la alineación, la fila de abajo le rechaza al portero sin motivo visible.
2. **Qué decide hoy**: con qué lado del campo se arriesga a un penalti — al atacar y al entrar. *(La
   colocación del portero **no** es hoy una decisión del jugador: `PlacementView` la fija en (0, 3) y rechaza
   cualquier otra casilla, hallazgo de la revisión independiente. La validación de `Simulator` solo afecta a
   estados construidos por código y a tests.)*
3. **Qué debería decidir**: lo mismo, con el campo simétrico: nada del diseño dice que atacar por abajo sea
   distinto de atacar por arriba.
4. **Qué regla representa**: el área de portería —dominio del portero (RF-047 y ss., portero) y falta en el
   área que se castiga con penalti—. La simetría no está escrita en `docs/requisitos.md` porque se daba por
   supuesta: se hace explícita aquí.
5. **Qué sistemas**: `/Sim` — `Pitch.IsInArea`, `Utility.ClampToArea`, la validación de la casilla-hogar del
   portero en `Simulator`, y las dos lecturas de `MatchEngine` (penalti y «el portero ha salido del área»).
   `/Game` — la pintura del área (`MatchPitchView3D`, `MatchPitchView`, `PitchView`): se creía derivada de
   las constantes, pero usa un desplazamiento fijo de 1, así que hay que tocarla (ver Consecuencias). `/data` — ninguno:
   las alineaciones de club colocan al portero en filas 1..4, que siguen siendo válidas.
6. **Alternativas**:
   - (a) `AreaRows` 4 → 5 (banda [1, 6]). Centra, pero **agranda** el área del 57 % al 71 % del ancho, justo
     lo que el comentario de `Cell.cs` descartó por excesivo. Cambia el dominio del portero y la tasa de
     penaltis de golpe.
   - (b) **Elegida**: misma altura, banda centrada — y ∈ [(Rows − AreaRows)/2, (Rows + AreaRows)/2] = [1,5; 5,5].
     El área conserva su tamaño y su proporción; solo se baja media casilla.
   - (c) Pintarla centrada y dejar la regla como está. Descartada: la línea dejaría de decir dónde se pita
     penalti, que es exactamente el tipo de mentira de interfaz que la regla 11 prohíbe.
7. **Trade-off**: el portero gana media casilla de dominio hacia abajo y la pierde hacia arriba; una franja
   de media casilla en cada banda cambia de «penalti» a «falta» y al revés. No hay poder gratis para nadie:
   el cambio es simétrico y afecta igual a los dos equipos.
8. **Estrategias**: la validación pasa a admitir la fila 5 (simétrica de la 1) — un superconjunto de lo que
   aceptaba, así que ninguna alineación guardada deja de valer. Mientras `PlacementView` siga fijando al
   portero en (0, 3), el efecto en estrategia es **solo** el de dónde se pita penalti. Ningún perk depende de
   la altura del área.
9. **Degeneración**: no crea combinación nueva. El riesgo medible es que la tasa de penaltis o de paradas
   se mueva fuera de banda.
10. **Cómo se demuestra**: lote de `/Balance` con baseline del mismo árbol y las 43 puertas, más un
    *behavioral audit* que es la razón de ser del cambio: **distribución por fila** de (a) la posición del
    portero y (b) las faltas señaladas como penalti, antes y después. Antes debe salir asimétrica; después,
    simétrica dentro del ruido.

## Decisión

**Alternativa (b)**, medida y aceptada: la banda del área pasa a `y ∈ [AreaTop, AreaBottom]` con
`AreaTop = (Rows − AreaRows)/2` y `AreaBottom = (Rows + AreaRows)/2` (1,5 y 5,5 con los valores actuales).
La casilla-hogar del portero se valida por el **centro de la fila** (`fila + 0,5`) dentro de la banda, que da
las filas 1..5, simétricas respecto al eje.

### Lo medido (20 sep 2026)

| | antes | después |
|---|---|---|
| Faltas señaladas como **penalti por fila** (0..6), 300 partidos, semilla 777 | 0 · **10** · 5 · 6 · 14 · **1** · 0 | 0 · **6** · 5 · 6 · 15 · **7** · 0 |
| Posición del portero: mínima / máxima en Y | 2,41 / 4,33 | **2,41** / 4,73 |
| Fotogramas del portero en la fila 2 / fila 4 | 5.660 / 5.765 | 5.714 / 5.863 |
| Métricas de `/Balance` fuera de banda (4.000 partidos, semilla 1) | — | ninguna cambia de estado; movimiento máximo 0,06 anchuras de banda |
| Puertas rojas de 43 | 3 | las **mismas** 3 (`badBuildsLoseToNone_elf_brawler`, `_elf_out_of_zone`, `buildsWinDifferently_injuries`) |

La asimetría que motivó la ADR era real y medible: **10 penaltis en la fila 1 contra 1 en la fila 5**. Tras el
cambio, 6 y 7. Con 36 y 39 penaltis totales y las **mismas semillas** en las dos tandas, es consistente con la
hipótesis pero **no la aísla del ruido**: LIKELY, no CONFIRMED (revisión independiente).

**La posición del portero no demuestra nada aquí**, y la ADR no debe apoyarse en ella: su mínimo en Y es
idéntico antes y después (2,41) y su recorrido no toca ningún borde, ni el viejo ni el nuevo. El movimiento de
la máxima (4,33 → 4,73) es divergencia de RNG aguas abajo, la misma causa que los valores dorados.

El desequilibrio entre las filas 2 y 4 (5 contra 14) **no lo explica esta ADR y es mayor que lo que arregla**:
queda abierto en [`docs/pendientes/BD-A.md`](../pendientes/BD-A.md), no descartado como ruido.

Tres valores dorados de `RecoveryExtraActionTests` se re-midieron (entradas 31→24 con la semilla 0,
`lane_reader` 19→18 y `sweeper_keeper` 23→24): consecuencia esperada de mover `ClampToArea`, el mismo patrón
que ya documentaron ADRs anteriores que tocaron esa función.

## Consecuencias

- `Cell.cs` deja de definir el área por «filas 1..AreaRows» y pasa a definirla por **altura centrada**; el
  comentario histórico del 57 % sigue valiendo, porque la altura no cambia.
- La validación de la casilla-hogar del portero pasa a aceptar las filas cuyo centro cae dentro de la banda
  (1..5), simétricas respecto al eje.
- **La pintura del campo NO se centra sola** (hallazgo del implementador, corregido respecto a lo que decía
  esta ADR antes de medir): `MatchPitchView3D`, `MatchPitchView` y `PitchView` dibujan el área con un
  desplazamiento fijo de 1 en vez de derivarlo de `AreaTop`. Queda pendiente en `/Game`; hasta que se haga,
  la línea pintada no coincide con la regla.
