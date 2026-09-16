---
name: game-design-review
description: Evaluar una mecánica de juego nueva o modificada (perk, rasgo, evento, regla de economía, primitiva de motor) antes de implementarla — fantasía, decisión del jugador, coste de oportunidad, interacción con sistemas existentes, degeneración posible. Usar SIEMPRE que se cree o modifique una mecánica; si hay duda de si algo cuenta como mecánica o como dato/balance, invocar de todos modos.
---

# Revisar el diseño de una mecánica antes de implementarla

Existe porque `CLAUDE.md` diagnosticó que confiar en que el criterio "viva en la cabeza" no basta: el
mismo agente que sabe la regla puede saltársela al creer que ya tiene la explicación o el diseño correcto.
Esta skill es el procedimiento, no un recordatorio.

**Regla de precedencia**: ante la duda entre "esto es dato/balance" y "esto es mecánica/diseño", se ejecuta
esta skill. Cuesta poco tiempo frente al coste de implementar una mecánica mala.

**Las skills se encadenan, no son excluyentes.** Una primitiva nueva típicamente pasa por
`game-design-review` → `architecture-review` → implementación → `balance-measure` (si toca balance) →
`build-and-test`.

## El protocolo — diez preguntas, en orden

1. **¿Qué experimenta el jugador?** No "qué hace el código" — qué ve, qué decide, qué entiende.
2. **¿Qué decisión toma el jugador con esto?** Si la respuesta es "ninguna, es un modificador invisible",
   es una señal de alarma (`comportamiento observable > modificadores numéricos invisibles`).
3. **¿Qué decisión DEBERÍA tomar?** Compara con la respuesta anterior.
4. **¿Qué regla del juego representa?** Cítala si existe en `docs/requisitos.md` (RF-xxx); si no existe,
   dilo explícitamente — no inventes una regla nueva sin decir que lo es.
5. **¿Qué sistemas intervienen?** `/Sim` (qué parte exacta), `/Game` (qué parte exacta), `/data` (qué
   fichero). Si la respuesta reparte lógica entre `/Sim` y `/Game` de forma que puedan contradecirse,
   es un fallo de diseño, no de implementación (RT-014).
6. **¿Hay alternativas?** Al menos una. Si la primera idea es la única considerada, hazlo explícito.
7. **¿Qué trade-off introduce?** Todo lo que da algo quita algo (`identidad memorable > bonus genéricos`).
   Si no hay coste de oportunidad legible, probablemente sea "poder gratis" — mirar el caso de Herencia en
   `docs/decisiones/` como ejemplo de un tope corregido antes de existir el perk, comparándolo contra la
   progresión del juego en vez de a ojo.
8. **¿Cómo cambia las estrategias posibles?** Combinaciones con lo que ya existe, no solo en aislado.
9. **¿Puede degenerar?** ¿Hay una combinación con perks/reglas existentes que rompa una banda o un
   principio (regla 11: nada malo sin ser previsible)?
10. **¿Cómo se demuestra que funciona?** Qué test, qué lote de `/Balance`, qué captura. Si la respuesta es
    "se ve a ojo", no basta para algo que toca `/Sim` o `/data`.

## Cuándo el resultado se escribe, y cuánto

**El gate se aplica al tamaño del cambio, no a cada perk suelto** — ocho puertas por cada uno de los
perks de una tanda sería parálisis (medido: 33 perks en una sesión). El umbral:

- **Primitiva nueva** (`EffectType`, objetivo, campo de estado nuevo), **regla que toca una ADR existente**,
  o **canal de balance nuevo** (oro, atributos, contador): las diez preguntas se responden por escrito,
  3-5 líneas de nota de diseño, **antes** de implementar. Vive junto al encargo o en el ADR si genera uno.
- **Contenido que reutiliza primitivas existentes** (un perk más de datos, un objeto): las preguntas se
  responden mentalmente al escribir el `_doc` del propio dato; no hace falta una nota aparte.

## Qué NO hace

- No decide si una mecánica es "buena" de forma abstracta — obliga a que la decisión esté justificada con
  las diez respuestas, no la sustituye por su propio juicio.
- No sustituye a `balance-measure`: puede concluir que una mecánica es coherente en diseño y aun así
  desequilibrar una métrica; eso se mide aparte.
- No se salta el orden: implementar antes de responder las diez preguntas es exactamente el fallo que la
  skill existe para evitar (el aviso de perk de la ADR 0112 se escribió parcialmente implementado antes de
  documentarse — no lo repitas).
