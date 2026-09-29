# ADR 0161 — Botín y cofre: la liga da un objeto común, el muerto deja una reliquia y el almacén se ve

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**: *«en los partidos normales podemos dar
objeto, pero no perk […] si diese oro y algún objeto común igual estaría mejor»* y *«yo pondría un objeto
más los objetos que tenía equipados. Hay que crear un "cofre" en la pantalla de alineaciones para poder
cambiar objetos entre jugadores fácilmente»* (`docs/plan-diversion.md`).
**Enmienda la palanca 2 de la ADR 0055** («el equipamiento sólo se consigue en el mercado»), sólo para
objetos **comunes** y para la reliquia.
**Requisitos:** RF-071, RF-075, RF-076, RF-076b, RF-093, RF-114g, RF-122, RT-022, RT-035
**Relacionada:** ADR 0048 (condición 4: el equipo del muerto vuelve al almacén), ADR 0055, ADR 0124 (la
carrera del jugador, de la que sale la reliquia)

## Lo que había (Regla G)

- El motor tiene un **almacén** (`RunState.StoredItems`) y una decisión para equipar desde él
  (`EquipStoredItem`). Sólo se llena con el equipo de los muertos (ADR 0048, `MatchResolution`).
- **`/Game` no lo enseña**: ningún consumidor de `StoredItems` ni de `EquipStoredItem`. El equipo del muerto
  desaparece a ojos del jugador. Tampoco usa `TransferItem`, así que RF-075 («transferible entre jugadores»)
  no se cumple en la interfaz.
- La liga paga oro y nada más (`nodeRewards.league.picks = 0`, `rewardItemWeight = 0`).

## Decisión

1. **Botín de liga.** Ganar un partido de **liga** da, además del oro, **un objeto común** al almacén,
   sorteado con el flujo de recompensas del nodo (RT-022). Élite y jefe no cambian. El informe lo enseña.
2. **Reliquia.** Cuando muere un jugador propio, además de su equipo, el almacén recibe **una reliquia**:
   un objeto de `data/items/` marcado como reliquia, elegido según **lo que ese jugador hizo en la run**
   (`RunCareer`): goleador, carnicero (lesiones causadas), muro (entradas ganadas) o, si no destaca en
   nada, una reliquia genérica. Es determinista (sin tirada). El nombre personal («la bota de Grok») queda
   para cuando el almacén guarde algo más que ids (fase 2).
3. **Cofre** en la pantalla de Equipo: el almacén a la vista, y tres gestos sobre el jugador señalado:
   **equipar** desde el cofre (`EquipStoredItem`), **guardar** su objeto en el cofre (decisión nueva
   `StoreItem`) y **pasar** su objeto a otro jugador (`TransferItem`, que ya existía). Sin coste: los
   objetos ya estaban pagados. Un objeto equipado que se sustituye vuelve al cofre, nunca desaparece.

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Un cofre con lo que ha ganado y lo que le dejaron sus muertos, y objetos que se
   mueven de un jugador a otro sin pasar por la tienda.
2. **Qué decide.** A quién equipar qué, en cada partido, según el rival. Es la decisión que RF-076 promete
   («no cuántos, sino cuál y en quién») y que hoy no se puede tomar.
3. **Qué debería decidir.** Lo mismo.
4. **Regla.** RF-075 (hoy incumplida en la interfaz), RF-076; enmienda de la palanca 2 de la ADR 0055.
5. **Sistemas.** `/Sim`: `GoldCalculator` o la recompensa de liga (botín), `MatchResolution` (reliquia),
   `EquipmentSystem` (`StoreItem`), `RunPolicy` (equipar desde el cofre). `/data`: `data/items/` (reliquias
   marcadas) y su esquema. `/Game`: `TeamScreen` (cofre), informe (botín).
6. **Alternativas.** Mantener la palanca 2: el revisor la descarta. Dar el objeto como cuarta opción de
   recompensa: vuelve a quitar oro de la decisión, y la ADR 0049 midió que eso sale mal.
7. **Trade-off.** El mercado pierde exclusividad en los comunes; los raros y legendarios siguen sólo en el
   mercado.
8. **Estrategias.** Con 6-7 ligas ganadas por run hay un surtido de comunes para rotar según el rival, y la
   reliquia hace que un muerto importante deje algo con su historia.
9. **Degeneración.** Venta en masa de comunes para sacar oro: se vigila `leftoverGoldShare` y
   `brokeMarketRunShare`; si se desbordan, el objeto del botín se marca como no vendible.
10. **Cómo se demuestra.** Tests: la liga ganada añade un común determinista y la perdida no; la reliquia
    depende de la carrera; `StoreItem` y el reemplazo no pierden objetos. Lote de campaña con las puertas de
    economía; captura del cofre.

## Enmienda tras la revisión independiente (29 sep 2026)

- **Pasar un objeto a quien ya lleva otro no lo vende.** `TransferItem` mandaba el desplazado a
  `SellItemGold`: el cofre ofrecía como destino a cualquiera y el receptor perdía su objeto por oro sin
  avisar. Ahora el desplazado vuelve al **almacén** (mismo criterio que equipar desde el cofre, §3);
  vender sigue siendo sólo `ToPlayerId < 0`.
- **La reliquia se anuncia.** El informe post-partido dice «X deja <reliquia> en el cofre» en la fila de la
  muerte (`CasualtyRow.RelicName`, `RelicSystem.RelicFor`, la misma regla con la que se llena el almacén).
- **El mercado no sortea los consumibles uniformes.** Con 4 legendarios de 20 una de cada cinco ofertas lo
  era. Peso por rareza 60/30/12/3 por consumible (≈ 60/30/8/2 % de las ofertas con el catálogo de hoy).
  **Provisional, sin medir** (Regla H): no sale de un lote de `/Balance`; los perks y objetos no pesan por
  rareza sino por catálogo y `frequency`, y un consumible no tiene ninguna de las dos.
- **Un consumible puede apuntar al rival** (`target: opposingTeam`, sólo `team` u `opposingTeam`; por
  defecto el equipo propio). `severeInjury` es el canal de quien SUFRE la lesión: `the_ambush` lo subía a
  los propios y ahora lo sube a los rivales. La plantilla del canal dice «sufrir una lesión grave».

