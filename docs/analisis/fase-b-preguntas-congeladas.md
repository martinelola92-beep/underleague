# Fase B — preguntas, métricas y predicciones congeladas

**Fecha:** 19 sep 2026. **Estado:** CONGELADO antes de medir nada, incluido el censo.

Misma disciplina que la Fase A (`fase-a-prediccion-congelada.md`): la pregunta que §25 dejó planteada
—¿debe un perk de exposición necesariamente binaria participar en un criterio pensado para detectar
población insuficientemente representada?— solo se puede responder si las métricas y lo que contaría como
cada conclusión están fijadas **antes**. Si no, el resultado de `bulwark_stance` acabaría definiendo
retrospectivamente qué consideramos útil.

**Esta fase es puramente descriptiva.** No modifica el suelo del 50%, ni el circuito del 20%, ni
`PopulationFitness`, ni la predicción congelada de la Fase A, ni `/data`, ni ningún perk. No decide si el
umbral debe cambiar: produce la evidencia con la que esa decisión se podrá tomar después.

## Definiciones exactas (fijadas aquí)

- **Familia `una-vez-por-partido`**: `perk.Trigger ∈ {MATCH_START, MATCH_END}`. El disparador ocurre
  exactamente una vez por partido, sin depender de ninguna acción.
- **Familia `por-jugada`**: `perk.Trigger = PLAY_START`. Ocurre varias veces por partido, tampoco depende
  de una acción del portador. Se separa de la anterior **a propósito**: `PerkAudit.ClassifyTriggerFrequency`
  las agrupa hoy bajo `AlwaysOnce`, y para esta pregunta no son lo mismo.
- **Familia `de-suceso`**: cualquier otro disparador (`TACKLE`, `SHOT`, `FOUL`, `INJURY`, `SAVE`,
  `DRIBBLE_ATTEMPTED`, `RECOVERY`, `GOAL`…).
- **Partido cualificado**: partido del brazo armado en el que el perk registró ≥1 activación. Para un perk
  de `una-vez-por-partido`, equivale a "el portador cumplía la condición".
- **Delta en submuestreo cualificado**: media de la métrica primaria en los partidos armados cualificados
  menos la media en **sus partidos de control emparejados** (mismas plantillas, mismas semillas). Nunca se
  compara contra partidos de control de otras plantillas.
- **Potencia**: `BalancePowerCheck.HasSufficientPower` tal cual, sin cambiar el multiplicador.

## Las tres preguntas y sus predicciones

### Q1 — Censo de familias sobre los 94

**Métrica**: recuento estático de los 94 por familia, derivado solo de `perk.Trigger`. Sin simular nada.

**Predicción congelada**: `una-vez-por-partido` será la familia **más numerosa**, y supondrá **al menos un
tercio** de los 94.

*Falsable*: si `de-suceso` resulta mayoritaria, o si `una-vez-por-partido` queda por debajo de 31 perks,
la predicción falla.

### Q2 — ¿El efecto es medible dentro del submuestreo cualificado?

**Caso**: `bulwark_stance` (`MATCH_START`, condición `hasTag(owner,'Bulwark')`, métrica primaria
`tacklesPerMatch`), medido sobre **Human** (población donde casi nadie cualifica, 5-7,5%) y sobre **Dwarf**
(población donde cualifica ~80%), 120 plantillas en ambos casos.

**Métricas**: nº de partidos cualificados; delta en el submuestreo cualificado; potencia sobre ese
submuestreo.

**Predicción congelada**, en dos partes:

1. **La magnitud del delta dentro del submuestreo cualificado será parecida en Human y en Dwarf** — el
   mecanismo es el mismo y, una vez el portador cualifica, actúa igual. La exposición difiere ~10×; el
   delta condicionado no debería.
2. **La potencia sí diferirá**: sobre Human el submuestreo cualificado será demasiado pequeño para superar
   el power-check; sobre Dwarf, mucho mayor, tendrá bastante más opción de superarlo.

*Falsable*: si el delta condicionado en Human difiere sustancialmente del de Dwarf (más allá del ruido),
la parte 1 falla — y entonces "exposición baja" sí estaría indicando algo sobre el efecto, no solo sobre
la población. Si Human ya alcanza potencia con su submuestreo, falla la parte 2.

### Q3 — ¿Qué aporta realmente el remuestreo ×6 de §5.1?

**Caso**: el mismo `bulwark_stance` sobre Human, con 20 plantillas frente a 120.

**Métricas**: exposición (%); nº absoluto de partidos cualificados; delta condicionado; potencia.

**Predicción congelada**:

1. **La exposición apenas se moverá** (menos de 5 puntos): es una proporción poblacional, y multiplicar
   portadores sorteados no la cambia.
2. **El número absoluto de partidos cualificados crecerá ~6×**.
3. **Por tanto el ×6 sí mejora la medibilidad del efecto** (más casos cualificados ⇒ menor error estándar
   del delta condicionado), **aunque no mejore la exposición** — es decir, el remedio funciona, pero no por
   el motivo que el protocolo le atribuye.

*Falsable*: si la exposición se mueve mucho, falla (1). Si los cualificados no escalan, falla (2). Si el
error estándar del delta condicionado no baja, falla (3).

## Qué conclusión metodológica sostendría cada resultado

Fijado antes de medir, para no elegir la lectura cómoda después:

- **Si Q2.1 se confirma** (delta condicionado parecido) **y Q3 se confirma**: sostiene la conclusión
  *"para `una-vez-por-partido`, el criterio actual está confundiendo insuficiencia de oportunidades con
  representatividad poblacional"* — la exposición baja no impedía medir el efecto; solo describía a cuánta
  población aplica.
- **Si Q2.1 falla** (el delta condicionado en la población donde casi nadie cualifica es distinto):
  sostiene la conclusión contraria, *"el criterio sigue siendo útil, la exposición solo tiene semántica
  distinta según la familia"* — porque entonces la exposición baja sí estaría correlacionada con una
  medición peor del efecto.
- **Si Q3.1 falla** (la exposición sí sube con ×6): entonces la premisa de §25.3 es falsa y toda la
  distinción habría que revisarla.

Ninguno de los tres desenlaces cambia por sí mismo el suelo del 50%: esa decisión sigue siendo normativa
y posterior.

## Fuera de alcance, explícitamente

`high_line` (`DESIGN_ESCALATION`, Δ −2,2095) sigue fuera de esta rama: es una discrepancia de dirección
del efecto, no de adecuación de población.
