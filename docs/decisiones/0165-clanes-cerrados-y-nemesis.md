# ADR 0165 — Clanes cerrados y némesis

Fecha: 29 sep 2026 · Estado: **aceptada**. **Decisión del revisor** (`docs/plan-diversion.md` §3): *«¿no se
supone que te vuelves a encontrar con los mismos equipos? Hablamos de hacer equipos cerrados y
reconocibles»*, *«el clan por raza que sube de categoría ok»* y *«entre actos hacemos que ese némesis lo
ficha otro equipo gracias a que se ha dado a conocer»*.
**Implementa** la F1.5 del plan Knavall (el clan cruza de acto). **Enmienda RF-015** (los rivales son
estáticos por acto y división): ahora son **los mismos clanes en los tres actos**, que suben de categoría.
**Requisitos:** RF-012b, RF-015, RF-093, RF-119, RF-122, RT-022, RT-030, RT-031
**Relacionada:** ADR 0124 (carrera y créditos de rival), ADR 0163 (la Gaceta y su villano), BR-B (la muerte
es terminal).

## Lo que había (Regla G)

Quince rivales de datos, cinco por acto (uno por raza), con jugadores con nombre. Los del acto 2 ya repiten
algunos nombres del acto 1, pero bajo **otro nombre de clan** («Yunque Verde» en el acto 1, «Horda de
Colmillo Rojo» en el 2). Los rivales **no guardan estado**: si le partes la pierna a Grok, el siguiente
partido llega sano. `RivalCredits` (ADR 0124) ya registra quién lesionó o mató a quién, y la Gaceta elige
con ello un villano.

## Decisión

1. **Un clan por raza en los tres actos.** Cada fichero de rival gana `clanId`; los tres de una raza
   comparten clan, **nombre de clan** (el del acto 1) y **nombres de jugador por puesto**: el que jugaba de
   central en el acto 1 sigue en el acto 2, subido de nivel. Las cifras (atributos, perks, dificultad) de
   cada acto **no cambian**: es un cambio de identidad, no de balance. La descripción de cada acto cuenta
   cómo ha crecido el clan.
2. **Los rivales tienen memoria.** Estado nuevo en la run (sube el guardado): por jugador rival de clan,
   si está **muerto** (no vuelve: su puesto lo ocupa un fichaje con nombre generado y las mismas cifras) y
   sus **títulos**. Las lesiones de los rivales no se arrastran (curan entre partidos).
3. **Némesis.** Un jugador rival que **mata** a uno de los tuyos se convierte en tu némesis:
   - gana un **título** con nombre («el Matahermanos», de datos, localizado) y **un nivel**;
   - el ojeo lo marca con su título y a quién mató; el mapa marca el nodo donde juega;
   - **entre actos lo ficha otro clan** («se ha dado a conocer»): al empezar el acto siguiente pasa, de forma
     determinista, a uno de los otros clanes, sustituyendo a su jugador de menos nivel del mismo puesto;
   - **venganza**: si lo lesionas o lo matas, el informe lo proclama, el jugador que lo hizo suma
     «venganzas» a su carrera (alimenta un apodo, ADR 0163) y la run cobra una recompensa de oro
     (provisional, sin medir). Un némesis muerto deja de serlo;
   - **tope**: dos némesis vivos a la vez; con dos, el siguiente asesino no se convierte (se anota).
4. **La Gaceta** prefiere al némesis vivo o vengado como villano de la temporada.
5. **Aleatoriedad**: los fichajes y el traspaso salen de un flujo propio derivado de la semilla de la run
   (`OfferStream`, desplazamiento 9500 o una sal propia), nunca del de partido (RT-022).

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Los mismos cinco clanes toda la run, con caras que reconoce, y un villano con
   nombre que le mató a alguien y que reaparece.
2. **Qué decide.** Evitar o buscar el nodo donde juega su némesis; alinear para vengarse (Brutos contra él);
   aceptar el riesgo de que vuelva a matar.
3. **Qué debería decidir.** Lo mismo: la venganza es opcional y cuesta exponerse.
4. **Regla.** RF-015 enmendada; memoria de rival nueva.
5. **Sistemas.** `/data`: `rivals/*.json` (`clanId`, nombres), `nemesis/` (títulos). `/Sim`: estado de rivales
   en `RunState` (guardado v8), `RivalTeamBuilder` (muertos, fichajes, némesis traspasado, +1 nivel),
   `MatchResolution` (convertir en némesis, venganza), carrera (`Revenges`), apodo nuevo, `GazetteView`.
   `/Game`: ojeo, mapa, informe, retransmisión (pregón de venganza si encaja).
6. **Alternativas.** Mantener clanes distintos por acto: es lo que el revisor señaló como fallo.
7. **Trade-off.** Un némesis con un nivel más es más peligroso: vengarse cuesta.
8. **Estrategias.** Da a las builds de violencia un objetivo con nombre y a las defensivas un motivo para
   esquivar nodos.
9. **Degeneración.** Muertes en cadena del mismo némesis: el tope de dos y que un nivel más no es mucho. Se
   mide `nemesesPerRun`, `revengesPerRun` y `deathsPerRun` contra la línea base.
10. **Cómo se demuestra.** Tests: clan y nombres coherentes en los tres actos; un rival muerto no vuelve;
    némesis al matar, traspaso determinista entre actos, venganza, tope; guardado v8 ida y vuelta. Lote de
    campaña antes y después; capturas del ojeo con némesis y del informe con venganza.

## Implementación (29 sep 2026)

- **Datos**: `clanId` en los quince `data/rivals/*.json`; los tres de un clan comparten nombre de clan y nombre
  y demarcación por puesto (el cargador lo exige: `RivalCatalog.CheckClans`, con número de jugadores y
  demarcación). Las cifras no cambian (`NoRivalChangedASingleNumberWithTheClans` lo fija por SHA).
  `data/nemesis/titles.json`: diez títulos es/en, `maxAlive` 2 y `levelBonus` 1 (provisionales, Regla H).
  `economy.json`: `revengeGold` 3 (provisional, sin medir).
- **Estado** (`RunState.RivalMemory`, guardado **v8**; un v7 se rechaza explícitamente, como con cada subida):
  vacantes por clan y puesto (`generation` = qué fichaje la cubre) y némesis (título, clan y puesto de origen y
  actual, víctima, muertes, estado, `avenged`).
- **Regla** (`NemesisSystem`, pura): al resolver un partido de catálogo, una muerte propia causada por un rival de
  clan crea un némesis (título por `OfferStream`, desplazamiento 9500 + id) o suma muerte al que ya lo era; con el
  tope, se anota en `nemesisCapped`. Una baja de un némesis causada por un jugador propio es una venganza. Al
  entrar en el acto siguiente, cada némesis vivo pasa a otro clan (nodo ficticio 9500 + acto), al titular de menos
  nivel de su demarcación.
- **Alineación** (`RivalTeamBuilder`): un puesto vacante lo cubre un fichaje con nombre generado
  (`RngStreams.Rewards`, sal 950.000.000 + clan + puesto + generación —*enmendado por la [ADR 0169](0169-un-nombre-no-se-repite-en-la-run.md):
  ahora sale del billete `generación·10 + puesto` del retículo de nombres de la raza, sin repetirse dentro del clan*—) y las mismas cifras; el némesis juega con su
  nombre y `levelBonus` niveles más; **un némesis de banquillo juega de titular** en lugar del último titular de
  su demarcación (regla nueva, no estaba en la decisión: «el mapa marca el nodo donde juega» tiene que ser verdad).
- **Vista y `/Game`**: `NemesisView` (ojeo en lacre con título y víctima, rombo en el mapa, pregón de la
  retransmisión, villano de la Gaceta), `PostMatchView` (némesis nacido y venganza en el informe, con el oro).
  Capturas: `Game/screenshots/{ojeo,mapa}-nemesis.png` e `informe-venganza.png` (`CapturasNemesis.tscn`). La de
  `informe-nemesis.png` **no sale**: en 250 primeros partidos no nace ningún némesis, porque los rivales del
  acto 1 no llevan perks letales (el ojeo lo dice: «este rival no puede matar a nadie») y la muerte por
  reincidencia es rara. No es un fallo de la regla: en runs completas nacen 0,94 por run (abajo).

## Revisión independiente (29 sep 2026)

Arreglado, con test que falla antes y pasa después cuando aplica:

- **Grave — el némesis que era un fichaje resucitaba al morir y quedaba duplicado en el traspaso**: al dejar el
  puesto se conservaba la generación de la vacante, que era él mismo. Ahora quien deja un puesto (muerto o
  traspasado) lo pasa siempre al siguiente fichaje (`ASigningWhoBecameANemesisDoesNotComeBackAfterDying`,
  `ASigningNemesisHandedOverIsNotAlsoLeftInItsHomeClan`; los dos fallan con la regla anterior).
- **El informe decía «lesionó» cuando la venganza mataba**: un rival llega sano, así que muere por un perk letal
  sobre una lesión del mismo partido; la lesión abría la venganza y la muerte ya no la actualizaba. Ahora sí
  (`AnInjuryThenADeathInTheSameMatchIsProclaimedAsSlain`).
- **Venganza repetible** (decisión tomada sin consultar, la pregunta 9 no la contemplaba): lesionar al mismo
  némesis en cada partido pagaba oro y sumaba venganzas cada vez. **Una deuda de sangre se cobra una vez**
  (`avenged`); si el némesis vuelve a matar, vuelve a deber; matarlo ya vengado se proclama sin segundo cobro
  (`ABloodDebtIsPaidOnceAndComesBackIfTheNemesisKillsAgain`).
- **El ojeo contaba los reencuentros por fichero de acto**: el clan del acto 2 no era «2.ª vez». Ahora por clan
  (`RivalHistory.AgainstClan`). Y marca al némesis por puesto, no por nombre (un fichaje puede llamarse igual).
- Dos némesis de banquillo de la misma demarcación se pisaban en la alineación (latente con los datos de hoy);
  `CheckClans` indexaba sin comprobar tamaños; la tabla de flujos de `OfferStream` no listaba los nuevos.

Anotado sin arreglar: los créditos de rival y el epitafio de la Gaceta nombran al jugador de datos aunque
ocupara el puesto un fichaje o un némesis traspasado → [BS-A](../pendientes/BS-A.md). Los textos nuevos de
`UiText` están sólo en español, como el resto de esa tabla (los títulos sí son es/en). «Con un nivel más» está
escrito en el texto del ojeo aunque `levelBonus` es un dato.

## Medición de campaña (29 sep 2026)

`/Balance --full-runs 600 --seed {1,2}` (tres doctrinas; resumen sobre el lote principal de 600), desde copias
congeladas de cada commit: `main` (base), `main` + ADR 0166 (gritos) y la integración con esta ADR ya revisada.

| métrica | base s1 / s2 | + gritos s1 / s2 | + clanes s1 / s2 |
|---|---|---|---|
| `runWinRate` | 16,33 / 18,50 | 16,00 / 18,33 | 18,00 / 18,67 |
| `deathsPerRun` | 2,22 / 2,23 | 2,21 / 2,22 | 2,17 / 2,26 |
| `bossWinRateAct3` | 38,6 / 47,0 | 38,1 / 46,4 | 42,7 / 45,9 |
| `nemesesPerRun` | — | — | 0,94 / 0,94 |
| `revengesPerRun` | — | — | 0,23 / 0,22 |
| `nemesesCappedPerRun` | — | — | 0,36 / 0,38 |

**Lectura** (Regla F):
- **LIKELY: los clanes no mueven las muertes ni la victoria.** Las diferencias (−0,04 / +0,04 muertes; +2,0 /
  +0,3 puntos de victoria) caen dentro del error típico de 600 runs (≈ 1,6 puntos con p ≈ 0,18) y cambian de
  signo o de tamaño entre semillas. Nada cambia de estado salvo `brokeMarketRunShare` en la semilla 2, que roza el
  suelo (10,17 frente a 10): ruido. `runWinRate` ya estaba bajo banda en `main` (balance aplazado por el revisor).
- **El sistema se activa**: casi un némesis por run, estable entre semillas. **El tope muerde**: por cada 0,94
  némesis hay 0,37 asesinos que no se convierten (~28 % de los candidatos). **La venganza es rara**: una de cada
  cuatro deudas se cobra; la política automática no busca al némesis, así que esto mide el azar, no la decisión.
- Sin medir: el efecto del +1 nivel aislado, la frecuencia de BS-A, y si un jugador real busca o esquiva el nodo
  del némesis (la política no lee la memoria de rivales).

### Puertas completas (29 sep 2026): 4 rojas de 45

Tres son las de `main` (curva de jefes `eternal_crown_excellent` 41,6; `orc_violence` ×2, 53,4).
`TheThreeDoctrinesBuyDifferently`, roja en `main`, pasa. **Una es nueva**:
`TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` — el 0,19 % de los actos de la muestra de la puerta puede
pagar los cuatro sumideros (RF-114k), y la puerta exige 0 exacto.

- **REJECTED — el oro de venganza**: con `revengeGold` = 0, la semilla 1 del lote sigue dando 0,08 %.
- **LIKELY — un acto de cola que cruza el umbral por efecto mariposa**: el oro ganado por acto no se mueve (máximo
  idéntico en los tres actos; percentil 99 dentro de ±4), y en la semilla 2 un acto 3 más rico que cualquiera de
  la semilla 1 no llega a los cuatro. Es ~1 acto de cada 1.200.
- **No se toca la puerta** (RT-057: moverla exige ADR con datos). Queda anotado: una puerta de «cero exacto» sobre
  una cola es frágil ante cualquier cambio que reordene las runs; la decisión (tolerancia o métrica por media) es
  del balance aplazado.
