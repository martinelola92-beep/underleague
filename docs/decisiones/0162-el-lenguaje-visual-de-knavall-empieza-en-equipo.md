# 0162. El lenguaje visual de Knavall empieza en Equipo

**Fecha:** 2026-09-29
**Estado:** Propuesta — **encargo del revisor**, pendiente de su visto bueno sobre las capturas
**Requisitos:** UI-001, UI-002, UI-004, UI-006, UI-010, UI-020, UI-021, RF-045, RF-075, RF-080..083, RT-014, RT-035, RT-073
**Relacionadas:** ADR 0120 (voz de pregón), ADR 0123 D6 (retransmisión + festival medieval + cómic), ADR 0161 §3 (cofre), `docs/estilo-visual.md` §1 (tinta gruesa y color plano), `docs/ui/README.md` §1-§2

## Contexto

La pantalla de Equipo funcionaba pero se leía como un menú de gestión: listas de texto, fichas expandibles
con párrafos, iconos de 10 px y botones de formulario. El revisor encargó (29 sep 2026, con dos imágenes de
referencia) rehacerla con el lenguaje del mundo de Knavall —retransmisión deportiva, torneo medieval, cómic
bruto, humor absurdo, objetos y carteles construidos a mano— **sin tocar la lógica** y **solo en Equipo**,
para que sirva de referencia antes de extenderlo al resto del juego. La segunda referencia, la «limpia», es
la que vale: pocos elementos decorativos, iconos grandes, jugador señalado protagonista, y la explicación en
carteles contextuales de uno en uno, nunca todos a la vez.

Lo que ya estaba decidido y se respeta:

- **Tinta gruesa y color plano** (`estilo-visual.md` §1), **irregularidad en el marco, nunca en el
  contenido** y **material en el marco, plano en el contenido** (`docs/ui/README.md` §1-§2).
- **Grenze Gotisch** para lo que proclama y **Barlow Condensed** para los datos (revisor, 20 sep 2026).
- **Sin calaveras, huesos ni marcos góticos** (RA-026): la muerte es una lápida y la lesión una muleta.
- **Color y forma siempre juntos** (UI-002): cada puesto y cada estado tiene icono además de color.
- **Nada de arte importado hasta cerrar la fase 2**: todo lo nuevo es procedural y determinista.
- **La pantalla no calcula nada del juego** (RT-014); el cofre lo resuelve `/Sim` (ADR 0161).

## Decisión

1. **Un kit de dibujo propio, `Game/Ui/Knavall/`**, que es la base del lenguaje para el resto de pantallas:
   `Ink` (paleta rojo/ocre/verde/negro/papel, papel envejecido, madera, brochazos, sellos, barras de
   atributo, modificadores), `InkIcons` (~60 iconos grandes por concepto: atributos, puestos, objetos,
   consumibles, rasgos, estados, acciones), `Portrait` (bustos de cómic por raza con variación por id),
   `InkTooltip` (el cartel de ayuda), `InkCanvas` (control dibujado con zonas que llevan cartel y acción),
   `PlaqueButton` (botón como placa física) y `Tiles` (casilla de inventario).
2. **Composición de Equipo**: cabecera de madera con escudo, cartel del club y **pestañas colgadas como
   placas**; la **plantilla siempre a la izquierda** con retrato grande, nivel, nombre, puesto y estado; y
   **cuatro pestañas**: **Plantilla** (la ficha del jugador señalado como protagonista, con el cofre y los
   consumibles en compacto a la derecha), **Alineación** (el campo de colocación de siempre, con zonas,
   cobertura, vínculos y riesgo), **Consumibles** y **Cofre**.
   *Alineación es la cuarta pestaña que el encargo no nombraba*: la colocación es la mitad de la pantalla y
   no cabe en la columna de la ficha (el campo necesita ~800 px de ancho para 16 casillas legibles), así que
   se le da su pestaña en vez de esconderla o recortarla.
3. **La explicación vive en carteles de ayuda**, uno por elemento, al pasar el ratón: pizarra oscura, borde
   de papel, icono, título y una o dos frases de **qué hace en el juego**. Desaparecen de la pantalla los
   textos permanentes de ayuda («hasta 3 equipados y al menos uno manual (RF-080…)», «sin salario
   (economía: fase 2)»…): esa información se ve ahora en la forma (tres huecos «al partido», el primero con
   su mano de manual) o en el cartel.
4. **Atributos con el valor con el que se juega**: cifra = `base + objeto`, recortada a 1..99 como hace
   `MatchPlayer`; la barra enseña la base en ocre, lo que suma en verde y lo que quita rayado en rojo; el
   modificador negativo va en placa roja. El cartel dice la cuenta («base 55, objeto −60: juega con 1»).
5. **El retrato sustituye al medallón abstracto en Equipo** (plantilla, ficha y fichas del campo), con el
   mismo fondo por raza y el mismo distintivo de puesto, para que la identidad no cambie de una pantalla a
   otra. `PlayerCard` y `Medallion` **siguen** en Mercado, Ojeo y Fin de run hasta que el lenguaje llegue ahí.
6. **El cofre se resuelve en un solo sitio**: lo elegido del cofre y la lista de PASAR A OTRO los guarda
   `TeamScreen`, y la ficha de Plantilla y la pestaña Cofre son dos vistas del mismo estado. Las tres
   decisiones (`EquipStoredItem`, `StoreItem`, `TransferItem`) no cambian.

## Alternativas descartadas

- **Tres pestañas exactas (Plantilla / Consumibles / Cofre) con el campo dentro de Plantilla**: con la ficha
  protagonista y el cofre a la vista, al campo le quedarían ~400 px: 25 px por casilla, ilegible para
  arrastrar y sin sitio para zonas y cobertura.
- **Reescribir `PlayerCard` en el nuevo estilo**: la comparten Mercado, Ojeo y Fin de run; cambiarla ahora
  extendería el rediseño a pantallas que el encargo deja fuera.
- **Imágenes pintadas (retratos, iconos)**: la regla de fase lo impide y un marcador procedural en el
  lenguaje correcto permite juzgar la composición ya.
- **Carteles de ayuda nativos de Godot con un Theme**: el panel nativo no admite la flecha ni el icono; el
  control propio sí, y el panel nativo se vacía solo en el Theme de esta pantalla (copia del de pantallas
  viejas), sin tocar los tooltips del resto.

## Consecuencias

- **Pantallas viejas intactas**: `Widgets`, `Style`, `PlayerCard` y `Medallion` no cambian. `Toast`,
  `PitchView`, `ChestPanel` y `ConsumablesPanel` sí, pero solo los usa Equipo.
- **Mando**: la colocación sigue completa con mando (UI-006) y los gatillos (o Q / E) cambian de pestaña;
  la ficha, el cofre y los consumibles siguen siendo **solo de ratón**, como ya lo eran, y la ayuda al pie
  lo dice. Los carteles de ayuda tampoco tienen todavía disparador de mando: **pendiente**.
- **Humor con medida**: en nombres, rasgos y alguna frase de cartel («con poca técnica, el balón le rebota
  en la espinilla»), en la bandera blanca del cobarde o la lápida con «RIP»; ningún chiste fijo en pantalla.
- **Texto**: todo sale de `UiText` (claves `ui.kn.*`) o del catálogo, en español como el resto de
  `UiText`; la deuda de inglés de la ADR 0123 D7 sigue igual.
- **Siguiente paso** cuando el revisor dé el visto bueno a Equipo: llevar el kit a Mercado (que comparte
  cofre, objetos y consumibles) y sustituir `PlayerCard` por la fila y la ficha nuevas; este ADR pasaría
  entonces a Aceptada.
- Capturas: `Game/screenshots/equipo*.png` (`-- --screenshots`), con tres nuevas: `equipo-ayuda`,
  `equipo-ayuda-rasgo` (carteles de ayuda) y `equipo-pasar` (PASAR A OTRO abierto).
