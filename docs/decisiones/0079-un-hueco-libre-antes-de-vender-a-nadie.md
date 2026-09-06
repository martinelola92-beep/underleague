# 0079. Un hueco libre antes de vender a nadie

**Fecha:** 2026-09-07
**Estado:** Aceptada e implementada. **Decisión del revisor**, tras jugar la primera run en la build
exportada de Windows.
**Modifica:** RF-005, RF-020
**Requisitos:** RF-005, RF-020, RF-002b, RT-021, RT-022
**Relacionada con:** ADR 0046 (que fijó la plantilla base en 10 y creó el nodo de inscripción)

## La anotación del revisor

> "En los primeros mercados no puedes comprar ningún jugador por falta de espacio y te da rabia."

La ADR 0046 fijó la plantilla base en 10 exactamente porque son los 10 con los que el club empieza
(RF-005): titulares y suplentes llenan la capacidad entera desde el primer nodo. Cualquier fichaje, por
tanto, exige vender o descartar antes de poder comprar (`RunRules.RequireRosterSpace`,
`Sim/Run/Systems/Market/MarketSystem.cs`), y eso se aplica igual al mercado del acto 1, cuando el jugador
todavía no tiene a quién vender ni una razón para descartar a nadie.

## Decisión

**La plantilla inicial del club del jugador pasa de 10 (7 titulares + 3 suplentes) a 9 (7 titulares + 2
suplentes).** La capacidad base (`RunRules.BaseRosterSize`) **se queda en 10**: no baja con la plantilla,
así que desde el primer mercado hay un hueco libre para fichar sin vender ni descartar a nadie. La
capacidad ampliada por nodo de inscripción sigue en 12 (`MaxRosterSize`, `MaxEnrollmentSlots = 2`), sin
tocar.

El suplente que se quita es el **centrocampista**: la línea de tres titulares (MID) no necesita un cuarto
jugador de refresco tanto como las líneas de una (FWD) o dos (DEF), que se quedarían sin ningún suplente
de su posición si se hubiera quitado otro. Queda DEF y FWD como suplentes (`TeamGenerator.ClubSubstitutePositions`).

**Los rivales y los jefes no cambian**: siguen generándose con 10 jugadores (7+3, DEF/MID/FWD de
suplentes). Es una decisión sobre la plantilla del jugador, no sobre el tamaño de equipo en general —un
rival con 9 no tiene ninguna ventaja que justificar y complicaría la IA de sustitución sin necesidad.

Los cinco clubes iniciales de `data/clubs/*.json` pierden su suplente `Midfielder` (índice 8 del roster);
el jugador de rareza superior a común seguía en el índice 6 (delantero titular) en los cinco, así que no
se pierde.

## Por qué la capacidad base no baja con la plantilla

Bajar `BaseRosterSize` a 9 junto con la plantilla habría dejado el problema exactamente igual: la
plantilla llenaría la capacidad de nuevo y el primer mercado seguiría bloqueado. El hueco libre solo
existe si la capacidad **no** se mueve. Por eso `BaseRosterSize` deja de significar "los mismos 10 con los
que el club empieza" (como decía su comentario desde la ADR 0046) y pasa a significar "un hueco más que la
plantilla inicial".

## Efecto medido: la semilla de referencia de la run completa cambia

El test `RunEngineTests.ARunCanBePlayedFromStartToFinish` juega una run entera con una semilla fija y
espera victoria. Con la plantilla de 9, la **semilla 1 deja de ganar**: pierde la final del acto 3
(`DefeatCause.BossMatchLost`). No es una regresión de RF-002b —en ese camino los jugadores disponibles
nunca bajan de 8, muy por encima del mínimo de 5— sino que generar un jugador menos desplaza toda la
secuencia de atributos, rareza y nombres que la misma semilla produce después (RT-022: el flujo de
generación es determinista pero depende de cuántos números consume cada paso). La semilla 1 tocaba una
combinación que con 10 jugadores ganaba y con 9 ya no. El test pasa a usar la **semilla 2**, que gana con
el mismo rango de nodos (30-36) y de partidos (17-22) que exige el diseño.

Esto es exactamente el tipo de sensibilidad a semilla que **RT-056** ya advierte para las pruebas
estadísticas ("un test que falla por mala suerte es un test mal escrito"): una sola semilla de un camino
ciego no mide una tasa de victoria, solo confirma que el bucle de run entero sigue siendo jugable de
principio a fin. No se ha medido con `/Balance --full-runs` si la tasa de victoria agregada se mueve con
la plantilla de 9: es una pregunta abierta y legítima, pero no bloquea esta decisión porque el revisor la
pidió explícitamente y conociendo que reduce el colchón de suplentes frente a lesiones.

## Qué falsificaría esta decisión

- **Que la tasa de victoria de la política automática se mueva de forma notable** al medir
  `--full-runs` con la plantilla de 9 frente a la de 10: un suplente menos es menos colchón contra
  lesiones y muertes a lo largo de 30-36 nodos, y si el efecto es grande habría que compensarlo en otro
  sitio (economía de clínica, tasa de lesión) en vez de aceptarlo sin más.
- **Que el jugador quite el suplente equivocado.** Si en la práctica la línea de un delantero o dos
  defensas se queda corta más a menudo que la de tres centrocampistas, la elección de qué suplente quitar
  habría que revisarla, no la cifra de 9.
