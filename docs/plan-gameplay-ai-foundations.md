# Gameplay AI Foundations Pass — plan de implementación

**Encargo del revisor (24 sep 2026).** Completar e integrar las piezas fundamentales que le faltan a la IA
de jugadores para que un equipo de fútbol 7 pueda *comportarse* como tal, y para que esas piezas
interactúen entre sí. **No es una fase de balance.**

## Política de medición durante este pass — obligatoria y literal

> No hagas balanceo basado en runs reales durante este pass. No ajustes pesos porque una acción aparezca
> demasiado o demasiado poco. No lleves las frecuencias hacia porcentajes "correctos". No compenses un
> cambio de gameplay reduciendo otra utilidad. No hagas tuning estadístico hasta que TODO el pass esté
> implementado e integrado.

Los lotes de `/Balance` y las 43 puertas **solo** se usan como smoke test: que el código corre, que no hay
estados imposibles, que no hay bucles, que las ramas nuevas son **alcanzables**, que los datos fluyen.

**Las puertas se van a poner rojas y eso no es un fallo de este pass.** Queda expresamente suspendida, para
este paquete y solo para él, la regla de `CLAUDE.md` de cerrar con lote de balance en verde; la decisión es
del revisor y está registrada aquí. Lo que **no** se suspende: RT-024 (determinismo), la validación de
esquemas de `/data`, `TreatWarningsAsErrors`, y que la solución compile y los tests de comportamiento pasen.

Al terminar: **parar y entregar informe**. El balance es la fase siguiente y separada.

## Criterio de éxito

No es una distribución de acciones. Es que el motor tenga mecanismos suficientes para que **emerja** una
cadena como:

> portero atrapa → busca compañero libre → juega corto → el compañero recibe bajo presión → protege →
> el centrocampista ofrece apoyo → pase → el delantero ataca la profundidad → pase profundo → el defensa
> despeja → balón aéreo → duelo → rechace → segunda jugada → tiro → parada → rechace → remate

y que la urgencia la modifique:

> 0-1 y quedan segundos → el equipo pasa a ofensivo → sube el riesgo → el portero sale → balón largo →
> duelo → segunda jugada → ocasión

---

# Decisiones de arquitectura (protocolo `architecture-review`)

## D1 · La percepción compartida **ya existe**: se extiende `UtilityContext`, no se crea un sistema nuevo

`UtilityContext` es literalmente «la vista del mundo que necesita la IA, rellenada una vez por tick y
reutilizada en todas las decisiones de ese tick (RT-051)». Ya cachea `TacticalStates`, `NearestToBall`,
`HoldingTeam` y `BallDead`. **El patrón del repositorio para percepción compartida es éste**, y la regla 1
del protocolo —buscar la convención antes de inventar— dice que se extiende.

**Elimina complejidad real, no la mueve** (punto 2 del protocolo): hoy cada `Evaluate*` recalcula por
candidato, por jugador y por tick `NearestOpponentDistance`, `TeammatesNear`, `LaneDanger` y las distancias
a compañeros. Centralizarlo en un cacheo por tick **quita** trabajo cuadrático repetido, no lo traslada.

Se añade a `UtilityContext` un bloque `TeamContext` por equipo, calculado en `UpdateContextCaches`:

| campo | qué es | por qué se comparte |
|---|---|---|
| `Carrier[team]` | quién lleva el balón de ese equipo | hoy cada evaluador lo redescubre |
| `PressureOn[index]` | rivales dentro del radio de presión de cada jugador, entero | lo usan portador, pase, despeje y fatiga |
| `Openness[index]` | 0-100, cuán libre está un jugador para recibir | lo usan pase, apoyo, portero y arranque |
| `IsMarked[index]` | si tiene un marcador asignado encima | `Marking` ya lo calcula, nadie lo lee |
| `AttackingDepth[index]` | si ese compañero está atacando el espacio | lo produce el arranque coordinado (D2) |
| `DangerToGoal[team]` | amenaza sobre la portería propia, 0-100 | despeje, cobertura, portero |
| `Chaser[team]` | perseguidor designado (ya existe como `NearestToBall`) | se conserva, se le añade histéresis |

**Determinismo**: arrays por índice de jugador, rellenados en orden de id ascendente, aritmética entera
salvo distancias en casillas (RT-023). Ningún `Dictionary`. El bloque se calcula **antes** del bucle de
jugadores, como ya hacen `BallDead` y `HoldingTeam`, y con el mismo desfase de un tick ya documentado.

## D2 · La intención compartida reutiliza la ventana de armado del pase

El pase ya tiene un tiempo de armado: `Decide` → `EnterState(Passing, PassingTicks=5)` → `LaunchPass`. Esa
ventana de 5 ticks **es** el canal del arranque coordinado, sin sistema paralelo:

1. el pasador decide `ThroughPass`/`Cross`/`ShortPass` y **publica su intención** en el contexto
   (`Intent{ PasserIndex, ReceiverIndex, Target, ExpiresTick }`);
2. durante esos ticks el receptor, al decidir, **ve una invitación dirigida a él** y puede atacar ese
   espacio (sube la utilidad de `FindSpace` hacia el punto, y marca `AttackingDepth`);
3. `LaunchPass` apunta a donde el receptor va a estar, que es lo que `PassTarget` ya hace.

Una intención es **un struct por equipo con caducidad en ticks**, no una cola ni un bus. El receptor puede
ignorarla: es una oferta, no una orden (`reglas locales > cambios globales de IA`).

## D3 · La urgencia y la orden del jugador **no** añaden estados tácticos

`TacticalState` se queda en cuatro y sigue saliendo solo de la posesión. Marcador, minuto y orden del
jugador entran como **un multiplicador más en la misma fórmula**, que ya es
`Base * Tactical / 100 * TraitMult / 100 + Context`:

```
mentalidad_efectiva = clamp(orden_del_jugador + desplazamiento_por_urgencia, Defensive..Offensive)
score = Base * Tactical/100 * Mentality/100 * TraitMult/100 + Context
```

`Mentality` es una tabla más en `data/ai/weights.json` con tres filas (`Defensive`, `Neutral`,
`Offensive`) y una entrada por acción. La urgencia (diferencia de goles × cercanía del final, entera)
desplaza la fila efectiva. Esto cumple la regla 17 del encargo —la complejidad sale de la interacción, no
de multiplicar acciones ni estados— y es *dato*, no código.

## D4 · Solo dos acciones nuevas, y cada una se justifica por su consecuencia

De 15 a 17. Se admite una acción nueva **solo** si su consecuencia sobre el balón es distinta, no si es
«la misma acción con otro contexto»:

- **`Clear` (despejar)**: el balón sale **alto, largo y sin receptor**, y queda en disputa. No es un pase
  malo: es la renuncia deliberada a la posesión. Consecuencia propia → acción propia.
- **`Shield` (proteger)**: el portador **conserva el balón sin avanzar** y resiste la entrada. Estado
  propio con duración, hermano de `Dribbling` (ADR 0137). Consecuencia propia → acción propia.

Todo lo demás del encargo —presión según marcador, portero que sale, centrocampista todocampista,
represalia— se resuelve con **contexto, percepción y utilidad**, sin acciones nuevas.

## D5 · El resultado de la parada es una *resolución*, no una acción de jugador

Atrapar / blocar / desviar a córner / rechazar / fallar no son decisiones que el portero tome desde la
tabla de utilidad: son el **resultado** del duelo, elegido con atributos y contexto del tiro. Vive en
`ResolveSaveDuel`, que hoy ya existe y siempre termina en `SetOwner`. No toca `PlayerAction`.

## D6 · La fatiga es estado del jugador, no una rampa del reloj

`MatchPlayer.Energy` (0..1000, entero). Se gasta por esfuerzo real del tick (correr, conducir, entrar,
cargar, saltar) y se recupera al no esforzarse, con la tasa modulada por `Stamina` y por
`FatigueResistancePercent` —**que ya existe como escalar de rasgo y hoy no hace nada útil**—. Sustituye a
la rampa global `fatigueStartTick`, que es lo que el encargo rechaza explícitamente. Entra en la utilidad
como penalización de contexto de las acciones caras.

## D7 · Represalia y turba son **modificadores de regla**, no comportamientos escritos

- **Represalia**: `GrudgeTarget` + `GrudgeTicks` en el jugador; añade contexto a `Tackle`/`Block` **contra
  ese rival concreto**. Temporal y local (`reglas locales > cambios globales de IA`).
- **Turba (`Lawless`)**: una fase que cambia **las reglas de interacción** —el árbitro no pita, las
  tarjetas no salen, la tolerancia al contacto sube—, no la geometría del campo. El encargo lo pide
  explícitamente y la geometría **no se toca** salvo que el código demuestre que hace falta.

## D8 · Fronteras que no se tocan

`/Sim` sigue sin conocer Godot ni hacer E/S. Todo lo nuevo sale por eventos y por el informe; el render
**consume**, nunca decide (RT-014). La orden táctica del jugador entra por `MatchSetup` —estado inicial,
como la sustitución forzada de la ADR 0094— y no por una llamada en vivo. Sin `Dictionary` iterado, sin
`Random` estático, sin reloj. RT-024 debe seguir verde en Windows y Linux.

**Aviso de segundo orden (punto 5 del protocolo)**: cada tirada nueva (resultado de parada, duelo aéreo,
dirección del despeje) **desplaza el flujo de RNG** y cambia todos los partidos. Es esperado y aceptado;
por eso los tests de este pass afirman **reglas y alcanzabilidad**, nunca huellas exactas de RNG —el error
que `RecoveryExtraActionTests` ya pagó cinco veces (ADR 0136).

---

# Decisiones de diseño (protocolo `game-design-review`, diez preguntas)

Respondidas por escrito para las cinco mecánicas que lo son de verdad. Las demás son percepción o
instrumento y no lo necesitan.

## M1 · Órdenes tácticas Defensive / Neutral / Offensive

1. **Qué experimenta**: elige antes del partido cómo quiere que juegue su equipo y **lo ve** en el campo.
2. **Qué decide**: arriesgar o conservar, con el rival ya conocido (RF-012b: el ojeo es completo y gratis).
3. **Qué debería decidir**: exactamente eso — hoy su única decisión táctica es la alineación.
4. **Qué regla representa**: ninguna existente. **Es una regla nueva** y se declara como tal; encaja con
   RF-012d (todo lo malo previsible) porque es una preferencia anunciada, no un efecto oculto.
5. **Sistemas**: `/Sim` (`Utility.Choose` + `MatchSetup`), `/data` (`weights.json`), `/Game` (un selector).
6. **Alternativas**: (a) no implementarlo y dejar solo la urgencia automática; (b) órdenes por jugador en
   vez de globales — descartada: multiplica la superficie y el encargo pide tres órdenes globales.
7. **Trade-off**: ofensivo concede espacio a la espalda; defensivo renuncia a la profundidad. Legible.
8. **Estrategias**: se combina con la plantilla (una build violenta defensiva aguanta; técnica ofensiva
   corre riesgos) y con la urgencia, que puede empujar al jugador fuera de su orden.
9. **Degeneración**: que «ofensivo» domine siempre. Se vigila en la fase de balance, **no aquí**.
10. **Demostración**: test de que la misma situación con distinta orden produce distinta utilidad.

## M2 · Fatiga como recurso

1. **Qué experimenta**: sus jugadores se cansan por lo que **hacen**, no por el reloj; un `Stamina` alto se
   nota al final.
2. **Qué decide**: a quién alinea, y —vía la orden táctica— cuánto esfuerzo pide.
3. **Qué debería decidir**: eso mismo. Hoy `Stamina` solo modula una rampa global: el atributo es casi
   decorativo.
4. **Regla**: RF-097 (el estado físico persiste y se muestra) es de run, no de partido. La fatiga **dentro**
   del partido es regla nueva, declarada.
5. **Sistemas**: `/Sim` (`MatchPlayer`, `SpeedPerTick`, utilidad), `/data` (`tuning.json`), informe.
6. **Alternativas**: (a) mantener la rampa global — rechazada por el encargo; (b) fatiga solo de velocidad
   — se queda corta: el encargo pide que entre en decisión, precisión y fuerza.
7. **Trade-off**: presionar arriba cuesta piernas al final. Ése es el coste de oportunidad legible.
8. **Estrategias**: `FatigueResistancePercent` pasa a valer de verdad; los rasgos existentes se activan.
9. **Degeneración**: que todos acaben exhaustos y el final sea un partido lento. Se acota con recuperación
   real y se **mide en la fase siguiente**.
10. **Demostración**: test de que la energía baja con esfuerzo y **sube** sin él.

## M3 · Portero con resultado de parada y salida condicionada

1. **Qué experimenta**: paradas que no son todas iguales — hay rechaces, córners y manos.
2. **Qué decide**: qué portero ficha y con qué rasgos (`Cat`, `Wall`, `Rusher`, RF-057e, hoy casi inertes).
3. **Debería**: eso. Hoy los tres rasgos son ±8 y un multiplicador que el clamp anula.
4. **Regla**: RF-057c (la parada es un porcentaje con atributos) **existe y está a medias**; RF-057e existe
   y no se cumple. La salida del área es regla nueva, acotada a situación de urgencia.
5. **Sistemas**: `/Sim` (`ResolveSaveDuel`, `ClampToArea`), `/data` (`tuning.json`), eventos.
6. **Alternativas**: (a) portero adelantado permanente — **rechazada por el encargo**; (b) rechace siempre
   — rechazada: quita la identidad del portero bueno.
7. **Trade-off**: un portero que sale gana balones y regala la portería. Previsible por el rasgo.
8. **Estrategias**: abre la segunda jugada como fuente de gol; premia al delantero rematador.
9. **Degeneración**: rechaces encadenados en bucle. **Se acota con un tope duro de rechaces por jugada**.
10. **Demostración**: tests de alcanzabilidad de las cinco ramas del resultado.

## M4 · Represalia

1. **Qué experimenta**: le rompen un jugador y **los suyos van a por el culpable**, con nombre.
2. **Qué decide**: alinear o no a un violento junto a alguien frágil; y si venga o no (vía orden táctica).
3. **Debería**: eso — hoy una lesión no tiene ninguna consecuencia social en el campo.
4. **Regla**: nueva. Se apoya en la atribución que la **ADR 0124** ya construyó (`KillerPlayerId`).
5. **Sistemas**: `/Sim` (`ResolveInjury` → estado del jugador → utilidad), `/data` (`tuning.json`).
6. **Alternativas**: (a) represalia automática («lesión → atacar al culpable») — **rechazada por el
   encargo** y por diseño: mata la diversidad; (b) represalia de equipo entero — demasiado global.
7. **Trade-off**: vengarse cuesta posición y expone a la tarjeta. Coste legible.
8. **Estrategias**: convierte una lesión en una historia con dos nombres, que es la identidad del juego.
9. **Degeneración**: espiral de lesiones. Se acota con duración corta y un solo objetivo por jugador.
10. **Demostración**: test de que la represalia se activa, caduca y apunta a quien debe.

## M5 · Turba sin árbitro (`Lawless`)

1. **Qué experimenta**: en el gol de oro **deja de haber reglas** y se nota.
2. **Qué decide**: si le conviene llegar a la turba, y con qué plantilla.
3. **Debería**: eso. Hoy la turba es una etiqueta: cuatro líneas y ningún cambio de regla, en el 27,6 % de
   los partidos.
4. **Regla**: **RF-055d existe y promete exactamente esto** («la turba es el único tramo sin árbitro»). No
   es regla nueva: es una promesa incumplida.
5. **Sistemas**: `/Sim` (árbitro, faltas, tarjetas, tolerancia al contacto), `/data` (`tuning.json`).
6. **Alternativas**: (a) estrechar el campo — **rechazada salvo que el código lo exija**, por el encargo;
   (b) solo subir la violencia sin quitar el árbitro — se queda corta.
7. **Trade-off**: la build violenta gana su ventana; la técnica corre su mayor riesgo. Es RF-055d literal.
8. **Estrategias**: da sentido a llegar empatado con una plantilla dura.
9. **Degeneración**: masacre en la prórroga. **Es el punto que más vigila la fase de balance**, no ésta.
10. **Demostración**: test de que en `Lawless` una falta no se pita y una tarjeta no sale.

---

# Paquetes, en orden de dependencia

Cada paquete cierra con: compila, sus tests de comportamiento pasan, RT-024 verde, commit propio con
RF/RT. **Ningún paquete ajusta un peso para perseguir una frecuencia.**

| # | Paquete | Depende de | Núcleo del cambio |
|---|---|---|---|
| P1 | Percepción compartida (`TeamContext`) ✅ | — | `UtilityContext` + `UpdateContextCaches` |
| P2 | Portador completo ✅: `Shield`, y utilidad del portador con presión y espacio | P1 | `PlayerAction`, `StateMachine`, `Utility` |
| P3 | Intención compartida y arranque coordinado ✅ | P1, P2 | `Intent` en el contexto, `EvaluateFindSpace` |
| P4 | Balón aéreo ✅: `Clear`, altura general, rechace, duelo aéreo, segunda jugada | P1 | `Ball`, `UpdateFlight`, `AERIAL_DUEL` |
| P5 | Portero ✅: resultado de parada, saque evaluado, salida condicionada | P1, P4 | `ResolveSaveDuel`, `ClampToArea` |
| P6 | Marcador, minuto y urgencia ✅ | P1 | `Utility.Choose`, `weights.json` |
| P7 | Órdenes Defensive/Neutral/Offensive ✅ | P6 | `MatchSetup`, `weights.json` |
| P8 | Fatiga como recurso ✅ | P1 | `MatchPlayer.Energy`, `SpeedPerTick`, utilidad |
| P9 | Balón parado como fase con decisión ✅ | P1, P4 | `BeginRestart`/`ResolveRestart`/`TakeRestart` |
| P10 | Acciones muertas ✅: `OfferSupport`, `PressCarrier`, `Block` — causa por causa | P1, P6 | gates de `Utility` |
| P11 | Centrocampista todocampista ✅ (medido, sin cambios) | P1, P10 | contexto por puesto, sin bonus arbitrarios |
| P12 | Represalia ✅ | P1 | `ResolveInjury`, utilidad |
| P13 | Turba `Lawless` ✅ | P6 | árbitro, faltas, tarjetas |
| P14 | `modifyUtility` en el esquema ✅ + rasgos que hoy no pueden manifestarse | P2..P13 | `perks.schema.json`, `PerkLoader` |
| P15 | Tests de integración de cadena + informe ✅ | todo | `Sim.Tests` |

## Lo que este plan NO hace, y es deliberado

- **No reescribe `/Sim`.** Se extiende `UtilityContext`, se reutiliza la ventana de armado del pase, se
  reutiliza `EnterState(estado, ticks)` para `Shield`, se reutiliza `Marking` y se reutiliza la atribución
  de la ADR 0124. Ningún sistema paralelo.
- **No añade acciones más allá de `Clear` y `Shield`.**
- **No toca la geometría del campo** (ADR 0109 dejó escrito que ensancharla destruye la profundidad).
- **No ajusta ningún peso contra un lote.** La calibración es la fase siguiente.
- **No toca el catálogo de 102 perks** (ADR 0122).
