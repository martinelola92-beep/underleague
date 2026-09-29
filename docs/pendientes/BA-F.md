# BA-F — El 3D está mal.

**Estado: CERRADA (29 sep 2026)** por la [ADR 0174](../decisiones/0174-la-camara-del-partido-es-perspectiva.md)
(decisión del revisor: cámara en perspectiva), **con la lectura en partida real sin validar** (H3, LIKELY): se
reabre si el revisor, jugando, sigue viendo el campo como 2D. Abierta en la primera partida del revisor, antes de
que existiera la retransmisión (ADR 0120).

## Observación

**El 3D está mal.** «No veo a los porteros en pantalla. Sigo viendo el grid como si fuera en 2D.» Referencia pedida:
**cámara estilo FIFA**, con perspectiva perceptible; si hace falta, 2D de fondo para simular movimiento.

## Análisis

La ficha decía «contradice en parte la ADR 0102, que eligió ortográfica: hay que decidir si se acepta perspectiva».
**La perspectiva ya estaba aceptada en la práctica** desde el 19 sep (enmienda de la ADR 0120: elevación 45°, FOV 30°
en la retransmisión) y ningún documento lo recogía: la ADR 0102, `docs/ui-partido.md`, `docs/estilo-visual.md` y esta
ficha seguían diciendo «ortográfica» (Regla G: se buscó por el concepto y no por el identificador).

## Hipótesis

| # | Hipótesis | Estado |
|---|---|---|
| H1 | La retransmisión no tiene perspectiva | **REJECTED**: la tiene desde el 19 sep (`BroadcastScreen.DefaultVariant`, variante D) |
| H2 | El modo depuración (F3, «3D: sí») enseña un encuadre ortográfico de otra época y **corta el campo** | **CONFIRMED**: `OrthoSize` 5,3 se escribió para 16×5 en 1120×350; con 16×7 en 1120×500 enseñaba 11,9 de las 16 columnas y ~6,1 de las 7 filas. Ni porterías ni porteros en pantalla, una rejilla oscura plana. Captura `Game/screenshots/camara/antes-depuracion.jpg`; cuenta en la ADR 0174 |
| H3 | La perspectiva de 45°/30° es demasiado leve para percibirse como 3D | **LIKELY**: el borde lejano medía el 80 % del cercano y ahora el 72 %; se compararon ocho cámaras sobre los mismos fotogramas (ADR 0174) pero **nadie ha jugado una partida con la nueva**. El revisor decidió cambiarla |
| H4 | Los jugadores no proyectan sombra y «flotan» sobre el césped | **CONFIRMED con experimento aislado**, no estaba en la observación: `DirectionalShadowMaxDistance` 18 contado desde la cámara y la cámara a 22,5 dejaban el césped entero (profundidad 20,1-25,0) fuera de alcance (`solo-sombras-gol.jpg`: la cámara de antes con el alcance corregido de entonces, distancia + 9, y la grada más ancha —que no puede sombrear el campo—). Arreglado con el alcance calculado desde la distancia encajada: (distancia + 4,3) / 0,8 |
| H5 | Los porteros no se ven porque los tapa algo del dibujo | **CONFIRMED en parte**: en el saque inicial y en cada acontecimiento nivel 3 y 4 el **pergamino de la escena** (26 % del ancho, a la izquierda, hasta y≈525 de 800) tapa la portería izquierda y a su portero; y **la cámara no lo arregla**: la portería ocupa y 405-546 con la cámara de antes y 405-540 con la nueva, y solo asoma su borde inferior por debajo del pergamino (con 55°/50° habría quedado tapada del todo, 381-522). Es UI del pregón: **decisión C5 abierta** de `docs/ui/README.md` §9 (posición de la N3); no se toca aquí |
| H6 | (de la revisión independiente) Una elevación mayor, que daría más campo, aplasta la altura de las fichas de la fila cercana | **CONFIRMED por geometría** con `tools/camara-encaje.py`: a 55°/50° el alto/ancho de un orco en la fila cercana es 0,40 (la ortográfica de 60° daba 0,56) y la diferencia de alto entre elfo y enano cae de 16 a 9,7 px. Primera elección descartada por eso; ver la ADR |

## Arreglo

ADR 0174: perspectiva con elevación 45° y FOV vertical 45° (definido para 16:10 y conservado en horizontal),
`PitchFit`, sombras, grada y explanada más anchas; más el arnés de barrido `-- camara <elevación>/<FOV> ...`. Modelo
de la geometría: `tools/camara-encaje.py`. Capturas antes y después en `Game/screenshots/camara/`.

## Hermanos

- **El dorsal está tapado por el cuerpo** en las cápsulas de las razas sin modelo, con cualquier cámara: el anillo y el
  número van en el suelo, bajo el cuerpo (ADR 0102 lo preveía). No se cierra aquí. Salida ya anotada en
  `docs/ui-partido.md` §4: levantarlo (sobre la cabeza o al pecho), no apagar la profundidad.
- **C5** (`docs/ui/README.md` §9): la escena de N3/N4 se dibuja a la izquierda del campo y tapa la portería propia.
