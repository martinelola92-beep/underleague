# ADR 0192 — La pausa enseña quién y después de qué; la tirada del destino es una ruleta (sólo presentación)

Fecha: 9 oct 2026 · Estado: **aceptada** — decisión del revisor en su partida del 3 oct (BX-14..BX-18,
`docs/pendientes/BX-playtest-3oct.md`). **Enmienda la ADR 0173** (pausa breve) y **la ADR 0171** (presentación de la
tirada del destino). **No toca `/Sim`, `/data` ni ningún requisito**: el partido está simulado y esto sólo decide qué
fotograma se enseña, cuánto rato y con qué rótulos (RT-014). Relacionadas: ADR 0119/0120 (director), `docs/ui/README.md`
§2 y §4.

## Contexto (lo que dijo el revisor)

- BX-15: *«Las faltas deberían pitarse DESPUÉS de la entrada. Hoy se pita, se para, se ven jugadores cayendo, pero no
  queda claro quién ha caído ni quién ha hecho la falta.»* La ADR 0173 congelaba el **fotograma anterior al suceso**
  (regla C.2): el silbato y el sello llegaban antes de que se viera la entrada.
- BX-16: *«La tirada del destino tiene que ser algo gráfico, aunque pare el partido: una ruleta verde y roja con una
  aguja que gira. Hoy se siente atropellado.»*
- BX-17: *«Pasan cosas demasiado rápido, no tengo tiempo a procesar, sale un cartel 1 segundo, escucho un grito, no sé
  quién es… Busca pausas, zooms.»* Corrige la tendencia a evitar parones a toda costa.

## Decisión

1. **La pausa llega después de la caída** (BX-15). A 1×, un suceso que detiene el juego (`PlayStops.Holds`, sin
   cambios) se presenta `HoldLeadFrames` = **6 fotogramas** (0,4 s) después de ocurrir: se ve la entrada y la caída
   (la caída toca el suelo a 0,03-0,30 s, BV-B), y entonces suenan el silbato y el sello y se congela ese fotograma.
   **La regla C.2 sigue mandando donde importa:** si algún implicado sale del campo antes (un lesionado grave o un
   expulsado desaparecen en el tick del suceso), el margen se acorta hasta el último fotograma en que todos siguen en
   el campo, y con margen 0 la pausa vuelve a la de la ADR 0173 (fotograma anterior, salto al del suceso). Lo decide la
   pantalla con la traza (`BroadcastScreen.HoldLead`); el director sólo pregunta. A ×4/×16 no hay margen ni pausa.
2. **Quién es quién** (BX-15/BX-17). Durante la pausa, un cartel sobre la cabeza de cada implicado con su papel y su
   nombre, leído de los eventos del momento: FALTA (quien la hace) y AL SUELO (quien cae), AMARILLA/ROJA, TOCADO/HERIDO
   GRAVE y LE ENTRA. Un jugador con dos papeles se queda con el más grave (`MomentTags`, pieza editable). Y un
   acercamiento de cámara ×1,2 al sitio, sólo a 1× como todos los gestos.
3. **La tirada del destino es una ruleta** (BX-16). La cámara lenta de la ADR 0171 se conserva hasta el fotograma
   anterior a los dados; ahí **se congela el partido** y una ruleta gira `FateSpin` = 2,6 s y se queda `FateResult` =
   1,6 s enseñando el resultado (`FateWheel`, pieza editable). **El sector rojo mide exactamente la probabilidad del
   golpe** que trae `FATE_ROLL` (RF-012d: lo que se ve es la cuenta de verdad) y la aguja cae dentro del sector que
   `/Sim` ya decidió, en un punto que sale del tick (nunca del azar). Cuando una entrada tira dos dados al mismo
   jugador en el mismo momento (muerte y grave), la ruleta enseña el que acertó y, si ninguno, el de la muerte. A ×4 la
   tirada sigue comprimida, sin ruleta.
4. **Ritmo** (BX-17): sello N1 1,0 → **1,6 s**, N2 1,5 → **2,2 s**, pausa breve 0,6 → **1,4 s**. Todas **provisionales,
   sin medir** (Regla H): se ajustan con la siguiente partida del revisor.
5. **Mezcla** (BX-18): esfuerzos y regates −9 dB, caída −5 dB, el grito de la lesión leve −8 dB (la grave y la muerte,
   igual), amarilla −4 dB y silbato de falta −3 dB; el bus de ambiente sube de −12 a −7 dB. A oído, provisional.

## Revisión independiente (9 oct) y arreglos

Veredicto MERGE CON ARREGLOS. Arreglado en el mismo paquete: (1) los tests del director (`Sim.Tests/Run/
PresentationDirector{Hold,Fate}Tests.cs`) seguían las duraciones viejas — actualizados y siete casos nuevos; (2) cada
`FATE_ROLL` es su propio momento, así que la elección de la tirada decisiva se hace entre todas las del mismo tick y
jugador, y la segunda tirada no se encola (caducaba durante la ruleta y se perdía la que acertó); (3) una tirada que
arranca tarde ya no congela hacia atrás: la ruleta sólo gira si la imagen está en el fotograma anterior a los dados o
en el suyo; (4) una orden durante el margen ya no pierde la falta (`IsPending` mira cuándo se presenta); (5) al acabar
la ruleta se salta al fotograma de los dados, para que ninguna orden pueda volver a tirarlos con el resultado visto
(ADR 0183); (6) el margen se corta también ante un teletransporte del motor (penalti), con el umbral de la cortinilla
3D; (7) la segunda amarilla se rotula ROJA; (8) el margen nunca pasa por encima del momento siguiente.

**Lo que no está demostrado** (DESIGN CLAIM NOT PROVEN): qué parte de las pausas consigue margen —las de lesión, que
son las del grito, sacan al lesionado grave en el tick y se quedan sin él, aunque los carteles sí salen sobre el
fotograma anterior—; que 1,4 / 1,6 / 2,2 / 2,6 s y las ganancias sean las buenas. Se valida con la siguiente partida.

## Coste

Cada pausa pasa de 0,6 a 1,4 s y hay ~3 por partido: ~2,4 s más de reloj de pared por partido a 1× (+2 %), más
~4,2 s por tirada del destino presentada a 1×. A ×4 y ×16, nada. Es lo que el revisor pidió: *«no tiene por qué ser
excesivo, pero hay que buscar algo»*.

## Verificación

Capturas: `pausa-1-congelada.png` (carteles FALTA / AL SUELO sobre los dos implicados), `destino-*-2-ruleta-gira.png`
y `destino-*-3-ruleta-parada.png` (salva, cae, muerte). Sin prueba de interfaz (RT-084). Pendiente: que el revisor lo
juegue a 1× y diga si la pausa sobra o falta.
