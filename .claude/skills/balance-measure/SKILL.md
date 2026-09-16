---
name: balance-measure
description: Medir el efecto de un cambio que puede alterar comportamiento cuantificable del partido o de la run — no cualquier cambio en /Sim o /data (una traducción, un DTO, un fix de replay no lo son). Usar cuando el cambio toca pesos de IA, probabilidades, tablas de economía, o el catálogo de perks/objetos en tamaño o distribución.
---

# Medir balance con criterio, no por reflejo

Sustituye a `balance-check`. La diferencia no es solo de nombre: el disparador de la V1
(`cambio en /Sim o /data`) era demasiado amplio y producía falsos positivos —corregir un DTO, arreglar un
bug de identidad de jugador o tocar una traducción no son cambios de balance. El disparador correcto es
**"¿puede este cambio alterar una métrica cuantificable del partido o de la run?"**.

Fuente de rangos: `docs/balance.md`. No uses otros valores.

## Antes de medir

1. **Una hipótesis por tanda.** Nunca varios parámetros a la vez.
2. **Baseline del mismo árbol, no de memoria.** `git stash push --include-untracked`, medir en HEAD limpio,
   `git stash pop`, medir con el cambio. Comparar los dos números reales, no lo que "se sabe" que valía
   antes — un commit puede haber movido algo sin decirlo (fue el caso real de `a0a8b33`, que afirmaba no
   mover balance y sí lo movía).
3. **El número de partidos más pequeño que resuelva la duda.** El lote de `/Balance` no es un test de humo.

## Al medir: TODAS las métricas de diferenciación, no solo la que motivó el cambio

**Lección con nombre propio**: `ChaseBall pen=50` se aceptó descartando su puerta roja como ruido —y se
demostró con un barrido, la métrica rebotaba 13 puntos—, sin mirar que otras dos métricas de
diferenciación de builds se movían a la vez y en la misma dirección. Sobre un baseline sucio no se veía;
sobre uno limpio el daño era 3 puertas rojas → 5 → 7. **Una métrica ruidosa aislada se descarta; dos o
tres apuntando al mismo sitio son una señal**, incluso si cada una por separado parece ruido.

## Behavioral audit — lo que un agregado en rango no enseña

`docs/analisis/auditoria-organizacion-v2.md` (V3, revisor) señala un riesgo que una métrica de rango no
detecta: el sistema puede estar **en banda y haciendo lo incorrecto**.

- `tackleRate = 12` puede estar perfectamente en rango. Pero si el 80 % de las entradas las hace siempre
  el mismo jugador, hay un problema de comportamiento que ninguna banda agregada revela.
- `shotsPerMatch = 8` puede estar en rango. Pero si el 70 % de los tiros salen de posiciones absurdas
  (mira la distribución de `distanceToGoal` o de posición del tirador, no solo el conteo), el número
  correcto esconde una IA que dispara mal.

**Antes de cerrar una medición de balance, comprueba al menos una distribución además del agregado**:
concentración por jugador, por posición, o por zona del campo — lo que sea relevante al cambio. Un
agregado dentro de rango con una distribución rota no es un balance correcto, es un promedio que oculta el
problema.

## Comandos

Ver `build-and-test` para los comandos de compilación y prueba — no se repiten aquí. `summary.csv` se lee
con `grep -E "^métrica,"` de las filas que importan, nunca entero.

## Qué NO hace

- No se lanza "para ver si sigue bien" sobre algo que no se ha tocado.
- No declara balance correcto con solo el agregado si el cambio afecta a cómo se reparte una acción entre
  jugadores o posiciones (behavioral audit de arriba).
- No sustituye a `game-design-review` cuando el cambio también es de diseño de mecánica.
