# 0026. Habilidades raciales como perks de equipo

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor; diseño propuesto por Claude, pendiente de su visto bueno)
**Requisitos:** RF-031, RF-032, RF-035, RF-104, RT-035

## Contexto

RF-031 ya exige que cada raza aporte "una regla o afinidad exclusiva", pero no estaba diseñada. El revisor pide una habilidad especial por raza de la que se beneficien todos sus jugadores.

## Decisión

**Se implementan como perks internos** con `race:` (ADR 0023), asignados automáticamente a toda la plantilla y no ocupando slot. Reutilizan el motor de efectos, los límites y —lo importante— la **descripción generada** (RT-035), así que es imposible por construcción que su texto y su efecto divirjan.

Criterio de diseño: cada habilidad usa un **canal distinto** y ninguna es "más números", porque los sesgos de atributos ya cubren eso. Se apoyan además en los canales con recorrido real medidos en `docs/balance/fase1-perks.md` (intercepción, lesión, correa, parada, regate), no en los saturados.

| Raza | Habilidad | Efecto | Por qué |
|---|---|---|---|
| **Humanos** | Adaptables | Ganan experiencia más deprisa | Club de referencia y tutorial implícito: no distorsiona el partido, premia la continuidad y hace legible la progresión desde la primera run |
| **Orcos** | Sangre caliente | Sus entradas dejan al rival derribado más tiempo | Convierte la violencia en ventaja **posicional**, no solo en lesiones; se nota en el campo sin depender del árbitro |
| **Elfos** | Toque | Esquivan mejor las entradas ~~y las intercepciones~~ (ver *Presupuesto de impacto*) | Cubre los dos canales de mayor recorrido medido (intercepción y regate) sin tocar el de pase, que está saturado; y expresa la fragilidad élfica como *evitar* el contacto en vez de ganarlo. La mitad de intercepción se retiró en 2026-09-04: el canal no admite ningún valor legal que no sea una inmunidad |
| **Enanos** | Raíces | No pueden ser desplazados por empujes | Da identidad mecánica a los cuerpos de la ADR 0020 y hace del bloque enano una fantasía real, compensando su correa corta |
| **No-muertos** | No sienten nada | Inmunes al duelo y las lesiones leves no les penalizan | Ya insinuado en RF-035 y RF-104; convierte el desgaste, que es el recurso central del juego, en su ventaja |

Razas de DLC, esbozadas para comprobar que el espacio de diseño da de sí: elfos oscuros (sus faltas desplazan menos el criterio del árbitro), demonios (su masa desplaza a dos rivales a la vez), vampiros (se curan con las lesiones que provocan, RF-035), lagartos (convierten lesiones graves en leves, RF-035).

## Presupuesto de impacto de una habilidad racial (añadido 2026-09-04, D-29)

La tabla de arriba reparte **canales** pero no repartía **presupuesto**, y esa es la mitad que faltaba. La
puerta `Sim.Tests/Analysis/RaceBalanceTests.cs` sacó el defecto a la luz: las cinco habilidades no jugaban
en la misma liga ni de lejos. Medido apagando cada habilidad en los datos y volviendo a medir la matriz
todas-contra-todas de las cinco referencias `*_none` (1.000 plantillas por pareja, 40.000 partidos,
semilla 1); la línea de base son las cinco razas **sin ninguna habilidad dentro del partido**, donde solo
pesan los sesgos de atributos: enanos 49,5 · elfos 51,0 · humanos 49,3 · orcos 51,8 · no-muertos 48,3.

| Habilidad | Cuándo actúa | Canal (base) | Valía |
|---|---|---|---|
| **Toque**, `tackleEvasion` +10 y `interceptEvasion` +10 (como estaba) | siempre | entrada (2.800) e intercepción (250) | **+10,4** |
| Toque, solo `tackleEvasion` +10 | siempre | entrada (2.800) | +3,8 |
| Toque, solo `tackleEvasion` +5 | siempre | entrada (2.800) | **+1,7** |
| **Sangre caliente**, +5 ticks (como estaba) | al ganar una entrada o un bloqueo | ticks de derribo (18) | +0,9 |
| Sangre caliente, +15 ticks | al ganar una entrada o un bloqueo | ticks de derribo (18) | **+1,4** |
| **Raíces** | al ser empujado | inmunidad, sin escala | +0,6 |
| **Adaptables**, **No sienten nada** | fuera del partido | experiencia, duelo, lesión leve | 0 por construcción |

La mitad de intercepción de Toque valía ella sola **+6,6 puntos**: cuatro veces lo que valen las otras
cuatro habilidades juntas, y más que catorce perks equipados sobre una plantilla desnuda medidos en el
paquete U. No era un número mal calibrado, era una **categoría equivocada**: `intercept` tiene base 250
(2,5%) y el escalón mínimo de la escala de `estilo-descripciones.md` son 5 pp, o sea 500 puntos. El valor
legal más pequeño que se puede escribir deja la probabilidad en cero. No existe "esquivar un poco mejor las
intercepciones"; solo existe "ser inmune a las intercepciones" (es D-30 exactamente).

### Criterio

Una habilidad racial es **gratis, irrenunciable y de plantilla entera** (RF-031b): no ocupa slot, no se
elige, no se puede quitar, no se puede jugar en su contra y el rival no puede construir para negarla. Por
eso no compite con el catálogo de perks y su presupuesto no es el de un perk raro. Es el más pequeño que el
juego sabe escribir. Tres reglas, en este orden:

1. **Canal legal antes que valor.** Una habilidad racial no puede vivir en un canal cuya base sea menor que
   el escalón mínimo de la escala (5 pp = 500 puntos): ahí el efecto no es un modificador, es un
   interruptor. Con las bases actuales eso deja fuera `intercept` (250), `injure` (40 en entrada limpia),
   `foul` (320) y `card` (250), y deja dentro `tackle` (2.800), `save` (5.000), `dribble` (7.200) y `pass`
   (7.700, con el tope de 9.800 comiéndose parte). Es la regla que este reequilibrio incumplía.
2. **Techo: un escalón.** El escalón mínimo de la escala sobre un canal con recorrido vale **+1,7 puntos**
   de tasa agrupada (medido: `tackleEvasion` +5). Ese es el tamaño de una habilidad racial. El techo de la
   puerta se fija en **+2,5 puntos** sobre la línea de base sin habilidades, que es ese escalón más el
   ruido de la medida (±0,8 de error típico por raza a 1.000 plantillas). Una habilidad cuyo canal no use
   la escala de puntos porcentuales —los ticks de derribo de Sangre caliente— se calibra midiendo hasta
   caer dentro del mismo techo, no copiando el número de otra.
3. **Fuera del partido, presupuesto aparte.** Adaptables y No sienten nada valen 0 puntos de tasa de
   victoria por construcción y no hay forma de meterlas en esta escala sin cambiarles el canal, que es
   justamente su identidad. Su presupuesto se mide en la campaña —ritmo de experiencia, desgaste de
   plantilla— y le toca a la fase 2, no a esta puerta. Que dos de las cinco razas no compren nada dentro
   del partido es una decisión, no un descuido: son las dos razas cuya ventaja es la **continuidad**.

### Qué cambió

- **Toque (elfos)**: `tackleEvasion` de 10 a **5**, y se retira `interceptEvasion`. De +10,4 a +1,7. El
  canal `interceptEvasion` queda abierto en el motor y sin usar en `/data`: la mitad de intercepción de
  RF-031b vuelve el día que D-30 ponga `intercept` en escala (base más alta, o escala relativa). Mientras
  tanto, RF-031b y la tabla de razas de §3.4 describen media habilidad de más; queda anotado en
  `docs/pendientes.md` como cambio de requisito pendiente de que el revisor lo confirme.
- **Sangre caliente (orcos)**: de 5 a **15** ticks de derribo. De +0,9 a +1,4, que es la misma liga que
  Toque. El canal satura pronto (de 5 a 15 ticks solo compra medio punto), así que subir más no compra
  identidad, solo alarga un derribo que ya dura el doble.
- **Raíces, Adaptables, No sienten nada**: sin cambios. Ya estaban dentro del presupuesto; Raíces por poco
  y las otras dos por construcción.
- **`data/races/elf.json`**: se restaura el sesgo de atributos que un ajuste anterior había borrado
  (`strength -35` y todo lo demás a 0) para compensar Toque. El sesgo de atributos **describe a la raza**
  (RF-024b, tabla §3.4: "Técnica, precisión") y no es una palanca de balance: renormalizado al presupuesto
  de generación es casi neutro, y medido sin habilidades las cinco razas caben en 3,5 puntos. Vuelve a
  `{ strength −12, speed +6, technique +14, stamina −6, leash +1 }`, y `discipline` a 35, su valor de
  diseño (el comentario de `Utility.OutsidePenalty` lo cita explícitamente).

## Alternativas descartadas

- **Habilidades como código especial por raza**: rompe el principio de que las reglas viven en datos (RT-031) y multiplica los casos especiales del simulador.
- **Habilidades como bonos de atributos**: redundante con el sesgo poblacional y aburrido de leer.

## Consecuencias

- Cada habilidad debe respetar RF-032: no puede empujar tan fuerte hacia un estilo que colapse las tres builds viables de su raza. Es material de vigilancia en `/Balance`.
- La habilidad se muestra en la pantalla de selección de club y en el informe de ojeo del rival (RF-012b): forma parte de lo que el jugador debe poder prever (RF-012d).
