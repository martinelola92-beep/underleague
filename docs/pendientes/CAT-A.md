# CAT-A — `field_bandage` usaba el canal `injure`, que protegía al rival.

**Estado:** Cerrada (13 sep 2026) — ver "Análisis / estado actual". Dejó abierto CAT-B

## Observación

**`field_bandage` usaba el canal `injure`, que protegía al rival.** El consumible «Vendaje de campaña» llevaba `{"probability": "injure", "value": -100}`. En el motor `injure` es la probabilidad de que el **entrante** lesione y `injury` la de que la **víctima** se lesione (`MatchEngine.cs:2298`: `Odds(tackler, Injure)` contra `Odds(victim, Injury)`), y un consumible alcanza a **todo el equipo propio en el campo** (`EffectEngine.ResolveConsumables`), así que el canal decide a qué equipo protege

## Análisis / estado actual

**CERRADA (13 sep 2026, decisión del revisor): `injure` → `injury`.** Medido con 3.000 partidos de referencia (semilla 1), vendaje sobre el local: sin vendaje 701 lesiones locales / 700 visitantes; con `injure` 707 / **384** (protegía al rival, −45 % en el equipo equivocado); con `injury` **387** / 701 (−45 % en el propio). El intercambio es simétrico y no cambia el total. **Ninguna métrica de run se mueve**: las políticas automáticas de `Sim.Analysis.RunPolicy` no emiten `SetConsumables` nunca, así que ni `--full-runs` ni las puertas ejercitan un consumible; el lote de referencia de `/Balance` tampoco equipa ninguno. Instrumento permanente: `Sim.Tests/Perks/MedicalConsumableTests.cs` (el canal en el dato, y la medida de a quién protege). 679 tests rápidos y 43 puertas en verde. **Deja abierto CAT-B**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
