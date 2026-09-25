# BK-A — RF-069 exige el 60 % de modificadores numéricos, y el producto pide lo contrario

Estado: **CERRADA** (25 sep 2026) por la [ADR 0149](../decisiones/0149-el-catalogo-se-mide-por-lo-que-se-ve.md).
El revisor autorizó expresamente sobreescribir RF-069. Nace de la auditoría
[13C](../analisis/perks-auditoria-situaciones.md) §5.

## El conflicto, con las dos citas delante

*(MEDIDO, `docs/requisitos.md`, RF-069)*

> | Relleno con condición | **60 %** | **Modificadores numéricos condicionados.** Dan grosor a las builds |

**Encargo del revisor, 25 sep 2026:**

> No: *«tengo 100 perks y el 60 % modifica estadísticas con condiciones»*. Sino: *«tengo un sistema
> estadístico que permite que 100 perks creen situaciones diferentes durante un partido»*.

Es una **contradicción literal**, no una cuestión de énfasis.

## Por qué importa más de lo que parece

**El catálogo actual no está incumpliendo RF-069: lo está cumpliendo.** *(MEDIDO, censo de los 102 ficheros
de `data/perks/`: 70,6 % de grado NÚMERO —invisible incluso en principio—, 14,7 % CONDUCTA, 14,7 %
SITUACIÓN.)*

**DERIVADO**: ninguna auditoría ni rediseño de catálogo puede arreglar esto, porque el documento que produce
el resultado está por encima del catálogo. Mientras RF-069 diga 60 %, un catálogo «arreglado» estará fuera
de requisitos, y mientras el requisito no se toque el rediseño de `perks-design-bible.md` está escribiendo
contra un objetivo que el revisor ya ha descartado.

## Lo que el conflicto NO es

**No es que RF-069 esté mal entera.** Sus tres filas miden **potencia** —cuánto rompe un perk las reglas del
simulador— y esa escala sigue siendo útil y sigue estando bien capada (el 10 % de rompe-reglas). Lo que
falta es una **segunda distribución ortogonal** que mida **visibilidad** —cuánto se ve un perk—, que hoy no
existe como requisito y por eso nadie la comprueba. Ver 13C §0 y §5.

## Forma propuesta para la decisión (HIPÓTESIS, la decisión es del revisor)

**Añadir, no sustituir.** Un perk de relleno **puede y debe** ser visible: Cabeza de Hierro es relleno
—no rompe ninguna regla— y es espectacular. Con dos distribuciones ortogonales la contradicción desaparece
sin retirar nada de lo que RF-069 protege:

- se conserva 60/30/10 como escala de **potencia**;
- se añade un **suelo de visibilidad**: qué fracción del catálogo tiene que ser SITUACIÓN o CONDUCTA. **Hoy
  es 29,4 %** *(MEDIDO)*, y ese número es la línea base contra la que se fijaría el suelo.

Exige **ADR** (RT-057) y cambio de versión de `docs/requisitos.md`.

## Lectura conservadora aplicada mientras tanto

**No se toca `/data`.** Ningún perk se reescribe ni se retira por este motivo hasta que la decisión exista.
Lo único que se ha hecho es dejar de escribir hoja de ruta contra el objetivo viejo (13C §6).

## Cómo se cierra

Con la decisión del revisor. Si es «sí, se añade el suelo»: ADR + requisitos v0.10 + la fila de
comprobación que hoy falta en `perks-design-bible.md` §5.3. Si es «no»: hay que retirar el encargo de 13C,
y conviene que quede escrito por qué.

---

## Cómo se cerró (25 sep 2026)

**Decisión del revisor: «te doy permiso para sobreescribir RF-069».** Se aplicó la forma propuesta arriba
—**añadir, no sustituir**—: RF-069 conserva 60/30/10 como eje de **potencia** y gana un eje de
**visibilidad** (Acto ≥ 50 % / Conducta ≥ 25 % / Soporte ≤ 25 %), más la regla que traduce el encargo:
**ningún perk tiene efectos exclusivamente de soporte**. La primera fila del eje de potencia deja de exigir
«modificadores numéricos condicionados». `docs/requisitos.md` sube a **v0.10**.

**El suelo de visibilidad no se fijó a ojo**: la línea base medida era **29,4 %** de perks de grado Acto o
Conducta, y el suelo queda en 75 % entre los dos. Convertir el catálogo es el trabajo que la ADR abre y va
por tandas, con la conversión diseñada en
[`perks-de-cuota-a-acto.md`](../analisis/perks-de-cuota-a-acto.md).

**Lo que quedó fuera y sigue sin decidir**: si un perk de **economía** debe seguir ocupando un slot de perk
(RF-069e lo exime de la cuota, no resuelve la pregunta).
