# Índice de problemas de gameplay

Un fichero por problema (mismo patrón que `docs/decisiones/`), no una tabla plana. Antes de
proponer una hipótesis sobre un síntoma, consulta aquí si ya existe un fichero para él o para
uno de sus hermanos — memoria que no se consulta es memoria inexistente
(`docs/analisis/auditoria-organizacion-v2.md` §3).

Estado epistemológico dentro de cada fichero (Regla F, ver V2/V3 de la auditoría de
organización): una hipótesis puede estar **REJECTED** por un experimento (no vuelve a
probarse sin evidencia nueva), **LIKELY** (consistente con lo medido pero sin experimento
propio que la aísle), o **CONFIRMED** (reproducida). Una hipótesis rejected bajo un sistema
puede reabrirse si ese sistema cambia — se anota "descartada bajo la ADR X", nunca "falsa"
a secas.

**Dos tablas**: arriba lo que sigue abierto —lo que hay que leer antes de hipotetizar sobre un
síntoma—, abajo lo ya cerrado, que se conserva porque las hipótesis descartadas de un problema
cerrado son la memoria que evita volver a probarlas. Una fila baja solo cuando no queda nada
por hacer en su fichero: si se resolvió una parte y queda otra, se queda arriba con la parte
viva anotada.

## Abiertas

| Id | Título | Estado |
|---|---|---|
| [BA-E](./BA-E.md) | Goles sin ángulo | Abierta, medida (A RECHAZADA por la ADR 0111, C disponible). **Vía nueva 23 sep**: la acción «centrar» del §7 de `plan-altura-del-balon.md` le da al delantero la alternativa que a la vía B le faltaba |
| [BA-F](./BA-F.md) | El 3D está mal | Abierta: decidir si se acepta perspectiva contra la ortográfica de la ADR 0102 |
| [BA-G](./BA-G.md) | Los nombres de los jugadores se repiten | Abierta: el sorteo de `data/*/names` no tiene memoria dentro de la run |
| [BA-H](./BA-H.md) | Los consumibles no se pueden usar | Abierta: CAT-B dio el equipado; falta el uso en vivo (RF-082, `ManualActivation`) |
| [BA-J](./BA-J.md) | Tras una parada, el equipo defensor debería replegarse | Abierta: hoy no hay fase de repliegue tras `SAVE` |
| [BA-L2](./BA-L2.md) | `CaptureRunner` pierde el árbol de escena entre `informe` y `recompensa` | Resuelta la secuencia; queda abierto `recompensa.png` en blanco |
| [BB-D](./BB-D.md) | Parar unos segundos en los eventos que detienen el juego | Analizada (`game-design-review`, 16 sep); pendiente de decidir si se implementa — se resuelve entera en `/Game`, sin tocar RT-020 |
| [BB-G2](./BB-G2.md) | El portero puede ser perseguidor designado fuera de su área y bloquear a los diez | Abierta, sin evidencia de activación |
| [BB-H](./BB-H.md) | «Todavía no hay eventos, habrá que diseñarlos» | Abierta: existen seis cartas, pero 0,38 resueltas por run — es densidad y catálogo, no ausencia |
| [BB-I](./BB-I.md) | «Depredador de área» pareció activarse en un momento que no era un tiro | BLOCKED / NEEDS-REPRODUCTION: falta la semilla |
| [BB-J](./BB-J.md) | «Mentalidad de manada» no sirve jugando con enanos | Previsibilidad resuelta (tooltip); diseño abierto |
| [BB-K](./BB-K.md) | Dos jugadores del mismo equipo que quieren la misma casilla «bailan y parpadean» | Abierta, **causa CONFIRMED**: `CoverSpace` es la única acción de colocación que no mira a los compañeros; nota de diseño hecha, arreglo pendiente de medir |
| [BB-N](./BB-N.md) | El saque de córner no ocurre nunca (1 en 2000 partidos) | Causa CONFIRMED y **decidido: se modela** ([ADR 0135](../decisiones/0135-el-balon-tiene-altura.md), el balón gana altura y el toque defensivo puede desviarlo). Se cierra con el paso 4 del plan |
| [BB-P](./BB-P.md) | Las puertas de un solo partido/semilla se leen como causa cuando son ruido | Abierta |
| [BB-R](./BB-R.md) | Dos perks dicen ser MAESTROS y no exigen ni cierran nada (ADR 0051 al 0 %) | Abierta |
| [BB-S](./BB-S.md) | `build-neutral-reference.py` dice que escribe las referencias y solo imprime | Abierta |
| [BB-T](./BB-T.md) | La puerta de equipamiento cae a 0,5: era RUIDO de una semilla, no el catálogo | Abierta (arreglo de instrumento) |
| [BB-U](./BB-U.md) | Un solo perk común rompe el invariante de cero de la puerta de economía | Abierta, sin causa |
| [BC-A](./BC-A.md) | El goleador sigue en campo rival cuando el otro equipo saca de centro | Abierta (causa CONFIRMED; rediseño de BB-C) |
| [BC-C](./BC-C.md) | «Doble disparo» no tiene sentido y no cambia el resultado | Abierta (rediseño) |
| [BC-D](./BC-D.md) | «Último hombre» se activa pero no hace nada | Abierta (mecánica nueva + ADR) |
| [BC-G](./BC-G.md) | El balón se queda suelto en el córner y nadie lo coge | Abierta (reabre BB-G en parte) |
| [BC-H](./BC-H.md) | El aviso de alineación incompleta es falso: el once se rellena solo | Resuelta la mitad de dentro (ADR 0134); abierta RF-002d antes del partido y la captura que lo regresione |
| [BD-A](./BD-A.md) | El reparto de los penaltis por fila: el sesgo arriba/abajo era ruido; queda la concentración en las filas centrales | Abierta (reducida) |
| [BE-B](./BE-B.md) | Un perk `injure` con `target: "actor"` acreditaría lesiones al compañero o a la propia víctima | Resuelta la atribución (23 sep 2026: los dos contadores comparan equipo, con tests). **Viva**: `ResolveInjury` sigue haciendo `ShiftBiasAgainst` sin comparar equipos, así que lesionar a un compañero mueve el criterio del árbitro en tu contra — eso cambia el partido y pide medición propia |
| [BE-C](./BE-C.md) | `MatchResolution` trata el partido completo en un sitio y solo hasta la derrota en otro | Resuelto el síntoma 2 (`PlayedTicks` compara equipo). **Viva y reabierta**: la ventana posterior a `defeatTick` **SÍ existe** —`CanStart` deja salir al lesionado grave marcado y `IsAvailable` no lo cuenta—, contra lo que se llegó a escribir. Rara y de gravedad baja, sin decidir si se arregla |
| [BE-F](./BE-F.md) | `NodeKinds.IsMatch` incluye `Boss`: el nodo de jefe guarda un `opponentId` fantasma | Resuelta la lectura en `/Sim` (`IsCatalogRivalMatch`, cuatro consumidores). **Viva**: el nodo sigue **guardando** el id fantasma, y quitarlo obliga a mover el cursor de `MapGenerator` (regeneraría todos los mapas). Ver [BH-B](./BH-B.md) |
| [BH-B](./BH-B.md) | El nodo de jefe se presenta con el nombre de un clan de liga, en el mapa y en el ojeo | Abierta, **CONFIRMED** (9 de 9 semillas). Es `/Game` leyendo el `opponentId` fantasma de [BE-F](./BE-F.md); pide `visual-review` |
| [BH-C](./BH-C.md) | El recorte al marco amortigua en silencio toda mecánica de puntería: un tiro cuya mira cruda se va tres semianchos se recorta al poste y sigue contando como tiro a puerta | Abierta, **CONFIRMED**. Estructural, no de la apertura: sale de la revisión del paso 3a (ADR 0135). **Conviene resolverla antes del paso 3c**, que concentra masa justo en la línea del poste |
| [BE-E](./BE-E.md) | Un jugador legendario no puede aparecer nunca | Reducida (23 sep 2026): diseño resuelto (ADR 0128) y dato documentado. **Queda** el techo de 5 slots de perk, inalcanzable mientras la ADR 0128 siga bloqueada |
| [BF-A](./BF-A.md) | `elf_brawler`, una build mala a propósito, gana el 46,6 % contra su referencia (techo 45): el único rojo verdadero de las 43 puertas tras la ADR 0131 | Abierta, medida en 8 plantillas |
| [BF-B](./BF-B.md) | Tres puertas que deciden con una semilla o sin margen (rareza, `orc_misplaced`, doctrinas): el patrón que la ADR 0131 arregló en las de build | Abierta, medida |
| [BF-C](./BF-C.md) | El delantero pega sin balón porque sus alternativas fuera de posesión son peores que pegar (`Tackle` 211 contra `MarkOpponent` 180) | Abierta, medida |
| [BG-A](./BG-A.md) | Tres sitios más tratan la alineación guardada como si fuera el once que juega; el peor reparte experiencia de carta | Abierta: la ADR 0134 la deja fuera a propósito porque mueve balance |
| [BG-B](./BG-B.md) | La colocación del jugador se tiraba al construir el partido y el indicador de riesgo no se enteraba: mover fichas movía el número, no el partido | Arreglada (ADR 0134); abierto medir las formaciones que ahora sí son posibles, y el jefe que empuja casillas |
| [BG-C](./BG-C.md) | Tres contratos que solo se sostenían porque nadie podía ejercerlos: `PlayOn` huérfano en silencio, `SetLineup` sin validar casillas, dos APIs que pasan a lanzar | Abierta, sin evidencia de activación |
| [BH-A](./BH-A.md) | El motor no tiene ninguna defensa contra el silencio: un partido puede congelarse 49 s y nadie se entera | Abierta, de primitiva; nace de la revisión de BB-O. La opción barata es una puerta de test, sin tocar `/Sim` |
| [CAT-C](./CAT-C.md) | La medición no puede ejercitar el consumible manual, así que subestima a la familia | Abierta: las cifras de la ADR 0101 son una cota inferior |
| [CAT-E](./CAT-E.md) | Con seis filas, todas las builds ganan más al equipo sin perks, y dos puertas de fase 1 se ponen rojas | Abierta: la ADR 0104 NO la cerró (la referencia no era la variable); decisión del revisor pendiente entre tres salidas |
| [CAT-F](./CAT-F.md) | `Block` (la carga sin balón, ADR 0030 §2) no se puede despertar subiéndole el peso | Abierta, con diagnóstico: el peso no es la palanca, se descarta el 74,9 % antes de puntuar |
| [CAT-H](./CAT-H.md) | ¿Compraría alguien un objeto maldito? | Abierta: la tabla de valor de la ADR 0038 es global y no expresa el canje de RF-077 |
| [CAT-I](./CAT-I.md) | La política automática casi nunca vende, así que las reglas de venta no se ejercitan | Abierta; no bloquea |
| [CAT-J](./CAT-J.md) | Tres métricas de `BuildGateTests` fuera de rango: no es ruido de semilla | Abierta |

## Cerradas

| Id | Título | Estado |
|---|---|---|
| [BA-A](./BA-A.md) | BLOQUEO: un nodo de evento sin opciones y sin salida | Cerrada |
| [BA-B](./BA-B.md) | BLOQUEO: en un jefe no dejó sustituir al lesionarse el segundo jugador | Cerrada |
| [BA-C](./BA-C.md) | Con seis filas no hay fila central y la alineación queda descentrada | Cerrada |
| [BA-D](./BA-D.md) | Los jugadores se teletransportan al reanudar una falta | Cerrada |
| [BA-I](./BA-I.md) | La sustitución por lesión no debería ser obligatoria | **CERRADA (ADR 0134 E)**: la lesión leve ya no saca del campo, y que quedarse suba el riesgo de morir es la decisión — se ve antes de elegir |
| [BA-K](./BA-K.md) | Cortinilla o transición | Resuelta (16 sep 2026), verificada con captura |
| [BA-L](./BA-L.md) | La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta máquina | Cerrada |
| [BA-M](./BA-M.md) | Origen de BA-N: la puerta de equipar pasó de roja a verde por cero centésimas | Cerrada |
| [BA-N](./BA-N.md) | Equipar ya no vale el escalón que la ADR 0033 exige | Cerrada (ADR 0116; deuda documentada) |
| [BB-A](./BB-A.md) | Los jugadores se teletransportan al reanudar | Resuelta (dos causas: BB-C en `/Sim`, BA-K en `/Game`) |
| [BB-B](./BB-B.md) | En el saque de centro los defensores van a robar el balón antes de que esté en juego | Resuelta (ADR 0115), tras dos intentos rechazados |
| [BB-C](./BB-C.md) | «Celebra» se activa cuando ya han vuelto a su campo | Resuelta (la causa era `ResetPositions`, no la celebración) |
| [BB-E](./BB-E.md) | Salen dos canteranos en el mercado; debería salir uno como mucho | Cerrada |
| [BB-F](./BB-F.md) | Un jugador se lesiona y no se ve en qué posición jugaba | **CERRADA (ADR 0134)**: la queja literal ya estaba resuelta en la pantalla que se juega (el análisis medía la de depuración); faltaba el riesgo y las otras dos respuestas |
| [BB-G](./BB-G.md) | El balón se queda parado en el campo | Resuelta (ADR 0117); la parte que queda viva se sigue en [BC-G](./BC-G.md) |
| [BB-L](./BB-L.md) | El lesionado «sale volando» del campo | Resuelta (con BA-K) |
| [BB-M](./BB-M.md) | «Sed de médula» lesionó a un jugador lejos de la acción | Resuelta; causa CONFIRMED, sin cambio de regla pendiente |
| [BB-O](./BB-O.md) | Un jugador fuera del campo puede conservar el balón y congelar el partido | **CERRADA (23 sep 2026)**: `TakeRestart` daba el saque a un jugador retirado durante la cuenta atrás. Reproducido tick a tick (semilla 144, árbol del 16 sep) y verificado: 2 episodios → 0, e inerte en HEAD |
| [BB-Q](./BB-Q.md) | «Arrollador» no se activa nunca (0/480 partidos): tres causas encadenadas | Resuelta (Alt 0: RECOVERY + `detail()`); exposición 0,0 % → 12,5 % |
| [BC-B](./BC-B.md) | El límite de usos de un perk no se respeta cuando su efecto vuelve a dispararlo | Resuelta (19 sep 2026) |
| [BC-E](./BC-E.md) | Re-simular con la sustitución elegida falla en el 9,4 % de los casos | Resuelta (19 sep 2026) |
| [BC-F](./BC-F.md) | Al sustituir, el partido re-simulado reasigna los dorsales | Resuelta (19 sep 2026) |
| [BE-D](./BE-D.md) | RF-125 será subcontable si se suma `Career` de la plantilla | **RESUELTA de diseño (23 sep 2026)**: lo dicta RF-125b — el contador es del club y vive en la run. A implementar con RF-125 |
| [BE-A](./BE-A.md) | El centrocampista nunca entra a su marcado sin balón: el comentario dice «Defensa y centrocampista» y el código dice `Defender` | **CERRADA (23 sep 2026, ADR 0133)**: entra, con el defensa por delante en las cinco plantillas medidas. Lo que queda es el delantero, en [BF-C](./BF-C.md) |
| [CAT-A](./CAT-A.md) | `field_bandage` usaba el canal `injure`, que protegía al rival | Cerrada (13 sep 2026): `injure` → `injury`. Dejó abierto CAT-B |
| [CAT-B](./CAT-B.md) | Un consumible se puede comprar pero no se puede equipar: nadie emite `SetConsumables` | Cerrada (ADR 0101). Dejó abierto CAT-C |
| [CAT-D](./CAT-D.md) | ¿Una fila más de campo (16×5 → 16×6), con las DOS filas centrales como «centro»? | Cerrada (ADR 0103): entra la fila. Dejó abierto CAT-E |
| [CAT-G](./CAT-G.md) | El equipo de la referencia neutra es una palanca que mueve tres métricas en direcciones opuestas | Cerrada (ADR 0107): los siete objetos más planos, desviación cero |
| [BI-A](./BI-A.md) | Lo que la revisión independiente dejó abierto en el primer paquete de audio | **Abierta** (23 sep 2026): cuatro defectos arreglados en el propio paquete (bolsa duplicada, crujido doble en la sustitución, lesión+muerte del mismo tick, la grada celebrando el gol del rival); quedan el hermano del desfase en la cámara, la traza de un partido entero a ×1 y los momentos que se descartan en silencio |
| [BI-B](./BI-B.md) | El balón se ve raso siempre: la altura se calculaba, se guardaba y no la leía nadie | **CERRADA en la vista 3D** (23 sep 2026): `BallHeightAt` tenía cero consumidores. Reportada jugando, causa aislada con un grep. La vista 2D de depuración se queda fuera a propósito |
| [BI-C](./BI-C.md) | Las animaciones se llevaban al modelo fuera de su anillo (root motion) | **CERRADA** (23 sep 2026): los clips traían el desplazamiento horneado en el hueso raíz —corriendo se salía 1,53 casillas, el trompicón 2,14—. Se fija el horizontal al cargar y se conserva el vertical |
