# 0020. Cuerpos con volumen, separación blanda y empuje

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor; implementación pendiente, ver `docs/rediseno-espacial.md`)
**Requisitos:** RF-033, RF-040..045, RF-057, RA-002, RT-015, RT-020, RT-051

## Contexto

Hasta la fase 1 los jugadores son **puntos sin volumen**: catorce pueden ocupar la misma coordenada. Las únicas magnitudes espaciales son radios de interacción sueltos y descoordinados (recogida 0,5 · duelo de regate 0,8 · intercepción 0,9 · presión y entrada 1,0).

Las mediciones de la fase 1 mostraron el coste de esa simplificación: el motor **solo modela la mitad mala de concentrarse**. Apiñar el equipo deja el campo descubierto (coste real y bien simulado) pero no produce nada de lo que la concentración da en un deporte de contacto: ni masa, ni bloqueo, ni estorbo, ni superioridad local. `elf_tiki_taka`, que apiña para conseguir adyacencias, gana el 12% de sus partidos contra elfos sin perks; `human_scattered`, diseñada como mala, tiene la formación más ancha y gana el 59%.

Consecuencias adicionales: las builds de bloque no pueden existir (su ventaja es física); **RF-033 queda vacío** (las razas grandes ocupan 2 casillas al colocar, o sea puro coste, sin presencia a cambio); y el eje fuerza/técnica se desequilibra porque la técnica actúa siempre y la fuerza solo cuando el contacto ocurre por casualidad geométrica.

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
- **Balance**: más contacto significa más entradas, más lesiones y menos intercepciones limpias. Hay que revalidar RT-056 y rehacer el ajuste. Se agrupa con las decisiones 0021 y 0022 para reajustar una sola vez.
- **Rendimiento**: 91 pares por tick × ~1.350 ticks ≈ 123.000 comprobaciones por partido. Estimación previa a la medida: de ~520 a ~380 partidos/s, muy por encima de los 167 que exige RT-051. Se mide, no se supone.
- Las razas grandes pasan a tener identidad mecánica: ocupan espacio de verdad.
