# ADR 0165 — Clanes cerrados y némesis

Fecha: 29 sep 2026 · Estado: **aceptada**. **Decisión del revisor** (`docs/plan-diversion.md` §3): *«¿no se
supone que te vuelves a encontrar con los mismos equipos? Hablamos de hacer equipos cerrados y
reconocibles»*, *«el clan por raza que sube de categoría ok»* y *«entre actos hacemos que ese némesis lo
ficha otro equipo gracias a que se ha dado a conocer»*.
**Implementa** la F1.5 del plan Knavall (el clan cruza de acto). **Enmienda RF-015** (los rivales son
estáticos por acto y división): ahora son **los mismos clanes en los tres actos**, que suben de categoría.
**Requisitos:** RF-012b, RF-015, RF-093, RF-119, RF-122, RT-022, RT-030, RT-031
**Relacionada:** ADR 0124 (carrera y créditos de rival), ADR 0163 (la Gaceta y su villano), BR-B (la muerte
es terminal).

## Lo que había (Regla G)

Quince rivales de datos, cinco por acto (uno por raza), con jugadores con nombre. Los del acto 2 ya repiten
algunos nombres del acto 1, pero bajo **otro nombre de clan** («Yunque Verde» en el acto 1, «Horda de
Colmillo Rojo» en el 2). Los rivales **no guardan estado**: si le partes la pierna a Grok, el siguiente
partido llega sano. `RivalCredits` (ADR 0124) ya registra quién lesionó o mató a quién, y la Gaceta elige
con ello un villano.

## Decisión

1. **Un clan por raza en los tres actos.** Cada fichero de rival gana `clanId`; los tres de una raza
   comparten clan, **nombre de clan** (el del acto 1) y **nombres de jugador por puesto**: el que jugaba de
   central en el acto 1 sigue en el acto 2, subido de nivel. Las cifras (atributos, perks, dificultad) de
   cada acto **no cambian**: es un cambio de identidad, no de balance. La descripción de cada acto cuenta
   cómo ha crecido el clan.
2. **Los rivales tienen memoria.** Estado nuevo en la run (sube el guardado): por jugador rival de clan,
   si está **muerto** (no vuelve: su puesto lo ocupa un fichaje con nombre generado y las mismas cifras) y
   sus **títulos**. Las lesiones de los rivales no se arrastran (curan entre partidos).
3. **Némesis.** Un jugador rival que **mata** a uno de los tuyos se convierte en tu némesis:
   - gana un **título** con nombre («el Matahermanos», de datos, localizado) y **un nivel**;
   - el ojeo lo marca con su título y a quién mató; el mapa marca el nodo donde juega;
   - **entre actos lo ficha otro clan** («se ha dado a conocer»): al empezar el acto siguiente pasa, de forma
     determinista, a uno de los otros clanes, sustituyendo a su jugador de menos nivel del mismo puesto;
   - **venganza**: si lo lesionas o lo matas, el informe lo proclama, el jugador que lo hizo suma
     «venganzas» a su carrera (alimenta un apodo, ADR 0163) y la run cobra una recompensa de oro
     (provisional, sin medir). Un némesis muerto deja de serlo;
   - **tope**: dos némesis vivos a la vez; con dos, el siguiente asesino no se convierte (se anota).
4. **La Gaceta** prefiere al némesis vivo o vengado como villano de la temporada.
5. **Aleatoriedad**: los fichajes y el traspaso salen de un flujo propio derivado de la semilla de la run
   (`OfferStream`, desplazamiento 9500 o una sal propia), nunca del de partido (RT-022).

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Los mismos cinco clanes toda la run, con caras que reconoce, y un villano con
   nombre que le mató a alguien y que reaparece.
2. **Qué decide.** Evitar o buscar el nodo donde juega su némesis; alinear para vengarse (Brutos contra él);
   aceptar el riesgo de que vuelva a matar.
3. **Qué debería decidir.** Lo mismo: la venganza es opcional y cuesta exponerse.
4. **Regla.** RF-015 enmendada; memoria de rival nueva.
5. **Sistemas.** `/data`: `rivals/*.json` (`clanId`, nombres), `nemesis/` (títulos). `/Sim`: estado de rivales
   en `RunState` (guardado v8), `RivalTeamBuilder` (muertos, fichajes, némesis traspasado, +1 nivel),
   `MatchResolution` (convertir en némesis, venganza), carrera (`Revenges`), apodo nuevo, `GazetteView`.
   `/Game`: ojeo, mapa, informe, retransmisión (pregón de venganza si encaja).
6. **Alternativas.** Mantener clanes distintos por acto: es lo que el revisor señaló como fallo.
7. **Trade-off.** Un némesis con un nivel más es más peligroso: vengarse cuesta.
8. **Estrategias.** Da a las builds de violencia un objetivo con nombre y a las defensivas un motivo para
   esquivar nodos.
9. **Degeneración.** Muertes en cadena del mismo némesis: el tope de dos y que un nivel más no es mucho. Se
   mide `nemesesPerRun`, `revengesPerRun` y `deathsPerRun` contra la línea base.
10. **Cómo se demuestra.** Tests: clan y nombres coherentes en los tres actos; un rival muerto no vuelve;
    némesis al matar, traspaso determinista entre actos, venganza, tope; guardado v8 ida y vuelta. Lote de
    campaña antes y después; capturas del ojeo con némesis y del informe con venganza.
