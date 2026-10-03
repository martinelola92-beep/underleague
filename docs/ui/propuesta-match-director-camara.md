# Propuesta del revisor (3 oct 2026): Match Director y cámara cinematográfica

Estado: **pendiente de análisis**. El revisor pidió analizarla al terminar los paquetes BX-1..13: «Actualmente el
espectáculo del partido se me hace aburrido». Ya existe un director de presentación (ADR 0119, niveles N1–N4,
momentos, residuos) y una cámara dinámica (ADR 0114, 0174). El análisis tiene que partir de ahí (Regla G), no
duplicarlo. Absorbe el paquete 3 de `docs/pendientes/BX-playtest-3oct.md` (BX-14..18: reloj, falta legible,
ruleta, timing y pausas, audio).

Ojo: `docs/ui/README.md` y la memoria del orquestador dicen «nada de estética cómic» (dirección: pregón,
heráldica). Esta propuesta pide «medieval + retransmisión deportiva + cómic + violencia absurda» y «comic burst
opcional». Lo dice el revisor, que tiene la última palabra, pero el análisis debe señalar la tensión con la
dirección acordada antes de decidir.

---

## Texto del revisor (resumen fiel por apartados)

**Objetivo.** El fútbol base ya funciona (pases, controles, conducciones, tiros, jugadas). El siguiente problema
no es añadir fútbol, sino que **ver un partido sea espectacular y entretenido**. Que no parezca una cámara fija
mirando una simulación, sino una retransmisión dirigida de un deporte fantástico y violento. La simulación está
precalculada, así que la presentación puede conocer de antemano los eventos. Con eso se construye un Match
Director que controla la cámara, el zoom, el encuadre, la velocidad temporal y ciertos efectos.

**1. Principio.** La cámara no sigue el balón; responde a «¿qué merece la pena que vea el espectador ahora?».
El flujo es SIMULATION → EVENTS / FUTURE EVENTS → MATCH DIRECTOR → PRESENTATION EVENT → CAMERA + TIME SCALE + FX.
Como el partido está presimulado, la cámara se prepara antes (ejemplo: se acerca en t=12,2 a una entrada que
impacta en t=12,7) en lugar de reaccionar después.

**2. Jerarquía, sin cámara cinematográfica constante.**
- L0 Normal: plano amplio, se entiende la posición de los dos equipos.
- L1 Acción: seguimiento algo más cercano (pases, controles, conducción, disputas).
- L2 Peligro: zoom progresivo (último tercio, 1 contra 1, ocasión clara, encarar portería, contraataque).
- L3 Espectáculo: cámara dedicada (entradas importantes, habilidades visuales, saltos, derribos, choques, tiros
  muy peligrosos, habilidades de raza o perk).
- L4 Momento decisivo: cinemática corta (gol, parada extraordinaria, alto impacto, jugada rara).
- L5 Highlight: excepcional, muy poco usado. Que el jugador piense «Hostia, ¿qué acaba de pasar?».

**3. Cámara base.** Estable, que mantenga visible la zona relevante, siga con suavidad, sin movimientos bruscos,
con zoom y posición dinámicos, órdenes temporales del director y vuelta suave al estado normal. Sin cortes
constantes; el estado normal tiene que ser cómodo para seguir el fútbol.

**4. Anticipación.** El director recibe `event`, `event_time`, `position`, `participants`, `importance`,
`event_type`, `duration` y, si lo hay, `future_event` y `future_event_time`. La cámara se mueve antes del evento.
En una entrada: antes se acerca, durante mantiene en cuadro a atacante, defensor y balón, en el impacto da un
pequeño punch, después sigue al balón y al final vuelve a la normal. Que parezca retransmitido a propósito, no un
accidente que la cámara persigue.

**5. Comportamientos reutilizables.** Follow, Focus, ZoomIn, ZoomOut (contraataques, pases largos, espacios),
Impact (sutil), Chase, HeroShot, GoalShot (sin cinemáticas largas; el gol sigue dentro del flujo).

**6. Slow motion.** Muy breve y selectivo. Entrada espectacular a 0,4–0,6x en el impacto; salto a 0,5x; tiro
especial a 0,4–0,6x; gran parada. Punto de partida: 0,3–0,8 s reales, que se ajustan jugando y no con números
teóricos.

**7. Efectos en slow motion.** Retransmisión deportiva exagerada, sin llenar la pantalla de partículas: viñeta
ligera, zoom pequeño, estelas sutiles, impacto de cámara, polvo, líneas de velocidad, freeze-frame de 1–2
fotogramas, shake pequeño, flash estilizado y, opcional, texto o comic burst. La estética es medieval +
retransmisión deportiva + cómic + violencia absurda. No una cinemática realista ni un efecto genérico.

**8. No más efectos, sino niveles.** Acción normal: casi sin efectos. Interesante: zoom y seguimiento.
Espectacular: cámara y FX. Excepcional: cámara, slow motion y FX. Gol o highlight: presentación especial.
Nunca todo junto en cada acción.

**9. Caso entrada, de los primeros.** Atacante con balón → defensor se acerca → el director detecta la entrada →
focus → zoom suave → entrada → 0,5x → impacto → balón despedido → la cámara sigue al balón → velocidad normal →
cámara normal.

**10. Caso salto élfico.** Elfo conduciendo, un defensor se interpone y el perk permite saltar. El director
encuadra a los dos → zoom → salta → 0,5x → seguimiento vertical → pasa por encima → aterriza → 0,7x → 1x →
sigue la jugada. Cita: «No queremos "Elfo tiene +10 % velocidad", queremos "acabo de ver a un elfo saltar
literalmente por encima de un jugador"».

**11. Casos iniciales.** Entrada importante, gran regate, contraataque, tiro peligroso, gran parada, gol,
choque o derribo, habilidad racial espectacular, perk visual, y rebote o balón caótico. No decenas de casos.

**12. Prioridades.** GOAL > EXCEPTIONAL_HIGHLIGHT > SPECTACULAR_PERK > MAJOR_SHOT > MAJOR_TACKLE >
DANGEROUS_ATTACK > NORMAL_ACTION. Un único «focus event»; nunca dos cámaras reaccionando a la vez.

**13. Cooldowns.** Después de un momento espectacular hay un cooldown. Si en 5 s ocurren cuatro perks no hace
falta presentar los cuatro: se buscan highlights, no documentarlo todo.

**14. Legibilidad.** «Nunca sacrificar la comprensión del fútbol por hacer una cámara bonita.» El jugador
siempre tiene que saber dónde está el balón, quién lo tiene, dónde está la portería, quién participa y qué acaba
de pasar.

**15. Sonido.** El director expone eventos (CAMERA_FOCUS, SLOW_MOTION_START, IMPACT, SLOW_MOTION_END,
HIGHLIGHT_START, HIGHLIGHT_END) para sincronizar después golpes, whoosh, público, silencio en el slow motion,
etc. El audio no hace falta ahora, pero la arquitectura tiene que permitirlo.

**16. Fases.**
1. Director de cámara básico: prioridades, eventos, focus, zoom, seguimiento y vuelta a la normal.
2. Slow motion: entrada, tiro, parada, habilidad.
3. Impacto y FX: punch, sacudidas, zoom de impacto, polvo, líneas de velocidad, comic bursts.
4. Highlights: gol, acción racial, perk excepcional, jugada extraordinaria.
5. Ajuste jugando. Se evalúa con preguntas: ¿quiero seguir mirando?, ¿he visto claro lo importante?, ¿la cámara
   me ha hecho fijarme en algo que se me habría escapado?, ¿se ha sentido nerviosa?, ¿lo especial parece especial?

**17. Criterio de éxito.** «Vale, está atacando…», «Uy, viene el defensa…», «¿Va a entrar?», «HOSTIA», «El
balón ha salido…», «Contraataque», «Espera…», «¿EL ELFO VA A—?», «JAJAJA». No se trata de mejorar la cámara,
sino de crear una retransmisión dirigida de Knavall, con ritmo, tensión, anticipación y momentos memorables. La
simulación pone el fútbol; el Match Director lo convierte en espectáculo.
