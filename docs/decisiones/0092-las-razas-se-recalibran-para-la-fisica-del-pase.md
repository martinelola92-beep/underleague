# 0092. Las razas se recalibran para la física del pase

**Fecha:** 2026-09-10
**Estado:** Aceptada e implementada (`data/races/*.json`, `data/tags/styles.json`)
**Decisión del revisor** (ante la ADR 0091 §investigación: «Recalibrar las razas»). **Amplía la ADR 0026**: el sesgo de atributos deja de ser «no es palanca» y pasa a tener una regla de forma (suma cero) y de amplitud, porque la física del pase cambió qué atributos deciden un partido. Nada de RF-024b cambia: la raza sigue describiendo a la población y cada individuo puede contradecirla.
**Requisitos:** RF-024, RF-024b, RF-031b, RF-032, RT-055, RT-057
**Relacionada:** ADR 0024 (estilos), ADR 0025 (presupuesto de generación), ADR 0026 (habilidades raciales y su presupuesto), ADR 0091 (la física del pase que lo provocó), D-29

## Contexto

Con la tanda 3 de AZ (ADR 0091) el abanico de las cinco razas sin perks (`RaceBalanceTests`, D-29,
10.000 partidos) pasó de 6,6 puntos al cierre de la ADR 0026 (47,6-54,1) a **21,8** (enanos 39,9 · elfos
61,7): el factor de proximidad de la intercepción multiplica por hasta 3,5 una cuota que ya lleva la
diferencia de técnica, y la carrera en ticks del pase en profundidad premia la velocidad. El revisor
prefirió recalibrar las razas antes que rebajar la física (`interceptContactPercent`), que es lo que había
pedido en la segunda partida.

## Lo que se aprendió midiendo (500 plantillas por pareja, 20.000 partidos por iteración, error ±1,1)

1. **El sesgo de raza sí es palanca ahora**, pero débil y con una trampa: `PlayerGenerator` renormaliza al
   presupuesto restando (o sumando) **1 a cada atributo por igual** hasta cuadrar la suma, así que un sesgo
   de suma positiva —el enano, +10 fuerza +12 aguante, suma −10 con la correa, pero +22 sin ella— se cobra en
   silencio **−4,4 de velocidad y −4,4 de técnica**, justo lo que la física nueva castiga. A la inversa, el
   elfo (suma +3) no perdía nada. Medido: sesgos a la mitad, 44 / 59; a cero, 48 / 55; con el sesgo del
   elfo a un tercio pero sin tocar la suma, otra vez 58-59.
2. **Los estilos pesan más que las razas**: `Fine` (70 % de los elfos) valía +10 técnica +6 velocidad;
   `Bulwark` (75 % de los enanos) −8 velocidad. Con el sesgo de raza a cero el elfo seguía en 54,6 y el
   enano en 48,3 solo por el estilo, los rasgos y la habilidad.
3. **Lo que no mueve nada** (probado una a una, dentro del error): la disciplina del enano (80 → 60), su
   correa (−18 → 0), sus rasgos (Resilient → Leader/Cerebral). `elf_touch` vale hoy +2,2 (dentro del techo
   de 2,5 de la ADR 0026) y `roots` +0,4.
4. **El experimento decisivo**: un enano con TODOS los datos del humano mide 47,6 contra 46,8 del humano
   (misma plantilla, ruido); así que el humano —estilo `Neutral`, sesgo cero— es hoy la referencia baja, y
   lo que sobra a orcos, elfos y no-muertos es la forma de sus estilos.

## Decisión

Tres reglas, y los datos que salen de ellas:

1. **El sesgo de raza es de suma cero.** La correa (`leash`), que no cambia el resultado de un partido
   (medido aquí y en el paquete U), es el atributo que cuadra la suma: el enano y el orco tienen correa
   corta, el elfo larga, que es además lo que dice la tabla §3.4 de requisitos.
2. **Técnica y velocidad no llevan sesgo de raza.** La identidad de la población se escribe en fuerza,
   aguante y correa; la técnica y la velocidad quedan para el **estilo** individual (Fine, Cold), con la
   mitad de la amplitud anterior, y para los rasgos (`Fast`, `Cerebral`).
3. **Techo del abanico: 8 puntos** en `RaceBalanceTests` (la banda 40-60 de D-29 sigue siendo la puerta;
   este techo es el criterio de recalibración y se anota aquí para la próxima vez).

| Dato | Antes | Ahora |
|---|---|---|
| `dwarf.attributeBias` | 10 / −14 / −6 / 18 / −18 | **8 / 0 / 0 / 10 / −18** |
| `elf.attributeBias` | −12 / 6 / 14 / −6 / 1 | **−6 / 0 / 0 / −2 / 8** |
| `orc.attributeBias` | 14 / −4 / −10 / 6 / −1 | **10 / 0 / −2 / 4 / −12** |
| `undead.attributeBias` | 2 / −10 / 4 / 14 / −2 | **2 / −4 / 4 / 8 / −10** |
| `human.attributeBias` | 0 | 0 |
| `styles.Fine` | −8 / 6 / 10 / −4 / 2 | **−4 / 2 / 4 / −2 / 1** |
| `styles.Bulwark` | 8 / −8 / −4 / 8 / −4 | **4 / −1 / −1 / 4 / −2** |
| `styles.Brute` | 10 / −2 / −8 / 4 / −2 | **5 / −1 / −3 / 2 / −1** |
| `styles.Cold` | −4 / −6 / 6 / 10 / −6 | **−2 / −3 / 3 / 5 / −3** |
| `elf.traitWeights` | Fast 16 · Scorer 12 · LongShot 14 · Cerebral 20 | **Fast 10 · Scorer 15 · LongShot 17 · Cerebral 14** |
| `dwarf.traitWeights` | Fast 4 · Resilient 28 | **Fast 8 · Resilient 24** |

(orden fuerza / velocidad / técnica / aguante / correa). Los pesos de estilo por raza, las disciplinas, los
cuerpos y las habilidades no cambian.

## Lo que se mide

`RaceBalanceTests`, 500 plantillas por pareja, semilla 1, iteración final: **enanos 46,1 · elfos 52,5 ·
humanos 47,3 · orcos 52,1 · no-muertos 52,1** (abanico 6,4; el de cierre de la ADR 0026 era 6,6). Con la
muestra de la puerta (250 plantillas) la puerta está en verde.

Referencia de 2.000 partidos (semilla 1, `data/balance/reference.json`, humanos) con las razas y estilos
nuevos, contra la de la ADR 0091 §ajuste: `shotsPerMatch` 7,72 → **8,12** (vuelve a banda: los estilos
recortados quitan velocidad y técnica a la defensa tanto como al ataque, y la conducción recupera algo),
`goalsPerMatch` 2,40 → 2,57, `possessionChanges` 22,5, `passChainAvgLength` 2,17, `ballThirdMaxShare` 50,4 →
51,1, `injuriesPerMatch` 0,97 → 0,92, `tacklesPerMatch` 11,2, `betterTeamWinRate` 60-40 90,1 → 89,2,
`passInterceptRate` 8,5 → 8,0, `throughPassesPerMatch` 8,0.

Puertas (`Category=Gate`, una invocación, 43 tests): **38 en verde**, incluidas `RaceBalanceTests`,
`RandomBuildLosesToItsBaseline` (`human_random` vuelve a banda) y `ShotsPerMatchAreInRange`. Cinco en rojo,
todas por décimas o por la escalera del acto 3, y ninguna banda se toca (instrucción del revisor):
`ballThirdMaxShare` 51,3 (≤ 50), `betterTeamWinRate` 60-40 88,55 (≤ 88), `eternal_crown` muy buena 54,6 no
mejora a buena 56,7 (las dos suben con las razas nuevas; ADR 0089), y una nueva: `grimhold_guns`
incoherente 38,1 sobre 35 ± 2,5 (la build incoherente del acto 1 pierde menos castigo cuando los estilos
recortan la técnica del jefe tanto como la suya).

## Consecuencias

- La ADR 0026 decía que el sesgo «describe a la raza y no es una palanca de balance»: sigue describiendo a la
  raza, pero **su suma y su reparto entre atributos son balance** y se miden. Se anota allí.
- Un elfo medio ya no es +13 de técnica sobre un humano sino +4 por su estilo (y sus rasgos de precisión);
  la diferencia se nota más en la ficha (`Fine`, `Cerebral`) que en la media. Es lo que pide RF-024b.
- `docs/fase1b-diseno.md` §1.1 y §1.2 muestran los datos de ejemplo con los valores nuevos.
- Palanca siguiente si el abanico vuelve a abrirse: las amplitudes de `styles.json`, nunca la suma de un
  sesgo de raza.
