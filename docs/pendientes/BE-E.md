# BE-E — Un jugador legendario no puede aparecer nunca, y hay un precio para él

Estado: **parte de diseño RESUELTA por la ADR 0128** (el legendario no sale del mercado a propósito: es el premio de ganar una run con un clan). **Queda abierta la incoherencia de dato**: `playerPriceByRarity` declara un precio de compra para una rareza que el mercado no puede ofrecer. CONFIRMED por lectura de código. Encontrada el 22 sep 2026 midiendo el censo de
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

## Hermanos

ADR 0127 (el censo que lo destapó) · [BE-D](./BE-D.md) · `docs/project-state.md`, patrón de divergencia
