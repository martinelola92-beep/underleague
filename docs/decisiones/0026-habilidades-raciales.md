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
| **Elfos** | Toque | Esquivan mejor las entradas y las intercepciones | Cubre los dos canales de mayor recorrido medido (intercepción y regate) sin tocar el de pase, que está saturado; y expresa la fragilidad élfica como *evitar* el contacto en vez de ganarlo |
| **Enanos** | Raíces | No pueden ser desplazados por empujes | Da identidad mecánica a los cuerpos de la ADR 0020 y hace del bloque enano una fantasía real, compensando su correa corta |
| **No-muertos** | No sienten nada | Inmunes al duelo y las lesiones leves no les penalizan | Ya insinuado en RF-035 y RF-104; convierte el desgaste, que es el recurso central del juego, en su ventaja |

Razas de DLC, esbozadas para comprobar que el espacio de diseño da de sí: elfos oscuros (sus faltas desplazan menos el criterio del árbitro), demonios (su masa desplaza a dos rivales a la vez), vampiros (se curan con las lesiones que provocan, RF-035), lagartos (convierten lesiones graves en leves, RF-035).

## Alternativas descartadas

- **Habilidades como código especial por raza**: rompe el principio de que las reglas viven en datos (RT-031) y multiplica los casos especiales del simulador.
- **Habilidades como bonos de atributos**: redundante con el sesgo poblacional y aburrido de leer.

## Consecuencias

- Cada habilidad debe respetar RF-032: no puede empujar tan fuerte hacia un estilo que colapse las tres builds viables de su raza. Es material de vigilancia en `/Balance`.
- La habilidad se muestra en la pantalla de selección de club y en el informe de ojeo del rival (RF-012b): forma parte de lo que el jugador debe poder prever (RF-012d).
