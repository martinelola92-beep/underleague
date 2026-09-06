# 0020. Cuerpos con volumen, separación blanda y empuje

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor; implementación pendiente, ver `docs/rediseno-espacial.md`)
**Requisitos:** RF-033, RF-040..045, RF-057, RA-002, RT-015, RT-020, RT-051

## Contexto

Hasta la fase 1 los jugadores son **puntos sin volumen**: catorce pueden ocupar la misma coordenada. Las únicas magnitudes espaciales son radios de interacción sueltos y descoordinados (recogida 0,5 · duelo de regate 0,8 · intercepción 0,9 · presión y entrada 1,0).

Las mediciones de la fase 1 mostraron el coste de esa simplificación: el motor **solo modela la mitad mala de concentrarse**. Apiñar el equipo deja el campo descubierto (coste real y bien simulado) pero no produce nada de lo que la concentración da en un deporte de contacto: ni masa, ni bloqueo, ni estorbo, ni superioridad local.

Cuantificado con plantillas emparejadas (`docs/balance/fase1-perks.md`), el coste de la formación es dominante y de un orden de magnitud superior al beneficio: la alineación apiñada de `elf_tiki_taka` cuesta **−23,8 puntos** de tasa de victoria y la de `orc_violence` **−16,1**, mientras que el mayor bono de adyacencia del catálogo vale **menos de 3**. El cambio mínimo que hace adyacentes a dos centrocampistas con radio 1 cuesta ya **16 puntos** por sí solo.

Consecuencias adicionales: las builds de bloque no pueden existir (su ventaja es física); **RF-033 queda vacío** (las razas grandes ocupan 2 casillas al colocar, o sea puro coste, sin presencia a cambio); y la **velocidad es un atributo casi muerto** (+0,3 puntos por cada +10 en humanos, −1,2 en orcos, frente a +4,1 de la técnica), precisamente porque sin cuerpos no hay a quién ganar la posición ni espacio que ocupar antes que otro.

## Decisión

Modelo **B + C ligero**:

1. **`bodyRadius` por raza** en `data/races/*.json` (orden de magnitud: enano 0,30 · humano 0,32 · elfo 0,30 · orco 0,38 · demonio 0,55). Un solo dato que sirve a tres cosas: separación entre cuerpos, escalado de los radios de interacción, y expresión física de RF-033.
2. **Separación blanda**: dos cuerpos que se solapan se empujan. No hay exclusión dura ni pathfinding.
3. **El empuje es una mecánica, no física**: el desplazamiento se reparte según fuerza y tamaño, no al 50%. Un orco abre hueco; un elfo sale despedido. Se aplica también al resolver una entrada (`TACKLE`).
4. **El balón no tiene colisión**: sigue siendo un punto, interceptable por radio.

## Alternativas descartadas

- **Puntos sin volumen** (statu quo): concentrarse solo penaliza; sin bloqueo ni masa; RF-033 inerte; los sprites se apilan, lo que compromete la legibilidad a x4 (RF-050b) y la diferenciación por silueta (RA-002).
- **Exclusión dura, una entidad por casilla**: exige pathfinding, produce atascos y jugadores encajados, la resolución de contactos es frágil para el determinismo, y **cambia el género** hacia Blood Bowl por turnos, en contra de RF-050 y RT-015.
- Motor de físicas de Godot: prohibido por RT-015 y por RT-011.

## Consecuencias

- **Determinismo**: la separación se resuelve en dos fases (estilo Jacobi): se acumulan todos los desplazamientos en un buffer y se aplican al final del tick. Aplicarlos sobre la marcha haría que el orden del bucle cambiara el resultado, que es exactamente el sesgo por id que hubo que corregir en la fase 0.
- **Balance**: más contacto significa más entradas, más lesiones y menos intercepciones limpias. Hay que revalidar RT-056 y rehacer el ajuste. Se espera además que la velocidad recupere valor: es el atributo que más debería ganar con cuerpos y con la búsqueda de espacio de la ADR 0022, y su valor marginal es la métrica que lo comprobará. Se agrupa con las decisiones 0021 y 0022 para reajustar una sola vez.
- **Rendimiento**: 91 pares por tick × ~1.350 ticks ≈ 123.000 comprobaciones por partido. Estimación previa a la medida: de ~520 a ~380 partidos/s, muy por encima de los 167 que exige RT-051. Se mide, no se supone.
- Las razas grandes pasan a tener identidad mecánica: ocupan espacio de verdad.

**Nota de la ADR 0061 (paquete AM):** la predicción se cumplió y la cifra de esta ADR —"+0,3 puntos por
cada +10 en humanos, −1,2 en orcos"— **está obsoleta y no debe citarse**. Con cuerpos y `FindSpace` la
velocidad pasó a +6,6 puntos (D-25), y medido de nuevo en la fase 2 partiendo el peso de los atributos canal
a canal, **es el canal más caro de los seis**: partir por la mitad el peso de la velocidad en el movimiento
cuesta 3,00 puntos de tasa de victoria del acto 2, por delante del remate (2,15) y del pase (1,18)
(`fase2-diseno.md` §29.5).
