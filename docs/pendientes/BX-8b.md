# BX-8b — En la falta a favor en el tercio propio, el despeje cobra el bono de peligro con el balón muerto

Hermano de [BX-8](./BX-8.md), anotado allí «sin ficha» por la segunda revisión independiente (10 oct): la falta a
favor admite cualquier acción (`Utility.RestartAllows` → `_ => true`, ADR 0143), y `Utility.EvaluateClear` sólo
quitaba el bono de peligro en el saque de puerta.

## Observación

No la vio el revisor: sale de la lectura del código. El síntoma que tendría en pantalla es el de BX-8 en pequeño: tras
una falta a favor cerca de su área, el defensa revienta el balón al rival en vez de jugarlo con el compañero de al lado.

## Hipótesis

El peligro (`MatchEngine`, `_context.Danger`) mide sólo lo cerca que está el balón de la portería propia, no quién
amenaza. Con el balón muerto y la barrera de la falta (dos casillas, BB-B) no describe amenaza ninguna.

- **H0, activación.** ¿Ocurren faltas a favor en el tercio propio, y el sacador despeja en ellas? **CONFIRMED.**
- **H1, el bono es lo que hace ganar al despeje.** **CONFIRMED.** Volcado de utilidad (RT-098) en los casos de despeje:
  `Clear` ganaba con contexto 410-545, por encima de un pase corto de ~700 en puntuación final. Sin el bono su contexto
  baja a 150 (base; la presión es 0, porque la barrera deja al rival a dos casillas) y deja de ganar en todos.
- **H2, el despeje era correcto porque había un rival encima.** **REJECTED.** El rival más cercano está a 2,0 casillas
  de mediana tanto en los despejes como en los pases cortos, y desde esas mismas posiciones el pase corto conservaba el
  93 %.

## Medición (sonda desechable, 200 partidos de `TestMatches.Reference`, semillas 1-200)

| | Faltas en el tercio propio | Despejes | Conservadas tras despejar | Pase corto conservado |
|---|---|---|---|---|
| Antes | 63 (de 699 faltas) | 12 | 4 de 12 (33 %) | 28 de 30 (93 %) |
| Después | 65 (de 710) | 0 | — | 35 de 37 (95 %) |

Los despejes quitados se reparten entre el pase corto (30 → 37) y el pase en profundidad (21 → 28); este conserva
11 de 21 (52 %) antes y 17 de 28 (61 %) después.

Sumando todas las acciones, el equipo de la falta conserva el balón 43 de 63 (68 %) → 52 de 65 (80 %) en las faltas de su tercio.

## Arreglo (11 oct 2026)

`Utility.EvaluateClear`: el bono de peligro se quita para **cualquier** sacador de reanudación, no sólo en el saque de
puerta. Banda, córner y centro no admiten despejar (`RestartAllows`), y el penalti no pasa por aquí porque `TakePenalty`
no marca sacador (`RestartTakerIndex`, que además dura una sola decisión: `MatchEngine.cs` lo pone y lo quita alrededor
de la primera). En la práctica son el saque de puerta y la falta: el balón muerto. Sin dato nuevo. El despeje sigue
siendo legal en la falta (ADR 0143) y la presión lo sigue puntuando.

**Acoplamiento anotado en el código:** si otra reanudación pasa a admitir `Clear`, hereda la exención sin que nadie lo
decida. Ningún test lo fija.

## Revisión de diseño (`game-design-review`, 11 oct)

La de BX-8 razonaba sobre el portero en el saque, con los rivales fuera del área; ésta es la falta, con un jugador de
campo y el rival a dos casillas. Pedida por la revisión independiente.

1. **Qué ve el jugador:** tras una falta a favor cerca de su área, su defensa revienta el balón y el rival se lo queda
   dos de cada tres veces.
2. **Qué decide hoy:** nada; es conducta de la IA, como el resto de decisiones en el campo.
3. **Qué debería ver:** que la falta que le hacen le dé el balón. Una falta en contra le cuesta al infractor la posesión;
   sin esto, el castigo se devolvía al pie del rival.
4. **Regla:** ADR 0143 (la falta admite cualquier acción) y RF-052 (la barrera). No se crea ninguna: el peligro mide
   dónde está el balón (`MatchEngine`, distancia a la portería), y con el balón muerto esa posición no es amenaza.
5. **Sistema:** `/Sim`, `Utility.EvaluateClear`. Nada en `/data` ni en `/Game`.
6. **Alternativas:**
   - (A) **elegida**, sin bono de peligro con el balón muerto;
   - (B) prohibir `Clear` en la falta en `RestartAllows`: contradice la ADR 0143 y quita el último recurso;
   - (C) bajar `clearDangerBonusPerCenti`: cambia el despeje en juego abierto, que funciona;
   - (D) no hacer nada: 0,06 despejes regalados por partido.
7. **Trade-off:** menos alivio. El balón largo sigue disponible como `LongPass`, que ahora sí compite por su valor.
8. **Estrategia:** cometer faltas en campo rival cuesta más, porque el rival juega desde atrás en vez de devolverlo.
   Encaja con la carnicería administrada: la falta tiene precio para quien la hace.
9. **Degeneración:** el despeje no aparece nunca (0 de 65). Con la barrera, la presión al sacador es 0 y siempre hay un
   pase seguro. Es «último recurso» que nunca hace falta, no una prohibición: si cambian el radio de la presión o la
   barrera, el despeje vuelve a puntuar por la presión. **ASSUMED**, sin test que lo fije.
10. **Demostración:** test permanente de despejes, sonda de conservación (arriba) y lote de balance (abajo).
    - Sin test permanente de conservación: la de la sonda es una medida única.
    - Lo nuevo del pase en profundidad desde el tercio propio no tiene medición aparte de la de arriba.

- Test permanente: `GoalKickDecisionTests.AFreeKickInTheOwnThirdIsNotAClearanceByDefault` (200 partidos, techo 5 %).
  Falla sin el cambio de `Utility.cs` (comprobado).
- `OnlyTheGoalKickDropsTheDangerBonusOfTheClearance` pasa a llamarse `OnlyTheDeadBallDropsTheDangerBonusOfTheClearance`;
  sigue comprobando que, con el balón en juego, el portero cobra el peligro.
- `MobNarrowingTests`: huellas renovadas (el árbol sin el cambio pasaba las seis).
- `GoalResetTests.NobodyWalksHomeDuringTheCelebration` reventaba con `First()` en un gol sin reinicio (el del final o
  el que abre la turba): el test hermano ya lo eximía y éste no. Arreglo de instrumento, no del motor.

## Balance (`--runs 3000`, semillas 1 y 2, base = `main` 9ba7ddf)

Puertas (`Category=Gate`, semilla 1): rojas sólo las dos que ya lo estaban en la base, `coherentBuildsBeatNone_orc_violence` (57,81; base 57,45) y `betterTeamWinRate_human_60_vs_human_40` (92,17). En el lote, ninguna métrica cambia de estado; la que más se mueve lo hace un 9 % de su banda (`share_over5goals`, INFO). Fuera de
banda sólo `betterTeamWinRate_human_60_vs_human_40`, que ya lo estaba en la base ([BV-C](./BV-C.md)).

## Queda abierto

- ~~El bono de peligro **tras blocar**~~: arreglado en [BX-8c](./BX-8c.md).
- **Instrumento validado (11 oct, Regla J):** clasificadas por el evento del sacador en vez de por `ActionAt`, las 63
  faltas coinciden una a una (12 despejes antes, 0 después).
