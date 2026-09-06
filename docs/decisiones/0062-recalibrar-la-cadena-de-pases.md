# 0062. `buildsWinDifferently_passChain` se recalibra contra lo que el canal de pase puede dar

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada** (`Sim.Analysis.BuildMetrics.MinPassChainRatio`, `fase2-diseno.md` §29.5)
**Cierra:** la mitad de **AJ-C** que quedaba abierta desde la ADR 0058
**Requisitos:** RT-055, RT-056, RT-057
**Relacionada con:** ADR 0012 (la métrica normalizada), ADR 0050 (P1, la escala de cuotas), ADR 0058
(techo por rareza), ADR 0060 (§28.3, la base del canal manda)

## El problema

`buildsWinDifferently_passChain` (§8 de `fase1-diseno.md`) es la única afirmación roja de las seis puertas
desde que se aplicó la P1. Mide

```
(cadena de elf_tiki_taka / cadena de elf_none)  ÷  (cadena de orc_violence / cadena de orc_none)
```

contra un umbral de **1,30**, y viene mejorando sin llegar: 1,16 → 1,19 → 1,23. La sospecha registrada en
AJ-C era que el umbral estaba calibrado contra un motor que ya no existe. **Lo está, y ahora está medido.**

## Contra qué se medía: un canal clavado en el 98%

`elf_tiki_taka` lleva siete `fine_touch`, uno por titular, y los siete cumplen su condición
(`hasTag(owner,'Fine')`). Con la fórmula **aditiva** anterior a la ADR 0050 P1, `fine_touch` era un
`pass +25`: sumaba 2.500 puntos sobre una base de 7.700, o sea 10.200, que el límite del canal recortaba a
**9.800 — el 98%**. Toda la cadena de pases del equipo se resolvía contra ese 98%, y de ahí salía una
cadena en torno a un 30% más larga que la de su referencia.

La escala de cuotas lo impide **por construcción**: multiplicar la cuota no puede sacar la probabilidad del
intervalo y, en un canal de base alta, casi no la mueve. Con la base de 7.700 la cuota es 3,348; el techo de
un perk **común** es ×2, que deja el pase en el 87,0%, y el techo **legendario** de toda la escala es ×6,
que lo deja en el 95,3%. El 98% de la fórmula vieja exigiría un ×14,6, que no existe en la escala.

## Contra qué se mide ahora: la respuesta medida del canal

Se midió el canal **aislado**, sin el resto de la build: una build sintética de **solo** siete `fine_touch`
sobre el once (`elf_pass_only`), contra `elf_none`, con 40 plantillas y 1.440 partidos por celda, y el
valor de `fine_touch` recorriendo los cuatro techos de rareza de la ADR 0058:

| multiplicador de cuota del perk de pase | cadena propia / su referencia |
|---|---|
| ×2 — techo **común**, que es el que lleva la build de medida | **1,108** |
| ×3 — techo poco común | 1,143 |
| ×4 — techo raro | 1,155 |
| ×6 — techo **legendario**, el de toda la escala | 1,191 |

**Ni con el techo legendario siete perks de pase alargan su propia cadena un 20%.** Con el techo común
llegan al **10,8%**. Es exactamente el hallazgo de la ADR 0060 §28.3 y de AL-A —en un canal de base alta
multiplicar la cuota no compra casi nada— visto en la otra magnitud: no en tasa de victoria, sino en
longitud de cadena.

Un umbral de 1,30 pedía un 30%. **Ninguna build del catálogo, con ningún perk de ninguna rareza, puede
producirlo.** No es que `elf_tiki_taka` no llegue: es que el número no existe en este motor.

## Decisión

**`MinPassChainRatio` pasa de 1,30 a 1,11**, y deja de ser una constante puesta a mano para pasar a ser
**lo que el canal de pase da con los perks que la build de medida puede llevar legalmente**: el 10,8%
medido, redondeado a la baja. La derivación queda escrita en el propio `BuildMetrics` para que el número no
vuelva a quedarse huérfano de su referencia.

**No se ha elegido el techo de la escala** (1,19, o 1,24 una vez normalizado contra `orc_violence`), que
habría dejado la puerta roja por una centésima. `elf_tiki_taka` lleva perks **comunes**; pedirle a una build
de comunes lo que sólo da un legendario no es recalibrar el umbral, es cambiar la afirmación — y la
afirmación de §8 es "las builds ganan de formas distintas", no "la build técnica lleva legendarios".

**Con el umbral nuevo la puerta pasa: 1,233 contra 1,11.** Y el margen es atribuible, que es lo que hace
que el número siga midiendo algo:

| de dónde sale el 1,233 | |
|---|---|
| Los siete `fine_touch` sobre su propia cadena | ×1,108 |
| Los demás perks de posesión de `elf_tiki_taka` (`fine_orchestra`, `own_third_anchor`, `covering_shadow`, `crowd_control`), que también alargan la posesión | ×1,065 → **1,180** |
| Que `orc_violence` **acorta** su propia cadena un 4,2% (0,958), que es la otra mitad de "ganan de formas distintas" | ÷0,958 → **1,233** |

(El 1,180 es la cifra del lote de la puerta —40 plantillas × 12 partidos, semilla 1, las cuatro builds—; en
el lote aislado, con otro conjunto de emparejamientos, `elf_tiki_taka` mide 1,165 y `elf_pass_only` 1,108.
La diferencia entre los dos lotes está dentro de lo que mueve cambiar el rival de la matriz.)

## Consecuencias

- La puerta de salida de fase 1 queda **verde en las seis métricas** por primera vez desde la P1; con ella,
  las seis puertas del proyecto. La única afirmación que sigue fuera de banda es `runWinRate` (17,00 sobre
  20-30), que es una **métrica** y no un test.
- **Este umbral hay que volver a derivarlo hacia arriba cuando AL-A se resuelva.** Hoy lo acota la
  aritmética del canal, no el diseño de la build: si el pase recupera recorrido —bajando su base o moviendo
  los perks a canales vivos— la respuesta medida sube y el 1,11 se queda corto. Queda anotado en
  `pendientes.md` colgando de AL-A, no como decisión abierta propia.
- La mitad de lesiones (`buildsWinDifferently_injuries`, ≥1,5×) **no se toca**: mide un canal de base baja
  (`injury`, 1,4%) donde la escala de cuotas sí tiene recorrido, y pasa con holgura.

## Qué falsificaría esta decisión

- **Que una build técnica cualquiera pase el 1,11 sin llevar perks de pase.** Sería que el umbral ya no
  distingue y habría que subirlo o cambiar la build de medida. Hoy no ocurre: `elf_pass_only` sin sus siete
  `fine_touch` es `elf_none`, y mide 1,00 por construcción.
- **Que el 10,8% no sea el techo del canal sino de esta build.** Se midió con una build de **solo** perks de
  pase para separar el canal del resto; si aparece una combinación legal que alargue más la cadena por la
  vía del pase, el umbral está mal derivado y hay que rehacerlo con ella.
