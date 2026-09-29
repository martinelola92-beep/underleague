# BB-D — Propuesta del revisor: parar unos segundos en los eventos que detienen el juego

**Estado:** **CERRADA (30 sep 2026, ADR 0173).** Se implementó la variante (A) del análisis: pausa breve **sólo en
`/Game`**, sin tocar RT-020 ni un solo tick, con la traza como fuente de «el juego se ha parado». Decisión del revisor
(29 sep, noche): *«pausar un poco en los eventos que detienen el juego»*. El análisis del 16 sep, debajo, se conserva
tal cual.

## Observación

**Propuesta del revisor: parar unos segundos en los eventos que detienen el juego** (gol, falta), y que esos parones **no gasten ticks** sino que los añadan. Se considerarían «cinemática»

## Análisis de diseño (protocolo de diez preguntas, `game-design-review`)

1-4. **Qué experimenta/decide el jugador**: nada jugable — es pura legibilidad. Tras un gol/falta
importante, el jugador ve 19 jugadores saltar instantáneamente a sus posiciones de reanudación sin ningún
respiro para procesar lo que acaba de pasar. No hay RF-xxx que lo cite; el principio aplicable es "eventos
explícitos > transiciones invisibles" (CLAUDE.md) — el salto instantáneo es justo eso, una transición
invisible.

5. **Qué sistemas intervienen — el punto que decide todo**: dos variantes.
   - **(A) Pausa solo en `/Game`**: el render ya recibe la secuencia de eventos con su `Tick`; nada impide
     que decida quedarse más tiempo real mostrando un fotograma antes de avanzar al siguiente, sin que
     `/Sim` sepa nada de eso ni cuente esos segundos como ticks. Respeta RT-014 al pie de la letra (el
     render consume eventos y no decide nada del partido) y es exactamente el mismo patrón que la ADR 0114
     ya usa para la cámara cinematográfica —"consume el flujo de eventos y no decide nada del partido"—.
   - **(B) La redactada en el ticket**: meter en `/Sim` un concepto de "ticks de pausa" que no simulan
     nada pero cuentan en la duración del partido. Reparte una decisión puramente estética (cuántos
     segundos dramáticos dar a un evento) dentro de la capa que se supone agnóstica a la presentación —el
     antipatrón que la pregunta 5 del protocolo avisa evitar—, y **rompe todo lo que asume 1.200 ticks de
     reglamento** (las puertas de RT-081, `docs/balance.md`, la curva de la ADR 0033), con implicaciones
     de determinismo (RT-024) sobre si esos ticks "vacíos" consumen RNG o no.

6. **Alternativas**: (A) y (B) de arriba; una tercera, híbrida, no hace falta —`/Game` ya tiene toda la
   información que necesitaría de `/Sim` en el propio `EventType` (`Goal`, `Injury`, `Death`, `Card`) para
   decidir cuánto pausar, sin que `/Sim` tenga que sugerir nada.

7. **Trade-off**: (A) no tiene coste de diseño ni de balance — es presentación pura. (B) tiene un coste
   arquitectónico alto (re-medir y probablemente recalibrar cualquier instrumento que asuma la duración
   de reglamento) por un beneficio idéntico al de (A).

8-9. **Estrategias / degeneración**: ninguna en juego en ningún caso — no es una mecánica jugable. (B)
introduce un vector de riesgo que (A) no tiene: si algo en `/Sim` avanzara por error durante un "tick de
pausa" (un cooldown, un temporizador de lesión), sería una rotura de determinismo sutil y difícil de ver.

10. **Cómo se demuestra**: (A) se demuestra con `visual-review` (captura antes/después), sin tocar
`/Sim` ni gates ni lote de `/Balance`. (B) necesitaría además re-medir y probablemente recalibrar bandas
de duración de partido en todo el proyecto.

**Conclusión**: la variante (A) resuelve el problema de fondo con coste de diseño y balance cero, sin
ninguna excepción a RT-020, y sin perder nada identificable frente a (B) — el juego no muestra un crono en
vivo del que dependa una decisión del jugador, así que "el reloj de partido avanza durante la pausa, como
el tiempo añadido real" (lo único que (B) daría y (A) no) no tiene ningún efecto observable en Underleague
tal como existe hoy. **No hace falta una ADR que toque RT-020.** Si se implementa, es una extensión pequeña
de la ADR 0114 (el estado Cinematográfica añade una pausa de reproducción antes de volver a Táctica), no
una ADR nueva sobre el reloj del partido — candidata a nota de 3-5 líneas junto a esa ADR, no a un
documento propio.

**Es la pieza que unía BB-A, BB-C y BB-L** en la lectura original del hilo de causa, pero con la variante
(A) deja de ser un requisito previo para arreglarlos: BB-C ya se resolvió aparte (en `/Sim`, sin relación
con esto) y BB-A/BB-L dependen de **BA-K** (cortinilla/transición en `/Game`), no de esta pausa. BB-D pasa
a ser una mejora de presentación independiente, no un bloqueante de los otros tres.

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición): [BB-A](./BB-A.md), [BB-C](./BB-C.md), [BB-L](./BB-L.md).

## Cierre (30 sep 2026, ADR 0173)

- **Qué se hizo:** el director de presentación (`Game/Match/PresentationDirector.cs`) congela **0,6 s** (provisional,
  sin medir: Regla H) el fotograma **anterior** al suceso cuando el motor ha parado el juego de verdad —una **falta
  que el árbitro pita**, una **tarjeta** (la roja habla a la vez con su voz), una **lesión que para el partido**—,
  con el sello en pantalla y el residuo ya reflejado, y suelta al fotograma del suceso.
  «El juego se ha parado» se lee de la **traza** (`MatchTrace.RestartAt` en los fotogramas del momento,
  `Game/Match/PlayStops.cs`), no se adivina por el tipo de suceso.
- **Coherencia:** sólo a **1×** (a ×4 y ×16 la presentación se degrada, y cambiar de velocidad corta la pausa); nunca con
  una voz alta en el escenario (**una sola voz alta a la vez**); ni gol, muerte, final ni decisión (ya congelan por sí
  solos); ni saque inicial, turba ni árbitro que se va (banda sin congelar, `docs/ui/README.md` §4).
- **Medido (300 partidos de referencia, sin lote de `/Balance`: es presentación):** 2,96 pausas por partido
  (faltas pitadas 2,63, amarillas 0,19, lesión leve 0,06, roja 0,04, lesión grave 0,06; cota superior, porque la grave
  propia ya congelaba por sí sola); **≤ +1,8 s** de reloj de pared por partido a 0,6 s (+1,6 % sobre los 110,6 s de un
  partido a ×1). **Instrumento validado (Regla J):** de 298
  faltas que el árbitro no vio, 0 producen pausa; de 784 pitadas, las 784 (con dos fotogramas de margen entraban 4 de
  las no vistas, así que no hay margen).
- **Capturas:** `pausa-1-congelada` (el fotograma anterior a la falta, con el sello), `pausa-2-sigue` (0,3 s reales
  después: el **mismo** fotograma, 972) y `pausa-3-reanuda` (ya reanudada, fotograma 980).
