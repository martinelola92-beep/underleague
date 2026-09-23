# BE-E — Un jugador legendario no puede aparecer nunca, y hay un precio para él

Estado: **parte de diseño RESUELTA por la ADR 0128** (el legendario no sale del mercado a propósito: es el premio de ganar una run con un clan). **Cerrada también la incoherencia de dato** (23 sep 2026): el cuarto precio se conserva —el tipo y el esquema exigen cuatro— y el `_doc` declara que el de compra es inalcanzable. **Queda abierto** el techo de 5 slots de perk del legendario, inalcanzable mientras la ADR 0128 siga bloqueada. Encontrada el 22 sep 2026 midiendo el censo de
ofertas de mercado para la ADR 0127 — de camino, no buscándola.

## Síntoma

En **12.600 ofertas de mercado muestreadas, ni un solo jugador legendario**. No es mala suerte:
`GeneratedPlayers.RarityWeights` es un record de **tres** campos —`(int Common, int Uncommon, int Rare)`—
y su método `Pick` solo puede devolver esas tres. **`Rarity.Legendary` es inalcanzable por construcción
del tipo**, no por el valor de unos pesos.

Afecta a las tres vías generadas: `RecruitWeights (60,32,8)`, `MercenaryWeights (25,55,20)` y
`RewardWeights (35,50,15)`. Los porcentajes medidos (60,1 / 31,8 / 8,1) coinciden exactos con la primera.

## Y el dato promete lo contrario

`data/economy/economy.json` declara **cuatro** precios: `playerPriceByRarity: [40, 60, 85, 140]`. El cuarto
es el precio de una rareza que **ningún mercado puede ofrecer**. Igual `playerSaleBaseByRarity: [4, 9, 18, 36]`.

Es el mismo patrón que `docs/project-state.md` ya tiene registrado nueve veces: *el texto o el dato
promete lo que el código no hace.*

## Por qué importa

- `Progression.PerkSlots` da **5 slots de perk** al legendario, contra 4 del raro. **Ese techo de build es
  hoy inalcanzable** salvo que la plantilla inicial del club traiga un legendario.
- La ADR 0127 propone un elenco de fichajes escrito a mano. Si se escribe replicando la distribución
  actual, **se hereda el hueco sin darse cuenta**; si se decide incluir legendarios, es una **capacidad
  nueva** que hay que medir, no un detalle de contenido.

## Qué NO se sabe

Si es una decisión deliberada (el legendario se reserva a un origen que no existe todavía) o un descuido.
**No hay ADR que lo registre.** El comentario de `economy.json` no menciona la rareza legendaria de
jugador en ningún punto.

## Antes de tocar nada

La pregunta es de diseño, no de código: **¿debe existir un jugador legendario en una run, y de dónde sale?**
Va a `game-design-review`, no a un arreglo. Si la respuesta es que sí, la vía natural es el elenco escrito
de la ADR 0127, y entonces hay que medir qué le hace a `runWinRate` un jugador con 5 slots de perk.

## Cerrada la incoherencia de dato (23 sep 2026)

La ADR 0128 ya decidió el diseño —el legendario es el premio de ganar una run con un clan, y **no sale del
mercado a propósito**— y dejó anotado que de los dos cuartos valores **el de venta sigue haciendo falta**
(un legendario heredado se podrá vender) y el de compra no.

**No se quita el cuarto valor de `playerPriceByRarity`, y no por pereza**: el esquema
(`data/schemas/economy.schema.json`, `$defs/priceByRarity`) y el cargador
(`EconomyData.ReadPriceByRarity`, que lanza *«debe tener exactamente 4 valores: [common, uncommon, rare,
legendary]»*) exigen los cuatro para **todos** los `*ByRarity`. Quitar uno sólo en este obligaría a un tipo
asimétrico y a una excepción en el validador, para ahorrar un número que no se lee. Sería cambiar una
incoherencia pequeña por una irregularidad estructural.

Lo que sí se arregla es lo que la ficha señalaba de verdad —*el dato promete lo que el código no hace*—:
el `_doc` de `data/economy/economy.json` declara ahora que **los dos** cuartos valores, el de compra y el
de venta, son hoy inalcanzables. Un primer intento dijo que el de venta «sí es real, porque un legendario
heredado se podrá vender»; la revisión independiente lo tumbó por dos motivos y tenía razón en los dos:
**contradecía al esquema** (`$defs/priceByRarity`: *«nada legendario se genera ni se pone a la venta»*) y
presentaba en presente una capacidad futura —sin legendarios en plantilla no hay nada que vender—, que es
el mismo patrón que esta ficha existe para cerrar.

**De paso se encontró algo peor en el mismo `_doc`**: cita cifras que ya no son las del fichero. Dice que
`playerPriceByRarity` vale `18-27-38-64` cuando el array es `[40, 60, 85, 140]`, y que `itemPriceByRarity`
es `8-14-28-52` cuando vale `[12, 22, 45, 85]`. Son las cifras del momento en que se escribió cada ADR,
conservadas como narrativa, pero el texto no decía que lo fueran — o sea, **el `_doc` describía mal el
mismo campo que documenta**. Ahora abre con un aviso de lectura que dice que manda el valor del campo, y
nombra las dos discrepancias.

**Sigue abierto, y es lo de fondo**: `Progression.PerkSlots` da **5 slots** al legendario contra 4 del
raro, así que **ese techo de build sigue siendo inalcanzable** mientras la ADR 0128 esté bloqueada por el
perfil entre runs (gate 4 de la ADR 0123). No es un hueco de dato: es una capacidad del juego que existe
en el código y que nadie puede tocar todavía.

## Hermanos

ADR 0127 (el censo que lo destapó) · [BE-D](./BE-D.md) · `docs/project-state.md`, patrón de divergencia
