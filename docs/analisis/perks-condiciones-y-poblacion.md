# Cuántos perks limitan por raza, y cuáles lo dicen

**19 sep 2026.** Censo de los **94** perks de `data/perks/` frente al planteamiento de diseño: *la mayoría
funcionan para todas las razas, algunos tienen condición de rasgo, y unos pocos son específicos de raza*.

## El reparto: el catálogo ya cumple el planteamiento

| nivel | nº | % | qué es |
|---|---|---|---|
| **1 — universal** | **73** | **78 %** | sin restricción de raza ni de etiqueta de estilo |
| **2 — de rasgo** | **12** | **13 %** | dependen de una etiqueta de estilo (`Brute`, `Fine`, `Bulwark`, `Cold`) |
| **3 — de raza** | **9** | **10 %** | condición fuerte: raza declarada |

Ningún perk del catálogo **impide** jugarse por raza salvo esos 9. Los 12 de rasgo **no limitan**: se
pueden llevar siempre, y lo que cambia es la probabilidad de que la etiqueta salga en tu plantilla. Esa es
la mano del jugador, no una restricción del dato.

## Los 9 de raza — los nueve lo declaran

`deathless_march` (Undead) · `elf_touch` (Elf) · `gentle_giant` (Orc) · `hot_blooded` (Orc) ·
`iron_gate` (Dwarf) · `numb` (Undead) · `quick_learner` (Human) · `roots` (Dwarf) ·
`unlikely_bulwark` (Elf + `Bulwark`).

**9 de 9 declaran `race` y/o `tagsRequired`.** El juego lo sabe, el cargador lo sabe, la asignación lo
respeta y el arnés de medición lo respeta. No hay ningún problema aquí — y `iron_gate` lo demuestra: al
buscarle una población mejor que la suya, el factor de mejora fue **1,0×**, porque ya se estaba midiendo
donde le toca.

## Los 12 de rasgo — solo **1** lo declara

| perk | rareza | etiqueta | dónde está la dependencia | ¿lo ve el previsualizador? |
|---|---|---|---|---|
| `fine_orchestra` | uncommon | Fine | **declarada** + condición | **sí** |
| `blood_tithe` | rare | Brute | solo en la condición | **sí** |
| `first_touch_school` | rare | Fine | solo en la condición | **sí** |
| `pack_mentality` | uncommon | Brute | solo en la condición | **sí** |
| `bruised_knuckles` | uncommon | Brute | solo en la condición | **sí** |
| `brute_boots` | common | Brute | solo en la condición | **sí** |
| `bulwark_stance` | common | Bulwark | solo en la condición | **sí** |
| `fine_touch` | common | Fine | solo en la condición | **sí** |
| `back_to_back` | common | Bulwark | solo en la condición (`nearAlly`) | no |
| `shadow_marker` | common | Brute | solo en la condición (`nearAlly`) | no |
| `crowd_control` | uncommon | Fine | solo en la condición (`nearOpponent`) | no |
| `cold_focus` | common | Cold | solo en la condición (sobre `actor`) | no |

**11 de 12 llevan su dependencia escondida en la condición.** Y eso **no hay que arreglarlo en `/data`**:
declarar la etiqueta en `tagsRequired` los convertiría en perks de raza, que es justo lo contrario de lo
que el nivel 2 pretende. Lo que falta no es una restricción — **es información**.

## Por qué importa: la composición racial no es uniforme

`data/races/*.json`, pesos de `styleTagWeights`:

| | Neutral | Brute | Fine | Bulwark | Cold |
|---|---|---|---|---|---|
| Human | 70 | 10 | 10 | 6 | 4 |
| Orc | — | **75** | — | — | — |
| Elf | — | — | **70** | — | — |
| Dwarf | — | — | — | **75** | — |
| Undead | — | — | — | — | **72** |

Un perk de `Bulwark` en plantilla humana vive con un peso de 6; en enanos, de 75. Medido: `bulwark_stance`
pasa de **7,5 % a 80 %** de exposición, `back_to_back` de **10 % a 90 %**, `shadow_marker` de **22,5 % a
90 %**. **Eso es el diseño funcionando**, no un fallo: el perk es peor en unas plantillas y mejor en
otras, y elegirlo es decisión del jugador.

Lo que falla es que **el jugador no puede verlo antes**.

## La cobertura del tooltip

`Sim/Perks/LineupPerkPreview.cs` decide, sin simular nada, qué perks enciende y apaga una colocación. De
los 12 de rasgo cubre **8**: los que preguntan `hasTag(owner,…)` o `teammatesWithTag(owner,…)`.

Los **4 restantes** quedan fuera, y el previsualizador calla a propósito:

- `back_to_back`, `shadow_marker`, `crowd_control` usan `nearAlly`/`nearOpponent`, que dependen de dónde
  estén los jugadores **durante la jugada**, no de la alineación. Un conteo de plantilla ("tienes 2
  Brutos") sería una pista honesta pero **no es la condición**, y prometerlo sería peor que callar.
- `cold_focus` pregunta por `actor`, no por `owner`: quién sea el actor depende del partido.

## La sensibilidad racial emergente es deseada, no un defecto

`grudge` no tiene ninguna condición de rasgo y aun así se expone **6,2×** mejor con orcos (12,5 % →
77,5 %), porque los orcos cometen y sufren más faltas. Es **sensibilidad emergente**, distinta de una
condición declarada: no sale del dato, sale de cómo juega cada raza.

**Decisión de diseño (revisor, 19 sep 2026): eso es lo suyo, y no se corrige.** Un perk universal puede
rendir mejor en unas plantillas que en otras, y encima **se puede compensar combinando**: `grudge` en un
humano acompañado de un segundo perk agresivo vuelve a tener sentido. Es material de build, no un
desequilibrio.

Consecuencia para el instrumento, que conviene tener presente al leer cualquier número de este proyecto:
**el arnés mide siempre UN perk aislado.** Nunca mide combinaciones. Así que una exposición baja o un
efecto pequeño en solitario no dicen lo que ese perk vale dentro de una build — y las builds son donde el
juego ocurre. No se persigue aquí; se anota para no confundir "flojo solo" con "flojo".

## Lo que este censo NO dice

- No dice nada sobre potencia: solo sobre con qué frecuencia el perk llega a existir.
- No dice nada sobre combinaciones, por lo de arriba.
- No mide la sensibilidad racial emergente de los otros 72 universales, y tras la decisión de arriba
  tampoco hace falta salvo que aparezca un caso extremo.
