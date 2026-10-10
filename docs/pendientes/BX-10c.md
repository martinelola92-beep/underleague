# BX-10c — el bloqueo del tiro tira al entrar en el radio y mide en el plano

**Hermano de [BX-10](./BX-10.md)** (10 oct 2026, revisión independiente): `MatchEngine.TryBlockShot` hace la tirada de
bloqueo de cada defensa al entrar el balón en su radio de 0,9, igual que la intercepción del pase antes de BX-10, y
mide con `Vec2.Distance` en el plano, no con `ReachDistance` (que cuenta la altura del balón).

**Por qué no se tocó con BX-10**: el tiro va a 0,7 casillas por tick, así que la tirada ya cae más cerca del cuerpo que
la del pase (a 0,25), y el revisor no lo reportó. Sin medir: sin evidencia de activación como síntoma.

**Si se toca**: la misma mirada un tick por delante de BX-10 y `ReachDistance`; vigilar `blockRate`, `shotsPerMatch` y
`goalsPerMatch` con dos semillas.
