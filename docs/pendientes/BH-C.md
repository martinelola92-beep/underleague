# BH-C — El recorte al marco amortigua en silencio toda mecánica de puntería.

**Estado:** Abierta, **CONFIRMED** y sin arreglar (23 sep 2026). Sale de la revisión independiente del
paso 3a de la ADR 0135, que la señaló como problema **hermano**: no es de la apertura, es estructural.

## Observación

`MatchEngine.LaunchShot` decide **primero** si el tiro va dentro o fuera (una tirada), y **después**
calcula el punto de mira. `OnTargetAim` acota ese punto al marco:

```csharp
float row = Math.Clamp(rawRow, -halfWidth, halfWidth);
```

Y `TryHitFrame` sólo mira una **banda centrada en el borde**, de `postThicknessCellsMilli` (0,06 casillas),
sobre el valor **crudo**. Consecuencia: un disparo cuya mira cruda se va **tres semianchos** fuera no es
palo *ni* es fuera — **se recorta al poste y sigue contando como tiro a puerta**.

## Por qué importa más allá del paso 3a

**Ninguna mecánica puede empeorar la puntería de un tiro que ya salió «dentro».** Todo término que ensanche
el error queda absorbido por el `Clamp` y se manifiesta como **palos**, no como fallos. Medido en el paso
3a: multiplicar el error por 1/apertura mueve la ventaja de conversión sólo de 1,307 a 1,235 (el 19 % del
recorrido) y en cambio sube los palos de 1,12 a 1,91 % (+70 % relativo). La geometría era correcta; el
motor no la dejaba hablar.

Afecta por igual a lo que venga después:

- el **error vertical** por apertura, aplazado en §10 del plan de la altura;
- fatiga, presión o un perk que degrade la puntería;
- cualquier término del paso **3c** (el portero), con el agravante de que el recorte concentra masa
  **exactamente sobre la línea del poste**, que es donde el portero menos llega: hoy da igual, pero en
  cuanto el alcance sea esférico esa masa acumulada se convierte en otra cosa. **Conviene resolverlo antes
  de 3c, no después.**

## La salida conocida, y su precio

Que la mira **cruda** decida dentro/fuera, en vez de una tirada aparte —o sea el «error angular completo»,
alternativa 1 de §10 de `docs/plan-altura-del-balon.md`—. Es lo correcto y es lo que hace portante la
geometría, pero **rehace la calibración del 70,5 % de tiros a puerta** de la ADR 0050 P2, así que no cabe
de propina en otro paso: necesita el suyo.

## Hermanos

- [BA-E](./BA-E.md) — el paso 3a chocó con esto y por eso su mitad geométrica quedó casi inerte.
- `docs/plan-altura-del-balon.md` §11.bis — la medición que lo destapó.
- [ADR 0050](../decisiones/0050-fundamentos-matematicos.md) P2 — la calibración que hay que
  rehacer para arreglarlo.
