# BX-10c — el bloqueo del tiro tira al entrar en el radio y mide en el plano

**Hermano de [BX-10](./BX-10.md)** (10 oct 2026, revisión independiente).
- `MatchEngine.TryBlockShot` hacía la tirada de bloqueo de cada defensa al entrar el balón en su radio de 0,9, igual que
  la intercepción del pase antes de BX-10.
- Medía con `Vec2.Distance`, en el plano, y no con `ReachDistance`, que cuenta la altura del balón.

**Por qué no se tocó con BX-10**:
- El tiro va a 0,7 casillas por tick, así que la tirada ya caía más cerca del cuerpo que la del pase (a 0,25).
- El revisor no lo reportó.

## Hipótesis (11 oct 2026)

- **H1, la tirada cae lejos del cuerpo** (la dilución de BX-10).
  - **CONFIRMED como mecanismo, pero sin daño en el tiro.**
  - Sonda de 200 partidos: de 510 entradas en el radio, el 9 % se tiraban a menos de 0,3 casillas, mientras que el balón
    pasaba luego a menos de 0,3 del 15 % de esos defensas. En el pase era 6 % frente a 20 %.
  - Probada la máxima aproximación de BX-10 en el bloqueo (revisión independiente, contadores dentro del motor):
    - la conversión por tirada no sube: 2,72 % antes y 2,57 % después, con la cuota publicada en 400 partidos;
    - con todos los bloqueos, la fracción en el punto más cercano casi no cambia: 71 % → 73 %.
      El 94 % del primer test era sólo del subconjunto medible.
  - **Efecto nuevo e ilegible:** el defensa que espera a su punto más cercano pierde la tirada si el portero para antes.
    Pasó 30 veces en 400 partidos, por el orden del tick: bloqueo, luego portero.
  - **No se aplica.** **REJECTED bajo la ADR 0135 tal como está**: en el tiro no aporta y sí trae un efecto ilegible.
- **H2, se bloquean balones que van por encima del alcance** (la esfera que decidió la ADR 0135). **CONFIRMED.**
  - Medido dentro del motor (revisión independiente): con el alcance en el plano, 6 de 51 bloqueos en 400 partidos con
    la cuota publicada; 17 de 170 con la cuota a 100.
  - Una «sonda corregida» mía dio 0 y estaba mal. Leía la altura de la traza en el tick del bloqueo, y es siempre 0
    porque `Ball.SetLoose` la pone a cero (Regla J: medido, 0 de 401 eventos con altura).

## Arreglo

`TryBlockShot` mide con `ReachDistance`, la esfera, como `TryIntercept` desde la ADR 0136. La tirada sigue al entrar en
el radio. Sin dato nuevo. `docs/plan-altura-del-balon.md` §9 asignaba esta fila al paso 3; ahora va sola, y el portero
(`WithinSaveReach`) sigue en el plano, a propósito, con su bucle de realimentación pendiente.

**Revisión de diseño** (`game-design-review`):
1. **Qué ve el jugador:** un tiro que pasa por encima de la cabeza de un defensa ya no se queda en él.
2. **Qué decide:** nada nuevo.
3. **Qué debería ver:** lo que decidió la ADR 0135. La altura del balón importa para quien lo quiere tocar.
4. **Regla:** ADR 0135, decisión 3 (el alcance es una esfera).
5. **Sistema:** `/Sim`, `MatchEngine.TryBlockShot`.
6. **Alternativas:**
   - (A) **elegida**, sólo la esfera;
   - (B) esfera y máxima aproximación: probada y descartada, ver H1;
   - (C) no tocarlo: dos medidas de alcance para la misma acción.
7. **Trade-off:** algunos bloqueos menos, los del balón alto. Se espera que esos tiros pasen a parada, gol o fuera.
8. **Estrategia:** ninguna nueva; el tiro bombeado no se elige.
9. **Degeneración:** ninguna esperable. Es la regla del corte del pase y del centro desde la ADR 0136.
10. **Demostración:** test permanente y lote de balance (abajo).

**Test permanente:** `InterceptApproachTests.NoShotIsBlockedAboveTheDefendersReach`.
- 3200 partidos, con la cuota a 100 para tener muestra.
- La altura del tick del bloqueo sale de la parábola de los tres fotogramas anteriores del mismo vuelo.
- Resultado: 0 de 49 bloqueos medibles por encima con la esfera, frente a 18 de 65 en el plano.

## Queda abierto

- **El portero** (`TryGoalkeeperReach` y la estirada) sigue en el plano y tirando al entrar en el radio. Es el resto del
  paso 3 de la ADR 0135, con su bucle de realimentación, que la enmienda 2 manda medir aparte.
- **`TryAerialChallenge`** resuelve en el primer tick en que alguien entra en `PickupRadius`, medido en el plano. Es
  otra mecánica (ADR 0139); candidato a ficha propia si da síntoma.

## Balance (`--runs 3000`, semillas 1 y 2, base = el lote final de BX-8c; BX-8d sólo tocó un comentario)

- `blockRate` (INFO): 1,39 → 1,20 y 1,52 → 1,28, un 14-16 % menos en las dos semillas. Es lo que anunciaba el plan
  (§9: «un cambio que baja `blockRate`»).
- `shotsPerMatch`: 7,94 → 7,87 y 7,76 → 7,80. `goalsPerMatch`: +0,02 en las dos semillas.
- Ninguna métrica cambia de estado. Sólo está fuera de banda `betterTeamWinRate_human_60_vs_human_40`, que ya lo estaba
  en la base ([BV-C](./BV-C.md)).

**Suite** (`Category!=Gate`):
- `MobNarrowingTests`: huellas renovadas; en `main` pasaban las seis.
- `RunPolicyItemSlotTests.TheGateNeverBlocksADoctrineThatDoesNotLookAtValue(Saver)`: Saver volvió a cruzar a cero
  compras con 48 runs, el ruido de muestra que la clase ya documentaba. En `main` pasa. El test pasa a 96 runs propias
  y vuelve a pasar; el criterio no cambia.

**Puertas** (`Category=Gate`, semilla 1): rojas sólo las dos que ya lo estaban en la base, `coherentBuildsBeatNone_orc_violence`
(56,69; base 57,24; error típico de una lectura ~2,3) y `betterTeamWinRate_human_60_vs_human_40` (90,36; base 91,57).
`badBuildsLoseToNone_elf_out_of_zone`, roja en la base por 0,34 (LIKELY ruido, [BX-8c](./BX-8c.md)), vuelve a verde.

**Revisión independiente** (Regla E, 11 oct). La primera versión llevaba también la máxima aproximación; la revisión:
- midió dentro del motor y desmontó mi «sin activación» de H2;
- encontró que el test medía la altura con la traza a 0;
- vio que la máxima aproximación quitaba bloqueos y dejaba al portero adelantarse.

Las tres cosas están corregidas arriba, y la máxima aproximación se quitó del paquete.
