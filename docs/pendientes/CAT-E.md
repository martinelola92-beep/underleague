# CAT-E — Con seis filas, todas las builds ganan más al equipo sin perks, y dos puertas de fase 1 se ponen rojas.

**Estado:** Abierta

## Observación

**Con seis filas, todas las builds ganan más al equipo sin perks, y dos puertas de fase 1 se ponen rojas.** `badBuildsLoseToNone_human_scattered` 58,12 (techo 45) y `TheThreeDoctrinesBuyDifferently` (contextual 1,26 contra ahorradora 1,27). Verificado que **no es la dispersión**: la alineación dispersa reexpresada para seis filas no resuelve ninguna de las seis relaciones direccionales, igual que la vieja. Lo que se mide es que **más espacio hace que los perks pesen más frente a no tener ninguno**

## Análisis / estado actual

**Atacada con la ADR 0104 y NO cerrada.** El revisor eligió cambiar la referencia: `<raza>_none` (cero perks) pasa a `<raza>_neutral` (catorce, regla declarada en `tools/build-neutral-reference.py`). **La medida falsó la hipótesis**: contra cero perks 58,12 y contra catorce **59,58** — la referencia no era la variable. Lo que sí encontró la investigación: `human_scattered` está hecha entera de perks que dependen de `linked()` colocados para que **ninguno resuelva**, y desde la **ADR 0088** («ningún perk es negativo», la rama `else` no hace nada) un perk que no se dispara **no castiga**. Es decir, **una build mala ya no puede ser mala, solo neutra**, y el criterio «las malas pierden, ≤45 %» es **insatisfacible por construcción** desde el paquete AY. Las seis filas no lo crearon: movieron un número que ya estaba al borde. **Decisión pendiente del revisor**: (1) que la build mala lo sea por canal y colocación, con perks que sí se disparan pero sobre lo que no decide el partido; (2) subir el techo de «mala» de 45 a 50 y decirlo en RT-055, porque si lo peor que puedes hacer es nada, nada es el 50 %; o (3) reabrir la ADR 0088. Detrás de las tres está **AL-A**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
