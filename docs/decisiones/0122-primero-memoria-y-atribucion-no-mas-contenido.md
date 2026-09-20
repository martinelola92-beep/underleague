# 0122 — Primero memoria y atribución; contenido nuevo, después

Estado: **Aceptada** (20 sep 2026, **decisión del revisor** tras leer
`docs/analisis/auditoria-identidad-generador-de-historias.md` completa). No cambia ninguna regla del juego
por sí misma: fija el **plan de registro** y el **criterio de aceptación** de lo que viene. Las mecánicas
concretas que habilita pasarán cada una por `game-design-review` cuando toque.

## Problema

La auditoría de identidad midió que **la oferta de sucesos no es el cuello de botella**: el 67,8 % de los
partidos tiene ya un suceso excepcional. Los cuellos de botella son **CB-1 (no hay memoria del
jugador-criatura)** y **CB-2 (no hay atribución causal)**.

El riesgo inmediato era actuar sobre el diagnóstico equivocado: añadir perks, eventos o sistemas
narrativos nuevos sobre un conducto que no transmite lo que ya ocurre. La auditoría lo formuló como
corolario —*no añadir sucesos raros nuevos hasta que los existentes se lean*— y el revisor lo adopta como
decisión.

## Decisión

**La identidad operativa de Underleague pasa a enunciarse así:**

> Underleague no necesita más emergencias, ni más sinergias, ni más contenido.
> **Necesita convertir su simulación en memoria.**
> Hoy la cadena es `simulación → resultado`. Falta
> `simulación → acontecimiento → atribución → personaje → memoria`.

**Tres fases, en este orden, y no se empieza una sin la anterior:**

**Fase 1 — Que el juego recuerde y explique.** Las dos piezas van juntas porque responden a la misma
pregunta desde dos lados:
- **1A · Historial individual persistente.** Partidos, goles, asistencias, entradas, lesiones causadas,
  muertes causadas. No añade contenido: convierte un nombre en alguien.
- **1B · Un único perk conductual con `modifyUtility`.** Uno, no cincuenta. Cazagoles, cuyo 24 % ya está
  medido con las siete métricas de RT-056 en verde.

**Fase 2 — Que el partido cuente lo que ya ocurre.** `SAVE`, `TACKLE`, `RECOVERY`, la crónica de
`MatchLogView`, el cartel de perk diciendo qué hizo, los dorsales estables, y el foco sobre lo importante.

**Fase 3 — Turba, prótesis, nemesis.** Deliberadamente al final: son los sucesos extraordinarios, y
primero se arregla el conducto que tiene que contarlos.

**El criterio de aceptación de la Fase 1 es conductual, no de balance.** Las dos preguntas que deciden si
la fase ha funcionado son del revisor, jugando:

1. *«¿Ahora puedo reconocer en el campo qué tipo de jugador es?»* — **no** «¿sube su tasa de victoria?».
2. *«¿Me estoy empezando a acordar de mis jugadores?»*

Si la segunda pasa de *no* a *sí*, el núcleo del juego está encontrado.

**Y una restricción explícita: no se toca el catálogo de 102 perks.** Ni para reducirlo ni para rehacerlo.
La auditoría midió 30 clase A y 28 clase B; el problema no es que falten sesenta perks, es **cómo pueden
afectar al comportamiento y cómo se comunica ese comportamiento**.

## Consecuencias

- **Reordena la hoja de ruta de la auditoría (§19).** La Tanda I y la Tanda III de aquel documento se
  fusionan en esta Fase 1 y esta Fase 2; la Tanda V (turba) se confirma al final pese a su alto potencial,
  exactamente por el motivo que la propia auditoría daba: hoy es una etiqueta, y meter una locura en un
  conducto que no transmite es gastar el presupuesto de balance sin recoger la historia.
- **La Tanda II de la auditoría (cerrar la divergencia declarado/implementado, CB-4) no aparece en el plan
  del revisor.** Se reparte así, y queda anotado para que nadie lo dé por olvidado: los arreglos de **texto
  o dato** (`numb`/RF-104, `gathering_thirst`, `shadow`, `no_dying`) entran en la **Fase 2**, porque son el
  mismo trabajo —que lo que el jugador lee sea verdad—. `kamikaze`/`iron_price` **quedan bloqueados** a la
  pregunta 4 de §20, que sigue sin respuesta y que toca PD-1.
- **Las cinco preguntas abiertas de §20 siguen abiertas y ninguna bloquea la Fase 1.** La primera —si el
  desgaste es recurso de run o de acto— bloquea la Fase 3, no antes.
- **RF-122 (obituario) deja de ser imposible** en cuanto cierre la Fase 1A, que es su prerrequisito real.

## Lo que esta ADR NO decide

- Qué campos exactos se persisten y cómo (eso es `docs/plan-fase-memoria-y-atribucion.md`, y su frontera
  pasa por `architecture-review`).
- Si `kamikaze` debe penalizar de verdad (§20, pregunta 4).
- El valor de producción de `modifyUtility` para Cazagoles: 24 % es lo medido en el piloto, no un valor
  cerrado (`c1-piloto-cazagoles-diseno.md` §6-§7).

## Hermanos

- `docs/analisis/auditoria-identidad-generador-de-historias.md` — el diagnóstico que la motiva.
- `docs/plan-fase-memoria-y-atribucion.md` — el plan de ejecución de la Fase 1.
- `docs/analisis/c1-piloto-cazagoles-diseno.md` — C1 medido y sin cerrar.
- `docs/analisis/decision-vinculos.md` — la recomendación de retirar RF-100..106, que la Fase 3 hereda.
