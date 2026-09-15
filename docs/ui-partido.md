# La pantalla de Partido

Hermana de `ui-equipo.md` (UI-021) para la pantalla donde se mira el partido. Fija lo que el arte no podrá
arreglar después y por eso hay que decidir **antes** de encargarlo: geometría, cámara, qué carga con la
identidad y qué tiene que poder leerse sin el log.

**Criterio de salida de la fase 3:** *el partido se lee sin necesidad del log.* Todo lo que hay aquí existe
para eso. Conviene decirlo sin rodeos: **hoy no se cumple**, y el panel de log de abajo a la izquierda está
haciendo casi todo el trabajo narrativo.

Decisiones de las que cuelga este documento: **ADR 0102** (3D con toon y cámara ortográfica fija en tres
cuartos) y **ADR 0103** (el campo tiene seis filas).

> **La cámara ya no es fija (ADR 0114, 15 sep 2026).** La vista táctica de este documento sigue siendo el
> estado normal del partido y toda su geometría vale igual, pero encima hay dos estados más —**acción** y
> **cinematográfica**— y uno de presentación fuera del partido. Eso cambia el briefing de arte: el modelo
> ya **no** tiene que resolverlo todo en 37 px. Ver `estilo-visual.md` §5ter.

---

## 1. Geometría, y por qué no se negocia

| | |
|---|---|
| Rejilla | **16 × 6** casillas (`Pitch.Columns`, `Pitch.Rows`) |
| Área | **2 × 4** casillas (`Pitch.AreaColumns`, `Pitch.AreaRows`) |
| Escala del mundo | **1 casilla = 1 unidad** |
| Cámara | `Camera3D` **ortográfica**, elevación **60°**, fija: ni sigue ni suaviza |
| Rectángulo en pantalla | 1120 × 420 px, es decir **70 px por casilla en los dos ejes** |

**Ortográfica, no perspectiva.** Con perspectiva la misma casilla mide distinto según dónde esté, y este
juego se decide en distancias de casilla que el jugador tiene que poder estimar a ojo: alcance de entrada,
radio de intercepción, correa. Una cámara que miente sobre la distancia rompe la previsibilidad de RF-012d.

**El campo entero, siempre.** No hay desplazamiento ni zoom. El partido se juzga leyendo la forma del
equipo, y eso exige verlo completo.

**Por qué 60° y no 45°.** Lo que la cámara dibuja mide `16 / (filas × sen elevación)`. Con 5 filas a 45°
daba 4,52:1 contra un marco de 3,20:1 y el campo flotaba en el hueco; con 6 filas a 60° da 3,08:1 y lo
llena. Ésa fue la razón de la ADR 0103.

---

## 2. El intercambio del ángulo: suelo contra cuerpo

Es la restricción más útil de todo el documento, y es permanente — vale igual con cápsulas que con modelos:

- Lo que está **tumbado en el suelo** (dorsales, anillos, líneas, casillas) se comprime por el **seno** de
  la elevación. Subir el ángulo lo **mejora**: a 30° un dorsal se aplasta al 50 %, a 60° al 87 %.
- Lo que está **de pie** (el cuerpo, y por tanto la silueta) se comprime por el **coseno**. Subir el ángulo
  lo **empeora**.

**Van en sentidos opuestos y no hay ángulo que gane en las dos cosas.** Hay que elegir qué carga con la
identidad. Si los modelos la llevan bien en la silueta —barba, cuernos, costillas, melena—, se puede subir
el ángulo y ganar lectura de campo. Si no, hay que bajarlo y perder casilla.

60° es la apuesta actual: que **la identidad la lleve el modelo** y el ángulo se dedique a que el campo se
lea. Es revisable con un modelo de verdad delante, y la elevación es un parámetro exportado justamente para
eso.

---

## 3. La sombra es estructural, no decorado

RA-008 pedía «sombra elíptica en el suelo obligatoria». Medido con cápsulas: **es lo que hace funcionar el
encuadre entero**.

En tres cuartos, dos cuerpos en filas contiguas se solapan en pantalla. Lo que impide que eso se lea como
una maraña no es el ángulo: es que cada cuerpo **proyecta al suelo** y esa sombra dice en qué casilla está.
Sin sombra, la profundidad desaparece y con ella el motivo de haber ido a 3D.

Queda abierto **si es sombra real o calcomanía**. La calcomanía tiene una ventaja concreta: bajo el
renderizador de compatibilidad, una cápsula con sombra real se auto-sombrea con un escalón en el ecuador, y
la calcomanía no tiene ese problema en ningún renderizador.

---

## 4. Un jugador: cuerpo, anillo y dorsal

**El cuerpo es el volumen que simula el motor.** El radio sale de `bodyRadius` de `data/races/` —el mismo
dato que consume `BodySeparation` (ADR 0020)— y la altura de la proporción de RA-002. No es un detalle de
purista: significa que lo que se ve **no puede mentir** sobre quién bloquea a quién.

| raza | RA-002 | radio | altura |
|---|---|---|---|
| Enano | 13×16 | 0,30 | 0,74 |
| Orco | 16×18 | 0,38 | 0,86 |
| No-muerto | 11×17 | 0,28 | 0,87 |
| Humano | 12×17 | 0,32 | 0,91 |
| Elfo | 11×20 | 0,30 | 1,09 |

**El anillo del suelo tiene el radio del cuerpo**, así que es a la vez indicador de dorsal y **huella real**:
de un vistazo se ve cuánto ocupa un orco frente a un no-muerto.

**El dorsal devuelve la identidad.** Sin él, veinte cápsulas son anónimas y el log nombra a gente que no se
puede encontrar. Con él, el «Torka Muerdehierro · dorsal 4» del log se sigue por el campo.

### El compromiso que hay que revisar con modelos

Anillo y dorsal se dibujan **sin prueba de profundidad**. Con cápsulas no había alternativa: el cuerpo mide
exactamente el radio del anillo, así que desde tres cuartos lo tapaba entero salvo una franja de 8,7 px y el
dorsal habría sido ilegible siempre. El precio es que el anillo de quien está detrás se pinta sobre el
cuerpo de quien está delante, lo que contradice justamente la profundidad del punto 3.

**Con un modelo real —más estrecho que un cilindro de radio completo, sobre todo por las piernas— la prueba
de profundidad vuelve a activarse.** Si aun así el dorsal queda tapado, la salida es **levantarlo** (sobre
la cabeza, o en el pecho), no volver a apagar la profundidad.

---

## 5. La prueba de silueta, y qué contestó de verdad

RA-002 exige que toda raza sea reconocible **en blanco y negro**. Con cápsulas negras planas sobre blanco:
**se distinguen tres de cinco.**

- **Elfo**: inconfundible, la única alta y estrecha.
- **Orco**: inconfundible, 10 px más ancho que cualquier otro.
- **Enano**: a medias; es el más bajo, pero contra el orco solo se separa por ancho.
- **Humano y no-muerto**: **la misma mancha** (5 px de ancho y 4 de alto de diferencia).

**Lo que esto NO significa** es que haya que exagerar la tabla de RA-002. Una cápsula tiene exactamente dos
grados de libertad —ancho y proporción— porque remata en una semiesfera del mismo radio. Justo lo que RA-002
usa para separar razas es lo que la cápsula no puede llevar: *barba que ocupa medio cuerpo, costillas y
cuenca vacía, melena, hombros y colmillos*. Ensanchar los números para que cinco píldoras se distingan sería
**sobreajustar al placeholder** y llegar a los modelos con proporciones exageradas que ya no hacían falta.

**Lo que sí hay que llevarse al briefing:** los tres cuartos **comprimen la proporción entre un 10 y un
15 %**, y eso seguirá siendo cierto con modelos. La tabla de RA-002 se escribió pensando en una lectura
plana y es optimista. **Humano y no-muerto tienen que separarse por rasgo firma, no por proporción**, y hay
que comprobarlo en el primer modelo, no en el último.

---

## 6. Lo que la vista 3D todavía le debe a la 2D

La 2D sigue siendo la vista de arranque, y con razón: **hoy se lee mejor**. La 3D no gana el puesto hasta
que lleve todo esto, que es la capa de información que el criterio de salida necesita:

| capa | 2D | 3D |
|---|---|---|
| dorsal | sí | **sí** (anillo) |
| color de equipo | sí | **sí** |
| estado (persigue, regatea, pasa, tira, entra, carga, celebra) | anillo de color | **no** |
| derribado / lesionado / expulsado | anillo de color | **sí**, se tumba |
| correa del elegido | sí | **no** |
| marcaje (a quién marca, quién va a por él) | líneas | **no** |
| selección y seguimiento de una ficha | sí | **no** |
| zona de acción | sí | **no** |

Mientras esa columna tenga «no», la leyenda de catorce entradas que hay sobre el campo describe la 2D y
**miente sobre la 3D**. Es lo primero que hay que arreglar al portar.

---

## 7. Sangre, estadio y lo que da identidad

- **La sangre es una calcomanía persistente** durante el partido y se limpia entre partidos (RA-027): es
  feedback y es historia del partido. En Godot es un nodo `Decal`, así que el coste es bajo y el efecto
  alto. Es probablemente **lo más barato que más identidad da**.
- El lenguaje visual es **cultura futbolística real cruzada con humor negro** (RA-025): marcadores de
  estadio, vallas publicitarias, pancartas de ultras. RA-026 avisa de lo que hay que **evitar**: calaveras,
  huesos y marcos góticos, que remiten a Blood Bowl y a un lenguaje ajeno. **El gore es de estadio, no de
  mazmorra.**
- Los **highlights** (RA-020..022) son estilo cómic, **no pixelart y no 3D**: son un encargo aparte, no les
  afecta la ADR 0102, y son lo que más acerca el criterio de salida por euro gastado. Un panel ilustrado
  cuando alguien muere es exactamente «leer el partido sin el log».

---

## 8. Huecos conocidos

1. **La leyenda describe la 2D** y sigue en pantalla en modo 3D (punto 6).
2. **Anillo y dorsal sin prueba de profundidad** (punto 4), a revisar con el primer modelo.
3. **Auto-sombra en el ecuador de la cápsula** bajo el renderizador de compatibilidad, que es el de las
   capturas por Xvfb. En Forward+ —el de la build de Windows— no se ve, y el toon lo sustituye.
4. **Humano y no-muerto no se separan** por proporción (punto 5).
5. **La secuencia de capturas se rompe después de `informe`.** Tras `captura: informe.png` sale
   `ERROR: Parameter "data.tree" is null` y la ejecución muere, así que **`recompensa.png` y `mercado.png`
   nunca se regeneran** y las que hay en el repositorio están viejas. Las nueve del partido sí salen, que
   son las que van antes. Es anterior al trabajo de hoy y explica por qué varias ejecuciones parecían
   «terminar bien» sin haber hecho lo que se esperaba.
6. **Nadie elige `MarkOpponent` en el partido de las capturas.** La propia secuencia lo avisa
   (`WARNING: ningún jugador elige MarkOpponent en todo el partido de las capturas`), de modo que
   `partido-marcaje.png` enseña la capa de marcaje **vacía**. Puede ser de ese partido concreto o puede
   ser que la acción esté muerta; hay que comprobarlo sobre un lote, no sobre una captura.
7. **La secuencia es lenta y frágil**: con renderizado por software tarda varios minutos y se
   ha cortado antes de llegar a los pasos del partido más de una vez. Hay que darle margen y comprobar la
   marca de tiempo del PNG, no fiarse de que el proceso termine con código 0.

## 9. Cómo se regeneran las capturas

Las de esta pantalla **no salen con `--screenshots`** —ése es el recorrido de la pantalla de Equipo— ni con
`--tour`, que hace inicio, mapa, ojeo y equipo-run. Salen de la escena de capturas:

```bash
dotnet build Game/Underleague.Game.csproj        # OBLIGATORIO: Godot carga este binario, no el de la raíz
godot --headless --path Game --import
xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
  --rendering-driver opengl3 --audio-driver Dummy res://Scenes/Capturas.tscn
```

Tarda **varios minutos** con renderizado por software. **Comprueba la marca de tiempo del PNG** antes de
mirarlo: que el proceso salga con código 0 no significa que haya escrito el fichero.

`partido.png`, `partido-correa.png`, `partido-marcaje.png` para la 2D; `partido-3d.png`,
`partido-3d-silueta.png`, `partido-3d-angulo-30.png`, `partido-3d-angulo-60.png` y `partido-3d-razas.png`
para la 3D. Las dos de silueta son la prueba de RA-002 y **no llevan anillos ni dorsales**: tienen que
seguir siendo cápsulas negras sobre blanco y nada más, o dejan de responder la pregunta que se les hace.
