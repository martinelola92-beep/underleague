# 0039. Los legendarios dejan de generarse: son personajes que se desbloquean

**Fecha:** 2026-09-05
**Estado:** Aceptada (idea del revisor)
**Modifica:** RF-023, RF-125..128, y **RF-127 de forma sustancial**
**Sustituye:** la salvaguarda de la ADR 0027

## Decisión

1. **Las rarezas generables pasan a ser tres**: común, poco común y raro. El legendario **desaparece de la generación aleatoria**.
2. **Cada raza tiene tres legendarios únicos**, diseñados a mano: nombre, retrato, atributos y perks fijos. No se generan, no se compran, no salen en el mercado.
3. **Se desbloquean ganando runs.** Al completar una división se obtiene uno, y la run siguiente —en la división superior, con rivales más duros— **empieza con él en la plantilla**. Al llegar a la división más alta, el jugador arranca con los tres.
4. **Son parodias de futbolistas reales cruzadas con su raza**, y material natural para cromos coleccionables.

## Por qué esto arregla algo, además de añadir

La ADR 0027 fijó que los legendarios deben ser netamente superiores, y para evitar que no encontrar ninguno condenara la run hubo que añadir una salvaguarda obligatoria: *"un equipo sin ningún legendario debe poder ganar al jefe final"*. Era una tensión incómoda: el premio tenía que notarse mucho y a la vez no ser necesario.

**Con esta decisión la tensión desaparece.** El juego base se equilibra entre común, poco común y raro, que es lo único que la generación produce; los legendarios son **progresión meta** y su presencia está garantizada de antemano, así que no hay lotería que compensar. La salvaguarda de la ADR 0027 deja de hacer falta.

Y hay un segundo beneficio: un legendario deja de ser "un jugador con más presupuesto" y pasa a ser **un personaje**. Es infinitamente más memorable perder a alguien con nombre y cara que perder una rareza.

## El conflicto con RF-127, y cómo se resuelve

RF-127 dice: *"No hay progresión meta que otorgue poder puro acumulativo. Los desbloqueos añaden variedad, no ventaja."* Empezar con un legendario **es** ventaja acumulativa, así que el requisito cambia.

La condición para que el cambio no rompa el juego es que **la dificultad de la división suba más de lo que aporta el legendario**. La progresión meta no facilita el juego: lo hace **distinto y más difícil**, y te da una herramienta con la que afrontarlo. Es medible y pasa a ser una métrica obligatoria: la tasa de victoria en la división N con sus legendarios debe ser **igual o menor** que en la división N−1 sin ellos.

## Cada división es una run distinta: la plantilla se reinicia

**No hay continuidad de plantilla entre divisiones.** Cada división es una run nueva que empieza desde el club inicial; lo único que se arrastra son los legendarios desbloqueados, y **entran sin perks y sin equipamiento**, como jugadores base superiores. Todo lo demás —build, objetos, niveles, vínculos— se construye otra vez desde cero.

Eso tiene tres consecuencias que ordenan el resto del diseño:

1. **El legendario no da poder, da capacidad.** Llega con mejores atributos y más slots, pero vacíos: hay que llenarlos durante la run igual que los de cualquier otro. La ventaja es el techo, no el punto de partida.
2. **Las muertes dejan de tener coste meta.** Perder a un jugador —incluido un legendario— condena como mucho la run en curso; en la siguiente vuelve. Eso permite que el desgaste sea tan duro como el juego necesita sin que arrastre castigo entre partidas, y confirma la dirección de la ADR 0034.
3. **La única progresión permanente, además de los legendarios, es el conocimiento del jugador.** Por eso el compendio de modificadores de jefe (RF-014b), los rivales estáticos que se aprenden (RF-015) y las descripciones que no mienten (RT-035) dejan de ser comodidades y pasan a ser el sistema de progresión real.

## Cuatro divisiones, tres legendarios

RF-128 definía cinco divisiones y esta ADR fija tres legendarios por raza, lo que dejaba un escalón sin premio. **Resuelto retirando la división Continental** (decisión del revisor): quedan **Tercera, Segunda, Primera y Mundial**, y sus reglas se reagrupan —los dos modificadores por jefe que llevaba Continental pasan a Mundial—. La secuencia encaja: empiezas sin ninguno en Tercera y ganas uno por división superada, de modo que llegas a Mundial con los tres.

## Los legendarios mueren

Un legendario desbloqueado está disponible **al empezar cada run** de esa división, pero dentro de la run es un jugador más: se lesiona, se puede quedar sin tratar y **puede morir** (RF-093). Si muere, se pierde **para esa run**, no para siempre — igual que el resto de la plantilla, que también se reinicia.

Es la decisión que más valor añade: el desgaste de plantilla es el recurso central del juego, y un legendario inmortal sería la única pieza a la que ese recurso no afecta. Perder a un personaje con nombre y cara en el acto 2 es exactamente la historia que el juego quiere producir, y hace que la ceremonia de muerte y el memorial (RF-122) tengan a quién llorar.

## Criterio para los nombres

**El nombre tiene que funcionar aunque no captes la referencia.** Un chiste que solo entiende quien conoce al futbolista deja fuera a media audiencia; el nombre debe sonar bien como nombre de criatura fantástica **y además** guiñar. "Paolo Maldito" funciona como no-muerto aunque no sepas quién es Maldini.

**Riesgo a tener en cuenta**: los nombres deben ser guiños **transformados**, no el nombre real con una letra cambiada. Cuanto más reconocible sea la persona concreta, más conviene deformar apellido y nombre a la vez, y evitar retratos parecidos a la persona real. No es asesoramiento legal, pero es un riesgo conocido del sector y sale más barato tenerlo en cuenta ahora que después.

## Consecuencias

- **RF-023 se reescribe**: tres rarezas generables con sus slots (común 2, poco común 3, raro 4) y los legendarios como categoría aparte, con 4 o 5 slots fijos por diseño.
- **Toda la generación, el mercado y las recompensas** dejan de producir legendarios. `data/balance/builds/*_excellent` se rehace: "muy buena" ya no puede apoyarse en rareza legendaria.
- **RF-128 (divisiones)** gana su recompensa: hoy subir de división solo añade reglas; ahora también da un personaje.
- Los legendarios son contenido de **arte** (retrato, cromo) y de **escritura**, no solo de datos: entran en el presupuesto de la fase 3 en adelante.
- Se necesita una métrica nueva: dificultad neta por división, para comprobar que la ventaja no supera al aumento de exigencia.
